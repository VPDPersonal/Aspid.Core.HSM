using Microsoft.CodeAnalysis;
using System.Collections.Generic;

namespace Aspid.Core.HSM.Generators.ControllerGroup.Factories;

internal sealed class InterfaceBucket(ITypeSymbol type)
{
    public readonly ITypeSymbol Type = type;
    public readonly List<int> Indexes = [];
}
