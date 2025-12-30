using Microsoft.CodeAnalysis;

namespace Aspid.Core.HSM.Generators.ControllerGroup.Data.Interfaces;

public readonly struct ControllerInterfaceMethodData(
    IMethodSymbol symbol,
    bool isReverse,
    AsyncMethodData? asyncMethod)
{
    public readonly bool IsReverse = isReverse;
    public readonly IMethodSymbol Symbol = symbol;
    public readonly AsyncMethodData? AsyncMethod = asyncMethod;
}
