// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM
{
    public interface IState
    {
        public void Enter() { }
        
        public void Exit() { }
    }
}
