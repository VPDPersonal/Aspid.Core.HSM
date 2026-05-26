using System;

// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class AsyncModeAttribute : Attribute
    {
        public Type AsyncInterface { get; }
        public AsyncExecutionMode Mode { get; }

        public AsyncModeAttribute(Type asyncInterface, AsyncExecutionMode mode)
        {
            AsyncInterface = asyncInterface;
            Mode = mode;
        }
    }
}
