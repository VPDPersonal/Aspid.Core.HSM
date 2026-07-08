#nullable enable

// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM
{
    /// <summary>
    /// Extension methods for <see cref="IState"/>.
    /// </summary>
    public static class StateExtensions
    {
        /// <summary>
        /// Returns the state itself cast to <typeparamref name="TController"/> if it implements
        /// the controller interface, or <c>null</c> otherwise.
        /// </summary>
        /// <typeparam name="TController">The controller interface to look up.</typeparam>
        /// <param name="state">The state to query.</param>
        public static TController? GetController<TController>(this IState state)
            where TController : IController
        {
            if (state is TController controller) return controller;
            return default;
        }
    }
}
