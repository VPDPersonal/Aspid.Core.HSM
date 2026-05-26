using System;

// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM
{
    public interface IStateScope : IDisposable
    {
        IStateScope? Parent { get; }
        
        IStateScope CreateChildScope();
    }
}
