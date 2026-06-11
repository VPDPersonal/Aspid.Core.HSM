// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM
{
    /// <summary>
    /// Controller invoked in the late-update phase for each active state in the chain.
    /// </summary>
    public interface ILateUpdateController : IController
    {
        /// <summary>
        /// Called once per frame after all <see cref="IUpdateController.Update"/> calls have completed.
        /// </summary>
        /// <param name="deltaTime">Time in seconds since the previous frame.</param>
        public void LateUpdate(float deltaTime);
    }
}
