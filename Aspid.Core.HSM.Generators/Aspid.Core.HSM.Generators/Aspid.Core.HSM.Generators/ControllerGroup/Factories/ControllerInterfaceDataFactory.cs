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

                var (syncIface, asyncIface) = ResolveSyncAsyncPair(@interface);
                var key = syncIface.ToDisplayString();

                if (!buckets.TryGetValue(key, out var bucket))
                {
                    bucket = new InterfaceBucket(syncIface);
                    buckets.Add(key, bucket);
                }

                if (asyncIface is not null)
                    bucket.AsyncType ??= asyncIface;

                bucket.Entries.Add((i, asyncIface is not null));
            }
        }

        return buckets.Values.Select(bucket =>
        {
            var asyncMethods = bucket.AsyncType?.GetMembers().OfType<IMethodSymbol>()
                .Where(m => m.MethodKind == MethodKind.Ordinary)
                .ToArray();

            var methods = bucket.SyncType.GetMembers()
                .OfType<IMethodSymbol>()
                .Where(method => method.ReturnsVoid)
                .Select(symbol =>
                {
                    var isReverse = symbol.HasAnyAttributeInSelf(ReverseExecuteAttribute);
                    AsyncMethodData? asyncData = null;
                    if (asyncMethods is { Length: > 0 })
                    {
                        var asyncSym = asyncMethods.FirstOrDefault();
                        if (asyncSym is not null)
                        {
                            asyncData = new AsyncMethodData(asyncSym);
                            if (asyncSym.HasAnyAttributeInSelf(ReverseExecuteAttribute))
                                isReverse = true;
                        }
                    }
                    return new ControllerInterfaceMethodData(symbol, isReverse, asyncData);
                })
                .ToImmutableArray();

            var indexes = bucket.Entries.Select(e => e.Index).ToImmutableArray();
            var asyncFlags = bucket.Entries.Select(e => e.IsAsync).ToImmutableArray();

            return new ControllerInterfaceData(bucket.SyncType, bucket.AsyncType, indexes, asyncFlags, methods);
        }).ToImmutableArray();
    }

    private static (ITypeSymbol SyncIface, ITypeSymbol? AsyncIface) ResolveSyncAsyncPair(INamedTypeSymbol @interface)
    {
        var asyncOf = @interface.GetAttributes()
            .FirstOrDefault(attr => attr.AttributeClass is { } cls
                && cls.ToDisplayString() == AsyncOfAttribute);

        if (asyncOf is null || asyncOf.ConstructorArguments.Length == 0)
            return (@interface, null);

        if (asyncOf.ConstructorArguments[0].Value is ITypeSymbol syncIface)
            return (syncIface, @interface);

        return (@interface, null);
    }
}
