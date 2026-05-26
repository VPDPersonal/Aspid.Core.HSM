using System.Linq;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Aspid.Core.HSM.Generators.ChildState.Data;
using static Aspid.Core.HSM.Generators.Descriptions.HsmClasses;

namespace Aspid.Core.HSM.Generators.ChildState.Factories;

public static class ChildStateDataFactory
{
    public static ChildStateData? Create(
        SemanticModel semanticModel,
        ClassDeclarationSyntax classDeclarationSyntax)
    {
        if (semanticModel.GetDeclaredSymbol(classDeclarationSyntax) is not INamedTypeSymbol classSymbol)
            return null;

        var parentStateAttribute = classSymbol.GetAttributes()
            .FirstOrDefault(attr => attr.AttributeClass?.Name == ParentStateAttribute.Name);

        var constructorArgument = parentStateAttribute?.ConstructorArguments.FirstOrDefault();
        if (constructorArgument?.Value is not ITypeSymbol parentStateType)
        {
            return null;
        }

        var cycleThrough = DetectCycle(classSymbol, parentStateType);
        if (cycleThrough != null)
        {
            var cycleInfo = new CyclicHierarchyInfo(classSymbol.Name, cycleThrough);
            return new ChildStateData(classDeclarationSyntax, parentStateType, cycleInfo);
        }

        return new ChildStateData(classDeclarationSyntax, parentStateType);
    }

    private static string? DetectCycle(INamedTypeSymbol classSymbol, ITypeSymbol parentStateType)
    {
        var visited = new HashSet<string> { classSymbol.ToDisplayString() };

        var current = parentStateType;
        while (current is INamedTypeSymbol currentNamed)
        {
            var currentName = currentNamed.ToDisplayString();
            if (visited.Contains(currentName))
            {
                return currentNamed.Name;
            }

            visited.Add(currentName);

            var attr = currentNamed.GetAttributes()
                .FirstOrDefault(a => a.AttributeClass?.Name == ParentStateAttribute.Name);

            var arg = attr?.ConstructorArguments.FirstOrDefault();
            if (arg?.Value is ITypeSymbol nextParent)
            {
                current = nextParent;
            }
            else
            {
                break;
            }
        }

        return null;
    }
}