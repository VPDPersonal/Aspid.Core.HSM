using Microsoft.CodeAnalysis;
using System.Collections.Generic;

namespace Aspid.Core.HSM.Generators.ControllerGroup.Factories;

internal sealed class InterfaceBucket(ITypeSymbol syncType)
{
    public readonly ITypeSymbol SyncType = syncType;
    public ITypeSymbol? AsyncType;
    public readonly List<(int Index, bool IsAsync)> Entries = [];
}
