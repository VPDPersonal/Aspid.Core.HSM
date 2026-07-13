using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM.Editor
{
	/// <summary>
	/// Строит модель дерева состояний HSM по <see cref="TypeCache"/>:
	/// узлы — неабстрактные реализации <see cref="IState"/> из всех сборок проекта
	/// (кроме служебного <see cref="EmptyState"/> и расширений <see cref="IExtensionState"/>),
	/// рёбра родитель-ребёнок — из <see cref="IChildState{T}"/>.
	/// </summary>
	public static class StateTreeGraphBuilder
	{
		public static List<StateTreeNode> Build()
		{
			List<Type> stateTypes = TypeCache.GetTypesDerivedFrom<IState>()
				.Where(type => !type.IsAbstract
					&& !type.IsInterface
					&& type != typeof(EmptyState)
					&& !typeof(IExtensionState).IsAssignableFrom(type))
				.ToList();

			var nodes = new Dictionary<Type, StateTreeNode>();
			foreach (Type type in stateTypes)
			{
				nodes[type] = new StateTreeNode(type);
			}

			var roots = new List<StateTreeNode>();
			foreach (Type type in stateTypes)
			{
				Type parentType = GetParentType(type);
				if (parentType != null && nodes.TryGetValue(parentType, out StateTreeNode parent))
				{
					nodes[type].parent = parent;
					parent.children.Add(nodes[type]);
				}
				else
				{
					roots.Add(nodes[type]);
				}
			}

			SortRecursive(roots);

			foreach (StateTreeNode root in roots)
			{
				AssignDepthRecursive(root, 0);
			}

			return roots;
		}

		private static Type GetParentType(Type stateType)
		{
			foreach (Type stateInterface in stateType.GetInterfaces())
			{
				if (stateInterface.IsGenericType && stateInterface.GetGenericTypeDefinition() == typeof(IChildState<>))
				{
					return stateInterface.GetGenericArguments()[0];
				}
			}

			return null;
		}

		private static void AssignDepthRecursive(StateTreeNode node, int depth)
		{
			node.depth = depth;
			foreach (StateTreeNode child in node.children)
			{
				AssignDepthRecursive(child, depth + 1);
			}
		}

		private static void SortRecursive(List<StateTreeNode> nodes)
		{
			nodes.Sort((left, right) => string.CompareOrdinal(left.stateType.Name, right.stateType.Name));
			foreach (StateTreeNode node in nodes)
			{
				SortRecursive(node.children);
			}
		}
	}
}
