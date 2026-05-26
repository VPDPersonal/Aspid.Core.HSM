using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Aspid.Core.HSM.Generators.Transition.Data;

public readonly struct TransitionData(
    ClassDeclarationSyntax classDeclaration,
    ITypeSymbol sourceStateType,
    ITypeSymbol targetStateType)
{
    public readonly ITypeSymbol SourceStateType = sourceStateType;
    public readonly ITypeSymbol TargetStateType = targetStateType;
    public readonly ClassDeclarationSyntax ClassDeclaration = classDeclaration;
}
