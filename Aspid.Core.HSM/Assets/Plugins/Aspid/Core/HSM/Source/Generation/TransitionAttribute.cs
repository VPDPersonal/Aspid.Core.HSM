using System;

// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class TransitionAttribute : Attribute
    {
        public Type SourceState { get; }
        public Type TargetState { get; }

        public TransitionAttribute(Type sourceState, Type targetState)
        {
            SourceState = sourceState;
            TargetState = targetState;
        }
    }
}
