using System;
using System.Collections.Generic;
using System.Text;

// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM.Editor
{
	/// <summary>
	/// Exports the HSM state tree model to Mermaid flowchart text: parent-child
	/// links as solid arrows and transitions as dashed arrows labeled with the
	/// transition type name. The full model is exported regardless of collapsed
	/// subtrees on the canvas.
	/// </summary>
	public static class StateTreeExporter
	{
		public static string BuildMermaid(
			List<StateTreeNode> roots,
			List<StateTreeTransition> transitions,
			StateTreeLayoutDirection direction)
		{
			var builder = new StringBuilder();
			builder.AppendLine(direction == StateTreeLayoutDirection.LeftToRight ? "flowchart LR" : "flowchart TD");

			var knownIds = new HashSet<string>();
			foreach (StateTreeNode root in roots)
			{
				AppendSubtree(builder, root, knownIds);
			}

			var hasTransitionHeader = false;
			foreach (StateTreeTransition transition in transitions)
			{
				string sourceId = GetId(transition.sourceState);
				string targetId = GetId(transition.targetState);

				if (!knownIds.Contains(sourceId) || !knownIds.Contains(targetId))
				{
					continue;
				}

				if (!hasTransitionHeader)
				{
					hasTransitionHeader = true;
					builder.AppendLine();
				}

				builder.AppendLine($"    {sourceId} -. {transition.transitionType.Name} .-> {targetId}");
			}

			return builder.ToString();
		}

		private static void AppendSubtree(StringBuilder builder, StateTreeNode node, HashSet<string> knownIds)
		{
			string id = GetId(node.stateType);
			if (knownIds.Add(id))
			{
				builder.AppendLine($"    {id}[\"{node.stateType.Name}\"]");
			}

			foreach (StateTreeNode child in node.children)
			{
				builder.AppendLine($"    {id} --> {GetId(child.stateType)}");
				AppendSubtree(builder, child, knownIds);
			}
		}

		private static string GetId(Type stateType)
		{
			string fullName = stateType.FullName ?? stateType.Name;
			var builder = new StringBuilder(fullName.Length);

			foreach (char symbol in fullName)
			{
				builder.Append(char.IsLetterOrDigit(symbol) ? symbol : '_');
			}

			return builder.ToString();
		}
	}
}
