#nullable enable
using System;
using System.Collections.Concurrent;

// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM
{
    /// <summary>
    /// Provides the parent-to-child relations that state types declare through <see cref="IChildState{T}"/>.
    /// </summary>
    /// <remarks>
    /// Read from the types alone, without creating a state. Results are cached per type.
    /// </remarks>
    public static class StateHierarchy
    {
        private static readonly ConcurrentDictionary<Type, Type?> _parentTypes = new();

        /// <summary>
        /// Returns the parent state type <paramref name="stateType"/> declares through <see cref="IChildState{T}"/>.
        /// </summary>
        /// <param name="stateType">The state type to inspect.</param>
        /// <returns>The parent state type; <see langword="null"/> for a root state.</returns>
        /// <exception cref="InvalidOperationException">
        /// <paramref name="stateType"/> implements <see cref="IChildState"/> without <see cref="IChildState{T}"/>.
        /// </exception>
        public static Type? GetParentType(Type stateType) =>
            _parentTypes.GetOrAdd(stateType, static type => ReadParentType(type));

        /// <summary>
        /// Returns whether <paramref name="stateType"/> is <paramref name="ancestorType"/> or one of its descendants.
        /// </summary>
        /// <remarks>
        /// For the leaf of the active chain this answers whether <paramref name="ancestorType"/> is active.
        /// </remarks>
        /// <param name="stateType">The state type to test, typically the active leaf.</param>
        /// <param name="ancestorType">The state type to look for in the chain above <paramref name="stateType"/>.</param>
        /// <returns><see langword="true"/> if the chain of parents from <paramref name="stateType"/> reaches
        /// <paramref name="ancestorType"/>; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="InvalidOperationException">The parents above <paramref name="stateType"/> form a cycle.</exception>
        public static bool IsSelfOrDescendantOf(Type stateType, Type ancestorType)
        {
            // Bounded instead of tracked: no allocation on this per-change path, and no real hierarchy is this deep.
            var depth = 0;
            for (Type? type = stateType; type is not null; type = GetParentType(type))
            {
                if (type == ancestorType)
                    return true;

                if (++depth > MaxDepth)
                    throw CycleException(stateType, type);
            }

            return false;
        }

        private const int MaxDepth = 256;

        internal static InvalidOperationException CycleException(Type stateType, Type repeatedType) =>
            new($"The parents declared through {nameof(IChildState)}<TParent> above '{stateType.Name}' form a cycle " +
                $"at '{repeatedType.Name}'.");

        private static Type? ReadParentType(Type stateType)
        {
            if (!typeof(IChildState).IsAssignableFrom(stateType))
                return null;

            foreach (var contract in stateType.GetInterfaces())
            {
                if (contract.IsGenericType && contract.GetGenericTypeDefinition() == typeof(IChildState<>))
                    return contract.GetGenericArguments()[0];
            }

            // The parent must be known from the type alone: a state is created inside its parent's scope,
            // so reading the parent off an instance would mean creating the state before its scope exists.
            throw new InvalidOperationException(
                $"{stateType} implements {nameof(IChildState)} without {nameof(IChildState)}<TParent>. " +
                $"Declare the parent by implementing {nameof(IChildState)}<TParent>.");
        }
    }
}
