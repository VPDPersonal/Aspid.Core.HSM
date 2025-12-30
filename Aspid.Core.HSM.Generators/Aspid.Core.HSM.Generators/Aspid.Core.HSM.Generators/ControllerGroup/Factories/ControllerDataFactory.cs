using System.Linq;
using Microsoft.CodeAnalysis;
using Aspid.Generators.Helper;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Aspid.Core.HSM.Generators.Descriptions;
using Aspid.Core.HSM.Generators.ControllerGroup.Data;

namespace Aspid.Core.HSM.Generators.ControllerGroup.Factories;

public static class ControllerDataFactory
{
    private const string AddControllers = nameof(AddControllers);
    
    public static ImmutableArray<ControllerData> Create(
        SemanticModel semanticModel,
        ClassDeclarationSyntax classDeclarationSyntax)
    {
        // Get AddControllers invocation
        var invocation = classDeclarationSyntax
            .DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .FirstOrDefault(invocation => invocation.Expression switch
            { 
                IdentifierNameSyntax { Identifier.Text: AddControllers } => true,
                MemberAccessExpressionSyntax { Name.Identifier.Text: AddControllers } => true,
                _ => false
            });
        
        if (invocation is null) return ImmutableArray<ControllerData>.Empty;
        
        var result = new List<ControllerData>();
        
        foreach (var argument in invocation.ArgumentList.Arguments)
        {
            var expression = argument.Expression;
            var typeInfo = semanticModel.GetTypeInfo(expression);
            if (typeInfo.Type is not INamedTypeSymbol namedTypeSymbol) continue;
            // if (!namedTypeSymbol.HasAnyAttributeInSelfAndBases(HsmClasses.IController)) continue;
            
            result.Add(new ControllerData(namedTypeSymbol));
        }
        
        return result.ToImmutableArray();
    }
}
