using System.Linq;
using Microsoft.CodeAnalysis;
using Aspid.Generators.Helper;
using Aspid.Core.HSM.Generators.ControllerGroup.Data;
using Aspid.Core.HSM.Generators.ControllerGroup.Data.Interfaces;
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
            .Select(d => (d.AsyncTypeSymbol ?? d.TypeSymbol).ToDisplayStringGlobal())
            .ToArray();

        code.BeginClass(namespaceText, declarationText, baseTypes)
            .AppendBody(data)
            .EndClass(namespaceText);

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
                foreach (var method in controllerInterface.Methods)
                {
                    var emittedMethodName = method.AsyncMethod is { } async
                        ? async.AsyncSymbol.Name
                        : method.Symbol.Name;
                    var markerName = GetMarkerNameForMethod(method.Symbol.Name);

                    code.AppendMultiline(
                        $"""
                         [{EditorBrowsableAttribute}({EditorBrowsableState}.Never)]
                         [{GeneratedCodeAttribute}("Aspid.Core.HSM.Generators.ControllerGroupGenerator", "0.0.1")]
                         private readonly static {ProfilerMarker} {markerName} = new("{className}.{emittedMethodName}");
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
                     [{GeneratedCodeAttribute}("Aspid.Core.HSM.Generators.ControllerGroupGenerator", "0.0.1")]
                     private void AddControllers(params {IController}[] controllers) =>
                         throw new global::System.NotImplementedException("No suitable code replacement generated, this is either due to generators failing, or lack of support in your current context");
                     """)
                .AppendLine()
                .AppendLine($"[{GeneratedCodeAttribute}(\"Aspid.Core.HSM.Generators.ControllerGroupGenerator\", \"0.0.1\")]")
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
                foreach (var method in interfaceData.Methods.Where(m => m.Symbol.ReturnsVoid))
                {
                    if (method.AsyncMethod is { } asyncMethod)
                        code.AppendAsyncInterfaceMethod(in interfaceData, in method, asyncMethod);
                    else
                        code.AppendSyncInterfaceMethod(in interfaceData, in method);

                    code.AppendLine();
                }
            }

            return code;
        }

        private CodeWriter AppendSyncInterfaceMethod(
            in ControllerInterfaceData interfaceData,
            in ControllerInterfaceMethodData method)
        {
            var methodSymbol = method.Symbol;
            var ifaceName = interfaceData.TypeSymbol.ToDisplayStringGlobal();

            var parameterDecls = string.Join(",",
                methodSymbol.Parameters.Select(p => $"{p.Type.ToDisplayStringGlobal()} {p.Name}"));
            var parameterArgs = string.Join(",",
                methodSymbol.Parameters.Select(p => p.Name));

            code.AppendMultiline(
                    $"""
                     [{GeneratedCodeAttribute}("Aspid.Core.HSM.Generators.ControllerGroupGenerator", "0.0.1")]
                     void {ifaceName}.{methodSymbol.Name}({parameterDecls})
                     """)
                .BeginBlock()
                .AppendLine($"using ({GetMarkerNameForMethod(methodSymbol.Name)}.Auto())")
                .BeginBlock();

            var indexes = interfaceData.ControllerIndexes;
            for (var step = 0; step < indexes.Length; step++)
            {
                var index = method.IsReverse
                    ? indexes[indexes.Length - 1 - step]
                    : indexes[step];

                code.AppendLine($"using ({GetMarkerNameForController(index)}.Auto())")
                    .BeginBlock()
                    .AppendLine($"(({ifaceName})__controller{index}).{methodSymbol.Name}({parameterArgs});")
                    .EndBlock();
            }

            return code.EndBlock().EndBlock();
        }

        private CodeWriter AppendAsyncInterfaceMethod(
            in ControllerInterfaceData interfaceData,
            in ControllerInterfaceMethodData method,
            AsyncMethodData asyncMethod)
        {
            var asyncSymbol = asyncMethod.AsyncSymbol;
            var asyncIfaceName = interfaceData.AsyncTypeSymbol!.ToDisplayStringGlobal();
            var syncIfaceName = interfaceData.TypeSymbol.ToDisplayStringGlobal();
            var returnType = asyncSymbol.ReturnType.ToDisplayStringGlobal();

            var parameterDecls = string.Join(",",
                asyncSymbol.Parameters.Select(p => $"{p.Type.ToDisplayStringGlobal()} {p.Name}"));
            var parameterArgs = string.Join(",",
                asyncSymbol.Parameters.Select(p => p.Name));

            code.AppendMultiline(
                    $"""
                     [{GeneratedCodeAttribute}("Aspid.Core.HSM.Generators.ControllerGroupGenerator", "0.0.1")]
                     async {returnType} {asyncIfaceName}.{asyncSymbol.Name}({parameterDecls})
                     """)
                .BeginBlock()
                .AppendLine($"using ({GetMarkerNameForMethod(method.Symbol.Name)}.Auto())")
                .BeginBlock();

            var indexes = interfaceData.ControllerIndexes;
            var isAsyncFlags = interfaceData.ControllerIsAsync;
            for (var step = 0; step < indexes.Length; step++)
            {
                var pos = method.IsReverse ? indexes.Length - 1 - step : step;
                var index = indexes[pos];
                var isAsync = isAsyncFlags[pos];

                code.AppendLine($"using ({GetMarkerNameForController(index)}.Auto())")
                    .BeginBlock();

                if (isAsync)
                {
                    code.AppendLine(
                        $"await (({asyncIfaceName})__controller{index}).{asyncSymbol.Name}({parameterArgs});");
                }
                else
                {
                    code.AppendLine(
                        $"(({syncIfaceName})__controller{index}).{method.Symbol.Name}();");
                }

                code.EndBlock();
            }

            return code.EndBlock().EndBlock();
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
