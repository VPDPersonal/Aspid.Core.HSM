using System;
using System.Collections.Generic;
using Aspid.FastTools.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM.Editor
{
	/// <summary>
	/// Minimap overlay in the canvas corner: a scaled-down view of all visible nodes
	/// (tinted by depth, with active/selected highlights) and the current viewport
	/// rectangle. Clicking or dragging inside the map pans the canvas to that point.
	/// </summary>
	public sealed class StateTreeMinimapElement : VisualElement
	{
		private const float MapWidth = 172f;
		private const float MapHeight = 112f;
		private const float InnerPadding = 6f;

		private readonly StateTreeCanvasElement m_canvas;
		private readonly HashSet<Type> m_activeTypes = new();
		private List<StateTreeNode> m_roots;
		private Vector2 m_contentSize;
		private Type m_selectedType;
		private int m_dragPointerId = -1;

		public StateTreeMinimapElement(StateTreeCanvasElement canvas)
		{
			m_canvas = canvas;

			this.SetPosition(Position.Absolute)
				.SetRight(10f)
				.SetBottom(10f)
				.SetSize(MapWidth, MapHeight)
				.SetBackgroundColor(StateTreePalette.Faded(StateTreePalette.panelBackground, 0.92f))
				.SetBorderColor(StateTreePalette.panelBorder)
				.SetBorderWidth(1f)
				.SetBorderRadius(6f);

			generateVisualContent += OnGenerateVisualContent;
			RegisterCallback<PointerDownEvent>(OnPointerDown);
			RegisterCallback<PointerMoveEvent>(OnPointerMove);
			RegisterCallback<PointerUpEvent>(OnPointerUp);

			// Follow the canvas view only while on a panel; a window re-dock detaches and
			// re-attaches the element, so the subscription must survive that round trip.
			RegisterCallback<AttachToPanelEvent>(_ =>
			{
				m_canvas.onViewChanged -= MarkDirtyRepaint;
				m_canvas.onViewChanged += MarkDirtyRepaint;
			});
			RegisterCallback<DetachFromPanelEvent>(_ => m_canvas.onViewChanged -= MarkDirtyRepaint);
		}

		public void SetContent(List<StateTreeNode> roots, Vector2 contentSize)
		{
			m_roots = roots;
			m_contentSize = contentSize;
			MarkDirtyRepaint();
		}

		public void SetActiveTypes(IEnumerable<Type> activeTypes)
		{
			m_activeTypes.Clear();
			m_activeTypes.UnionWith(activeTypes);
			MarkDirtyRepaint();
		}

		public void SetSelectedType(Type stateType)
		{
			m_selectedType = stateType;
			MarkDirtyRepaint();
		}

		private void OnPointerDown(PointerDownEvent pointerEvent)
		{
			if (pointerEvent.button != 0)
			{
				return;
			}

			m_dragPointerId = pointerEvent.pointerId;
			this.CapturePointer(m_dragPointerId);
			pointerEvent.StopPropagation();
			PanTo(pointerEvent.localPosition);
		}

		private void OnPointerMove(PointerMoveEvent pointerEvent)
		{
			if (pointerEvent.pointerId != m_dragPointerId)
			{
				return;
			}

			pointerEvent.StopPropagation();
			PanTo(pointerEvent.localPosition);
		}

		private void OnPointerUp(PointerUpEvent pointerEvent)
		{
			if (pointerEvent.pointerId != m_dragPointerId)
			{
				return;
			}

			this.ReleasePointer(m_dragPointerId);
			m_dragPointerId = -1;
			pointerEvent.StopPropagation();
		}

		private void PanTo(Vector2 localPoint)
		{
			if (!TryGetMapTransform(out float scale, out Vector2 origin))
			{
				return;
			}

			m_canvas.CenterOn((localPoint - origin) / scale);
		}

		/// <summary>
		/// Computes the content→map transform: a uniform scale that fits the whole
		/// content into the padded map area, and the map-space origin of the content.
		/// </summary>
		private bool TryGetMapTransform(out float scale, out Vector2 origin)
		{
			scale = 0f;
			origin = Vector2.zero;

			if (m_contentSize.x <= 0f || m_contentSize.y <= 0f)
			{
				return false;
			}

			float availableWidth = contentRect.width - InnerPadding * 2f;
			float availableHeight = contentRect.height - InnerPadding * 2f;

			if (availableWidth <= 0f || availableHeight <= 0f)
			{
				return false;
			}

			scale = Mathf.Min(availableWidth / m_contentSize.x, availableHeight / m_contentSize.y);
			origin = new Vector2(
				InnerPadding + (availableWidth - m_contentSize.x * scale) * 0.5f,
				InnerPadding + (availableHeight - m_contentSize.y * scale) * 0.5f);
			return true;
		}

		private void OnGenerateVisualContent(MeshGenerationContext context)
		{
			if (m_roots == null || !TryGetMapTransform(out float scale, out Vector2 origin))
			{
				return;
			}

			Painter2D painter = context.painter2D;

			foreach (StateTreeNode root in m_roots)
			{
				DrawSubtree(painter, root, scale, origin);
			}

			DrawViewport(painter, scale, origin);
		}

		private void DrawSubtree(Painter2D painter, StateTreeNode node, float scale, Vector2 origin)
		{
			Rect rect = node.position;
			var mapRect = new Rect(origin + rect.position * scale, rect.size * scale);

			Color fill = m_activeTypes.Contains(node.stateType)
				? StateTreePalette.activeBorder
				: StateTreePalette.Faded(StateTreePalette.GetDepthColor(node.depth), 0.75f);

			painter.fillColor = fill;
			FillRect(painter, mapRect);

			if (node.stateType == m_selectedType)
			{
				painter.strokeColor = StateTreePalette.selectionBorder;
				painter.lineWidth = 1.5f;
				StrokeRect(painter, mapRect);
			}

			if (node.isCollapsed)
			{
				return;
			}

			foreach (StateTreeNode child in node.children)
			{
				DrawSubtree(painter, child, scale, origin);
			}
		}

		private void DrawViewport(Painter2D painter, float scale, Vector2 origin)
		{
			Rect world = m_canvas.GetWorldViewport();
			var mapRect = new Rect(origin + world.position * scale, world.size * scale);

			// Clamp to the map bounds so a zoomed-out viewport doesn't paint outside the panel.
			float xMin = Mathf.Max(mapRect.xMin, 1f);
			float yMin = Mathf.Max(mapRect.yMin, 1f);
			float xMax = Mathf.Min(mapRect.xMax, contentRect.width - 1f);
			float yMax = Mathf.Min(mapRect.yMax, contentRect.height - 1f);

			if (xMax <= xMin || yMax <= yMin)
			{
				return;
			}

			var clamped = Rect.MinMaxRect(xMin, yMin, xMax, yMax);

			painter.fillColor = StateTreePalette.minimapViewportFill;
			FillRect(painter, clamped);

			painter.strokeColor = StateTreePalette.minimapViewportBorder;
			painter.lineWidth = 1f;
			StrokeRect(painter, clamped);
		}

		private static void FillRect(Painter2D painter, Rect rect)
		{
			painter.BeginPath();
			AddRectPath(painter, rect);
			painter.Fill();
		}

		private static void StrokeRect(Painter2D painter, Rect rect)
		{
			painter.BeginPath();
			AddRectPath(painter, rect);
			painter.Stroke();
		}

		private static void AddRectPath(Painter2D painter, Rect rect)
		{
			painter.MoveTo(new Vector2(rect.xMin, rect.yMin));
			painter.LineTo(new Vector2(rect.xMax, rect.yMin));
			painter.LineTo(new Vector2(rect.xMax, rect.yMax));
			painter.LineTo(new Vector2(rect.xMin, rect.yMax));
			painter.ClosePath();
		}
	}
}
