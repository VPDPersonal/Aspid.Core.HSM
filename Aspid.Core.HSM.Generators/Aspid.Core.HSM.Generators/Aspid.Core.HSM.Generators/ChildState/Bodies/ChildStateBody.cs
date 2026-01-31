using Microsoft.CodeAnalysis;
using Aspid.Generators.Helper;
using Aspid.Core.HSM.Generators.ChildState.Data;
using static Aspid.Generators.Helper.Classes;
using static Aspid.Core.HSM.Generators.Descriptions.HsmClasses;

namespace Aspid.Core.HSM.Generators.ChildState.Bodies;

public static class ChildStateBody
{
    public static void Generate(
        in ChildStateData data,
        NamespaceText? namespaceText,
        DeclarationText declarationText,
        in SourceProductionContext context)
    {
        var code = new CodeWriter();
        var parentStateTypeName = data.ParentStateType.ToDisplayStringGlobal();
        
        code.BeginClass(namespaceText, declarationText, IChildState.FullName)
            .AppendMultiline(
                $"""
                [{GeneratedCodeAttribute}("Aspid.Core.HSM.Generators.ChildStateGenerator", "0.0.1")]
                global::System.Type {IChildState.FullName}.ParentState => typeof({parentStateTypeName});
                """)
            .EndClass(namespaceText);
        
        var sourceText = code.GetSourceText();
        var fileName = declarationText.GetFileName(namespaceText, "ChildState");
        
        context.AddSource(fileName, sourceText);
    }
}