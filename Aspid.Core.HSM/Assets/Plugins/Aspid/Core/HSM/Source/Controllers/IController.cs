// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM
{
    /// <summary>
    /// Marker interface for state controllers. Concrete controllers (e.g. <see cref="IEnterController"/>,
    /// <see cref="IUpdateController"/>) are resolved on a state via <see cref="StateExtensions.GetController{TController}"/>.
    /// </summary>
    public interface IController { }
}
