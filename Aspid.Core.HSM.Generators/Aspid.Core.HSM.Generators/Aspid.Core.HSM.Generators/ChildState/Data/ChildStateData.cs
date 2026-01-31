using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Aspid.Core.HSM.Generators.ChildState.Data;

public readonly struct ChildStateData(
    ClassDeclarationSyntax classDeclaration,
    ITypeSymbol parentStateType)
{
    public readonly ITypeSymbol ParentStateType = parentStateType;
    public readonly ClassDeclarationSyntax ClassDeclaration = classDeclaration;
}