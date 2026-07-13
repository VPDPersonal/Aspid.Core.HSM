using System;
using System.Collections.Generic;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM.Editor
{
	/// <summary>
	/// Узел модели дерева состояний HSM для editor-визуализации:
	/// тип состояния, связи родитель-дети, глубина в дереве
	/// и вычисленная лейаутом позиция на канвасе.
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
