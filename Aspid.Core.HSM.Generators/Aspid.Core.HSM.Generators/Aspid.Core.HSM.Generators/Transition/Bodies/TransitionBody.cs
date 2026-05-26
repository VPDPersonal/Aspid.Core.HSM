using Microsoft.CodeAnalysis;
using Aspid.Generators.Helper;
using Aspid.Core.HSM.Generators.Transition.Data;
using static Aspid.Generators.Helper.Classes;
using static Aspid.Core.HSM.Generators.Descriptions.HsmClasses;

namespace Aspid.Core.HSM.Generators.Transition.Bodies;

public static class TransitionBody
{
    public static void Generate(
        in TransitionData data,
        NamespaceText? namespaceText,
        DeclarationText declarationText,
        in SourceProductionContext context)
    {
        var code = new CodeWriter();
        var sourceStateTypeName = data.SourceStateType.ToDisplayStringGlobal();
        var targetStateTypeName = data.TargetStateType.ToDisplayStringGlobal();

        code.BeginClass(namespaceText, declarationText, ITransition.FullName)
            .AppendMultiline(
                $"""
                [{GeneratedCodeAttribute}("Aspid.Core.HSM.Generators.TransitionGenerator", "0.0.1")]
                global::System.Type {ITransition.FullName}.SourceState => typeof({sourceStateTypeName});

                [{GeneratedCodeAttribute}("Aspid.Core.HSM.Generators.TransitionGenerator", "0.0.1")]
                global::System.Type {ITransition.FullName}.TargetState => typeof({targetStateTypeName});
                """)
            .EndClass(namespaceText);

        var sourceText = code.GetSourceText();
        var fileName = declarationText.GetFileName(namespaceText, "Transition");

        context.AddSource(fileName, sourceText);
    }
}
