using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;

// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM.Editor
{
	/// <summary>
	/// Builds the HSM state tree model from <see cref="TypeCache"/>:
	/// nodes are non-abstract <see cref="IState"/> implementations across all project
	/// assemblies (excluding the internal <see cref="EmptyState"/> and <see cref="IExtensionState"/>
	/// extensions), parent-child edges come from <see cref="IChildState{T}"/>.
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

		/// <summary>
		/// Collects transition edges from all non-abstract <see cref="ITransition"/>
		/// implementations whose source/target states can be resolved without running
		/// user code: via the <see cref="ITransition{TSource,TTarget}"/> interface,
		/// the <see cref="TransitionAttribute"/>, or (as a last resort) instantiating
		/// the transition through a public parameterless constructor.
		/// </summary>
		public static List<StateTreeTransition> BuildTransitions()
		{
			var transitions = new List<StateTreeTransition>();

			foreach (Type type in TypeCache.GetTypesDerivedFrom<ITransition>())
			{
				if (type.IsAbstract || type.IsInterface)
				{
					continue;
				}

				if (TryResolveEndpoints(type, out Type source, out Type target))
				{
					transitions.Add(new StateTreeTransition(type, source, target));
				}
			}

			transitions.Sort((left, right) =>
				string.CompareOrdinal(left.transitionType.Name, right.transitionType.Name));

			return transitions;
		}

		private static bool TryResolveEndpoints(Type transitionType, out Type source, out Type target)
		{
			foreach (Type transitionInterface in transitionType.GetInterfaces())
			{
				if (transitionInterface.IsGenericType
					&& transitionInterface.GetGenericTypeDefinition() == typeof(ITransition<,>))
				{
					Type[] arguments = transitionInterface.GetGenericArguments();
					source = arguments[0];
					target = arguments[1];
					return true;
				}
			}

			if (transitionType.GetCustomAttribute<TransitionAttribute>() is { } attribute)
			{
				source = attribute.SourceState;
				target = attribute.TargetState;
				return source != null && target != null;
			}

			// A hand-written ITransition with custom SourceState/TargetState properties:
			// the values are only readable off an instance.
			if (transitionType.GetConstructor(Type.EmptyTypes) != null)
			{
				try
				{
					var transition = (ITransition)Activator.CreateInstance(transitionType);
					source = transition.SourceState;
					target = transition.TargetState;
					return source != null && target != null;
				}
				catch (Exception)
				{
					// The constructor may require a scene or DI context — skip such transitions.
				}
			}

			source = null;
			target = null;
			return false;
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
