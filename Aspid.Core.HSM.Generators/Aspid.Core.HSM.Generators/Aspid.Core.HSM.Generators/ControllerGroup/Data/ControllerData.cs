using Microsoft.CodeAnalysis;

namespace Aspid.Core.HSM.Generators.ControllerGroup.Data;

public readonly struct ControllerData(INamedTypeSymbol symbol)
{
    public readonly INamedTypeSymbol Symbol = symbol;
}
