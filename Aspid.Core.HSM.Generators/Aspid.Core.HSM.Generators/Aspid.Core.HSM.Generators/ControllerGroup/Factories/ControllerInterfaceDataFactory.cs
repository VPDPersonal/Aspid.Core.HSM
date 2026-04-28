using System.Linq;
using Microsoft.CodeAnalysis;
using Aspid.Generators.Helper;
using System.Collections.Generic;
using System.Collections.Immutable;
using Aspid.Core.HSM.Generators.ControllerGroup.Data;
using Aspid.Core.HSM.Generators.ControllerGroup.Data.Interfaces;
using static Aspid.Core.HSM.Generators.Descriptions.HsmClasses;

namespace Aspid.Core.HSM.Generators.ControllerGroup.Factories;

public static class ControllerInterfaceDataFactory
{
    public static ImmutableArray<ControllerInterfaceData> Create(in ImmutableArray<ControllerData> controllers)
    {
        var buckets = new Dictionary<string, InterfaceBucket>();

        for (var i = 0; i < controllers.Length; i++)
        {
            var controllerSymbol = controllers[i].Symbol;

            foreach (var @interface in controllerSymbol.AllInterfaces)
            {
                if (@interface.ToDisplayString() == IController) continue;

                var derivesFromIController = @interface.AllInterfaces
                    .Any(child => child.ToDisplayString() == IController);
                if (!derivesFromIController) continue;

                if (!buckets.TryGetValue(@interface.Name, out var bucket))
                {
                    bucket = new InterfaceBucket(@interface);
                    buckets.Add(@interface.Name, bucket);
                }

                bucket.Indexes.Add(i);
            }
        }

        return buckets.Values.Select(bucket =>
        {
            var methods = bucket.Type.GetMembers()
                .OfType<IMethodSymbol>()
                .Where(method => method.ReturnsVoid)
                .Select(symbol =>
                {
                    var isReverse = symbol.HasAnyAttributeInSelf(ReverseExecuteAttribute);
                    // TODO Aspid.Core.HSM – add async support
                    return new ControllerInterfaceMethodData(symbol, isReverse, null);
                })
                .ToImmutableArray();

            return new ControllerInterfaceData(bucket.Type, bucket.Indexes.ToImmutableArray(), methods);
        }).ToImmutableArray();
    }
}
