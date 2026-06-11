// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM
{
    /// <summary>
    /// Controller invoked at a fixed timestep for each active state in the chain.
    /// </summary>
    public interface IFixedUpdateController : IController
    {
        /// <summary>
        /// Called once per fixed-rate frame.
        /// </summary>
        /// <param name="deltaTime">The fixed timestep interval in seconds.</param>
        public void FixedUpdate(float deltaTime);
    }
}
