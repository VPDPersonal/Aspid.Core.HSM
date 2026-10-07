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
                .AppendEnteredControllersField(data)
                .AppendLine()
                .AppendProfilerMarkerFields(data)
                .AppendLine()
                .AppendAddControllers(data)
                .AppendLine()
                .AppendControllerInterface(data);
        }

        // A group that has an enter path remembers how far its enter got through the controllers in
        // declaration order. Exit, update and dispose reach only the controllers the enter got to, so a
        // group whose enter threw or was cancelled halfway does not exit or tick controllers it never entered.
        private CodeWriter AppendEnteredControllersField(in ControllerGroupData data)
        {
            if (!HasEnterPath(data))
                return code;

            return code.AppendMultiline(
                $"""
                 [{EditorBrowsableAttribute}({EditorBrowsableState}.Never)]
                 [{GeneratedCodeAttribute}("Aspid.Core.HSM.Generators.ControllerGroupGenerator", "0.0.1")]
                 private int {EnteredControllersField};
                 """);
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
            var emittedMarkerNames = new System.Collections.Generic.HashSet<string>();

            foreach (var controllerInterface in data.ControllerInterfaces)
            {
                foreach (var method in controllerInterface.Methods)
                {
                    var emittedMethodName = method.AsyncMethod is { } async
                        ? async.AsyncSymbol.Name
                        : method.Symbol.Name;
                    var markerName = GetMarkerNameForMethod(method.Symbol.Name);

                    // Marker field names derive from the method's simple name, so methods that share a
                    // name across interfaces (or overloads) collide. Emit each field name only once.
                    if (!emittedMarkerNames.Add(markerName))
                        continue;

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
                // Full name with generic arguments: CountdownController<ClassicDrivingState> and
                // CountdownController<NetworkDrivingState> must not collapse into one profiler entry.
                var controllerName = data.Controllers[i].Symbol.ToDisplayString();

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
            var hasEnterPath = HasEnterPath(data);
            var controllersCount = data.Controllers.Length;

            foreach (var interfaceData in data.ControllerInterfaces)
            {
                var role = !hasEnterPath
                    ? EntryRole.None
                    : IsInterface(interfaceData, IEnterController)
                        ? EntryRole.Enter
                        : IsInterface(interfaceData, IExitController)
                            ? EntryRole.Exit
                            : EntryRole.Guarded;

                foreach (var method in interfaceData.Methods.Where(m => m.Symbol.ReturnsVoid))
                {
                    if (method.AsyncMethod is { } asyncMethod)
                        code.AppendAsyncInterfaceMethod(in interfaceData, in method, asyncMethod, role, controllersCount);
                    else
                        code.AppendSyncInterfaceMethod(in interfaceData, in method, role, controllersCount);

                    code.AppendLine();
                }
            }

            return code;
        }

        private CodeWriter AppendEntryStart(EntryRole role, int controllersCount, bool enterAllAtOnce)
        {
            if (role is EntryRole.Enter)
                code.AppendLine($"{EnteredControllersField} = {(enterAllAtOnce ? controllersCount : 0)};");

            return code;
        }

        private CodeWriter AppendEntryEnd(EntryRole role, int controllersCount)
        {
            switch (role)
            {
                case EntryRole.Enter:
                    code.AppendLine($"{EnteredControllersField} = {controllersCount};");
                    break;

                case EntryRole.Exit:
                    code.AppendLine($"{EnteredControllersField} = 0;");
                    break;
            }

            return code;
        }

        // Opens the per-controller block: an enter records the controller as reached before calling it
        // (a controller that throws from its own enter still gets its exit), the rest skip unreached ones.
        private CodeWriter BeginControllerCall(EntryRole role, int index, string? cancellationTokenName)
        {
            switch (role)
            {
                case EntryRole.Enter:
                    if (cancellationTokenName is not null)
                        code.AppendLine($"{cancellationTokenName}.ThrowIfCancellationRequested();");

                    code.AppendLine($"{EnteredControllersField} = {index + 1};");
                    break;

                case EntryRole.Exit:
                case EntryRole.Guarded:
                    code.AppendLine($"if ({EnteredControllersField} > {index})")
                        .BeginBlock();
                    break;
            }

            return code;
        }

        private CodeWriter EndControllerCall(EntryRole role)
        {
            if (role is EntryRole.Exit or EntryRole.Guarded)
                code.EndBlock();

            return code;
        }

        private CodeWriter AppendSyncInterfaceMethod(
            in ControllerInterfaceData interfaceData,
            in ControllerInterfaceMethodData method,
            EntryRole role,
            int controllersCount)
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
                .BeginBlock()
                .AppendEntryStart(role, controllersCount, enterAllAtOnce: false);

            var indexes = interfaceData.ControllerIndexes;
            for (var step = 0; step < indexes.Length; step++)
            {
                var index = method.IsReverse
                    ? indexes[indexes.Length - 1 - step]
                    : indexes[step];

                code.BeginControllerCall(role, index, cancellationTokenName: null)
                    .AppendLine($"using ({GetMarkerNameForController(index)}.Auto())")
                    .BeginBlock()
                    .AppendLine($"(({ifaceName})__controller{index}).{methodSymbol.Name}({parameterArgs});")
                    .EndBlock()
                    .EndControllerCall(role);
            }

            return code.AppendEntryEnd(role, controllersCount)
                .EndBlock()
                .EndBlock();
        }

        private CodeWriter AppendAsyncInterfaceMethod(
            in ControllerInterfaceData interfaceData,
            in ControllerInterfaceMethodData method,
            AsyncMethodData asyncMethod,
            EntryRole role,
            int controllersCount)
        {
            var asyncSymbol = asyncMethod.AsyncSymbol;
            var asyncIfaceName = interfaceData.AsyncTypeSymbol!.ToDisplayStringGlobal();
            var syncIfaceName = interfaceData.TypeSymbol.ToDisplayStringGlobal();
            var returnType = asyncSymbol.ReturnType.ToDisplayStringGlobal();

            var parameterDecls = string.Join(",",
                asyncSymbol.Parameters.Select(p => $"{p.Type.ToDisplayStringGlobal()} {p.Name}"));
            var parameterArgs = string.Join(",",
                asyncSymbol.Parameters.Select(p => p.Name));
            // Sync-only controllers in an async bucket are called through the sync interface, whose
            // method may take its own parameters — forward them instead of emitting empty parens.
            var syncParameterArgs = string.Join(",",
                method.Symbol.Parameters.Select(p => p.Name));

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

            // Count async controllers to decide if WhenAll is needed
            var asyncControllerCount = 0;
            for (var i = 0; i < isAsyncFlags.Length; i++)
            {
                if (isAsyncFlags[i]) asyncControllerCount++;
            }

            var isParallel = interfaceData.AsyncMode == 0;
            var useWhenAll = isParallel && asyncControllerCount >= 2;

            code.AppendEntryStart(role, controllersCount, enterAllAtOnce: useWhenAll);

            if (useWhenAll)
            {
                // Parallel mode with 2+ async controllers: emit sync calls first, then WhenAll for async.
                // Every controller is started at once, so an enter marks all of them as reached up front.
                var callRole = role is EntryRole.Enter ? EntryRole.None : role;

                // First pass: emit sync controllers in order
                for (var step = 0; step < indexes.Length; step++)
                {
                    var pos = method.IsReverse ? indexes.Length - 1 - step : step;
                    var index = indexes[pos];
                    var isAsync = isAsyncFlags[pos];

                    if (!isAsync)
                    {
                        code.BeginControllerCall(callRole, index, cancellationTokenName: null)
                            .AppendLine($"using ({GetMarkerNameForController(index)}.Auto())")
                            .BeginBlock()
                            .AppendLine(
                                $"(({syncIfaceName})__controller{index}).{method.Symbol.Name}({syncParameterArgs});")
                            .EndBlock()
                            .EndControllerCall(callRole);
                    }
                }

                // Second pass: collect async calls into WhenAll
                code.AppendLine("await Cysharp.Threading.Tasks.UniTask.WhenAll(");
                var first = true;
                for (var step = 0; step < indexes.Length; step++)
                {
                    var pos = method.IsReverse ? indexes.Length - 1 - step : step;
                    var index = indexes[pos];
                    var isAsync = isAsyncFlags[pos];

                    if (isAsync)
                    {
                        if (!first) code.AppendLine(",");

                        var call = $"(({asyncIfaceName})__controller{index}).{asyncSymbol.Name}({parameterArgs})";

                        code.Append(callRole is EntryRole.None
                            ? $"\t{call}"
                            : $"\t{EnteredControllersField} > {index} ? {call} : default({returnType})");

                        first = false;
                    }
                }

                code.AppendLine();
                code.AppendLine(");");
            }
            else
            {
                var cancellationTokenName = asyncSymbol.Parameters
                    .FirstOrDefault(p => p.Type.ToDisplayString() == "System.Threading.CancellationToken")
                    ?.Name;

                // Sequential mode (or Parallel with 0-1 async controllers): await each individually
                for (var step = 0; step < indexes.Length; step++)
                {
                    var pos = method.IsReverse ? indexes.Length - 1 - step : step;
                    var index = indexes[pos];
                    var isAsync = isAsyncFlags[pos];

                    code.BeginControllerCall(role, index, cancellationTokenName)
                        .AppendLine($"using ({GetMarkerNameForController(index)}.Auto())")
                        .BeginBlock();

                    if (isAsync)
                    {
                        code.AppendLine(
                            $"await (({asyncIfaceName})__controller{index}).{asyncSymbol.Name}({parameterArgs});");
                    }
                    else
                    {
                        code.AppendLine(
                            $"(({syncIfaceName})__controller{index}).{method.Symbol.Name}({syncParameterArgs});");
                    }

                    code.EndBlock()
                        .EndControllerCall(role);
                }
            }

            return code.AppendEntryEnd(role, controllersCount)
                .EndBlock()
                .EndBlock();
        }
    }

    private const string EnteredControllersField = "__enteredControllers";

    private enum EntryRole
    {
        None,
        Enter,
        Exit,
        Guarded
    }

    private static bool HasEnterPath(in ControllerGroupData data) =>
        data.ControllerInterfaces.Any(i => IsInterface(i, IEnterController));

    private static bool IsInterface(in ControllerInterfaceData interfaceData, TypeText interfaceType) =>
        interfaceData.TypeSymbol.ToDisplayString() == interfaceType;

    private static string GetMarkerNameForMethod(string methodName)
    {
        var firstChar = char.ToLower(methodName[0]);
        return $"__{firstChar}{methodName.Remove(0, 1)}Marker";
    }

    private static string GetMarkerNameForController(int controllerIndex) =>
        $"__controller{controllerIndex}Marker";
}
