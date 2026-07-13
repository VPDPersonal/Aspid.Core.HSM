using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM.Editor
{
	/// <summary>
	/// Слой рёбер графа HSM: рисует связи родитель-ребёнок через Painter2D
	/// в выбранном стиле (Безье, прямая или ортогональная), выбирая грани нод
	/// адаптивно и подсвечивая рёбра, оба конца которых входят в активную цепочку.
	/// </summary>
	public sealed class StateTreeEdgesElement : VisualElement
	{
		private const float OrthogonalPadding = 16f;

		private readonly List<StateTreeNode> m_roots;
		private readonly StateTreeLayoutDirection m_direction;
		private readonly HashSet<Type> m_activeTypes = new();
		private StateTreeEdgeStyle m_style;

		public StateTreeEdgesElement(List<StateTreeNode> roots, StateTreeLayoutDirection direction, StateTreeEdgeStyle style)
		{
			m_roots = roots;
			m_direction = direction;
			m_style = style;
			pickingMode = PickingMode.Ignore;
			generateVisualContent += OnGenerateVisualContent;
		}

		public void SetStyle(StateTreeEdgeStyle style)
		{
			m_style = style;
			MarkDirtyRepaint();
		}

		public void SetActiveTypes(IEnumerable<Type> activeTypes)
		{
			m_activeTypes.Clear();
			m_activeTypes.UnionWith(activeTypes);
			MarkDirtyRepaint();
		}

		private void OnGenerateVisualContent(MeshGenerationContext context)
		{
			Painter2D painter = context.painter2D;

			foreach (StateTreeNode root in m_roots)
			{
				DrawSubtreeEdges(painter, root);
			}
		}

		private void DrawSubtreeEdges(Painter2D painter, StateTreeNode node)
		{
			foreach (StateTreeNode child in node.children)
			{
				bool isActive = m_activeTypes.Contains(node.stateType) && m_activeTypes.Contains(child.stateType);
				DrawEdge(painter, node.position, child.position, isActive);
				DrawSubtreeEdges(painter, child);
			}
		}

		private void DrawEdge(Painter2D painter, Rect from, Rect to, bool isActive)
		{
			painter.strokeColor = isActive ? StateTreePalette.activeEdge : StateTreePalette.edge;
			painter.lineWidth = isActive ? 2.5f : 1.5f;

			Vector2 start = GetAnchor(from, to, out Vector2 startNormal);
			Vector2 end = GetAnchor(to, from, out Vector2 endNormal);

			painter.BeginPath();
			painter.MoveTo(start);

			switch (m_style)
			{
				case StateTreeEdgeStyle.Straight:
					painter.LineTo(end);
					break;

				case StateTreeEdgeStyle.Orthogonal:
					DrawOrthogonalPath(painter, start, startNormal, end, endNormal);
					break;

				default:
					float tangentLength = Mathf.Clamp(Vector2.Distance(start, end) * 0.4f, 16f, 96f);
					painter.BezierCurveTo(start + startNormal * tangentLength, end + endNormal * tangentLength, end);
					break;
			}

			painter.Stroke();
		}

		private static void DrawOrthogonalPath(
			Painter2D painter,
			Vector2 start,
			Vector2 startNormal,
			Vector2 end,
			Vector2 endNormal)
		{
			Vector2 exit = start + startNormal * OrthogonalPadding;
			Vector2 entry = end + endNormal * OrthogonalPadding;

			painter.LineTo(exit);

			bool startHorizontal = startNormal.x != 0f;
			bool endHorizontal = endNormal.x != 0f;

			if (startHorizontal == endHorizontal)
			{
				// Одинаковые оси выхода: два излома через середину между точками.
				if (startHorizontal)
				{
					float middleX = (exit.x + entry.x) * 0.5f;
					painter.LineTo(new Vector2(middleX, exit.y));
					painter.LineTo(new Vector2(middleX, entry.y));
				}
				else
				{
					float middleY = (exit.y + entry.y) * 0.5f;
					painter.LineTo(new Vector2(exit.x, middleY));
					painter.LineTo(new Vector2(entry.x, middleY));
				}
			}
			else
			{
				// Разные оси: единственный угол на пересечении направлений.
				painter.LineTo(startHorizontal
					? new Vector2(entry.x, exit.y)
					: new Vector2(exit.x, entry.y));
			}

			painter.LineTo(entry);
			painter.LineTo(end);
		}

		/// <summary>
		/// Возвращает точку на грани ноды, обращённой к другой ноде, и внешнюю
		/// нормаль этой грани. Ось выбирается по зазорам между прямоугольниками
		/// с приоритетом главной оси раскладки: в TopDown ребро идёт низ→верх,
		/// пока между нодами есть вертикальный зазор (даже при сильном боковом
		/// смещении), и лишь при нодах на одном уровне уходит на боковые грани;
		/// в LeftToRight — зеркально; в Radial решает больший зазор.
		/// </summary>
		private Vector2 GetAnchor(Rect rect, Rect other, out Vector2 normal)
		{
			float gapX = Mathf.Max(0f, Mathf.Max(other.xMin - rect.xMax, rect.xMin - other.xMax));
			float gapY = Mathf.Max(0f, Mathf.Max(other.yMin - rect.yMax, rect.yMin - other.yMax));
			Vector2 delta = other.center - rect.center;

			bool horizontal = m_direction switch
			{
				StateTreeLayoutDirection.TopDown => gapY <= 0f && (gapX > 0f || Mathf.Abs(delta.x) >= Mathf.Abs(delta.y)),
				StateTreeLayoutDirection.LeftToRight => gapX > 0f || (gapY <= 0f && Mathf.Abs(delta.x) >= Mathf.Abs(delta.y)),
				_ => gapX > gapY || (gapX == gapY && Mathf.Abs(delta.x) >= Mathf.Abs(delta.y))
			};

			if (horizontal)
			{
				normal = delta.x >= 0f ? Vector2.right : Vector2.left;
				return new Vector2(delta.x >= 0f ? rect.xMax : rect.xMin, rect.center.y);
			}

			normal = delta.y >= 0f ? Vector2.up : Vector2.down;
			return new Vector2(rect.center.x, delta.y >= 0f ? rect.yMax : rect.yMin);
		}
	}
}
