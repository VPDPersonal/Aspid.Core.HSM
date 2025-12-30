using System.Collections.Immutable;
using Aspid.Core.HSM.Generators.ControllerGroup.Data.Interfaces;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Aspid.Core.HSM.Generators.ControllerGroup.Data;

public readonly struct ControllerGroupData(
    ClassDeclarationSyntax classDeclaration,
    ImmutableArray<ControllerData> controllers,
    ImmutableArray<ControllerInterfaceData> controllersInterfaces)
{
    public readonly ClassDeclarationSyntax ClassDeclaration = classDeclaration;
    
    public readonly ImmutableArray<ControllerData> Controllers = controllers;
    public readonly ImmutableArray<ControllerInterfaceData> ControllerInterfaces = controllersInterfaces;
}
