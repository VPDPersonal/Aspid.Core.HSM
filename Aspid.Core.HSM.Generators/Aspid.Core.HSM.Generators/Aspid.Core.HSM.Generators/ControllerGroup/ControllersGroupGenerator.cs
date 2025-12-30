using System.Threading;
using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Aspid.Generators.Helper;
using Microsoft.CodeAnalysis.CSharp;
using System.Runtime.CompilerServices;
using Aspid.Core.HSM.Generators.ControllerGroup.Bodies;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Aspid.Core.HSM.Generators.ControllerGroup.Data;
using Aspid.Core.HSM.Generators.ControllerGroup.Factories;
using static Aspid.Core.HSM.Generators.Descriptions.HsmClasses;

namespace Aspid.Core.HSM.Generators.ControllerGroup;

[Generator(LanguageNames.CSharp)]
public sealed class ControllersGroupGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Get all classes that have the ControllerGroupAttribute
        var provider = context.SyntaxProvider.ForAttributeWithMetadataName(ControllerGroupAttribute.FullName, Predicate, Transform)
            .Where(static foundForSourceGenerator => foundForSourceGenerator.HasValue)
            .Select(static (foundForSourceGenerator, _) => foundForSourceGenerator!.Value);
        
        context.RegisterSourceOutput(provider, GenerateCode);
    }

    private static bool Predicate(SyntaxNode node, CancellationToken cancellationToken)
    {
        return node is ClassDeclarationSyntax candidate
            && candidate.Modifiers.Any(SyntaxKind.PartialKeyword)
            && !candidate.Modifiers.Any(SyntaxKind.StaticKeyword);
    }

    private static ControllerGroupData? Transform(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
    {
        if (context.TargetSymbol is not INamedTypeSymbol symbol) return null;

        Debug.Assert(context.TargetNode is ClassDeclarationSyntax);
        var classDeclaration = Unsafe.As<ClassDeclarationSyntax>(context.TargetNode);

        return ControllerGroupDataFactory.Create(context.SemanticModel, classDeclaration);
    }
    
    private static void GenerateCode(SourceProductionContext context, ControllerGroupData data)
    {
        var declarationSyntax = data.ClassDeclaration;
        var declarationText = new DeclarationText(declarationSyntax);
        NamespaceText? namespaceText = declarationSyntax.GetNamespaceName();
        ControllerGroupBody.Generate(data, namespaceText, declarationText, context);
    }
}