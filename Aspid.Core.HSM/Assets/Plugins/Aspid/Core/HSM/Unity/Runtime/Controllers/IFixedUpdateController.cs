// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM
{
    public interface IFixedUpdateController : IController
    {
        public void FixedUpdate(float deltaTime);
    }
}
