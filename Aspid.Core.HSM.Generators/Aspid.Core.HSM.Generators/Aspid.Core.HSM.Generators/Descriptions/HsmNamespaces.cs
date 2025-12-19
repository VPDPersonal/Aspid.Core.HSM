using Aspid.Generators.Helper;

// ReSharper disable InconsistentNaming
namespace Aspid.Core.HSM.Generators.Descriptions;

public static class HsmNamespaces
{
    public static readonly NamespaceText Aspid = new("Aspid");
    public static readonly NamespaceText Aspid_Core = new("Core", Aspid);
    public static readonly NamespaceText Aspid_Core_HSM = new("HSM", Aspid_Core);
}
