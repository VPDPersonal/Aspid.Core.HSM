using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM
{
    public sealed class StateTree : IStateHierarchy
    {
        private readonly Dictionary<Type, Type> _parents;

        internal StateTree(Dictionary<Type, Type> parents)
        {
            _parents = parents;
        }

        public bool TryGetParent(Type childType, [NotNullWhen(true)] out Type? parentType) =>
            _parents.TryGetValue(childType, out parentType);
    }

    public sealed class StateTreeBuilder
    {
        private readonly Dictionary<Type, Type> _parents = new();
        private readonly HashSet<Type> _declared = new();

        public StateTreeNode<TRoot> Root<TRoot>() where TRoot : IState
        {
            Declare(typeof(TRoot), parent: null);
            return new StateTreeNode<TRoot>(this);
        }

        internal void DeclareChild(Type parent, Type child)
        {
            Declare(child, parent);
        }

        private void Declare(Type type, Type? parent)
        {
            if (!_declared.Add(type))
                throw new InvalidOperationException($"State '{type}' is declared more than once in the state tree.");

            if (parent != null)
                _parents[type] = parent;
        }

        public StateTree Build()
        {
            foreach (var pair in _parents)
            {
                if (!_declared.Contains(pair.Value))
                    throw new InvalidOperationException(
                        $"State '{pair.Key}' references parent '{pair.Value}', which is not declared in the tree.");
            }

            var visiting = new HashSet<Type>();
            foreach (var type in _declared)
            {
                visiting.Clear();
                Type? current = type;
                while (current != null)
                {
                    if (!visiting.Add(current))
                        throw new InvalidOperationException($"Cycle detected in the state tree at '{type}'.");

                    _parents.TryGetValue(current, out current!);
                }
            }

            return new StateTree(_parents);
        }
    }

    public readonly struct StateTreeNode<TParent> where TParent : IState
    {
        private readonly StateTreeBuilder _builder;

        internal StateTreeNode(StateTreeBuilder builder)
        {
            _builder = builder;
        }

        public StateTreeNode<TParent> Child<TChild>() where TChild : IState
        {
            _builder.DeclareChild(typeof(TParent), typeof(TChild));
            return this;
        }

        public StateTreeNode<TParent> Child<TChild>(Action<StateTreeNode<TChild>> configure) where TChild : IState
        {
            _builder.DeclareChild(typeof(TParent), typeof(TChild));
            configure(new StateTreeNode<TChild>(_builder));
            return this;
        }

        public StateTree Build() => _builder.Build();
    }
}
