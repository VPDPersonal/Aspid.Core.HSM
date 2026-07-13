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
	/// По активным рёбрам от родителя к ребёнку бежит светящийся бегунок.
	/// </summary>
	public sealed class StateTreeEdgesElement : VisualElement
	{
		private const float OrthogonalPadding = 16f;
		private const int BezierSamples = 32;
		private const float RunnerSpeed = 90f;
		private const float RunnerRadius = 3f;
		private const float RunnerGlowRadius = 6.5f;
		private const long RunnerFrameIntervalMs = 33;

		private readonly List<StateTreeNode> m_roots;
		private readonly StateTreeLayoutDirection m_direction;
		private readonly HashSet<Type> m_activeTypes = new();
		private readonly List<Vector2> m_pathBuffer = new();
		private readonly IVisualElementScheduledItem m_runnerAnimation;
		private StateTreeEdgeStyle m_style;

		public StateTreeEdgesElement(List<StateTreeNode> roots, StateTreeLayoutDirection direction, StateTreeEdgeStyle style)
		{
			m_roots = roots;
			m_direction = direction;
			m_style = style;
			pickingMode = PickingMode.Ignore;
			generateVisualContent += OnGenerateVisualContent;

			m_runnerAnimation = schedule.Execute(MarkDirtyRepaint).Every(RunnerFrameIntervalMs);
			m_runnerAnimation.Pause();
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

			if (m_activeTypes.Count > 0) m_runnerAnimation.Resume();
			else m_runnerAnimation.Pause();

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
			Vector2 start = GetAnchor(from, to, out Vector2 startNormal);
			Vector2 end = GetAnchor(to, from, out Vector2 endNormal);

			m_pathBuffer.Clear();
			m_pathBuffer.Add(start);

			switch (m_style)
			{
				case StateTreeEdgeStyle.Straight:
					m_pathBuffer.Add(end);
					break;

				case StateTreeEdgeStyle.Orthogonal:
					AppendOrthogonalPath(m_pathBuffer, start, startNormal, end, endNormal);
					break;

				default:
					AppendBezierPath(m_pathBuffer, start, startNormal, end, endNormal);
					break;
			}

			painter.strokeColor = isActive ? StateTreePalette.activeEdge : StateTreePalette.edge;
			painter.lineWidth = isActive ? 2.5f : 1.5f;

			painter.BeginPath();
			painter.MoveTo(m_pathBuffer[0]);

			for (var i = 1; i < m_pathBuffer.Count; i++)
			{
				painter.LineTo(m_pathBuffer[i]);
			}

			painter.Stroke();

			if (isActive) DrawRunner(painter, m_pathBuffer);
		}

		/// <summary>
		/// Рисует бегунок — светящуюся точку, циклически бегущую вдоль полилинии
		/// от родителя к ребёнку. Скорость постоянна в пикселях, поэтому на длинных
		/// рёбрах цикл дольше, но темп движения одинаков по всему дереву.
		/// </summary>
		private static void DrawRunner(Painter2D painter, List<Vector2> path)
		{
			var totalLength = 0f;

			for (var i = 1; i < path.Count; i++)
			{
				totalLength += Vector2.Distance(path[i - 1], path[i]);
			}

			if (totalLength <= Mathf.Epsilon) return;

			var distance = (float)(UnityEditor.EditorApplication.timeSinceStartup * RunnerSpeed % totalLength);
			Vector2 position = path[^1];

			for (var i = 1; i < path.Count; i++)
			{
				float segmentLength = Vector2.Distance(path[i - 1], path[i]);

				if (distance <= segmentLength)
				{
					position = Vector2.Lerp(path[i - 1], path[i], segmentLength > 0f ? distance / segmentLength : 0f);
					break;
				}

				distance -= segmentLength;
			}

			Color glow = StateTreePalette.activeEdgeRunner;
			glow.a = 0.25f;

			painter.fillColor = glow;
			painter.BeginPath();
			painter.Arc(position, RunnerGlowRadius, 0f, 360f);
			painter.ClosePath();
			painter.Fill();

			painter.fillColor = StateTreePalette.activeEdgeRunner;
			painter.BeginPath();
			painter.Arc(position, RunnerRadius, 0f, 360f);
			painter.ClosePath();
			painter.Fill();
		}

		/// <summary>
		/// Сэмплирует кубическую кривую Безье (та же геометрия, что раньше рисовал
		/// BezierCurveTo) в полилинию, чтобы по ней можно было вести бегунок.
		/// </summary>
		private static void AppendBezierPath(
			List<Vector2> path,
			Vector2 start,
			Vector2 startNormal,
			Vector2 end,
			Vector2 endNormal)
		{
			float tangentLength = Mathf.Clamp(Vector2.Distance(start, end) * 0.4f, 16f, 96f);
			Vector2 control1 = start + startNormal * tangentLength;
			Vector2 control2 = end + endNormal * tangentLength;

			for (var i = 1; i <= BezierSamples; i++)
			{
				float t = i / (float)BezierSamples;
				float u = 1f - t;

				path.Add(u * u * u * start
					+ 3f * u * u * t * control1
					+ 3f * u * t * t * control2
					+ t * t * t * end);
			}
		}

		private static void AppendOrthogonalPath(
			List<Vector2> path,
			Vector2 start,
			Vector2 startNormal,
			Vector2 end,
			Vector2 endNormal)
		{
			Vector2 exit = start + startNormal * OrthogonalPadding;
			Vector2 entry = end + endNormal * OrthogonalPadding;

			path.Add(exit);

			bool startHorizontal = startNormal.x != 0f;
			bool endHorizontal = endNormal.x != 0f;

			if (startHorizontal == endHorizontal)
			{
				// Одинаковые оси выхода: два излома через середину между точками.
				if (startHorizontal)
				{
					float middleX = (exit.x + entry.x) * 0.5f;
					path.Add(new Vector2(middleX, exit.y));
					path.Add(new Vector2(middleX, entry.y));
				}
				else
				{
					float middleY = (exit.y + entry.y) * 0.5f;
					path.Add(new Vector2(exit.x, middleY));
					path.Add(new Vector2(entry.x, middleY));
				}
			}
			else
			{
				// Разные оси: единственный угол на пересечении направлений.
				path.Add(startHorizontal
					? new Vector2(entry.x, exit.y)
					: new Vector2(exit.x, entry.y));
			}

			path.Add(entry);
			path.Add(end);
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
