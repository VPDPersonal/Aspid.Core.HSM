using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using Aspid.FastTools.Types.Editors;
using Aspid.FastTools.UIElements;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM.Editor
{
	/// <summary>
	/// Inspector for the selected HSM graph node: a header card mirroring the node's
	/// design (depth-colored plate), a clickable root→state breadcrumb with children
	/// chips, the state's transitions, controller cards with role badges, runtime
	/// status, and <c>ChangeState</c> / <c>TransitionTo</c> commands in Play Mode.
	/// The panel width is draggable by the left edge and persisted.
	/// </summary>
	public sealed class StateTreeInspectorElement : VisualElement
	{
		private const float DefaultWidth = 280f;
		private const float MinPanelWidth = 220f;
		private const float MaxPanelWidth = 420f;
		private const string WidthPrefsKey = "Aspid.HSM.StateTree.InspectorWidth";

		public event Action<Type> onChangeStateRequested;
		public event Action<Type> onTransitionToRequested;
		public event Action<Type> onNavigateRequested;

		private readonly VisualElement m_content;

		private StateTreeNode m_node;
		private Label m_runtimeStatus;
		private Label m_runtimeHint;
		private Button m_changeStateButton;
		private Button m_transitionToButton;
		private List<StateTreeNode> m_roots;
		private List<StateTreeTransition> m_transitions;

		public StateTreeInspectorElement()
		{
			m_content = new VisualElement()
				.SetFlexGrow(1f)
				.SetPaddingX(12f)
				.SetPaddingY(10f);

			this.SetWidth(Mathf.Clamp(EditorPrefs.GetFloat(WidthPrefsKey, DefaultWidth), MinPanelWidth, MaxPanelWidth))
				.SetBackgroundColor(StateTreePalette.panelBackground)
				.SetBorderColorLeft(StateTreePalette.panelBorder)
				.SetBorderWidthLeft(1f)
				.AddChildren(m_content, BuildResizeHandle());

			ShowPlaceholder();
		}

		/// <summary>
		/// Thin grip on the panel's left edge: dragging it resizes the inspector
		/// (clamped), and the width is saved to EditorPrefs on release.
		/// </summary>
		private VisualElement BuildResizeHandle()
		{
			var handle = new VisualElement()
				.SetPosition(Position.Absolute)
				.SetLeft(0f)
				.SetTop(0f)
				.SetBottom(0f)
				.SetWidth(4f);

			int pointerId = -1;
			float startWidth = 0f;
			float startX = 0f;

			handle.RegisterCallback<PointerEnterEvent>(_ =>
				handle.SetBackgroundColor(StateTreePalette.Faded(StateTreePalette.selectionBorder, 0.6f)));
			handle.RegisterCallback<PointerLeaveEvent>(_ =>
			{
				if (pointerId < 0) handle.SetBackgroundColor(Color.clear);
			});
			handle.RegisterCallback<PointerDownEvent>(pointerEvent =>
			{
				if (pointerEvent.button != 0)
				{
					return;
				}

				pointerId = pointerEvent.pointerId;
				startWidth = resolvedStyle.width;
				startX = pointerEvent.position.x;
				handle.CapturePointer(pointerId);
				pointerEvent.StopPropagation();
			});
			handle.RegisterCallback<PointerMoveEvent>(pointerEvent =>
			{
				if (pointerEvent.pointerId != pointerId)
				{
					return;
				}

				// The handle is on the left edge, so dragging left grows the panel.
				this.SetWidth(Mathf.Clamp(startWidth + (startX - pointerEvent.position.x), MinPanelWidth, MaxPanelWidth));
			});
			handle.RegisterCallback<PointerUpEvent>(pointerEvent =>
			{
				if (pointerEvent.pointerId != pointerId)
				{
					return;
				}

				handle.ReleasePointer(pointerId);
				pointerId = -1;
				handle.SetBackgroundColor(Color.clear);
				EditorPrefs.SetFloat(WidthPrefsKey, resolvedStyle.width);
			});

			return handle;
		}

		/// <summary>
		/// Supplies the current graph so the empty-selection placeholder can show
		/// an overview of the machine and the node view can list its transitions.
		/// </summary>
		public void SetGraphInfo(List<StateTreeNode> roots, List<StateTreeTransition> transitions)
		{
			m_roots = roots;
			m_transitions = transitions;

			if (m_node == null)
			{
				ShowPlaceholder();
			}
		}

		public void ShowPlaceholder()
		{
			m_node = null;
			m_content.Clear();

			if (m_roots == null)
			{
				m_content.AddChild(new Label("Select a node on the graph")
					.SetFontSize(11)
					.SetColor(StateTreePalette.textDim)
					.SetMarginTop(16f)
					.SetAlignSelf(Align.Center));
				return;
			}

			var scroll = new ScrollView(ScrollViewMode.Vertical);
			m_content.AddChild(scroll.SetFlexGrow(1f));

			var totalStates = 0;
			var maxDepth = 0;
			foreach (StateTreeNode root in m_roots)
			{
				totalStates += 1 + root.CountDescendants();
				maxDepth = Mathf.Max(maxDepth, MeasureDepth(root));
			}

			scroll.AddChildren(
				new Label("HSM Overview")
					.SetFontSize(13)
					.SetColor(StateTreePalette.textPrimary)
					.SetUnityFontStyleAndWeight(FontStyle.Bold)
					.SetMarginBottom(2f),
				new Label("Select a node to inspect it")
					.SetFontSize(10)
					.SetColor(StateTreePalette.textDim)
					.SetMarginBottom(8f),
				BuildSectionTitle("Graph"),
				BuildInfoRow("States", totalStates.ToString()),
				BuildInfoRow("Roots", m_roots.Count.ToString()),
				BuildInfoRow("Transitions", m_transitions?.Count.ToString() ?? "0"),
				BuildInfoRow("Max depth", maxDepth.ToString()),
				BuildSectionTitle("Depth colors"),
				BuildDepthLegend(maxDepth),
				BuildSectionTitle("Shortcuts"),
				BuildShortcutRow("Click", "Select state"),
				BuildShortcutRow("Double-click", "Open script"),
				BuildShortcutRow("Right-click", "State commands"),
				BuildShortcutRow("Drag node", "Custom layout"),
				BuildShortcutRow("Wheel", "Zoom at cursor"),
				BuildShortcutRow("F", "Frame view / selection"),
				BuildShortcutRow("Ctrl+F", "Search states"),
				BuildShortcutRow("Esc", "Clear selection"));
		}

		public void Show(StateTreeNode node)
		{
			m_node = node;
			m_content.Clear();

			var scroll = new ScrollView(ScrollViewMode.Vertical);
			m_content.AddChild(scroll.SetFlexGrow(1f));

			scroll.AddChildren(
				BuildHeaderCard(node),
				BuildSectionTitle("Hierarchy"),
				BuildBreadcrumb(node),
				BuildChildrenChips(node));

			AddTransitionsSection(scroll, node);

			List<StateTreeControllerScanner.Entry> controllers = StateTreeControllerScanner.Scan(node.stateType);
			scroll.AddChildren(
				BuildSectionTitle("Controllers", controllers.Count),
				BuildControllersSection(controllers),
				BuildSectionTitle("Runtime"),
				BuildRuntimeSection());
		}

		public void UpdateRuntime(bool isPlaying, bool isMachineReady, bool isActive)
		{
			if (m_node == null)
			{
				return;
			}

			bool canControl = isPlaying && isMachineReady;
			m_changeStateButton.SetEnabled(canControl);
			m_transitionToButton.SetEnabled(canControl);

			if (!isPlaying)
			{
				SetRuntimePill("Edit Mode", StateTreePalette.textDim, StateTreePalette.cardBorder, StateTreePalette.cardBackground);
				m_runtimeHint.SetText("Enter Play Mode to control the machine").SetDisplay(DisplayStyle.Flex);
			}
			else if (!isMachineReady)
			{
				SetRuntimePill("Machine not found", StateTreePalette.textDim, StateTreePalette.cardBorder, StateTreePalette.cardBackground);
				m_runtimeHint.SetText("Waiting for an initialized MonoStateMachine…").SetDisplay(DisplayStyle.Flex);
			}
			else if (isActive)
			{
				SetRuntimePill("● Active", StateTreePalette.activeText,
					StateTreePalette.Faded(StateTreePalette.activeBorder, 0.7f), StateTreePalette.activeBackground);
				m_runtimeHint.SetDisplay(DisplayStyle.None);
			}
			else
			{
				SetRuntimePill("○ Inactive", StateTreePalette.textSecondary, StateTreePalette.cardBorder, StateTreePalette.cardBackground);
				m_runtimeHint.SetDisplay(DisplayStyle.None);
			}
		}

		private void SetRuntimePill(string text, Color textColor, Color borderColor, Color backgroundColor) =>
			m_runtimeStatus
				.SetText(text)
				.SetColor(textColor)
				.SetBorderColor(borderColor)
				.SetBackgroundColor(backgroundColor);

		/// <summary>
		/// Single-line header plate mirroring the graph node design: the depth color,
		/// the state name (full type name in the tooltip), a depth badge, and an inline
		/// open-script folder button as in FastTools' TypeSelector. No extra rows.
		/// </summary>
		private VisualElement BuildHeaderCard(StateTreeNode node)
		{
			Color depthColor = StateTreePalette.GetDepthColor(node.depth);

			var title = new Label(node.stateType.Name)
				.SetTooltip(node.stateType.FullName)
				.SetFontSize(12)
				.SetUnityFontStyleAndWeight(FontStyle.Bold)
				.SetColor(StateTreePalette.textPrimary)
				.SetFlexGrow(1f)
				.SetFlexShrink(1f)
				.SetOverflow(Overflow.Hidden)
				.SetTextOverflow(TextOverflow.Ellipsis)
				.SetWhiteSpace(WhiteSpace.NoWrap);

			var depthBadge = new Label($"L{node.depth}")
				.SetTooltip($"Depth {node.depth}" + (node.depth == 0 ? " — root" : string.Empty))
				.SetFontSize(9)
				.SetColor(depthColor)
				.SetFlexShrink(0f)
				.SetMarginRight(4f);

			Button openButton = MakeIconButton("d_Folder Icon", "d_FolderOpened Icon", "Open Script",
				() => node.stateType.OpenInScriptEditor());

			// A subtle tint instead of a solid depth-color fill, so the header reads as
			// part of the dark panel rather than shouting over it.
			return new VisualElement()
				.SetHeight(24f)
				.SetFlexDirection(FlexDirection.Row)
				.SetAlignItems(Align.Center)
				.SetPaddingLeft(8f)
				.SetPaddingRight(3f)
				.SetBorderRadius(8f)
				.SetBorderWidth(1f)
				.SetBorderColor(StateTreePalette.Faded(depthColor, 0.45f))
				.SetOverflow(Overflow.Hidden)
				.SetBackgroundColor(StateTreePalette.Faded(depthColor, 0.14f))
				.AddChildren(title, depthBadge, openButton);
		}

		/// <summary>
		/// 18×18 icon button in the TypeSelector spirit: a bare editor icon that swaps
		/// to the hover variant (e.g. closed → opened folder) under the cursor.
		/// </summary>
		private static Button MakeIconButton(string iconName, string hoverIconName, string tooltip, Action onClick)
		{
			var idleIcon = new StyleBackground((Texture2D)EditorGUIUtility.IconContent(iconName).image);
			var hoverIcon = new StyleBackground((Texture2D)EditorGUIUtility.IconContent(hoverIconName).image);

			var icon = new VisualElement().SetFlexGrow(1f);
			icon.style.backgroundImage = idleIcon;
			icon.pickingMode = PickingMode.Ignore;

			var button = new Button()
				.AddClicked(onClick)
				.SetTooltip(tooltip)
				.SetSize(18f, 18f)
				.SetFlexShrink(0f)
				.SetPadding(1f)
				.SetMargin(0f)
				.SetBackgroundColor(Color.clear)
				.SetBorderWidth(0f)
				.SetBorderRadius(3f)
				.AddChild(icon);

			button.RegisterCallback<PointerEnterEvent>(_ => icon.style.backgroundImage = hoverIcon);
			button.RegisterCallback<PointerLeaveEvent>(_ => icon.style.backgroundImage = idleIcon);

			return button;
		}

		/// <summary>
		/// Root→state path, one row per level: a depth-colored dot plus a clickable
		/// ancestor link; the inspected state itself is the bold, non-clickable tail.
		/// </summary>
		private VisualElement BuildBreadcrumb(StateTreeNode node)
		{
			var chain = new List<StateTreeNode>();
			for (StateTreeNode current = node; current != null; current = current.parent)
			{
				chain.Add(current);
			}

			chain.Reverse();

			var breadcrumb = new VisualElement().SetMarginBottom(4f);

			foreach (StateTreeNode level in chain)
			{
				bool isCurrent = level == node;

				var dot = new VisualElement()
					.SetSize(7f, 7f)
					.SetBorderRadius(3.5f)
					.SetMarginRight(6f)
					.SetFlexShrink(0f)
					.SetAlignSelf(Align.Center)
					.SetBackgroundColor(StateTreePalette.GetDepthColor(level.depth));

				VisualElement name = isCurrent
					? new Label(level.stateType.Name)
						.SetFontSize(11)
						.SetColor(StateTreePalette.textPrimary)
						.SetUnityFontStyleAndWeight(FontStyle.Bold)
					: MakeLink(level.stateType.Name, level.stateType);

				breadcrumb.AddChild(new VisualElement()
					.SetFlexDirection(FlexDirection.Row)
					.SetMarginLeft(level.depth * 10f)
					.SetMarginBottom(2f)
					.AddChildren(dot, name));
			}

			return breadcrumb;
		}

		/// <summary>Children as clickable pill chips (or a dim "leaf" note for a leaf state).</summary>
		private VisualElement BuildChildrenChips(StateTreeNode node)
		{
			if (node.children.Count == 0)
			{
				return new Label("No children — leaf state")
					.SetFontSize(10)
					.SetColor(StateTreePalette.textDim);
			}

			var chips = new VisualElement()
				.SetFlexDirection(FlexDirection.Row)
				.SetFlexWrap(Wrap.Wrap);

			foreach (StateTreeNode child in node.children)
			{
				chips.AddChild(BuildChildChip(child));
			}

			return chips;
		}

		private VisualElement BuildChildChip(StateTreeNode child)
		{
			var dot = new VisualElement()
				.SetSize(6f, 6f)
				.SetBorderRadius(3f)
				.SetMarginRight(4f)
				.SetFlexShrink(0f)
				.SetAlignSelf(Align.Center)
				.SetBackgroundColor(StateTreePalette.GetDepthColor(child.depth));

			var name = new Label(child.stateType.Name)
				.SetFontSize(10)
				.SetColor(StateTreePalette.textSecondary);

			var chip = new VisualElement()
				.SetTooltip(child.stateType.FullName)
				.SetFlexDirection(FlexDirection.Row)
				.SetAlignItems(Align.Center)
				.SetPaddingX(6f)
				.SetPaddingY(2f)
				.SetMarginRight(4f)
				.SetMarginBottom(4f)
				.SetBorderRadius(9f)
				.SetBorderWidth(1f)
				.SetBorderColor(StateTreePalette.cardBorder)
				.SetBackgroundColor(StateTreePalette.cardBackground)
				.AddChildren(dot, name);

			chip.RegisterCallback<PointerEnterEvent>(_ =>
			{
				chip.SetBorderColor(StateTreePalette.nodeBorderHover);
				name.SetColor(StateTreePalette.textPrimary);
			});
			chip.RegisterCallback<PointerLeaveEvent>(_ =>
			{
				chip.SetBorderColor(StateTreePalette.cardBorder);
				name.SetColor(StateTreePalette.textSecondary);
			});
			chip.RegisterCallback<ClickEvent>(_ => onNavigateRequested?.Invoke(child.stateType));

			return chip;
		}

		/// <summary>
		/// Adds a "Transitions" section listing outgoing (→ target) and incoming
		/// (← source) transitions of the state; skipped entirely when there are none.
		/// </summary>
		private void AddTransitionsSection(VisualElement parent, StateTreeNode node)
		{
			if (m_transitions == null)
			{
				return;
			}

			var rows = new List<VisualElement>();

			foreach (StateTreeTransition transition in m_transitions)
			{
				if (transition.sourceState == node.stateType)
				{
					rows.Add(BuildTransitionRow("→", transition.targetState, transition));
				}
				else if (transition.targetState == node.stateType)
				{
					rows.Add(BuildTransitionRow("←", transition.sourceState, transition));
				}
			}

			if (rows.Count == 0)
			{
				return;
			}

			parent.AddChild(BuildSectionTitle("Transitions", rows.Count));
			foreach (VisualElement row in rows)
			{
				parent.AddChild(row);
			}
		}

		private VisualElement BuildTransitionRow(string arrow, Type peerState, StateTreeTransition transition)
		{
			var arrowLabel = new Label(arrow)
				.SetFontSize(11)
				.SetColor(StateTreePalette.transitionEdge)
				.SetWidth(14f)
				.SetFlexShrink(0f);

			Label peerLink = MakeLink(peerState.Name, peerState);

			var transitionName = new Label(GetShortTransitionName(transition))
				.SetTooltip(transition.transitionType.FullName)
				.SetFontSize(9)
				.SetColor(StateTreePalette.textDim)
				.SetMarginLeft(6f)
				.SetFlexShrink(1f)
				.SetOverflow(Overflow.Hidden)
				.SetTextOverflow(TextOverflow.Ellipsis)
				.SetWhiteSpace(WhiteSpace.NoWrap)
				.SetAlignSelf(Align.Center);

			return new VisualElement()
				.SetFlexDirection(FlexDirection.Row)
				.SetMarginBottom(2f)
				.AddChildren(arrowLabel, peerLink, transitionName);
		}

		private static string GetShortTransitionName(StateTreeTransition transition)
		{
			const string suffix = "Transition";
			string name = transition.transitionType.Name;
			return name.Length > suffix.Length && name.EndsWith(suffix, StringComparison.Ordinal)
				? name[..^suffix.Length]
				: name;
		}

		private const int ControllerFilterThreshold = 9;

		/// <summary>
		/// Compact controller list: one row per controller (name left, role badges right)
		/// so a hundred controllers stay scannable; long lists get a filter field on top.
		/// </summary>
		private VisualElement BuildControllersSection(List<StateTreeControllerScanner.Entry> controllers)
		{
			var section = new VisualElement();

			if (controllers.Count == 0)
			{
				section.AddChild(new Label("No controllers")
					.SetFontSize(11)
					.SetColor(StateTreePalette.textDim));
				return section;
			}

			var rows = new List<(VisualElement element, string name)>(controllers.Count);
			var rowsContainer = new VisualElement();

			foreach (StateTreeControllerScanner.Entry controller in controllers)
			{
				VisualElement row = BuildControllerRow(controller.title, controller.type);
				rows.Add((row, controller.title));
				rowsContainer.AddChild(row);
			}

			if (controllers.Count >= ControllerFilterThreshold)
			{
				section.AddChild(BuildControllerFilter(rows));
			}

			section.AddChild(rowsContainer);
			return section;
		}

		private VisualElement BuildControllerFilter(List<(VisualElement element, string name)> rows)
		{
			var field = new TextField();
			field
				.SetFontSize(10)
				.SetMarginBottom(3f);

			var placeholder = new Label("Filter…")
				.SetPosition(Position.Absolute)
				.SetLeft(6f)
				.SetTop(0f)
				.SetBottom(0f)
				.SetFontSize(10)
				.SetColor(StateTreePalette.textDim)
				.SetUnityTextAlign(TextAnchor.MiddleLeft);
			placeholder.pickingMode = PickingMode.Ignore;
			field.Add(placeholder);

			field.RegisterValueChangedCallback(change =>
			{
				string query = change.newValue?.Trim() ?? string.Empty;
				placeholder.SetDisplay(string.IsNullOrEmpty(change.newValue) ? DisplayStyle.Flex : DisplayStyle.None);

				foreach ((VisualElement element, string name) in rows)
				{
					bool isVisible = query.Length == 0 || name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
					element.SetDisplay(isVisible ? DisplayStyle.Flex : DisplayStyle.None);
				}
			});

			return field;
		}

		/// <summary>
		/// Single-line controller row: the clickable class name (ellipsized) on the left
		/// and the role badges (Update, Enter, …) on the right; a badge click jumps to
		/// the implementing method.
		/// </summary>
		private VisualElement BuildControllerRow(string title, Type controllerType)
		{
			var badges = new VisualElement()
				.SetFlexDirection(FlexDirection.Row)
				.SetFlexWrap(Wrap.Wrap)
				.SetJustifyContent(Justify.FlexEnd)
				.SetFlexShrink(0f)
				.SetAlignSelf(Align.Center);

			foreach (Type controllerInterface in controllerType.GetInterfaces())
			{
				if (controllerInterface == typeof(IController)
					|| !typeof(IController).IsAssignableFrom(controllerInterface))
				{
					continue;
				}

				string role = controllerInterface.Name.TrimStart('I').Replace("Controller", string.Empty);
				MethodInfo[] methods = controllerInterface.GetMethods();

				badges.AddChild(BuildBadge(role,
					methods.Length > 0 ? () => OpenControllerMethod(controllerType, controllerInterface, methods[0]) : null));
			}

			Label titleLabel = MakeLinkWithAction(title, () => controllerType.OpenInScriptEditor());
			titleLabel
				.SetTooltip(controllerType.FullName)
				.SetFlexGrow(1f)
				.SetFlexShrink(1f)
				.SetOverflow(Overflow.Hidden)
				.SetTextOverflow(TextOverflow.Ellipsis)
				.SetWhiteSpace(WhiteSpace.NoWrap)
				.SetAlignSelf(Align.Center)
				.SetMarginRight(6f);

			var row = new VisualElement()
				.SetFlexDirection(FlexDirection.Row)
				.SetPaddingX(4f)
				.SetPaddingY(1f)
				.SetBorderRadius(4f)
				.SetMarginBottom(1f)
				.AddChildren(titleLabel, badges);

			row.RegisterCallback<PointerEnterEvent>(_ => row.SetBackgroundColor(StateTreePalette.cardBackground));
			row.RegisterCallback<PointerLeaveEvent>(_ => row.SetBackgroundColor(Color.clear));

			return row;
		}

		private VisualElement BuildRuntimeSection()
		{
			m_runtimeStatus = new Label()
				.SetFontSize(10)
				.SetPaddingX(8f)
				.SetPaddingY(2f)
				.SetBorderRadius(9f)
				.SetBorderWidth(1f)
				.SetAlignSelf(Align.FlexStart)
				.SetMarginBottom(6f);

			m_changeStateButton = MakeActionButton("Change State", () => onChangeStateRequested?.Invoke(m_node.stateType))
				.SetTooltip("Hard-switch the machine to this state")
				.SetFlexGrow(1f)
				.SetMarginRight(4f);

			m_transitionToButton = MakeActionButton("Transition To", () => onTransitionToRequested?.Invoke(m_node.stateType))
				.SetTooltip("Switch through a registered transition")
				.SetFlexGrow(1f);

			var buttons = new VisualElement()
				.SetFlexDirection(FlexDirection.Row)
				.AddChildren(m_changeStateButton, m_transitionToButton);

			m_runtimeHint = new Label()
				.SetFontSize(9)
				.SetColor(StateTreePalette.textDim)
				.SetWhiteSpace(WhiteSpace.Normal)
				.SetMarginTop(4f);

			return new VisualElement().AddChildren(m_runtimeStatus, buttons, m_runtimeHint);
		}

		/// <summary>Flat bordered button used for inspector actions (Open Script, runtime commands).</summary>
		private static Button MakeActionButton(string text, Action onClick)
		{
			var button = new Button()
				.SetText(text)
				.AddClicked(onClick)
				.SetFontSize(11)
				.SetColor(StateTreePalette.textPrimary)
				.SetBackgroundColor(StateTreePalette.cardBackground)
				.SetBorderColor(StateTreePalette.cardBorder)
				.SetBorderWidth(1f)
				.SetBorderRadius(4f)
				.SetMargin(0f)
				.SetPaddingX(8f)
				.SetPaddingY(3f);

			button.RegisterCallback<PointerEnterEvent>(_ =>
			{
				if (button.enabledSelf) button.SetBackgroundColor(StateTreePalette.nodeBackgroundHover);
			});
			button.RegisterCallback<PointerLeaveEvent>(_ =>
				button.SetBackgroundColor(StateTreePalette.cardBackground));

			return button;
		}

		private Label MakeLink(string text, Type target) =>
			MakeLinkWithAction(text, () => onNavigateRequested?.Invoke(target));

		private static Label MakeLinkWithAction(string text, Action onClick)
		{
			var link = new Label(text)
				.SetFontSize(11)
				.SetColor(StateTreePalette.selectionBorder);

			link.RegisterCallback<PointerEnterEvent>(_ =>
				link.SetColor(Color.Lerp(StateTreePalette.selectionBorder, Color.white, 0.35f)));
			link.RegisterCallback<PointerLeaveEvent>(_ =>
				link.SetColor(StateTreePalette.selectionBorder));
			link.RegisterCallback<ClickEvent>(_ => onClick());

			return link;
		}

		private VisualElement BuildDepthLegend(int maxDepth)
		{
			var legend = new VisualElement();
			int levels = Mathf.Min(maxDepth, 5);

			for (var depth = 0; depth <= levels; depth++)
			{
				var dot = new VisualElement()
					.SetSize(8f, 8f)
					.SetBorderRadius(4f)
					.SetMarginRight(6f)
					.SetAlignSelf(Align.Center)
					.SetFlexShrink(0f)
					.SetBackgroundColor(StateTreePalette.GetDepthColor(depth));

				var label = new Label(depth == 0 ? "Level 0 — root" : $"Level {depth}")
					.SetFontSize(11)
					.SetColor(StateTreePalette.textSecondary);

				legend.AddChild(new VisualElement()
					.SetFlexDirection(FlexDirection.Row)
					.SetMarginBottom(2f)
					.AddChildren(dot, label));
			}

			return legend;
		}

		private VisualElement BuildShortcutRow(string key, string action)
		{
			var keyLabel = new Label(key)
				.SetFontSize(10)
				.SetColor(StateTreePalette.textSecondary)
				.SetBackgroundColor(StateTreePalette.cardBackground)
				.SetBorderColor(StateTreePalette.cardBorder)
				.SetBorderWidth(1f)
				.SetBorderRadius(4f)
				.SetPaddingX(5f)
				.SetPaddingY(1f)
				.SetMarginRight(6f)
				.SetFlexShrink(0f)
				.SetAlignSelf(Align.Center);

			var actionLabel = new Label(action)
				.SetFontSize(11)
				.SetColor(StateTreePalette.textDim)
				.SetAlignSelf(Align.Center);

			return new VisualElement()
				.SetFlexDirection(FlexDirection.Row)
				.SetMarginBottom(3f)
				.AddChildren(keyLabel, actionLabel);
		}

		private static int MeasureDepth(StateTreeNode node)
		{
			int depth = node.depth;
			foreach (StateTreeNode child in node.children)
			{
				depth = Mathf.Max(depth, MeasureDepth(child));
			}

			return depth;
		}

		private VisualElement BuildInfoRow(string title, string value)
		{
			var titleLabel = new Label(title)
				.SetFontSize(11)
				.SetColor(StateTreePalette.textDim)
				.SetWidth(72f)
				.SetFlexShrink(0f);

			var valueLabel = new Label(value)
				.SetFontSize(11)
				.SetColor(StateTreePalette.textSecondary);

			return new VisualElement()
				.SetFlexDirection(FlexDirection.Row)
				.SetMarginBottom(2f)
				.AddChildren(titleLabel, valueLabel);
		}

		private Label BuildSectionTitle(string title, int count = -1)
		{
			string text = count >= 0 ? $"{title.ToUpperInvariant()} · {count}" : title.ToUpperInvariant();

			return new Label(text)
				.SetFontSize(9)
				.SetLetterSpacing(1f)
				.SetColor(StateTreePalette.textDim)
				.SetMarginTop(12f)
				.SetMarginBottom(4f)
				.SetBorderColorBottom(StateTreePalette.panelBorder)
				.SetBorderWidthBottom(1f)
				.SetPaddingBottom(2f);
		}

		private VisualElement BuildBadge(string text, Action onClick = null)
		{
			var badge = new Label(text)
				.SetFontSize(8)
				.SetColor(StateTreePalette.textSecondary)
				.SetBackgroundColor(StateTreePalette.nodeBackground)
				.SetBorderColor(StateTreePalette.cardBorder)
				.SetBorderWidth(1f)
				.SetBorderRadius(6f)
				.SetPaddingX(4f)
				.SetPaddingY(0f)
				.SetMarginLeft(3f)
				.SetMarginY(1f)
				.SetAlignSelf(Align.Center);

			if (onClick != null)
			{
				badge.SetTooltip("Jump to the implementing method");
				badge.RegisterCallback<PointerEnterEvent>(_ =>
				{
					badge.SetColor(StateTreePalette.textPrimary);
					badge.SetBorderColor(StateTreePalette.nodeBorderHover);
				});
				badge.RegisterCallback<PointerLeaveEvent>(_ =>
				{
					badge.SetColor(StateTreePalette.textSecondary);
					badge.SetBorderColor(StateTreePalette.cardBorder);
				});
				badge.RegisterCallback<ClickEvent>(_ => onClick());
			}

			return badge;
		}

		/// <summary>
		/// Opens the controller's script at the line where it implements the given controller
		/// interface method. Falls back to the type declaration line if the method line can't be
		/// located (e.g. minified/generated source, or the member is expression-bodied on one line
		/// shared with other text the regex doesn't expect).
		/// </summary>
		private static void OpenControllerMethod(Type controllerType, Type controllerInterface, MethodInfo method)
		{
			MonoScript script = controllerType.FindMonoScript();

			if (script is null)
			{
				Debug.LogWarning($"MonoScript for type {controllerType.AssemblyQualifiedName} not found.");
				return;
			}

			int line = FindMethodLineNumber(script.text, controllerInterface.Name, method.Name);
			AssetDatabase.OpenAsset(script, line);
		}

		private static int FindMethodLineNumber(string text, string interfaceName, string methodName)
		{
			if (string.IsNullOrEmpty(text))
			{
				return 1;
			}

			string[] lines = text.Split('\n');

			// Prefer an explicit interface implementation (e.g. "void IUpdateController.Update(").
			var explicitRegex = new Regex($@"\b{Regex.Escape(interfaceName)}\.{Regex.Escape(methodName)}\s*\(");
			for (var i = 0; i < lines.Length; i++)
			{
				if (explicitRegex.IsMatch(lines[i]))
				{
					return i + 1;
				}
			}

			// Fall back to a regular member declaration (e.g. "public void Update(").
			var declRegex = new Regex(
				$@"\b(public|private|protected|internal|static|virtual|override|async)\b[^;{{]*\b{Regex.Escape(methodName)}\s*\(");
			for (var i = 0; i < lines.Length; i++)
			{
				if (declRegex.IsMatch(lines[i]))
				{
					return i + 1;
				}
			}

			// Last resort: first occurrence of the method name at all.
			var plainRegex = new Regex($@"\b{Regex.Escape(methodName)}\s*\(");
			for (var i = 0; i < lines.Length; i++)
			{
				if (plainRegex.IsMatch(lines[i]))
				{
					return i + 1;
				}
			}

			return 1;
		}
	}
}
