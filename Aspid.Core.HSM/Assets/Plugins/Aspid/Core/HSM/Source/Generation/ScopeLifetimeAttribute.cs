using System;

// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class ScopeLifetimeAttribute : Attribute
    {
        public ScopeLifetime Lifetime { get; }

        public ScopeLifetimeAttribute(ScopeLifetime lifetime)
        {
            Lifetime = lifetime;
        }
    }
}
