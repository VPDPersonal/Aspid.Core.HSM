using System;

// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM
{
    public interface ITransition
    {
        Type SourceState { get; }
        Type TargetState { get; }
        bool CanTransition() => true;
        void OnBeforeTransition() { }
        void OnAfterTransition() { }
    }

    public interface ITransition<TSource, TTarget> : ITransition
        where TSource : IState
        where TTarget : IState
    {
        Type ITransition.SourceState => typeof(TSource);
        Type ITransition.TargetState => typeof(TTarget);
    }
}
