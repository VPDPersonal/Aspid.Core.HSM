using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Aspid.Core.HSM.Generators.ControllerGroup.Data;

namespace Aspid.Core.HSM.Generators.ControllerGroup.Factories;

public static class ControllerGroupDataFactory
{
    public static ControllerGroupData Create(
        SemanticModel semanticModel,
        ClassDeclarationSyntax classDeclarationSyntax)
    {
        var controllers = ControllerDataFactory.Create(semanticModel, classDeclarationSyntax);
        var controllerInterfaces = ControllerInterfaceDataFactory.Create(controllers);
        
        return new ControllerGroupData(classDeclarationSyntax, controllers, controllerInterfaces);
    }
}
