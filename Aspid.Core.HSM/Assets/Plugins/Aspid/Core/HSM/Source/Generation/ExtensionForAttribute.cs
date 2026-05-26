using System;

// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class ExtensionForAttribute : Attribute
    {
        public Type[] CompatibleStates { get; }

        public ExtensionForAttribute(params Type[] compatibleStates)
        {
            CompatibleStates = compatibleStates;
        }
    }
}
