using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Aspid.Core.HSM.Generators;

public readonly struct ControllerGroupData(ClassDeclarationSyntax classDeclaration)
{
    public readonly ClassDeclarationSyntax ClassDeclaration = classDeclaration;
}
