using System;

// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM.Editor
{
	/// <summary>
	/// Transition edge of the HSM graph model: the <see cref="ITransition"/>
	/// implementation type and its source/target state types resolved statically
	/// (from <see cref="ITransition{TSource,TTarget}"/> or <see cref="TransitionAttribute"/>).
	/// </summary>
	public sealed class StateTreeTransition
	{
		public readonly Type transitionType;
		public readonly Type sourceState;
		public readonly Type targetState;

		public StateTreeTransition(Type transitionType, Type sourceState, Type targetState)
		{
			this.transitionType = transitionType;
			this.sourceState = sourceState;
			this.targetState = targetState;
		}
	}
}
