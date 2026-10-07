using System;

// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM
{
    /// <summary>
    /// Configures the <see cref="ScopeLifetime"/> for a state's <see cref="IStateScope"/>.
    /// Defaults to <see cref="HSM.ScopeLifetime.Transient"/> when not specified.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class ScopeLifetimeAttribute : Attribute
    {
        /// <summary>
        /// The scope lifetime strategy for the decorated state.
        /// </summary>
        public ScopeLifetime Lifetime { get; }

        /// <param name="lifetime">The scope lifetime to apply.</param>
        public ScopeLifetimeAttribute(ScopeLifetime lifetime)
        {
            Lifetime = lifetime;
        }
    }
}
