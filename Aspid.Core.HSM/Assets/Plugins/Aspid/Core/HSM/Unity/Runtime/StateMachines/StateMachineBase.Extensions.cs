using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;

// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM
{
    public partial class StateMachineBase
    {
        private readonly List<IExtensionState> _activeExtensions = new();

        /// <inheritdoc />
        public IReadOnlyList<IExtensionState> ActiveExtensions => _activeExtensions;

        /// <inheritdoc />
        public void AttachExtension<T>() where T : IExtensionState
        {
            // Scopes and initialization are keyed by Type in StateFactory, so a second instance of
            // the same extension type would share (and later dispose) the first one's scope. Dedupe.
            for (int i = 0; i < _activeExtensions.Count; i++)
            {
                if (_activeExtensions[i] is T)
                    return;
            }

            // An extension that declares a parent through IChildState<TParent> lives in a child of that
            // parent's scope, so it can attach only while the parent is active.
            var parentType = _stateFactory.GetParentType(typeof(T));
            if (parentType is not null && IndexOfActiveState(parentType) < 0)
                return;

            var extension = (IExtensionState)_stateFactory.CreateState(typeof(T));
            var leafState = _currentStates[^1];

            if (!extension.CanAttachTo(leafState))
            {
                _stateFactory.Release(extension);
                return;
            }

            _activeExtensions.Add(extension);
            _stateFactory.MarkInitialized(extension);
            extension.Enter();
            extension.GetController<IEnterController>()?.OnEnter();
            extension.OnAttached(leafState);
        }

        /// <inheritdoc />
        public void DetachExtension<T>() where T : IExtensionState
        {
            for (int i = _activeExtensions.Count - 1; i >= 0; i--)
            {
                if (_activeExtensions[i] is T)
                {
                    DetachExtensionAt(i);
                    return;
                }
            }
        }

        // A detach, once started, always completes: a throwing callback must not leave the extension attached,
        // for example under a parent whose scope is disposed next.
        private void DetachExtensionAt(int index)
        {
            var extension = _activeExtensions[index];
            var leafState = _currentStates[^1];

            try
            {
                extension.OnDetached(leafState);
                extension.GetController<IExitController>()?.OnExit();
            }
            finally
            {
                try
                {
                    extension.Exit();
                }
                finally
                {
                    _activeExtensions.RemoveAt(index);
                    if (index <= _tickedExtensionIndex)
                        _tickedExtensionIndex--;

                    _stateFactory.Release(extension);
                }
            }
        }

        // Every bound extension is detached even if one of them throws: none may outlive its parent's scope.
        private void DetachExtensionsBoundTo(IState state)
        {
            var stateType = state.GetType();
            List<Exception>? failures = null;
            for (int i = _activeExtensions.Count - 1; i >= 0; i--)
            {
                if (_stateFactory.GetParentType(_activeExtensions[i].GetType()) != stateType)
                    continue;

                try
                {
                    DetachExtensionAt(i);
                }
                catch (Exception exception)
                {
                    (failures ??= new List<Exception>()).Add(exception);
                }
            }

            ThrowFailures(failures);
        }

        private int IndexOfActiveState(Type stateType)
        {
            for (int i = 0; i < _currentStates.Count; i++)
            {
                if (_currentStates[i].GetType() == stateType)
                    return i;
            }

            return -1;
        }

        private void AutoDetachIncompatibleExtensions() =>
            ThrowFailures(DetachIncompatibleExtensions(failures: null));

        // Every incompatible extension is detached even if one of them throws, as in DetachExtensionsBoundTo.
        // The failures are added to the given list rather than thrown, so a caller that already collects
        // failures keeps them flat instead of nesting an AggregateException. Returns the list.
        private List<Exception>? DetachIncompatibleExtensions(List<Exception>? failures)
        {
            var leafState = _currentStates[^1];
            for (int i = _activeExtensions.Count - 1; i >= 0; i--)
            {
                try
                {
                    if (!_activeExtensions[i].CanAttachTo(leafState))
                        DetachExtensionAt(i);
                }
                catch (Exception exception)
                {
                    (failures ??= new List<Exception>()).Add(exception);
                }
            }

            return failures;
        }

        // A single failure is rethrown as it is, with its stack trace; several go out together.
        private static void ThrowFailures(List<Exception>? failures)
        {
            if (failures is null)
                return;

            if (failures.Count == 1)
                ExceptionDispatchInfo.Capture(failures[0]).Throw();

            throw new AggregateException(failures);
        }
    }
}
