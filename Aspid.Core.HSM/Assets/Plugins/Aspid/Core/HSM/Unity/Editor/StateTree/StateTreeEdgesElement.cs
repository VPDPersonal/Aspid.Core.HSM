using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM.Editor
{
	/// <summary>
	/// HSM graph edge layer: draws parent-child links via Painter2D
	/// in the selected style (Bezier, straight, or orthogonal), picking node
	/// anchors adaptively and highlighting edges whose both ends are in the active chain.
	/// A glowing runner travels along active edges from parent to child.
	/// Optionally overlays transitions as dashed arrows between the states they connect.
	/// </summary>
	public sealed class StateTreeEdgesElement : VisualElement
	{
		private const float OrthogonalPadding = 16f;
		private const int BezierSamples = 32;
		private const float RunnerSpeed = 90f;
		private const float RunnerRadius = 3f;
		private const float RunnerGlowRadius = 6.5f;
		private const long RunnerFrameIntervalMs = 33;
		private const float DashLength = 6f;
		private const float DashGapLength = 5f;
		private const float ArrowLength = 8f;
		private const float ArrowHalfWidth = 3.5f;
		private const float SelfLoopRadius = 14f;

		private readonly List<StateTreeNode> m_roots;
		private readonly List<StateTreeTransition> m_transitions;
		private readonly Dictionary<Type, StateTreeNode> m_visibleNodes = new();
		private readonly StateTreeLayoutDirection m_direction;
		private readonly HashSet<Type> m_activeTypes = new();
		private readonly List<Vector2> m_pathBuffer = new();
		private readonly IVisualElementScheduledItem m_runnerAnimation;
		private StateTreeEdgeStyle m_style;
		private bool m_transitionsVisible;

		public StateTreeEdgesElement(
			List<StateTreeNode> roots,
			List<StateTreeTransition> transitions,
			StateTreeLayoutDirection direction,
			StateTreeEdgeStyle style,
			bool transitionsVisible)
		{
			m_roots = roots;
			m_transitions = transitions;
			m_direction = direction;
			m_style = style;
			m_transitionsVisible = transitionsVisible;
			pickingMode = PickingMode.Ignore;
			generateVisualContent += OnGenerateVisualContent;

			foreach (StateTreeNode root in roots)
			{
				RegisterVisibleNodes(root);
			}

			m_runnerAnimation = schedule.Execute(MarkDirtyRepaint).Every(RunnerFrameIntervalMs);
			m_runnerAnimation.Pause();
		}

		public void SetStyle(StateTreeEdgeStyle style)
		{
			m_style = style;
			MarkDirtyRepaint();
		}

		public void SetTransitionsVisible(bool isVisible)
		{
			m_transitionsVisible = isVisible;
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

		private void RegisterVisibleNodes(StateTreeNode node)
		{
			m_visibleNodes[node.stateType] = node;

			if (node.isCollapsed)
			{
				return;
			}

			foreach (StateTreeNode child in node.children)
			{
				RegisterVisibleNodes(child);
			}
		}

		private void OnGenerateVisualContent(MeshGenerationContext context)
		{
			Painter2D painter = context.painter2D;

			foreach (StateTreeNode root in m_roots)
			{
				DrawSubtreeEdges(painter, root);
			}

			if (m_transitionsVisible)
			{
				DrawTransitions(painter);
			}
		}

		private void DrawSubtreeEdges(Painter2D painter, StateTreeNode node)
		{
			if (node.isCollapsed)
			{
				return;
			}

			foreach (StateTreeNode child in node.children)
			{
				bool isActive = m_activeTypes.Contains(node.stateType) && m_activeTypes.Contains(child.stateType);
				DrawEdge(painter, node.position, child.position, isActive);
				DrawSubtreeEdges(painter, child);
			}
		}

		/// <summary>
		/// Draws transitions whose both states are visible on the canvas as dashed
		/// arrows from source to target; a transition into the same state is drawn
		/// as a small loop on the node's right edge.
		/// </summary>
		private void DrawTransitions(Painter2D painter)
		{
			foreach (StateTreeTransition transition in m_transitions)
			{
				if (!m_visibleNodes.TryGetValue(transition.sourceState, out StateTreeNode source)
					|| !m_visibleNodes.TryGetValue(transition.targetState, out StateTreeNode target))
				{
					continue;
				}

				if (source == target)
				{
					DrawSelfLoop(painter, source.position);
					continue;
				}

				BuildEdgePath(source.position, target.position);
				painter.strokeColor = StateTreePalette.transitionEdge;
				painter.lineWidth = 1.5f;
				StrokeDashed(painter, m_pathBuffer);
				DrawArrowHead(painter, m_pathBuffer);
			}
		}

		private void DrawEdge(Painter2D painter, Rect from, Rect to, bool isActive)
		{
			BuildEdgePath(from, to);

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
		/// Fills <see cref="m_pathBuffer"/> with the polyline between two node rects
		/// in the current edge style — shared by hierarchy edges and transition edges.
		/// </summary>
		private void BuildEdgePath(Rect from, Rect to)
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
		}

		/// <summary>
		/// Strokes the polyline as a dash pattern continuous across segment boundaries;
		/// all dashes are emitted as subpaths of a single stroke call.
		/// </summary>
		private static void StrokeDashed(Painter2D painter, List<Vector2> path)
		{
			const float patternLength = DashLength + DashGapLength;

			painter.BeginPath();
			var traveled = 0f;

			for (var i = 1; i < path.Count; i++)
			{
				Vector2 from = path[i - 1];
				Vector2 to = path[i];
				float segmentLength = Vector2.Distance(from, to);

				if (segmentLength <= Mathf.Epsilon)
				{
					continue;
				}

				Vector2 direction = (to - from) / segmentLength;
				var offset = 0f;

				while (offset < segmentLength)
				{
					float patternPosition = (traveled + offset) % patternLength;

					if (patternPosition < DashLength)
					{
						float drawLength = Mathf.Min(DashLength - patternPosition, segmentLength - offset);
						painter.MoveTo(from + direction * offset);
						painter.LineTo(from + direction * (offset + drawLength));
						offset += drawLength;
					}
					else
					{
						offset += Mathf.Min(patternLength - patternPosition, segmentLength - offset);
					}
				}

				traveled += segmentLength;
			}

			painter.Stroke();
		}

		private static void DrawArrowHead(Painter2D painter, List<Vector2> path)
		{
			Vector2 tip = path[^1];
			Vector2 previous = tip;

			for (int i = path.Count - 2; i >= 0; i--)
			{
				if ((path[i] - tip).sqrMagnitude > 0.01f)
				{
					previous = path[i];
					break;
				}
			}

			if (previous == tip)
			{
				return;
			}

			DrawArrowTriangle(painter, tip, (tip - previous).normalized);
		}

		private static void DrawArrowTriangle(Painter2D painter, Vector2 tip, Vector2 direction)
		{
			var normal = new Vector2(-direction.y, direction.x);
			Vector2 back = tip - direction * ArrowLength;

			painter.fillColor = StateTreePalette.transitionEdge;
			painter.BeginPath();
			painter.MoveTo(tip);
			painter.LineTo(back + normal * ArrowHalfWidth);
			painter.LineTo(back - normal * ArrowHalfWidth);
			painter.ClosePath();
			painter.Fill();
		}

		private static void DrawSelfLoop(Painter2D painter, Rect rect)
		{
			var center = new Vector2(rect.xMax, rect.center.y);

			painter.strokeColor = StateTreePalette.transitionEdge;
			painter.lineWidth = 1.5f;
			painter.BeginPath();
			painter.Arc(center, SelfLoopRadius, -80f, 80f);
			painter.Stroke();

			// The arrowhead sits at the arc's end and points along the clockwise tangent,
			// back toward the node.
			float endRadians = 80f * Mathf.Deg2Rad;
			Vector2 tip = center + new Vector2(Mathf.Cos(endRadians), Mathf.Sin(endRadians)) * SelfLoopRadius;
			var tangent = new Vector2(-Mathf.Sin(endRadians), Mathf.Cos(endRadians));
			DrawArrowTriangle(painter, tip, tangent);
		}

		/// <summary>
		/// Draws the runner — a glowing dot cyclically traveling along the polyline
		/// from parent to child. Speed is constant in pixels, so longer edges
		/// take a longer cycle but the pace of motion is the same across the whole tree.
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
		/// Samples a cubic Bezier curve (the same geometry that BezierCurveTo used
		/// to draw) into a polyline so the runner can travel along it.
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
				// Same exit axis: two bends through the midpoint between the points.
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
				// Different axes: a single corner at the intersection of the directions.
				path.Add(startHorizontal
					? new Vector2(entry.x, exit.y)
					: new Vector2(exit.x, entry.y));
			}

			path.Add(entry);
			path.Add(end);
		}

		/// <summary>
		/// Returns the point on a node's edge facing the other node, and the outward
		/// normal of that edge. The axis is chosen by the gaps between the rectangles
		/// with priority given to the layout's main axis: in TopDown the edge runs
		/// bottom→top as long as there's a vertical gap between the nodes (even with a
		/// strong lateral offset), and only falls back to the side edges when nodes
		/// are at the same level; in LeftToRight it's mirrored; in Radial the larger gap wins.
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
