using System.Threading;
using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Aspid.Generators.Helper;
using Microsoft.CodeAnalysis.CSharp;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Aspid.Core.HSM.Generators.ExtensionState.Data;
using Aspid.Core.HSM.Generators.ExtensionState.Bodies;
using Aspid.Core.HSM.Generators.ExtensionState.Factories;
using static Aspid.Core.HSM.Generators.Descriptions.HsmClasses;

namespace Aspid.Core.HSM.Generators.ExtensionState;

[Generator(LanguageNames.CSharp)]
public sealed class ExtensionStateGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var provider = context.SyntaxProvider.ForAttributeWithMetadataName(ExtensionForAttribute.FullName, Predicate, Transform)
            .Where(static data => data.HasValue)
            .Select(static (data, _) => data!.Value);

        context.RegisterSourceOutput(provider, GenerateCode);
    }

    private static bool Predicate(SyntaxNode node, CancellationToken cancellationToken)
    {
        return node is ClassDeclarationSyntax candidate
            && candidate.Modifiers.Any(SyntaxKind.PartialKeyword)
            && !candidate.Modifiers.Any(SyntaxKind.StaticKeyword);
    }

    private static ExtensionStateData? Transform(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
    {
        if (context.TargetSymbol is not INamedTypeSymbol) return null;

        Debug.Assert(context.TargetNode is ClassDeclarationSyntax);
        var classDeclaration = Unsafe.As<ClassDeclarationSyntax>(context.TargetNode);

        return ExtensionStateDataFactory.Create(context.SemanticModel, classDeclaration);
    }

    private static void GenerateCode(SourceProductionContext context, ExtensionStateData data)
    {
        var declarationSyntax = data.ClassDeclaration;
        var declarationText = new DeclarationText(declarationSyntax);
        NamespaceText? namespaceText = declarationSyntax.GetNamespaceName();
        ExtensionStateBody.Generate(data, namespaceText, declarationText, context);
    }
}
