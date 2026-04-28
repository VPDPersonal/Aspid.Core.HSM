// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM
{
    internal sealed class MonoStateMachineCore : StateMachineBase
    {
        private readonly MonoStateMachine _owner;

        public MonoStateMachineCore(MonoStateMachine owner, StateFactory factory)
            : base(factory)
        {
            _owner = owner;
        }

        public void InvokeUpdate(float deltaTime) => Update(deltaTime);

        public void InvokeLateUpdate(float deltaTime) => LateUpdate(deltaTime);

        public void InvokeFixedUpdate(float deltaTime) => FixedUpdate(deltaTime);

        protected override void OnChangingState() => _owner.RaiseChangingState();

        protected override void OnChangedState() => _owner.RaiseChangedState();

        protected override void OnEnteringState(IState state) => _owner.RaiseEnteringState(state);

        protected override void OnEnteredState(IState state) => _owner.RaiseEnteredState(state);

        protected override void OnExitingState(IState state) => _owner.RaiseExitingState(state);

        protected override void OnExitedState(IState state) => _owner.RaiseExitedState(state);

        protected override void Disposing() => _owner.RaiseDisposing();

        protected override void Disposed() => _owner.RaiseDisposed();
    }
}
