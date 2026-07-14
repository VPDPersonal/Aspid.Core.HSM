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
	/// with three layout modes and node dragging, an inspector for the selected node,
	/// a transition history, and control of a running <see cref="MonoStateMachine"/>
	/// (active chain highlighting, ChangeState / TransitionTo) in Play Mode.
	/// </summary>
	public sealed class StateTreeWindow : EditorWindow
	{
		private const long ActivePollIntervalMs = 200;
		private const string DirectionPrefsKey = "Aspid.HSM.StateTree.Direction";
		private const string EdgeStylePrefsKey = "Aspid.HSM.StateTree.EdgeStyle";
		private const string HistoryPrefsKey = "Aspid.HSM.StateTree.HistoryVisible";
		private const string TransitionsPrefsKey = "Aspid.HSM.StateTree.TransitionsVisible";
		private const string CollapsedPrefsKey = "Aspid.HSM.StateTree.Collapsed";

		private readonly Dictionary<Type, StateTreeNodeElement> m_nodeElements = new();
		private readonly HashSet<Type> m_activeTypes = new();
		private readonly HashSet<string> m_collapsedTypes = new();
		private List<StateTreeNode> m_roots;
		private List<StateTreeTransition> m_transitions;
		private StateTreeEdgesElement m_edges;
		private StateTreeCanvasElement m_canvas;
		private StateTreeInspectorElement m_inspector;
		private StateTreeHistoryElement m_history;
		private Label m_statusLabel;
		private Label m_extensionsLabel;
		private Button m_directionButton;
		private Button m_edgeStyleButton;
		private Button m_exportButton;
		private MonoStateMachine m_stateMachine;
		private StateTreeLayoutDirection m_direction;
		private StateTreeEdgeStyle m_edgeStyle;
		private bool m_transitionsVisible;
		private Type m_selectedType;
		private string m_lastLeafName;

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

			m_inspector = new StateTreeInspectorElement();
			m_inspector.onChangeStateRequested += type => ExecuteMachineCommand("ChangeState", type);
			m_inspector.onTransitionToRequested += type => ExecuteMachineCommand("TransitionTo", type);
			m_inspector.onNavigateRequested += SelectNode;

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
				.AddChildren(m_canvas, m_extensionsLabel);

			rootVisualElement.AddChildren(
				BuildToolbar(),
				new VisualElement()
					.SetFlexDirection(FlexDirection.Row)
					.SetFlexGrow(1f)
					.AddChildren(canvasContainer, m_inspector),
				m_history);
		}

		private VisualElement BuildToolbar()
		{
			m_statusLabel = new Label()
				.SetFontSize(11)
				.SetMarginX(8f)
				.SetAlignSelf(Align.Center)
				.SetColor(StateTreePalette.textSecondary);

			m_directionButton = new Button()
				.SetText(GetDirectionCaption())
				.AddClicked(CycleDirection);

			m_edgeStyleButton = new Button()
				.SetText(GetEdgeStyleCaption())
				.AddClicked(CycleEdgeStyle);

			var transitionsToggle = new Toggle { text = "Transitions", value = m_transitionsVisible };
			transitionsToggle
				.SetFontSize(11)
				.SetMarginX(6f)
				.SetAlignSelf(Align.Center);
			transitionsToggle.RegisterValueChangedCallback(change =>
			{
				m_transitionsVisible = change.newValue;
				EditorPrefs.SetBool(TransitionsPrefsKey, m_transitionsVisible);
				m_edges?.SetTransitionsVisible(m_transitionsVisible);
			});

			m_exportButton = new Button()
				.SetText("Export")
				.AddClicked(ShowExportMenu);

			return new VisualElement()
				.SetFlexDirection(FlexDirection.Row)
				.SetHeight(24f)
				.SetBackgroundColor(StateTreePalette.panelBackground)
				.SetBorderColorBottom(StateTreePalette.panelBorder)
				.SetBorderWidthBottom(1f)
				.AddChildren(
					new Button()
						.SetText("Refresh")
						.AddClicked(RebuildGraph),
					m_directionButton,
					m_edgeStyleButton,
					transitionsToggle,
					new Button()
						.SetText("Reset Layout")
						.AddClicked(ClearCustomLayout),
					new Button()
						.SetText("History")
						.AddClicked(ToggleHistory),
					m_exportButton,
					new VisualElement().SetFlexGrow(1f),
					m_statusLabel);
		}

		private void RebuildGraph()
		{
			m_roots = StateTreeGraphBuilder.Build();
			m_transitions = StateTreeGraphBuilder.BuildTransitions();
			ApplyCollapsedState(m_roots);
			Vector2 canvasSize = StateTreeLayout.Arrange(m_roots, m_direction);
			ApplyCustomLayout(m_roots);

			m_canvas.Clear();
			m_nodeElements.Clear();
			m_activeTypes.Clear();

			m_edges = new StateTreeEdgesElement(m_roots, m_transitions, m_direction, m_edgeStyle, m_transitionsVisible);
			m_canvas.AddChild(m_edges
				.SetPosition(Position.Absolute)
				.SetLeft(0f)
				.SetTop(0f)
				.SetWidth(canvasSize.x)
				.SetHeight(canvasSize.y));

			foreach (StateTreeNode root in m_roots)
			{
				CreateNodeElements(root);
			}

			RestoreSelection();
			m_canvas.FrameContent(canvasSize);
			PollActiveStates();
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
			var element = new StateTreeNodeElement(node);
			element.onSelected += selected => SelectNode(selected.stateType);
			element.onMoved += _ => m_edges.MarkDirtyRepaint();
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

			if (stateType != null && m_nodeElements.TryGetValue(stateType, out StateTreeNodeElement element))
			{
				m_inspector.Show(element.node);
				UpdateInspectorRuntime();
			}
			else
			{
				m_inspector.ShowPlaceholder();
			}
		}

		private void RestoreSelection()
		{
			if (m_selectedType != null && !m_nodeElements.ContainsKey(m_selectedType))
			{
				m_selectedType = null;
			}

			SelectNode(m_selectedType);
		}

		private void CycleDirection()
		{
			m_direction = (StateTreeLayoutDirection)(((int)m_direction + 1) % 3);
			EditorPrefs.SetInt(DirectionPrefsKey, (int)m_direction);
			m_directionButton.SetText(GetDirectionCaption());
			RebuildGraph();
		}

		private string GetDirectionCaption() =>
			m_direction switch
			{
				StateTreeLayoutDirection.TopDown => "View: Top Down",
				StateTreeLayoutDirection.LeftToRight => "View: Left to Right",
				_ => "View: Radial"
			};

		private void CycleEdgeStyle()
		{
			m_edgeStyle = (StateTreeEdgeStyle)(((int)m_edgeStyle + 1) % 3);
			EditorPrefs.SetInt(EdgeStylePrefsKey, (int)m_edgeStyle);
			m_edgeStyleButton.SetText(GetEdgeStyleCaption());
			m_edges?.SetStyle(m_edgeStyle);
		}

		private string GetEdgeStyleCaption() =>
			m_edgeStyle switch
			{
				StateTreeEdgeStyle.Straight => "Lines: Straight",
				StateTreeEdgeStyle.Orthogonal => "Lines: Orthogonal",
				_ => "Lines: Curved"
			};

		private void ClearCustomLayout()
		{
			StateTreeLayoutStorage.Clear(m_direction);
			RebuildGraph();
		}

		private void ToggleHistory()
		{
			bool isVisible = m_history.resolvedStyle.display == DisplayStyle.Flex;
			m_history.SetDisplay(isVisible ? DisplayStyle.None : DisplayStyle.Flex);
			EditorPrefs.SetBool(HistoryPrefsKey, !isVisible);
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
