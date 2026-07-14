using System;
using System.Collections.Generic;
using Aspid.FastTools.Types.Editors;
using Aspid.FastTools.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM.Editor
{
	/// <summary>
	/// Visual node of the HSM graph: a card with the state type name and an accent
	/// bar colored by depth. Supports hover, click selection, active-chain highlighting,
	/// mouse dragging, and opening the script on double-click.
	/// </summary>
	public sealed class StateTreeNodeElement : VisualElement
	{
		private const float DragThreshold = 4f;

		public event Action<StateTreeNode> onSelected;
		public event Action<StateTreeNode> onMoved;
		public event Action<StateTreeNode> onDragCompleted;
		public event Action<StateTreeNode> onCollapseToggled;

		public StateTreeNode node { get; }

		private readonly Label m_label;
		private readonly VisualElement m_accentBar;

		private bool m_isActive;
		private bool m_isSelected;
		private bool m_isHovered;
		private bool m_isDragging;
		private int m_pressedClickCount;
		private int m_dragPointerId = -1;
		private Vector2 m_dragPointerStart;
		private Vector2 m_dragNodeStart;

		public StateTreeNodeElement(StateTreeNode treeNode)
		{
			node = treeNode;

			m_accentBar = new VisualElement()
				.SetPosition(Position.Absolute)
				.SetLeft(0f)
				.SetTop(0f)
				.SetBottom(0f)
				.SetWidth(3f)
				.SetBorderRadiusLeft(5f)
				.SetBackgroundColor(StateTreePalette.GetDepthColor(treeNode.depth));
			m_accentBar.pickingMode = PickingMode.Ignore;

			bool isRoot = treeNode.depth == 0;
			m_label = new Label(treeNode.stateType.Name)
				.SetFontSize(12)
				.SetUnityFontStyleAndWeight(isRoot ? FontStyle.Bold : FontStyle.Normal)
				.SetColor(StateTreePalette.textPrimary)
				.SetPaddingLeft(6f)
				.SetUnityTextAlign(TextAnchor.MiddleCenter);
			m_label.pickingMode = PickingMode.Ignore;

			this.SetName(treeNode.stateType.Name)
				.SetTooltip(treeNode.stateType.FullName)
				.SetBorderWidth(1.5f)
				.SetBorderRadius(6f)
				.SetJustifyContent(Justify.Center)
				.AddChildren(m_accentBar, m_label);

			if (treeNode.children.Count > 0)
			{
				m_label.SetPaddingRight(20f);
				this.AddChild(BuildCollapseBadge());
			}

			style.transitionProperty = new List<StylePropertyName> { "background-color", "border-color" };
			style.transitionDuration = new List<TimeValue> { new(0.1f), new(0.1f) };

			ApplyNodePosition();
			UpdateVisualState();

			RegisterCallback<PointerEnterEvent>(OnPointerEnter);
			RegisterCallback<PointerLeaveEvent>(OnPointerLeave);
			RegisterCallback<PointerDownEvent>(OnPointerDown);
			RegisterCallback<PointerMoveEvent>(OnPointerMove);
			RegisterCallback<PointerUpEvent>(OnPointerUp);
		}

		/// <summary>
		/// Small clickable indicator on the node's right edge: "▾" when expanded,
		/// "▸ N" with the hidden-descendant count when collapsed. The pointer-down
		/// is swallowed so a click never starts a drag or selects the node.
		/// </summary>
		private Label BuildCollapseBadge()
		{
			Color idleColor = node.isCollapsed ? StateTreePalette.textSecondary : StateTreePalette.textDim;

			var badge = new Label(node.isCollapsed ? $"▸{node.CountDescendants()}" : "▾")
				.SetPosition(Position.Absolute)
				.SetRight(4f)
				.SetTop(0f)
				.SetBottom(0f)
				.SetPaddingX(3f)
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
			badge.RegisterCallback<PointerEnterEvent>(_ => badge.SetColor(StateTreePalette.textPrimary));
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
			if (node.depth == 0)
			{
				UpdateRootVisualState();
				return;
			}

			Color background = m_isActive
				? StateTreePalette.activeBackground
				: m_isHovered ? StateTreePalette.nodeBackgroundHover : StateTreePalette.nodeBackground;

			Color border = m_isSelected
				? StateTreePalette.selectionBorder
				: m_isActive
					? StateTreePalette.activeBorder
					: m_isHovered ? StateTreePalette.nodeBorderHover : StateTreePalette.nodeBorder;

			this.SetBackgroundColor(background)
				.SetBorderColor(border);
			m_label.SetColor(m_isActive ? StateTreePalette.activeText : StateTreePalette.textPrimary);
			m_accentBar.SetBackgroundColor(m_isActive
				? StateTreePalette.activeBorder
				: StateTreePalette.GetDepthColor(node.depth));
		}

		// The root is highlighted with a solid accent fill so the tree's center reads at a glance.
		private void UpdateRootVisualState()
		{
			Color fill = m_isActive ? StateTreePalette.activeBorder : StateTreePalette.GetDepthColor(0);
			if (m_isHovered)
			{
				fill = Color.Lerp(fill, Color.white, 0.15f);
			}

			Color border = m_isSelected ? StateTreePalette.selectionBorder : Color.Lerp(fill, Color.white, 0.25f);

			this.SetBackgroundColor(fill)
				.SetBorderColor(border);
			m_label.SetColor(new Color(0.09f, 0.09f, 0.09f));
			m_accentBar.SetDisplay(DisplayStyle.None);
		}
	}
}
