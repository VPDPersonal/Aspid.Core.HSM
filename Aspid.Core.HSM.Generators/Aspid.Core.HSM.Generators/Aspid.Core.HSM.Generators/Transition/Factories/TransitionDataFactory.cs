using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Aspid.Core.HSM.Generators.Transition.Data;
using static Aspid.Core.HSM.Generators.Descriptions.HsmClasses;

namespace Aspid.Core.HSM.Generators.Transition.Factories;

public static class TransitionDataFactory
{
    public static TransitionData? Create(
        SemanticModel semanticModel,
        ClassDeclarationSyntax classDeclarationSyntax)
    {
        var classSymbol = semanticModel.GetDeclaredSymbol(classDeclarationSyntax);
        if (classSymbol is null) return null;

        var transitionAttribute = classSymbol.GetAttributes()
            .FirstOrDefault(attr => attr.AttributeClass?.Name == TransitionAttribute.Name);

        var constructorArguments = transitionAttribute?.ConstructorArguments;
        if (constructorArguments is not { Length: 2 })
        {
            return null;
        }

        if (constructorArguments.Value[0].Value is not ITypeSymbol sourceStateType ||
            constructorArguments.Value[1].Value is not ITypeSymbol targetStateType)
        {
            return null;
        }

        return new TransitionData(classDeclarationSyntax, sourceStateType, targetStateType);
    }
}
