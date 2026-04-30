using Microsoft.CodeAnalysis;

namespace Aspid.Core.HSM.Generators.ControllerGroup.Data.Interfaces;

public readonly struct AsyncMethodData(IMethodSymbol asyncSymbol)
{
    public readonly IMethodSymbol AsyncSymbol = asyncSymbol;
}
