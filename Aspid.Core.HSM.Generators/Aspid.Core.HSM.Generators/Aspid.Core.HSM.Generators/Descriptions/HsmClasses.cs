using Aspid.Generators.Helper;

namespace Aspid.Core.HSM.Generators.Descriptions;

public static class HsmClasses
{
    public static readonly TypeText IController = new(nameof(IController), namespaceText: HsmNamespaces.Aspid_Core_HSM);
    public static readonly TypeText IChildState = new(nameof(IChildState), namespaceText: HsmNamespaces.Aspid_Core_HSM);
    public static readonly TypeText ITransition = new(nameof(ITransition), namespaceText: HsmNamespaces.Aspid_Core_HSM);
    public static readonly TypeText IState = new(nameof(IState), namespaceText: HsmNamespaces.Aspid_Core_HSM);
    public static readonly TypeText IExtensionState = new(nameof(IExtensionState), namespaceText: HsmNamespaces.Aspid_Core_HSM);

    public static readonly AttributeText ReverseExecuteAttribute = new(nameof(ReverseExecuteAttribute), namespaceText: HsmNamespaces.Aspid_Core_HSM);
    public static readonly AttributeText ControllerGroupAttribute = new(nameof(ControllerGroupAttribute), namespaceText: HsmNamespaces.Aspid_Core_HSM);

    public static readonly AttributeText TransitionAttribute = new(nameof(TransitionAttribute), namespaceText: HsmNamespaces.Aspid_Core_HSM);
    public static readonly AttributeText AsyncOfAttribute = new(nameof(AsyncOfAttribute), namespaceText: HsmNamespaces.Aspid_Core_HSM);
    public static readonly AttributeText AsyncModeAttribute = new(nameof(AsyncModeAttribute), namespaceText: HsmNamespaces.Aspid_Core_HSM);
    public static readonly AttributeText ExtensionForAttribute = new(nameof(ExtensionForAttribute), namespaceText: HsmNamespaces.Aspid_Core_HSM);
}
