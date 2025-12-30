using Microsoft.CodeAnalysis;
using System.Collections.Immutable;

namespace Aspid.Core.HSM.Generators.ControllerGroup.Data.Interfaces;

public readonly struct ControllerInterfaceData(
    ITypeSymbol typeSymbol,
    ImmutableArray<int> controllerIndexes,
    ImmutableArray<ControllerInterfaceMethodData> methods)
{
    public readonly ITypeSymbol TypeSymbol = typeSymbol;
    public readonly ImmutableArray<int> ControllerIndexes = controllerIndexes;
    public readonly ImmutableArray<ControllerInterfaceMethodData> Methods = methods;
}
