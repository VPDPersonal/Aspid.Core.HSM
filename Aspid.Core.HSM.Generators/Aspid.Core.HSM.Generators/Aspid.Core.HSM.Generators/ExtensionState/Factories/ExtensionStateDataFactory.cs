using System.Linq;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Aspid.Core.HSM.Generators.ExtensionState.Data;
using static Aspid.Core.HSM.Generators.Descriptions.HsmClasses;

namespace Aspid.Core.HSM.Generators.ExtensionState.Factories;

public static class ExtensionStateDataFactory
{
    public static ExtensionStateData? Create(
        SemanticModel semanticModel,
        ClassDeclarationSyntax classDeclarationSyntax)
    {
        var classSymbol = semanticModel.GetDeclaredSymbol(classDeclarationSyntax);
        if (classSymbol is null) return null;

        var extensionForAttribute = classSymbol.GetAttributes()
            .FirstOrDefault(attr => attr.AttributeClass?.Name == ExtensionForAttribute.Name);

        if (extensionForAttribute is null) return null;

        var constructorArguments = extensionForAttribute.ConstructorArguments;
        if (constructorArguments.Length == 0) return null;

        // For params Type[], Roslyn wraps the arguments in a single TypedConstant
        // of kind Array containing the individual TypedConstant values.
        var firstArg = constructorArguments[0];

        ImmutableArray<ITypeSymbol> compatibleStates;

        if (firstArg.Kind == TypedConstantKind.Array)
        {
            var values = firstArg.Values;
            if (values.IsEmpty) return null;

            var builder = ImmutableArray.CreateBuilder<ITypeSymbol>(values.Length);
            foreach (var value in values)
            {
                if (value.Value is not ITypeSymbol typeSymbol) return null;
                builder.Add(typeSymbol);
            }

            compatibleStates = builder.MoveToImmutable();
        }
        else if (firstArg.Value is ITypeSymbol singleType)
        {
            compatibleStates = ImmutableArray.Create(singleType);
        }
        else
        {
            return null;
        }

        return new ExtensionStateData(classDeclarationSyntax, compatibleStates);
    }
}
