using System;

// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM
{
    [AttributeUsage(AttributeTargets.Interface)]
    public sealed class AsyncOfAttribute : Attribute
    {
        public Type SyncInterface { get; }

        public AsyncOfAttribute(Type syncInterface)
        {
            SyncInterface = syncInterface;
        }
    }
}
