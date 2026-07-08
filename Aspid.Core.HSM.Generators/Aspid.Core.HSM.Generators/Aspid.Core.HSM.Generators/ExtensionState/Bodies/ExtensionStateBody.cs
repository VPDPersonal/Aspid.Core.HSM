using System.Linq;
using Microsoft.CodeAnalysis;
using Aspid.Generators.Helper;
using Aspid.Core.HSM.Generators.ExtensionState.Data;
using static Aspid.Generators.Helper.Classes;
using static Aspid.Core.HSM.Generators.Descriptions.HsmClasses;

namespace Aspid.Core.HSM.Generators.ExtensionState.Bodies;

public static class ExtensionStateBody
{
    public static void Generate(
        in ExtensionStateData data,
        NamespaceText? namespaceText,
        DeclarationText declarationText,
        in SourceProductionContext context)
    {
        var code = new CodeWriter();
        var patternExpression = BuildPatternExpression(data);

        code.BeginClass(namespaceText, declarationText, IExtensionState.FullName)
            .AppendMultiline(
                $"""
                [{GeneratedCodeAttribute}("Aspid.Core.HSM.Generators.ExtensionStateGenerator", "0.0.1")]
                bool {IExtensionState.FullName}.CanAttachTo({IState.FullName} hostState) =>
                    {patternExpression};
                """)
            .EndClass(namespaceText);

        var sourceText = code.GetSourceText();
        var fileName = declarationText.GetFileName(namespaceText, "ExtensionState");

        context.AddSource(fileName, sourceText);
    }

    private static string BuildPatternExpression(in ExtensionStateData data)
    {
        var typeNames = data.CompatibleStates
            .Select(static t => t.ToDisplayStringGlobal());

        return "hostState is " + string.Join(" or ", typeNames);
    }
}
