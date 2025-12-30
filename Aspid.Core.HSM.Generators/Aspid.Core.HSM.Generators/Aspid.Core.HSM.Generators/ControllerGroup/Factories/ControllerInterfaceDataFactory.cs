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
        // string - interface name
        // type - interface type
        // indexes - controller index
        var result = new Dictionary<string, (ITypeSymbol type, List<int> indexes)>();
        
        for (var i = 0; i < controllers.Length; i++)
        {
            var controllerSymbol = controllers[i].Symbol;

            foreach (var @interface in controllerSymbol.AllInterfaces)
            {
                // IController is tag. Skip it.
                if (@interface.ToDisplayString() == IController) continue;
                
                // If interface implemented IController interface
                var isIController = @interface.AllInterfaces.Any(childInterface => childInterface.ToDisplayString( ) == IController);
                if (!isIController) continue;

                if (!result.TryGetValue(@interface.Name, out var tuple))
                {
                    tuple.indexes = [];
                    tuple.type = @interface;
                    
                    result.Add(@interface.Name, tuple);
                }
                
                tuple.indexes.Add(i);
            }
        }

        return result.Select(pair =>
        {
            var type = pair.Value.type;
            var controllerIndexes = pair.Value.indexes.ToImmutableArray();

            var methodsSymbols = type.GetMembers()
                .OfType<IMethodSymbol>()
                .Where(method => method.ReturnsVoid);

            var methods = methodsSymbols.Select(symbol =>
            {
                var isReverse = symbol.HasAnyAttributeInSelf(ReverseExecuteAttribute);
                
                // TODO Aspid.Core.HSM – add async support
                return new ControllerInterfaceMethodData(symbol, isReverse, null);
            }).ToImmutableArray();
            
            return new ControllerInterfaceData(type, controllerIndexes, methods);
        }).ToImmutableArray();
    }
}
