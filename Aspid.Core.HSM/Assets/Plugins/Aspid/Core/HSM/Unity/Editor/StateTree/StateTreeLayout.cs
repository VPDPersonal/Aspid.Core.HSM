using System.Collections.Generic;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM.Editor
{
	/// <summary>
	/// State tree layout supporting the <see cref="StateTreeLayoutDirection"/> modes:
	/// tiered top-down and left-to-right layouts (the parent is centered over its
	/// subtree's span) and a radial layout with the root in the center and tiers as rings.
	/// Writes positions into <see cref="StateTreeNode.position"/> and returns the canvas size.
	/// </summary>
	public static class StateTreeLayout
	{
		public const float NodeWidth = 180f;
		public const float NodeHeight = 44f;
		public const float Padding = 32f;
		public const float TopDownCrossGap = 24f;
		public const float TopDownMainGap = 64f;
		public const float LeftToRightCrossGap = 16f;
		public const float LeftToRightMainGap = 96f;
		public const float RadialRingStep = 220f;

		public static Vector2 Arrange(List<StateTreeNode> roots, StateTreeLayoutDirection direction)
		{
			if (direction == StateTreeLayoutDirection.Radial)
			{
				return ArrangeRadial(roots);
			}

			float crossGap = GetCrossGap(direction);
			float cross = Padding;
			int maxDepth = 0;

			foreach (StateTreeNode root in roots)
			{
				ArrangeSubtree(root, cross, 0, direction);
				cross += SubtreeCrossSize(root, direction) + crossGap;
				maxDepth = Mathf.Max(maxDepth, MaxDepth(root));
			}

			float crossExtent = cross - crossGap + Padding;
			float mainExtent = Padding * 2f + maxDepth * GetMainStep(direction) + GetMainSize(direction);

			return direction == StateTreeLayoutDirection.TopDown
				? new Vector2(crossExtent, mainExtent)
				: new Vector2(mainExtent, crossExtent);
		}

		private static Vector2 ArrangeRadial(List<StateTreeNode> roots)
		{
			int maxDepth = 0;
			int totalLeaves = 0;

			foreach (StateTreeNode root in roots)
			{
				maxDepth = Mathf.Max(maxDepth, MaxDepth(root));
				totalLeaves += LeafCount(root);
			}

			float radius = maxDepth * RadialRingStep;
			float extent = 2f * (radius + Padding) + NodeWidth;
			var center = new Vector2(extent * 0.5f, extent * 0.5f);

			float angle = -Mathf.PI * 0.5f;
			foreach (StateTreeNode root in roots)
			{
				float span = 2f * Mathf.PI * LeafCount(root) / totalLeaves;
				ArrangeRadialSubtree(root, center, angle, angle + span, 0);
				angle += span;
			}

			return new Vector2(extent, extent);
		}

		private static void ArrangeRadialSubtree(
			StateTreeNode node,
			Vector2 center,
			float fromAngle,
			float toAngle,
			int depth)
		{
			float midAngle = (fromAngle + toAngle) * 0.5f;
			float radius = depth * RadialRingStep;
			Vector2 point = center + new Vector2(Mathf.Cos(midAngle), Mathf.Sin(midAngle)) * radius;
			node.position = new Rect(point.x - NodeWidth * 0.5f, point.y - NodeHeight * 0.5f, NodeWidth, NodeHeight);

			if (node.isCollapsed)
			{
				return;
			}

			int leaves = LeafCount(node);
			float childFrom = fromAngle;

			foreach (StateTreeNode child in node.children)
			{
				float childSpan = (toAngle - fromAngle) * LeafCount(child) / leaves;
				ArrangeRadialSubtree(child, center, childFrom, childFrom + childSpan, depth + 1);
				childFrom += childSpan;
			}
		}

		private static int LeafCount(StateTreeNode node)
		{
			if (node.isCollapsed || node.children.Count == 0)
			{
				return 1;
			}

			int count = 0;
			foreach (StateTreeNode child in node.children)
			{
				count += LeafCount(child);
			}

			return count;
		}

		private static void ArrangeSubtree(StateTreeNode node, float cross, int depth, StateTreeLayoutDirection direction)
		{
			float crossSize = GetCrossSize(direction);
			float subtreeSize = SubtreeCrossSize(node, direction);
			float main = Padding + depth * GetMainStep(direction);
			float nodeCross = cross + (subtreeSize - crossSize) * 0.5f;

			node.position = direction == StateTreeLayoutDirection.TopDown
				? new Rect(nodeCross, main, NodeWidth, NodeHeight)
				: new Rect(main, nodeCross, NodeWidth, NodeHeight);

			if (node.isCollapsed)
			{
				return;
			}

			float childrenSize = ChildrenCrossSize(node, direction);
			float childCross = cross + (subtreeSize - childrenSize) * 0.5f;

			foreach (StateTreeNode child in node.children)
			{
				ArrangeSubtree(child, childCross, depth + 1, direction);
				childCross += SubtreeCrossSize(child, direction) + GetCrossGap(direction);
			}
		}

		private static float SubtreeCrossSize(StateTreeNode node, StateTreeLayoutDirection direction)
		{
			if (node.isCollapsed || node.children.Count == 0)
			{
				return GetCrossSize(direction);
			}

			return Mathf.Max(GetCrossSize(direction), ChildrenCrossSize(node, direction));
		}

		private static float ChildrenCrossSize(StateTreeNode node, StateTreeLayoutDirection direction)
		{
			if (node.isCollapsed || node.children.Count == 0)
			{
				return 0f;
			}

			float size = 0f;
			foreach (StateTreeNode child in node.children)
			{
				size += SubtreeCrossSize(child, direction);
			}

			return size + GetCrossGap(direction) * (node.children.Count - 1);
		}

		private static int MaxDepth(StateTreeNode node)
		{
			if (node.isCollapsed)
			{
				return 0;
			}

			int depth = 0;
			foreach (StateTreeNode child in node.children)
			{
				depth = Mathf.Max(depth, MaxDepth(child) + 1);
			}

			return depth;
		}

		private static float GetCrossSize(StateTreeLayoutDirection direction) =>
			direction == StateTreeLayoutDirection.TopDown ? NodeWidth : NodeHeight;

		private static float GetMainSize(StateTreeLayoutDirection direction) =>
			direction == StateTreeLayoutDirection.TopDown ? NodeHeight : NodeWidth;

		private static float GetCrossGap(StateTreeLayoutDirection direction) =>
			direction == StateTreeLayoutDirection.TopDown ? TopDownCrossGap : LeftToRightCrossGap;

		private static float GetMainStep(StateTreeLayoutDirection direction) =>
			direction == StateTreeLayoutDirection.TopDown
				? NodeHeight + TopDownMainGap
				: NodeWidth + LeftToRightMainGap;
	}
}
