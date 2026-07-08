// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM
{
    /// <summary>
    /// Controls how a state's <see cref="IStateScope"/> is managed when the state is exited.
    /// </summary>
    public enum ScopeLifetime
    {
        /// <summary>
        /// The scope is disposed when the state exits. A fresh scope is created on re-entry.
        /// </summary>
        Transient,

        /// <summary>
        /// The scope is preserved when the state exits and reused if the state is re-entered.
        /// </summary>
        Cached
    }
}
