using Microsoft.CodeAnalysis;

namespace Aspid.Core.HSM.Generators.Diagnostics;

public static class HsmDiagnostics
{
    public static readonly DiagnosticDescriptor CyclicHierarchy = new(
        id: "HSM001",
        title: "Cyclic state hierarchy",
        messageFormat: "State '{0}' creates a cyclic hierarchy through '{1}'",
        category: "Aspid.Core.HSM",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}
