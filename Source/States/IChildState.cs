#nullable enable

using System;

// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM
{
    public interface IChildState<T>
        where T : IState
    {
        public Type ParentState => typeof(T);
    }
}
