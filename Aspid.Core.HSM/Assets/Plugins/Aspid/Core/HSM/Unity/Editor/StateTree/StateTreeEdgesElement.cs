using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM.Editor
{
	/// <summary>
	/// Слой рёбер графа HSM: рисует кривые родитель-ребёнок через Painter2D
	/// с учётом направления раскладки, подсвечивая рёбра, оба конца которых
	/// входят в активную цепочку.
	/// </summary>
	public sealed class StateTreeEdgesElement : VisualElement
	{
		private readonly List<StateTreeNode> m_roots;
		private readonly StateTreeLayoutDirection m_direction;
		private readonly HashSet<Type> m_activeTypes = new();

		public StateTreeEdgesElement(List<StateTreeNode> roots, StateTreeLayoutDirection direction)
		{
			m_roots = roots;
			m_direction = direction;
			pickingMode = PickingMode.Ignore;
			generateVisualContent += OnGenerateVisualContent;
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

			if (m_direction == StateTreeLayoutDirection.Radial)
			{
				// Радиальный вид: прямое ребро центр-центр, концы скрыты под нодами.
				painter.BeginPath();
				painter.MoveTo(from.center);
				painter.LineTo(to.center);
				painter.Stroke();
				return;
			}

			Vector2 start;
			Vector2 end;
			Vector2 tangent;

			if (m_direction == StateTreeLayoutDirection.TopDown)
			{
				start = new Vector2(from.center.x, from.yMax);
				end = new Vector2(to.center.x, to.yMin);
				tangent = Vector2.up * StateTreeLayout.TopDownMainGap * 0.5f;
			}
			else
			{
				start = new Vector2(from.xMax, from.center.y);
				end = new Vector2(to.xMin, to.center.y);
				tangent = Vector2.right * StateTreeLayout.LeftToRightMainGap * 0.5f;
			}

			painter.BeginPath();
			painter.MoveTo(start);
			painter.BezierCurveTo(start + tangent, end - tangent, end);
			painter.Stroke();
		}
	}
}
