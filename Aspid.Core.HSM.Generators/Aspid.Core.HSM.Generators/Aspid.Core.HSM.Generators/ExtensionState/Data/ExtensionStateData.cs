using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Aspid.Core.HSM.Generators.ExtensionState.Data;

public readonly struct ExtensionStateData(
    ClassDeclarationSyntax classDeclaration,
    ImmutableArray<ITypeSymbol> compatibleStates,
    bool includeDescendants)
{
    public readonly ImmutableArray<ITypeSymbol> CompatibleStates = compatibleStates;
    public readonly bool IncludeDescendants = includeDescendants;
    public readonly ClassDeclarationSyntax ClassDeclaration = classDeclaration;
}
