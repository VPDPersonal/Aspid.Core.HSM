// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM
{
    /// <summary>
    /// Controller invoked every frame for each active state in the chain.
    /// </summary>
    public interface IUpdateController : IController
    {
        /// <summary>
        /// Called once per frame.
        /// </summary>
        /// <param name="deltaTime">Time in seconds since the previous frame.</param>
        public void Update(float deltaTime);
    }
}
