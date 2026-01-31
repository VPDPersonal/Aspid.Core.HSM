using System.Linq;
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
        var classSymbol = semanticModel.GetDeclaredSymbol(classDeclarationSyntax);
        if (classSymbol is null) return null;
        
        var parentStateAttribute = classSymbol.GetAttributes()
            .FirstOrDefault(attr => attr.AttributeClass?.Name == ParentStateAttribute.Name);

        var constructorArgument = parentStateAttribute?.ConstructorArguments.FirstOrDefault();
        if (constructorArgument?.Value is not ITypeSymbol parentStateType)
        {
            return null;
        }
        
        return new ChildStateData(classDeclarationSyntax, parentStateType);
    }
}