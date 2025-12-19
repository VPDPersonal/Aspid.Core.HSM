// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM
{
    public interface ILateUpdateController : IController
    {
        public void LateUpdate(float deltaTime);
    }
}
