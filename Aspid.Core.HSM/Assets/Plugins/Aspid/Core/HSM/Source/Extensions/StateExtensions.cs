#nullable enable

// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM
{
    public static class StateExtensions
    {
        public static TController? GetController<TController>(this IState state)
            where TController : IController
        {
            if (state is TController controller) return controller;
            return default;
        }
    }
}
