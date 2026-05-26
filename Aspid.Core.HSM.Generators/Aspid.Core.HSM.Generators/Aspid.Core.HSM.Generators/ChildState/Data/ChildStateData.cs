using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Aspid.Core.HSM.Generators.ChildState.Data;

public readonly struct ChildStateData
{
    public readonly ITypeSymbol ParentStateType;
    public readonly ClassDeclarationSyntax ClassDeclaration;
    public readonly CyclicHierarchyInfo? CycleInfo;

    public ChildStateData(
        ClassDeclarationSyntax classDeclaration,
        ITypeSymbol parentStateType)
    {
        ClassDeclaration = classDeclaration;
        ParentStateType = parentStateType;
        CycleInfo = null;
    }

    public ChildStateData(
        ClassDeclarationSyntax classDeclaration,
        ITypeSymbol parentStateType,
        CyclicHierarchyInfo cycleInfo)
    {
        ClassDeclaration = classDeclaration;
        ParentStateType = parentStateType;
        CycleInfo = cycleInfo;
    }
}

public readonly struct CyclicHierarchyInfo(string className, string cycleThrough)
{
    public readonly string ClassName = className;
    public readonly string CycleThrough = cycleThrough;
}