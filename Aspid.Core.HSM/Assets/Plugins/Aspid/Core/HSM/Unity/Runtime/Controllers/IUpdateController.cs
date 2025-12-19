// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM
{
    public interface IUpdateController : IController
    {
        public void Update(float deltaTime);
    }
}
