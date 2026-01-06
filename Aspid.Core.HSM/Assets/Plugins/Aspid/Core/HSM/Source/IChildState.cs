using System;

// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM
{
    public interface IChildState
    {
        public Type ParentState { get; }
    }
}
