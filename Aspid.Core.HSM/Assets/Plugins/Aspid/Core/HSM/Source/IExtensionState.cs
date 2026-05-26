// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM
{
    public interface IExtensionState : IState
    {
        bool CanAttachTo(IState hostState);
        void OnAttached(IState hostState) { }
        void OnDetached(IState hostState) { }
    }
}
