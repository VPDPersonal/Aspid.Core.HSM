using System;
using System.Diagnostics.CodeAnalysis;

// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM
{
    public interface IStateHierarchy
    {
        bool TryGetParent(Type childType, [NotNullWhen(true)] out Type? parentType);
    }
}
