// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM
{
    public interface IExtensionState : IState
    {
        public bool CanAttachTo(IState hostState);
        
        public void OnAttached(IState hostState) { }
        
        public void OnDetached(IState hostState) { }
    }
}
