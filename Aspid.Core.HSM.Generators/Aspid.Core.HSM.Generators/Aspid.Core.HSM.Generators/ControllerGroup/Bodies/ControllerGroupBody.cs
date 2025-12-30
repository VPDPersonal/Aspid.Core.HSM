using System.Linq;
using Microsoft.CodeAnalysis;
using Aspid.Generators.Helper;
using Aspid.Core.HSM.Generators.ControllerGroup.Data;
using static Aspid.Generators.Helper.Classes;
using static Aspid.Generators.Helper.Unity.UnityClasses;
using static Aspid.Core.HSM.Generators.Descriptions.HsmClasses;

namespace Aspid.Core.HSM.Generators.ControllerGroup.Bodies;

public static class ControllerGroupBody
{
    public static void Generate(
        in ControllerGroupData data,
        NamespaceText? namespaceText,
        DeclarationText declarationText,
        in SourceProductionContext context)
    {
        var code = new CodeWriter();
        var baseTypes = data.ControllerInterfaces
            .Select(data => data.TypeSymbol.ToDisplayStringGlobal())
            .ToArray();
        
        code.BeginClass(namespaceText, declarationText, baseTypes)
            .AppendBody(data)
            .EndClass(namespaceText);
        
        code.AppendLine($"// {data.Controllers.Length}");
        var sourceText = code.GetSourceText();
        var fileName = declarationText.GetFileName(namespaceText, "State");
        
        context.AddSource(fileName, sourceText);
    }

    extension(CodeWriter code)
    {
        private CodeWriter AppendBody(in ControllerGroupData data)
        {
            return code
                .AppendControllerFields(data)
                .AppendLine()
                .AppendProfilerMarkerFields(data)
                .AppendLine()
                .AppendAddControllers(data)
                .AppendLine()
                .AppendControllerInterface(data);
        }

        private CodeWriter AppendControllerFields(in ControllerGroupData data)
        {
            for (var i = 0; i < data.Controllers.Length; i++)
            {
                var controller = data.Controllers[i];

                code.AppendMultiline(
                    $"""
                     [{EditorBrowsableAttribute}({EditorBrowsableState}.Never)]
                     [{GeneratedCodeAttribute}("Aspid.Core.HSM.Generators.ControllerGroupGenerator", "0.0.1")]
                     private {controller.Symbol.ToDisplayStringGlobal()} __controller{i}; 
                     """);
            }

            return code;
        }

        private CodeWriter AppendProfilerMarkerFields(in ControllerGroupData data)
        {
            var className = data.ClassDeclaration.Identifier.Text;

            foreach (var controllerInterface in data.ControllerInterfaces)
            {
                foreach (var methodName in controllerInterface.Methods.Select(method => method.Symbol.Name))
                {
                    var markerName = GetMarkerNameForMethod(methodName);
                
                    code.AppendMultiline(
                        $"""
                         [{EditorBrowsableAttribute}({EditorBrowsableState}.Never)]
                         [{GeneratedCodeAttribute}("Aspid.Core.HSM.Generators.ControllerGroupGenerator", "0.0.1")]
                         private readonly static {ProfilerMarker} {markerName} = new("{className}.{methodName}");
                         """);
                }
            }
        
            code.AppendLine();

            for (var i = 0; i < data.Controllers.Length; i++)
            {
                var markerName = GetMarkerNameForController(i);
                var controllerName = data.Controllers[i].Symbol.Name;
                
                code.AppendMultiline(
                    $"""
                     [{EditorBrowsableAttribute}({EditorBrowsableState}.Never)]
                     [{GeneratedCodeAttribute}("Aspid.Core.HSM.Generators.ControllerGroupGenerator", "0.0.1")]
                     private readonly static {ProfilerMarker} {markerName} = new("{controllerName}");
                     """);
            }

            return code;
        }

        private CodeWriter AppendAddControllers(in ControllerGroupData data)
        {
            code.AppendMultiline(
                    $"""
                     [{GeneratedCodeAttribute}("GameModesGenerator.StateGenerator.ControllerGroupGenerator", "0.0.1")]
                     private void AddControllers(params {IController}[] controllers) =>
                         throw new global::System.NotImplementedException("No suitable code replacement generated, this is either due to generators failing, or lack of support in your current context");
                     """)
                .AppendLine()
                .AppendLine($"[{GeneratedCodeAttribute}(\"GameModesGenerator.StateGenerator.ControllerGroupGenerator\", \"0.0.1\")]")
                .AppendLine("private void AddControllers(");

            for (var i = 0; i < data.Controllers.Length; i++)
            {
                var controller = data.Controllers[i];

                if (i > 0) code.AppendLine(",");
                code.Append($"\t{controller.Symbol.ToDisplayStringGlobal()} controller{i}");
            }
        
            code.AppendLine(")")
                .BeginBlock();
        
            for (var i = 0; i < data.Controllers.Length; i++)
            {
                code.AppendLine($"__controller{i} = controller{i};");
            }
        
            code.EndBlock();
            return code;
        }

        private CodeWriter AppendControllerInterface(in ControllerGroupData data)
        {
            foreach (var interfaceData in data.ControllerInterfaces)
            {
                foreach (var method in interfaceData.Methods.Where(method => method.Symbol.ReturnsVoid))
                {
                    var methodSymbol = method.Symbol;
                
                    var methodParameterNames = string.Join(
                        ",",
                        methodSymbol.Parameters.Select(methodParameter => methodParameter.Name));
                
                    var methodParameters = string.Join(
                        ",",
                        methodSymbol.Parameters.Select(methodParameter => $"{methodParameter.Type.ToDisplayStringGlobal()} {methodParameter.Name}"));
                
                    code.AppendMultiline(
                            $"""
                             [{GeneratedCodeAttribute}("GameModesGenerator.StateGenerator.ControllerGroupGenerator", "0.0.1")]
                             void {interfaceData.TypeSymbol.ToDisplayStringGlobal()}.{methodSymbol.Name}({methodParameters})
                             """)
                        .BeginBlock()
                        .AppendLine($"using ({GetMarkerNameForMethod(methodSymbol.Name)}.Auto())")
                        .BeginBlock();

                    var indexes = method.IsReverse
                        ? interfaceData.ControllerIndexes.Reverse()
                        : interfaceData.ControllerIndexes;
                
                    foreach (var index in indexes)
                    {
                        code.AppendMultiline(
                            $"""
                             using ({GetMarkerNameForController(index)}.Auto());
                                 (({interfaceData.TypeSymbol.ToDisplayStringGlobal()})__controller{index}).{methodSymbol.Name}({methodParameterNames});
                             """);
                    }   
                
                    code.EndBlock()
                        .EndBlock()
                        .AppendLine();
                }
            }
        
            return code;
        }
    }

    private static string GetMarkerNameForMethod(string methodName)
    {
        var firstChar = char.ToLower(methodName[0]);
        return $"__{firstChar}{methodName.Remove(0, 1)}Marker";
    }

    private static string GetMarkerNameForController(int controllerIndex) =>
        $"__controller{controllerIndex}Marker";
}