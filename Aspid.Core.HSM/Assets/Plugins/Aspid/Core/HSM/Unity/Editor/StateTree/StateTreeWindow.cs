using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Aspid.FastTools.Types.Editors;
using Aspid.FastTools.UIElements;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UIElements;

// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM.Editor
{
	/// <summary>
	/// Editor window for visualizing the HSM state tree: a node graph on VisualElement
	/// with three layout modes and node dragging, a minimap, state search, focus mode
	/// around the selected node, an inspector, a transition history, and control of a
	/// running <see cref="MonoStateMachine"/> (active chain highlighting,
	/// ChangeState / TransitionTo) in Play Mode.
	/// </summary>
	public sealed class StateTreeWindow : EditorWindow
	{
		private const long ActivePollIntervalMs = 200;
		private const float ZoomStep = 1.2f;
		private const string DirectionPrefsKey = "Aspid.HSM.StateTree.Direction";
		private const string EdgeStylePrefsKey = "Aspid.HSM.StateTree.EdgeStyle";
		private const string HistoryPrefsKey = "Aspid.HSM.StateTree.HistoryVisible";
		private const string TransitionsPrefsKey = "Aspid.HSM.StateTree.TransitionsVisible";
		private const string CollapsedPrefsKey = "Aspid.HSM.StateTree.Collapsed";

		private readonly Dictionary<Type, StateTreeNodeElement> m_nodeElements = new();
		private readonly Dictionary<Type, int> m_transitionCounts = new();
		private readonly HashSet<Type> m_activeTypes = new();
		private readonly HashSet<string> m_collapsedTypes = new();
		private List<StateTreeNode> m_roots;
		private List<StateTreeTransition> m_transitions;
		private StateTreeEdgesElement m_edges;
		private StateTreeCanvasElement m_canvas;
		private StateTreeMinimapElement m_minimap;
		private StateTreeInspectorElement m_inspector;
		private StateTreeHistoryElement m_history;
		private Label m_statusLabel;
		private Label m_searchCountLabel;
		private Label m_extensionsLabel;
		private Button m_directionButton;
		private Button m_edgeStyleButton;
		private Button m_exportButton;
		private Button m_zoomLabel;
		private TextField m_searchField;
		private Label m_searchPlaceholder;
		private MonoStateMachine m_stateMachine;
		private StateTreeLayoutDirection m_direction;
		private StateTreeEdgeStyle m_edgeStyle;
		private bool m_transitionsVisible;
		private Type m_selectedType;
		private string m_searchQuery = string.Empty;
		private int m_searchMatchIndex;
		private string m_lastLeafName;
		private Vector2 m_canvasSize;

		private bool canControlMachine =>
			EditorApplication.isPlaying && m_stateMachine != null && m_stateMachine.IsInitialized;

		[MenuItem("Tools/Aspid/HSM State Tree")]
		public static void Open() =>
			GetWindow<StateTreeWindow>("HSM State Tree");

		private void CreateGUI()
		{
			m_direction = (StateTreeLayoutDirection)EditorPrefs.GetInt(DirectionPrefsKey, 0);
			m_edgeStyle = (StateTreeEdgeStyle)EditorPrefs.GetInt(EdgeStylePrefsKey, 0);
			m_transitionsVisible = EditorPrefs.GetBool(TransitionsPrefsKey, true);

			foreach (string typeName in EditorPrefs.GetString(CollapsedPrefsKey, string.Empty)
				.Split(';', StringSplitOptions.RemoveEmptyEntries))
			{
				m_collapsedTypes.Add(typeName);
			}

			BuildUi();
			RebuildGraph();
			rootVisualElement.schedule.Execute(PollActiveStates).Every(ActivePollIntervalMs);
			EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
		}

		private void OnDestroy() =>
			EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;

		private void BuildUi()
		{
			m_canvas = new StateTreeCanvasElement();
			m_canvas.onBackgroundClicked += () => SelectNode(null);
			m_canvas.onViewChanged += UpdateZoomLabel;
			m_canvas.RegisterCallback<KeyDownEvent>(OnCanvasKeyDown);

			m_minimap = new StateTreeMinimapElement(m_canvas);

			m_inspector = new StateTreeInspectorElement();
			m_inspector.onChangeStateRequested += type => ExecuteMachineCommand("ChangeState", type);
			m_inspector.onTransitionToRequested += type => ExecuteMachineCommand("TransitionTo", type);
			m_inspector.onNavigateRequested += type =>
			{
				SelectNode(type);
				FrameSelectionOrAll();
			};

			m_history = new StateTreeHistoryElement();
			m_history.SetDisplay(EditorPrefs.GetBool(HistoryPrefsKey, true) ? DisplayStyle.Flex : DisplayStyle.None);

			m_extensionsLabel = new Label()
				.SetPosition(Position.Absolute)
				.SetLeft(8f)
				.SetBottom(8f)
				.SetFontSize(10)
				.SetColor(StateTreePalette.textSecondary)
				.SetBackgroundColor(StateTreePalette.panelBackground)
				.SetBorderColor(StateTreePalette.panelBorder)
				.SetBorderWidth(1f)
				.SetBorderRadius(8f)
				.SetPaddingX(8f)
				.SetPaddingY(3f)
				.SetDisplay(DisplayStyle.None);

			var canvasContainer = new VisualElement()
				.SetFlexGrow(1f)
				.AddChildren(m_canvas, m_extensionsLabel, m_minimap);

			rootVisualElement.AddChildren(
				BuildToolbar(),
				new VisualElement()
					.SetFlexDirection(FlexDirection.Row)
					.SetFlexGrow(1f)
					.AddChildren(canvasContainer, m_inspector),
				m_history);

			rootVisualElement.RegisterCallback<KeyDownEvent>(OnGlobalKeyDown, TrickleDown.TrickleDown);
		}

		private VisualElement BuildToolbar()
		{
			m_statusLabel = new Label()
				.SetFontSize(11)
				.SetMarginX(6f)
				.SetAlignSelf(Align.Center)
				.SetColor(StateTreePalette.textSecondary)
				.SetFlexShrink(1f)
				.SetOverflow(Overflow.Hidden)
				.SetTextOverflow(TextOverflow.Ellipsis)
				.SetWhiteSpace(WhiteSpace.NoWrap);

			m_directionButton = MakeToolbarButton(GetDirectionCaption(), "Layout direction", ShowDirectionMenu);
			m_edgeStyleButton = MakeToolbarButton(GetEdgeStyleCaption(), "Edge style", ShowEdgeStyleMenu);
			m_exportButton = MakeToolbarButton("Export ▾", "Export the graph as PNG or Mermaid", ShowExportMenu);

			Button transitionsChip = MakeToggleChip(
				"Transitions",
				"Show transition edges (dashed arrows)",
				m_transitionsVisible,
				isOn =>
				{
					m_transitionsVisible = isOn;
					EditorPrefs.SetBool(TransitionsPrefsKey, isOn);
					m_edges?.SetTransitionsVisible(isOn);
				});

			Button historyChip = MakeToggleChip(
				"History",
				"Show the transition history panel",
				EditorPrefs.GetBool(HistoryPrefsKey, true),
				isOn =>
				{
					m_history.SetDisplay(isOn ? DisplayStyle.Flex : DisplayStyle.None);
					EditorPrefs.SetBool(HistoryPrefsKey, isOn);
				});

			m_searchCountLabel = new Label()
				.SetFontSize(10)
				.SetColor(StateTreePalette.textDim)
				.SetAlignSelf(Align.Center)
				.SetMarginRight(2f);

			m_searchField = new TextField { selectAllOnFocus = true };
			m_searchField
				.SetTooltip("Search states by name (Ctrl+F). Enter jumps to the next match.")
				.SetWidth(150f)
				.SetFontSize(11)
				.SetMarginY(2f)
				.SetMarginX(2f)
				.SetAlignSelf(Align.Center);
			m_searchField.RegisterValueChangedCallback(change => OnSearchChanged(change.newValue));
			m_searchField.RegisterCallback<KeyDownEvent>(OnSearchKeyDown);

			// This Unity version has no built-in TextField placeholder (ITextEdition came later),
			// so the hint is an overlay label hidden while the field has text.
			m_searchPlaceholder = new Label("Search…")
				.SetPosition(Position.Absolute)
				.SetLeft(6f)
				.SetTop(0f)
				.SetBottom(0f)
				.SetFontSize(11)
				.SetColor(StateTreePalette.textDim)
				.SetUnityTextAlign(TextAnchor.MiddleLeft);
			m_searchPlaceholder.pickingMode = PickingMode.Ignore;
			m_searchField.Add(m_searchPlaceholder);

			m_zoomLabel = MakeToolbarButton("100%", "Reset zoom to 100%", () => m_canvas.SetZoom(1f))
				.SetMinWidth(44f);

			return new VisualElement()
				.SetFlexDirection(FlexDirection.Row)
				.SetHeight(26f)
				.SetPaddingX(2f)
				.SetBackgroundColor(StateTreePalette.panelBackground)
				.SetBorderColorBottom(StateTreePalette.panelBorder)
				.SetBorderWidthBottom(1f)
				.AddChildren(
					MakeToolbarButton("Refresh", "Rebuild the graph from code", RebuildGraph),
					MakeToolbarButton("Fit", "Frame the whole graph (F)", () => m_canvas.FrameContent(m_canvasSize)),
					MakeSeparator(),
					m_directionButton,
					m_edgeStyleButton,
					MakeSeparator(),
					transitionsChip,
					historyChip,
					MakeSeparator(),
					MakeToolbarButton("Reset Layout", "Discard dragged node positions for this view", ClearCustomLayout),
					m_exportButton,
					new VisualElement().SetFlexGrow(1f),
					m_statusLabel,
					MakeSeparator(),
					m_searchCountLabel,
					m_searchField,
					MakeSeparator(),
					MakeToolbarButton("−", "Zoom out", () => m_canvas.ZoomBy(1f / ZoomStep)),
					m_zoomLabel,
					MakeToolbarButton("+", "Zoom in", () => m_canvas.ZoomBy(ZoomStep)));
		}

		/// <summary>Flat toolbar button: transparent at rest, subtly lit on hover.</summary>
		private static Button MakeToolbarButton(string text, string tooltip, Action onClick)
		{
			var button = new Button()
				.SetText(text)
				.SetTooltip(tooltip)
				.AddClicked(onClick)
				.SetFontSize(11)
				.SetColor(StateTreePalette.textPrimary)
				.SetBackgroundColor(Color.clear)
				.SetBorderWidth(0f)
				.SetBorderRadius(4f)
				.SetMarginX(1f)
				.SetMarginY(3f)
				.SetPaddingX(8f)
				.SetPaddingY(0f);

			button.RegisterCallback<PointerEnterEvent>(_ =>
				button.SetBackgroundColor(StateTreePalette.toolbarButtonHover));
			button.RegisterCallback<PointerLeaveEvent>(_ =>
				button.SetBackgroundColor(Color.clear));

			return button;
		}

		/// <summary>
		/// Toolbar toggle styled as a chip: an accent background and border while on,
		/// so the state is readable at a glance (unlike a plain button).
		/// </summary>
		private static Button MakeToggleChip(string text, string tooltip, bool initialValue, Action<bool> onChanged)
		{
			bool isOn = initialValue;

			var chip = new Button()
				.SetText(text)
				.SetTooltip(tooltip)
				.SetFontSize(11)
				.SetBorderWidth(1f)
				.SetBorderRadius(4f)
				.SetMarginX(1f)
				.SetMarginY(3f)
				.SetPaddingX(8f)
				.SetPaddingY(0f);

			void Apply() =>
				chip.SetBackgroundColor(isOn ? StateTreePalette.toggleOnBackground : Color.clear)
					.SetBorderColor(isOn ? StateTreePalette.toggleOnBorder : Color.clear)
					.SetColor(isOn ? StateTreePalette.textPrimary : StateTreePalette.textSecondary);

			chip.AddClicked(() =>
			{
				isOn = !isOn;
				Apply();
				onChanged(isOn);
			});
			chip.RegisterCallback<PointerEnterEvent>(_ =>
			{
				if (!isOn) chip.SetBackgroundColor(StateTreePalette.toolbarButtonHover);
			});
			chip.RegisterCallback<PointerLeaveEvent>(_ => Apply());

			Apply();
			return chip;
		}

		private static VisualElement MakeSeparator() =>
			new VisualElement()
				.SetWidth(1f)
				.SetMarginX(4f)
				.SetMarginY(6f)
				.SetBackgroundColor(StateTreePalette.panelBorder);

		private void RebuildGraph()
		{
			m_roots = StateTreeGraphBuilder.Build();
			m_transitions = StateTreeGraphBuilder.BuildTransitions();
			ApplyCollapsedState(m_roots);
			m_canvasSize = StateTreeLayout.Arrange(m_roots, m_direction);
			ApplyCustomLayout(m_roots);
			CountTransitionsPerState();

			m_canvas.Clear();
			m_nodeElements.Clear();
			m_activeTypes.Clear();

			m_edges = new StateTreeEdgesElement(m_roots, m_transitions, m_direction, m_edgeStyle, m_transitionsVisible);
			m_canvas.AddChild(m_edges
				.SetPosition(Position.Absolute)
				.SetLeft(0f)
				.SetTop(0f)
				.SetWidth(m_canvasSize.x)
				.SetHeight(m_canvasSize.y));

			foreach (StateTreeNode root in m_roots)
			{
				CreateNodeElements(root);
			}

			m_inspector.SetGraphInfo(m_roots, m_transitions);
			m_minimap.SetContent(m_roots, m_canvasSize);
			RestoreSelection();
			m_canvas.FrameContent(m_canvasSize);
			PollActiveStates();
		}

		private void CountTransitionsPerState()
		{
			m_transitionCounts.Clear();

			foreach (StateTreeTransition transition in m_transitions)
			{
				m_transitionCounts.TryGetValue(transition.sourceState, out int sourceCount);
				m_transitionCounts[transition.sourceState] = sourceCount + 1;

				if (transition.targetState == transition.sourceState)
				{
					continue;
				}

				m_transitionCounts.TryGetValue(transition.targetState, out int targetCount);
				m_transitionCounts[transition.targetState] = targetCount + 1;
			}
		}

		private void ApplyCollapsedState(List<StateTreeNode> roots)
		{
			var pending = new Stack<StateTreeNode>(roots);
			while (pending.Count > 0)
			{
				StateTreeNode node = pending.Pop();
				node.isCollapsed = node.children.Count > 0 && m_collapsedTypes.Contains(node.stateType.FullName);

				foreach (StateTreeNode child in node.children)
				{
					pending.Push(child);
				}
			}
		}

		private void ApplyCustomLayout(List<StateTreeNode> roots)
		{
			Dictionary<string, Vector2> customPositions = StateTreeLayoutStorage.Load(m_direction);
			if (customPositions.Count == 0)
			{
				return;
			}

			var pending = new Stack<StateTreeNode>(roots);
			while (pending.Count > 0)
			{
				StateTreeNode node = pending.Pop();
				if (customPositions.TryGetValue(node.stateType.FullName, out Vector2 position))
				{
					node.position = new Rect(position, node.position.size);
				}

				foreach (StateTreeNode child in node.children)
				{
					pending.Push(child);
				}
			}
		}

		private void CreateNodeElements(StateTreeNode node)
		{
			m_transitionCounts.TryGetValue(node.stateType, out int transitionCount);

			var element = new StateTreeNodeElement(node, transitionCount);
			element.onSelected += selected => SelectNode(selected.stateType);
			element.onMoved += _ =>
			{
				m_edges.RefreshAfterNodeMove();
				m_minimap.MarkDirtyRepaint();
			};
			element.onDragCompleted += moved =>
				StateTreeLayoutStorage.Save(m_direction, moved.stateType, moved.position.position);
			element.onCollapseToggled += ToggleCollapse;
			element.AddManipulator(new ContextualMenuManipulator(populate => PopulateNodeMenu(populate, node)));

			m_nodeElements[node.stateType] = element;
			m_canvas.AddChild(element);

			if (node.isCollapsed)
			{
				return;
			}

			foreach (StateTreeNode child in node.children)
			{
				CreateNodeElements(child);
			}
		}

		private void ToggleCollapse(StateTreeNode node)
		{
			string typeName = node.stateType.FullName;
			if (!m_collapsedTypes.Remove(typeName))
			{
				m_collapsedTypes.Add(typeName);
			}

			EditorPrefs.SetString(CollapsedPrefsKey, string.Join(";", m_collapsedTypes));
			RebuildGraph();
		}

		private void PopulateNodeMenu(ContextualMenuPopulateEvent populate, StateTreeNode node)
		{
			DropdownMenuAction.Status controlStatus = canControlMachine
				? DropdownMenuAction.Status.Normal
				: DropdownMenuAction.Status.Disabled;

			populate.menu.AppendAction(
				"Change State",
				_ => ExecuteMachineCommand("ChangeState", node.stateType),
				_ => controlStatus);
			populate.menu.AppendAction(
				"Transition To",
				_ => ExecuteMachineCommand("TransitionTo", node.stateType),
				_ => controlStatus);
			populate.menu.AppendSeparator();

			if (node.children.Count > 0)
			{
				populate.menu.AppendAction(
					node.isCollapsed ? "Expand Subtree" : "Collapse Subtree",
					_ => ToggleCollapse(node));
			}

			populate.menu.AppendAction("Open Script", _ => node.stateType.OpenInScriptEditor());
		}

		private void SelectNode(Type stateType)
		{
			m_selectedType = stateType;

			foreach (KeyValuePair<Type, StateTreeNodeElement> pair in m_nodeElements)
			{
				pair.Value.SetSelected(pair.Key == stateType);
			}

			m_minimap.SetSelectedType(stateType);

			if (stateType != null && m_nodeElements.TryGetValue(stateType, out StateTreeNodeElement element))
			{
				m_inspector.Show(element.node);
				UpdateInspectorRuntime();
			}
			else
			{
				m_inspector.ShowPlaceholder();
			}

			UpdateHighlights();
		}

		private void RestoreSelection()
		{
			if (m_selectedType != null && !m_nodeElements.ContainsKey(m_selectedType))
			{
				m_selectedType = null;
			}

			SelectNode(m_selectedType);
		}

		/// <summary>
		/// Applies focus mode and search highlighting in one pass: while searching,
		/// matches get a bright ring and everything else fades; otherwise a selection
		/// fades all nodes unrelated to it (parent, children, transition peers) and
		/// tells the edge layer which node to focus.
		/// </summary>
		private void UpdateHighlights()
		{
			bool searchActive = !string.IsNullOrEmpty(m_searchQuery);
			HashSet<Type> related = !searchActive && m_selectedType != null ? BuildRelatedSet(m_selectedType) : null;

			foreach (KeyValuePair<Type, StateTreeNodeElement> pair in m_nodeElements)
			{
				bool isMatch = searchActive && IsSearchMatch(pair.Key);
				pair.Value.SetSearchMatch(isMatch);
				pair.Value.SetDimmed(searchActive ? !isMatch : related != null && !related.Contains(pair.Key));
			}

			m_edges?.SetFocusedType(searchActive ? null : m_selectedType);
		}

		private HashSet<Type> BuildRelatedSet(Type stateType)
		{
			var related = new HashSet<Type> { stateType };

			if (m_nodeElements.TryGetValue(stateType, out StateTreeNodeElement element))
			{
				StateTreeNode node = element.node;
				if (node.parent != null)
				{
					related.Add(node.parent.stateType);
				}

				foreach (StateTreeNode child in node.children)
				{
					related.Add(child.stateType);
				}
			}

			foreach (StateTreeTransition transition in m_transitions)
			{
				if (transition.sourceState == stateType)
				{
					related.Add(transition.targetState);
				}
				else if (transition.targetState == stateType)
				{
					related.Add(transition.sourceState);
				}
			}

			return related;
		}

		private bool IsSearchMatch(Type stateType) =>
			stateType.Name.IndexOf(m_searchQuery, StringComparison.OrdinalIgnoreCase) >= 0;

		private void OnSearchChanged(string query)
		{
			m_searchQuery = query?.Trim() ?? string.Empty;
			m_searchMatchIndex = 0;
			m_searchPlaceholder.SetDisplay(string.IsNullOrEmpty(query) ? DisplayStyle.Flex : DisplayStyle.None);
			UpdateSearchCountLabel();
			UpdateHighlights();
		}

		private void UpdateSearchCountLabel()
		{
			if (string.IsNullOrEmpty(m_searchQuery))
			{
				m_searchCountLabel.SetText(string.Empty);
				return;
			}

			var count = 0;
			foreach (Type stateType in m_nodeElements.Keys)
			{
				if (IsSearchMatch(stateType))
				{
					count++;
				}
			}

			m_searchCountLabel
				.SetText(count.ToString())
				.SetColor(count > 0 ? StateTreePalette.textSecondary : StateTreePalette.textDim);
		}

		private void OnSearchKeyDown(KeyDownEvent keyEvent)
		{
			if (keyEvent.keyCode is KeyCode.Return or KeyCode.KeypadEnter)
			{
				JumpToNextSearchMatch();
				keyEvent.StopPropagation();
			}
			else if (keyEvent.keyCode == KeyCode.Escape)
			{
				ClearSearch();
				m_canvas.Focus();
				keyEvent.StopPropagation();
			}
		}

		private void JumpToNextSearchMatch()
		{
			var matches = new List<Type>();
			foreach (Type stateType in m_nodeElements.Keys)
			{
				if (IsSearchMatch(stateType))
				{
					matches.Add(stateType);
				}
			}

			if (matches.Count == 0)
			{
				return;
			}

			matches.Sort((left, right) => string.CompareOrdinal(left.Name, right.Name));
			Type match = matches[m_searchMatchIndex % matches.Count];
			m_searchMatchIndex++;

			SelectNode(match);
			m_canvas.CenterOn(m_nodeElements[match].node.position.center);
		}

		private void ClearSearch()
		{
			m_searchField.SetValueWithoutNotify(string.Empty);
			m_searchPlaceholder.SetDisplay(DisplayStyle.Flex);
			m_searchQuery = string.Empty;
			m_searchMatchIndex = 0;
			UpdateSearchCountLabel();
			UpdateHighlights();
		}

		private void OnCanvasKeyDown(KeyDownEvent keyEvent)
		{
			if (keyEvent.keyCode == KeyCode.F && !keyEvent.ctrlKey && !keyEvent.commandKey)
			{
				FrameSelectionOrAll();
				keyEvent.StopPropagation();
			}
			else if (keyEvent.keyCode == KeyCode.Escape)
			{
				ClearSearch();
				SelectNode(null);
				keyEvent.StopPropagation();
			}
		}

		private void OnGlobalKeyDown(KeyDownEvent keyEvent)
		{
			if (keyEvent.keyCode == KeyCode.F && (keyEvent.ctrlKey || keyEvent.commandKey))
			{
				m_searchField.Focus();
				keyEvent.StopPropagation();
			}
		}

		private void FrameSelectionOrAll()
		{
			if (m_selectedType != null && m_nodeElements.TryGetValue(m_selectedType, out StateTreeNodeElement element))
			{
				m_canvas.CenterOn(element.node.position.center);
			}
			else
			{
				m_canvas.FrameContent(m_canvasSize);
			}
		}

		private void UpdateZoomLabel() =>
			m_zoomLabel.SetText($"{Mathf.RoundToInt(m_canvas.zoom * 100f)}%");

		private void ShowDirectionMenu()
		{
			var menu = new GenericMenu();
			AddDirectionItem(menu, "Top Down", StateTreeLayoutDirection.TopDown);
			AddDirectionItem(menu, "Left to Right", StateTreeLayoutDirection.LeftToRight);
			AddDirectionItem(menu, "Radial", StateTreeLayoutDirection.Radial);
			menu.DropDown(m_directionButton.worldBound);
		}

		private void AddDirectionItem(GenericMenu menu, string caption, StateTreeLayoutDirection direction) =>
			menu.AddItem(new GUIContent(caption), m_direction == direction, () => SetDirection(direction));

		private void SetDirection(StateTreeLayoutDirection direction)
		{
			if (m_direction == direction)
			{
				return;
			}

			m_direction = direction;
			EditorPrefs.SetInt(DirectionPrefsKey, (int)m_direction);
			m_directionButton.SetText(GetDirectionCaption());
			RebuildGraph();
		}

		private string GetDirectionCaption() =>
			m_direction switch
			{
				StateTreeLayoutDirection.TopDown => "View: Top Down ▾",
				StateTreeLayoutDirection.LeftToRight => "View: Left to Right ▾",
				_ => "View: Radial ▾"
			};

		private void ShowEdgeStyleMenu()
		{
			var menu = new GenericMenu();
			AddEdgeStyleItem(menu, "Curved", StateTreeEdgeStyle.Bezier);
			AddEdgeStyleItem(menu, "Straight", StateTreeEdgeStyle.Straight);
			AddEdgeStyleItem(menu, "Orthogonal", StateTreeEdgeStyle.Orthogonal);
			menu.DropDown(m_edgeStyleButton.worldBound);
		}

		private void AddEdgeStyleItem(GenericMenu menu, string caption, StateTreeEdgeStyle style) =>
			menu.AddItem(new GUIContent(caption), m_edgeStyle == style, () => SetEdgeStyle(style));

		private void SetEdgeStyle(StateTreeEdgeStyle style)
		{
			if (m_edgeStyle == style)
			{
				return;
			}

			m_edgeStyle = style;
			EditorPrefs.SetInt(EdgeStylePrefsKey, (int)m_edgeStyle);
			m_edgeStyleButton.SetText(GetEdgeStyleCaption());
			m_edges?.SetStyle(m_edgeStyle);
		}

		private string GetEdgeStyleCaption() =>
			m_edgeStyle switch
			{
				StateTreeEdgeStyle.Straight => "Lines: Straight ▾",
				StateTreeEdgeStyle.Orthogonal => "Lines: Orthogonal ▾",
				_ => "Lines: Curved ▾"
			};

		private void ClearCustomLayout()
		{
			StateTreeLayoutStorage.Clear(m_direction);
			RebuildGraph();
		}

		private void ShowExportMenu()
		{
			var menu = new GenericMenu();
			menu.AddItem(new GUIContent("PNG (current view)…"), false, ExportPng);
			menu.AddItem(new GUIContent("Mermaid to file…"), false, ExportMermaidToFile);
			menu.AddItem(new GUIContent("Copy Mermaid to clipboard"), false, CopyMermaidToClipboard);
			menu.DropDown(m_exportButton.worldBound);
		}

		private void ExportMermaidToFile()
		{
			string path = EditorUtility.SaveFilePanel("Export Mermaid", string.Empty, "HSM_StateTree", "mmd");
			if (string.IsNullOrEmpty(path))
			{
				return;
			}

			File.WriteAllText(path, StateTreeExporter.BuildMermaid(m_roots, m_transitions, m_direction));
			m_history.AddMessage($"Mermaid exported: {path}");
			EditorUtility.RevealInFinder(path);
		}

		private void CopyMermaidToClipboard()
		{
			EditorGUIUtility.systemCopyBuffer = StateTreeExporter.BuildMermaid(m_roots, m_transitions, m_direction);
			m_history.AddMessage("Mermaid copied to clipboard");
		}

		private void ExportPng()
		{
			string path = EditorUtility.SaveFilePanel("Export PNG", string.Empty, "HSM_StateTree", "png");
			if (string.IsNullOrEmpty(path))
			{
				return;
			}

			// Give the save dialog time to close and the window a repaint before
			// reading screen pixels — the capture is literally what is on screen.
			rootVisualElement.schedule.Execute(() => CaptureCanvasPng(path)).ExecuteLater(150);
		}

		private void CaptureCanvasPng(string path)
		{
			Rect canvasBound = m_canvas.worldBound;
			Vector2 screenPoint = position.position + canvasBound.position;
			float pixelsPerPoint = EditorGUIUtility.pixelsPerPoint;
			int width = Mathf.RoundToInt(canvasBound.width * pixelsPerPoint);
			int height = Mathf.RoundToInt(canvasBound.height * pixelsPerPoint);

			if (width <= 0 || height <= 0)
			{
				m_history.AddMessage("PNG export failed: canvas has no visible area");
				return;
			}

			Color[] pixels = InternalEditorUtility.ReadScreenPixel(screenPoint * pixelsPerPoint, width, height);
			var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);

			try
			{
				texture.SetPixels(pixels);
				texture.Apply();
				File.WriteAllBytes(path, texture.EncodeToPNG());
			}
			finally
			{
				DestroyImmediate(texture);
			}

			m_history.AddMessage($"PNG exported: {path}");
			EditorUtility.RevealInFinder(path);
		}

		private void ExecuteMachineCommand(string methodName, Type stateType)
		{
			if (!canControlMachine)
			{
				return;
			}

			try
			{
				MethodInfo method = typeof(MonoStateMachine)
					.GetMethod(methodName, Type.EmptyTypes)
					.MakeGenericMethod(stateType);
				method.Invoke(m_stateMachine, null);
				m_history.AddMessage($"Command {methodName}<{stateType.Name}>");
			}
			catch (Exception exception)
			{
				m_history.AddMessage($"Error {methodName}<{stateType.Name}>: {exception.InnerException?.Message ?? exception.Message}");
				Debug.LogException(exception);
			}

			PollActiveStates();
		}

		private void OnPlayModeStateChanged(PlayModeStateChange change)
		{
			if (change == PlayModeStateChange.EnteredPlayMode)
			{
				m_history.AddMessage("Play Mode started");
			}
			else if (change == PlayModeStateChange.EnteredEditMode)
			{
				m_history.AddMessage("Play Mode stopped");
				m_stateMachine = null;
				m_lastLeafName = null;
				PollActiveStates();
			}
		}

		private void PollActiveStates()
		{
			IReadOnlyList<IState> currentStates = ResolveCurrentStates();

			UpdateExtensionsOverlay();
			UpdateInspectorRuntime();

			var activeTypes = new HashSet<Type>();
			foreach (IState state in currentStates)
			{
				activeTypes.Add(state.GetType());
			}

			if (m_activeTypes.SetEquals(activeTypes))
			{
				return;
			}

			LogTransition(currentStates);
			m_activeTypes.Clear();
			m_activeTypes.UnionWith(activeTypes);

			foreach (KeyValuePair<Type, StateTreeNodeElement> pair in m_nodeElements)
			{
				pair.Value.SetActive(m_activeTypes.Contains(pair.Key));
			}

			m_edges.SetActiveTypes(m_activeTypes);
			m_minimap.SetActiveTypes(m_activeTypes);
			m_statusLabel.SetText(BuildStatusText(currentStates));
		}

		private void LogTransition(IReadOnlyList<IState> currentStates)
		{
			string leafName = currentStates.Count > 0 ? currentStates[^1].GetType().Name : null;
			if (leafName == m_lastLeafName)
			{
				return;
			}

			if (leafName != null)
			{
				m_history.AddTransition(m_lastLeafName ?? "∅", leafName);
			}

			m_lastLeafName = leafName;
		}

		private IReadOnlyList<IState> ResolveCurrentStates()
		{
			if (!EditorApplication.isPlaying)
			{
				m_stateMachine = null;
				return Array.Empty<IState>();
			}

			if (m_stateMachine == null)
			{
				m_stateMachine = FindFirstObjectByType<MonoStateMachine>(FindObjectsInactive.Include);
				if (m_stateMachine != null)
				{
					m_history.AddMessage($"MonoStateMachine found: {m_stateMachine.name}");
				}
			}

			return m_stateMachine == null ? Array.Empty<IState>() : m_stateMachine.CurrentStates;
		}

		private void UpdateExtensionsOverlay()
		{
			IReadOnlyList<IExtensionState> extensions = m_stateMachine != null
				? m_stateMachine.ActiveExtensions
				: Array.Empty<IExtensionState>();

			if (!EditorApplication.isPlaying || extensions.Count == 0)
			{
				m_extensionsLabel.SetDisplay(DisplayStyle.None);
				return;
			}

			var names = new List<string>(extensions.Count);
			foreach (IExtensionState extension in extensions)
			{
				names.Add(extension.GetType().Name);
			}

			m_extensionsLabel
				.SetText($"Extensions: {string.Join(", ", names)}")
				.SetDisplay(DisplayStyle.Flex);
		}

		private void UpdateInspectorRuntime()
		{
			bool isActive = m_selectedType != null && m_activeTypes.Contains(m_selectedType);
			m_inspector.UpdateRuntime(EditorApplication.isPlaying, canControlMachine, isActive);
		}

		private string BuildStatusText(IReadOnlyList<IState> currentStates)
		{
			if (!EditorApplication.isPlaying)
			{
				return "Edit Mode — machine not running";
			}

			if (m_stateMachine == null)
			{
				return "Play Mode — MonoStateMachine not found";
			}

			if (currentStates.Count == 0)
			{
				return "Play Mode — machine not initialized";
			}

			var names = new List<string>(currentStates.Count);
			foreach (IState state in currentStates)
			{
				names.Add(state.GetType().Name);
			}

			return string.Join(" → ", names);
		}
	}
}
