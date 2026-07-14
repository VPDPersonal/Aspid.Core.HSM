using System;
using System.Collections.Generic;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM.Editor
{
	/// <summary>
	/// Node of the HSM state tree model for editor visualization:
	/// state type, parent-child links, tree depth,
	/// and the position on the canvas computed by the layout.
	/// </summary>
	public sealed class StateTreeNode
	{
		public readonly Type stateType;
		public readonly List<StateTreeNode> children = new();

		public StateTreeNode parent { get; set; }
		public int depth { get; set; }
		public Rect position { get; set; }

		/// <summary>
		/// When <c>true</c>, the node's subtree is hidden: the layout treats the node
		/// as a leaf, and its descendants get no elements or edges on the canvas.
		/// </summary>
		public bool isCollapsed { get; set; }

		public StateTreeNode(Type stateType)
		{
			this.stateType = stateType;
		}

		public int CountDescendants()
		{
			var count = 0;
			foreach (StateTreeNode child in children)
			{
				count += 1 + child.CountDescendants();
			}

			return count;
		}
	}
}
