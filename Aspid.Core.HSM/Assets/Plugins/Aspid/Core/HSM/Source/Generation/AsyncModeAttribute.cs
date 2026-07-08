using System;

// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM
{
    /// <summary>
    /// Overrides the <see cref="AsyncExecutionMode"/> for a specific async controller interface
    /// within a <see cref="ControllerGroupAttribute"/> class. May be applied multiple times
    /// to configure different interfaces independently.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class AsyncModeAttribute : Attribute
    {
        /// <summary>
        /// The async controller interface whose execution mode is being configured
        /// (e.g. <c>typeof(IAsyncEnterController)</c>).
        /// </summary>
        public Type AsyncInterface { get; }

        /// <summary>
        /// How the controllers behind this interface should be executed.
        /// </summary>
        public AsyncExecutionMode Mode { get; }

        /// <param name="asyncInterface">The async controller interface type.</param>
        /// <param name="mode">The execution mode to use.</param>
        public AsyncModeAttribute(Type asyncInterface, AsyncExecutionMode mode)
        {
            AsyncInterface = asyncInterface;
            Mode = mode;
        }
    }
}
