using System;
using System.Collections.Generic;
using Aspid.FastTools.Types.Editors;
using Aspid.FastTools.UIElements;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM.Editor
{
	/// <summary>
	/// Visual node of the HSM graph in the Shader Graph spirit: a card with a colored
	/// header plate (depth color) holding the state name and the collapse badge, and a body
	/// row of info chips (children, controllers, transitions). Supports hover, click selection,
	/// active-chain highlighting with a pulsing glow, focus-mode dimming, search-match
	/// highlighting, mouse dragging, and opening the script on double-click.
	/// </summary>
	public sealed class StateTreeNodeElement : VisualElement
	{
		private const float DragThreshold = 4f;
		private const float HeaderHeight = 22f;
		private const float CardRadius = 8f;
		private const long GlowFrameIntervalMs = 50;

		public event Action<StateTreeNode> onSelected;
		public event Action<StateTreeNode> onMoved;
		public event Action<StateTreeNode> onDragCompleted;
		public event Action<StateTreeNode> onCollapseToggled;

		public StateTreeNode node { get; }

		private readonly VisualElement m_glow;
		private readonly VisualElement m_card;
		private readonly VisualElement m_header;
		private readonly VisualElement m_body;
		private readonly Label m_title;
		private readonly IVisualElementScheduledItem m_glowPulse;

		private bool m_isActive;
		private bool m_isSelected;
		private bool m_isHovered;
		private bool m_isDimmed;
		private bool m_isSearchMatch;
		private bool m_isDragging;
		private int m_pressedClickCount;
		private int m_dragPointerId = -1;
		private Vector2 m_dragPointerStart;
		private Vector2 m_dragNodeStart;

		public StateTreeNodeElement(StateTreeNode treeNode, int transitionCount)
		{
			node = treeNode;

			VisualElement shadow = BuildShadow();
			m_glow = BuildGlow();
			m_title = BuildTitle();
			m_header = BuildHeader();
			m_body = BuildBody(transitionCount);

			m_card = new VisualElement()
				.SetPosition(Position.Absolute)
				.SetLeft(0f)
				.SetRight(0f)
				.SetTop(0f)
				.SetBottom(0f)
				.SetBorderWidth(1.5f)
				.SetBorderRadius(CardRadius)
				.SetOverflow(Overflow.Hidden)
				.AddChildren(m_header, m_body);
			m_card.pickingMode = PickingMode.Ignore;
			m_card.style.transitionProperty = new List<StylePropertyName> { "background-color", "border-color" };
			m_card.style.transitionDuration = new List<TimeValue> { new(0.1f), new(0.1f) };

			this.SetName(treeNode.stateType.Name)
				.SetTooltip(treeNode.stateType.FullName)
				.AddChildren(shadow, m_glow, m_card);

			m_glowPulse = schedule.Execute(UpdateGlowPulse).Every(GlowFrameIntervalMs);
			m_glowPulse.Pause();

			ApplyNodePosition();
			UpdateVisualState();

			RegisterCallback<PointerEnterEvent>(OnPointerEnter);
			RegisterCallback<PointerLeaveEvent>(OnPointerLeave);
			RegisterCallback<PointerDownEvent>(OnPointerDown);
			RegisterCallback<PointerMoveEvent>(OnPointerMove);
			RegisterCallback<PointerUpEvent>(OnPointerUp);
		}

		/// <summary>
		/// Soft offset shadow behind the card: two translucent rounded layers instead of
		/// a blur (which Painter-less UI Toolkit elements can't do).
		/// </summary>
		private static VisualElement BuildShadow()
		{
			var far = new VisualElement()
				.SetPosition(Position.Absolute)
				.SetLeft(-2f)
				.SetRight(-2f)
				.SetTop(4f)
				.SetBottom(-6f)
				.SetBorderRadius(CardRadius + 3f)
				.SetBackgroundColor(StateTreePalette.Faded(StateTreePalette.nodeShadow, 0.45f));
			far.pickingMode = PickingMode.Ignore;

			var near = new VisualElement()
				.SetPosition(Position.Absolute)
				.SetLeft(0f)
				.SetRight(0f)
				.SetTop(2f)
				.SetBottom(-3f)
				.SetBorderRadius(CardRadius + 1f)
				.SetBackgroundColor(StateTreePalette.nodeShadow);
			near.pickingMode = PickingMode.Ignore;

			var shadow = new VisualElement()
				.SetPosition(Position.Absolute)
				.SetLeft(0f)
				.SetRight(0f)
				.SetTop(0f)
				.SetBottom(0f)
				.AddChildren(far, near);
			shadow.pickingMode = PickingMode.Ignore;
			return shadow;
		}

		/// <summary>Amber outline shown around the card while the state is in the active chain.</summary>
		private static VisualElement BuildGlow()
		{
			var glow = new VisualElement()
				.SetPosition(Position.Absolute)
				.SetLeft(-4f)
				.SetRight(-4f)
				.SetTop(-4f)
				.SetBottom(-4f)
				.SetBorderRadius(CardRadius + 4f)
				.SetBorderWidth(2f)
				.SetBorderColor(StateTreePalette.activeBorder)
				.SetDisplay(DisplayStyle.None);
			glow.pickingMode = PickingMode.Ignore;
			return glow;
		}

		private Label BuildTitle()
		{
			var title = new Label(node.stateType.Name)
				.SetFontSize(11)
				.SetUnityFontStyleAndWeight(node.depth == 0 ? FontStyle.Bold : FontStyle.Normal)
				.SetColor(StateTreePalette.headerText)
				.SetFlexGrow(1f)
				.SetFlexShrink(1f)
				.SetOverflow(Overflow.Hidden)
				.SetTextOverflow(TextOverflow.Ellipsis)
				.SetWhiteSpace(WhiteSpace.NoWrap)
				.SetUnityTextAlign(TextAnchor.MiddleLeft);
			title.pickingMode = PickingMode.Ignore;
			return title;
		}

		private VisualElement BuildHeader()
		{
			var header = new VisualElement()
				.SetHeight(HeaderHeight)
				.SetFlexShrink(0f)
				.SetFlexDirection(FlexDirection.Row)
				.SetAlignItems(Align.Center)
				.SetPaddingLeft(8f)
				.SetPaddingRight(4f)
				.AddChild(m_title);
			header.pickingMode = PickingMode.Ignore;

			if (node.children.Count > 0)
			{
				header.AddChild(BuildCollapseBadge());
			}

			return header;
		}

		private VisualElement BuildBody(int transitionCount)
		{
			var body = new VisualElement()
				.SetFlexGrow(1f)
				.SetFlexDirection(FlexDirection.Row)
				.SetAlignItems(Align.Center)
				.SetPaddingX(6f);
			body.pickingMode = PickingMode.Ignore;

			if (node.children.Count > 0)
			{
				int descendants = node.CountDescendants();
				body.AddChild(BuildChip(
					StateTreePalette.GetDepthColor(node.depth + 1),
					node.children.Count.ToString(),
					descendants == node.children.Count
						? $"{node.children.Count} child state(s)"
						: $"{node.children.Count} child state(s), {descendants} in subtree"));
			}

			if (node.controllerCount > 0)
			{
				body.AddChild(BuildChip(
					StateTreePalette.chipControllersDot,
					node.controllerCount.ToString(),
					$"{node.controllerCount} controller(s)"));
			}

			if (transitionCount > 0)
			{
				body.AddChild(BuildChip(
					StateTreePalette.transitionEdge,
					transitionCount.ToString(),
					$"{transitionCount} transition(s)"));
			}

			return body;
		}

		/// <summary>Small "colored dot + count" chip in the card body.</summary>
		private static VisualElement BuildChip(Color dotColor, string text, string tooltip)
		{
			var dot = new VisualElement()
				.SetSize(5f, 5f)
				.SetBorderRadius(2.5f)
				.SetMarginRight(3f)
				.SetFlexShrink(0f)
				.SetBackgroundColor(dotColor);

			var label = new Label(text)
				.SetFontSize(9)
				.SetColor(StateTreePalette.textSecondary);

			var chip = new VisualElement()
				.SetTooltip(tooltip)
				.SetFlexDirection(FlexDirection.Row)
				.SetAlignItems(Align.Center)
				.SetHeight(15f)
				.SetPaddingX(5f)
				.SetMarginRight(4f)
				.SetBorderRadius(7.5f)
				.SetBorderWidth(1f)
				.SetBorderColor(StateTreePalette.cardBorder)
				.SetBackgroundColor(StateTreePalette.cardBackground)
				.AddChildren(dot, label);
			chip.pickingMode = PickingMode.Ignore;
			return chip;
		}

		/// <summary>
		/// Small clickable indicator on the header's right edge: "▾" when expanded,
		/// "▸ N" with the hidden-descendant count when collapsed. The pointer-down
		/// is swallowed so a click never starts a drag or selects the node.
		/// </summary>
		private Label BuildCollapseBadge()
		{
			var idleColor = new Color(0f, 0f, 0f, 0.55f);
			var hoverColor = new Color(0f, 0f, 0f, 0.9f);

			var badge = new Label(node.isCollapsed ? $"▸{node.CountDescendants()}" : "▾")
				.SetFlexShrink(0f)
				.SetPaddingX(4f)
				.SetFontSize(10)
				.SetColor(idleColor)
				.SetUnityTextAlign(TextAnchor.MiddleCenter)
				.SetTooltip(node.isCollapsed ? "Expand subtree" : "Collapse subtree");

			badge.RegisterCallback<PointerDownEvent>(pointerEvent => pointerEvent.StopPropagation());
			badge.RegisterCallback<ClickEvent>(clickEvent =>
			{
				clickEvent.StopPropagation();
				onCollapseToggled?.Invoke(node);
			});
			badge.RegisterCallback<PointerEnterEvent>(_ => badge.SetColor(hoverColor));
			badge.RegisterCallback<PointerLeaveEvent>(_ => badge.SetColor(idleColor));

			return badge;
		}

		public void SetActive(bool isActive)
		{
			if (m_isActive == isActive)
			{
				return;
			}

			m_isActive = isActive;
			UpdateVisualState();
		}

		public void SetSelected(bool isSelected)
		{
			if (m_isSelected == isSelected)
			{
				return;
			}

			m_isSelected = isSelected;
			UpdateVisualState();
		}

		/// <summary>Fades the whole card in focus/search mode when the node is unrelated.</summary>
		public void SetDimmed(bool isDimmed)
		{
			if (m_isDimmed == isDimmed)
			{
				return;
			}

			m_isDimmed = isDimmed;
			UpdateVisualState();
		}

		/// <summary>Highlights the card with a bright ring while it matches the search query.</summary>
		public void SetSearchMatch(bool isMatch)
		{
			if (m_isSearchMatch == isMatch)
			{
				return;
			}

			m_isSearchMatch = isMatch;
			UpdateVisualState();
		}

		private void OnPointerEnter(PointerEnterEvent pointerEvent)
		{
			m_isHovered = true;
			UpdateVisualState();
		}

		private void OnPointerLeave(PointerLeaveEvent pointerEvent)
		{
			m_isHovered = false;
			UpdateVisualState();
		}

		private void OnPointerDown(PointerDownEvent pointerEvent)
		{
			if (pointerEvent.button != 0)
			{
				return;
			}

			m_dragPointerId = pointerEvent.pointerId;
			m_pressedClickCount = pointerEvent.clickCount;
			m_dragPointerStart = parent.WorldToLocal(pointerEvent.position);
			m_dragNodeStart = node.position.position;
			m_isDragging = false;
			this.CapturePointer(m_dragPointerId);
			pointerEvent.StopPropagation();
		}

		private void OnPointerMove(PointerMoveEvent pointerEvent)
		{
			if (pointerEvent.pointerId != m_dragPointerId)
			{
				return;
			}

			Vector2 delta = (Vector2)parent.WorldToLocal(pointerEvent.position) - m_dragPointerStart;
			if (!m_isDragging && delta.magnitude < DragThreshold)
			{
				return;
			}

			m_isDragging = true;
			Rect position = node.position;
			position.position = m_dragNodeStart + delta;
			node.position = position;

			ApplyNodePosition();
			onMoved?.Invoke(node);
		}

		private void OnPointerUp(PointerUpEvent pointerEvent)
		{
			if (pointerEvent.pointerId != m_dragPointerId)
			{
				return;
			}

			this.ReleasePointer(m_dragPointerId);
			m_dragPointerId = -1;

			if (m_isDragging)
			{
				m_isDragging = false;
				onDragCompleted?.Invoke(node);
				return;
			}

			onSelected?.Invoke(node);

			if (m_pressedClickCount >= 2)
			{
				node.stateType.OpenInScriptEditor();
			}
		}

		private void ApplyNodePosition()
		{
			this.SetPosition(Position.Absolute)
				.SetLeft(node.position.x)
				.SetTop(node.position.y)
				.SetWidth(node.position.width)
				.SetHeight(node.position.height);
		}

		private void UpdateVisualState()
		{
			Color headerColor = m_isActive ? StateTreePalette.activeBorder : StateTreePalette.GetDepthColor(node.depth);
			if (m_isHovered)
			{
				headerColor = Color.Lerp(headerColor, Color.white, 0.12f);
			}

			Color background = m_isActive
				? StateTreePalette.activeBackground
				: m_isHovered ? StateTreePalette.nodeBackgroundHover : StateTreePalette.nodeBackground;

			Color border = m_isSelected
				? StateTreePalette.selectionBorder
				: m_isSearchMatch
					? StateTreePalette.searchHighlight
					: m_isActive
						? StateTreePalette.activeBorder
						: m_isHovered ? StateTreePalette.nodeBorderHover : StateTreePalette.nodeBorder;

			m_header.SetBackgroundColor(headerColor);
			m_card.SetBackgroundColor(background)
				.SetBorderColor(border);
			this.SetOpacity(m_isDimmed ? 0.3f : 1f);

			bool glowVisible = m_isActive;
			m_glow.SetDisplay(glowVisible ? DisplayStyle.Flex : DisplayStyle.None);
			if (glowVisible) m_glowPulse.Resume();
			else m_glowPulse.Pause();
		}

		private void UpdateGlowPulse()
		{
			var phase = (float)(EditorApplication.timeSinceStartup * 2.4);
			m_glow.SetOpacity(0.45f + 0.35f * Mathf.Sin(phase));
		}
	}
}
