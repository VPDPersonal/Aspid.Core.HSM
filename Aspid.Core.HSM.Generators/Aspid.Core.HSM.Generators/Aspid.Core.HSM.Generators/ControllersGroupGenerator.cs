using System.Threading;
using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Aspid.Generators.Helper;
using Microsoft.CodeAnalysis.CSharp;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Aspid.Core.HSM.Generators.Descriptions;

namespace Aspid.Core.HSM.Generators;

[Generator(LanguageNames.CSharp)]
public sealed class ControllersGroupGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // ReSharper disable once NullableWarningSuppressionIsUsed
        var provider = context.SyntaxProvider.ForAttributeWithMetadataName(HsmClasses.ControllerGroupAttribute.FullName, Predicate, Transform)
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

        return new ControllerGroupData(classDeclaration);
    }
    
    private static void GenerateCode(SourceProductionContext context, ControllerGroupData data)
    {
        var declarationSyntax = data.ClassDeclaration;
        var declarationText = new DeclarationText(declarationSyntax);
        NamespaceText? namespaceText = declarationSyntax.GetNamespaceName();

        var code = new CodeWriter();
        code.BeginClass(namespaceText, declarationText, "Aspid.Core.HSM.IController");
        code.AppendLine("protected virtual void AddControllers(params Aspid.Core.HSM.IController[] controllers) { }");
        code.EndClass(declarationSyntax.GetNamespaceName());
        
        var fileName = declarationText.GetFileName(namespaceText, "State");
        
        context.AddSource(fileName, code.GetSourceText());
    }
}