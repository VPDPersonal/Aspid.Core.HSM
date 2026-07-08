// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM
{
    /// <summary>
    /// Controller invoked when a state is entered. Called after <see cref="IState.Enter"/>.
    /// </summary>
    public interface IEnterController : IController
    {
        /// <summary>
        /// Called once each time the state is entered.
        /// </summary>
        public void OnEnter();
    }
}
