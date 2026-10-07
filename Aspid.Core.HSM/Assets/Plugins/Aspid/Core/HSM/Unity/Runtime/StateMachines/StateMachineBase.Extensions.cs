using System.Collections.Generic;

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

            var extension = (IExtensionState)_stateFactory.CreateInstance(typeof(T));
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

        private void DetachExtensionAt(int index)
        {
            var extension = _activeExtensions[index];
            var leafState = _currentStates[^1];

            extension.OnDetached(leafState);
            extension.GetController<IExitController>()?.OnExit();
            extension.Exit();
            _stateFactory.Release(extension);
            _activeExtensions.RemoveAt(index);
        }

        private void AutoDetachIncompatibleExtensions()
        {
            var leafState = _currentStates[^1];
            for (int i = _activeExtensions.Count - 1; i >= 0; i--)
            {
                if (!_activeExtensions[i].CanAttachTo(leafState))
                    DetachExtensionAt(i);
            }
        }
    }
}
