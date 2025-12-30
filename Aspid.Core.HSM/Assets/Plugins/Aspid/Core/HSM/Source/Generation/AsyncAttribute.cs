using System;

// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM
{
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class AsyncAttribute : Attribute
    {
        public bool IsWait { get; private set; } = true;
    }
}
