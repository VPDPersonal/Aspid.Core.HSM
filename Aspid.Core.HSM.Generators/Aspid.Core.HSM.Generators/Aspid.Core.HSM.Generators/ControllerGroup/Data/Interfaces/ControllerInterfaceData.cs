using Microsoft.CodeAnalysis;
using System.Collections.Immutable;

namespace Aspid.Core.HSM.Generators.ControllerGroup.Data.Interfaces;

public readonly struct ControllerInterfaceData(
    ITypeSymbol typeSymbol,
    ITypeSymbol? asyncTypeSymbol,
    ImmutableArray<int> controllerIndexes,
    ImmutableArray<bool> controllerIsAsync,
    ImmutableArray<ControllerInterfaceMethodData> methods)
{
    public readonly ITypeSymbol TypeSymbol = typeSymbol;
    public readonly ITypeSymbol? AsyncTypeSymbol = asyncTypeSymbol;
    public readonly ImmutableArray<int> ControllerIndexes = controllerIndexes;
    public readonly ImmutableArray<bool> ControllerIsAsync = controllerIsAsync;
    public readonly ImmutableArray<ControllerInterfaceMethodData> Methods = methods;
}
