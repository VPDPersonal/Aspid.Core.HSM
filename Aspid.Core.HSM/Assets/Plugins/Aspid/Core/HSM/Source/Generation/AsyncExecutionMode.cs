// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM
{
    /// <summary>
    /// Determines how multiple async controllers within a <see cref="ControllerGroupAttribute"/> class are invoked.
    /// </summary>
    public enum AsyncExecutionMode
    {
        /// <summary>
        /// All async controllers are started concurrently and awaited together via <c>UniTask.WhenAll</c>.
        /// </summary>
        Parallel,

        /// <summary>
        /// Async controllers are awaited one at a time in declaration order.
        /// </summary>
        Sequential
    }
}
