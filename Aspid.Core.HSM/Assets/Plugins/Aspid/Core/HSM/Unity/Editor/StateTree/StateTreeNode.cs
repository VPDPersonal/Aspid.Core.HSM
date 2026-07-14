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

		public StateTreeNode(Type stateType)
		{
			this.stateType = stateType;
		}
	}
}
