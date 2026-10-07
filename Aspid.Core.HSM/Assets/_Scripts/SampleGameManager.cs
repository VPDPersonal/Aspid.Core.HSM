using _Scripts.Extensions;
using _Scripts.States;
using _Scripts.Transitions;
using Aspid.Core.HSM;
using UnityEngine;
using UnityEngine.InputSystem;

namespace _Scripts
{
    public class SampleGameManager : MonoStateMachine
    {
        private bool _debugOverlayActive;
        private bool _fpsCounterActive;

        private void Awake()
        {
            var factory = new SampleStateFactory();
            Initialize(factory);

            RegisterTransition(new MenuToGameplayTransition());
            RegisterTransition(new GameplayToPauseTransition());
            RegisterTransition(new PauseToGameplayTransition());

            ChangeState<MainMenuState>();
            Debug.Log("[Sample] Ready. Controls: Enter=Play, Escape=Pause/Resume, F1=DebugOverlay, F2=FPS, Backspace=MainMenu");
        }

        protected override void OnUpdating()
        {
            base.OnUpdating();
            HandleInput();
        }

        private void HandleInput()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            var leaf = CurrentStates[^1];

            switch (leaf)
            {
                case MainMenuState:
                    HandleMainMenuInput(keyboard);
                    break;
                case SinglePlayerState:
                    HandleGameplayInput(keyboard);
                    break;
                case PauseState:
                    HandlePauseInput(keyboard);
                    break;
            }

            HandleGlobalInput(keyboard);
        }

        private void HandleMainMenuInput(Keyboard keyboard)
        {
            if (keyboard.enterKey.wasPressedThisFrame)
            {
                Debug.Log("[Sample] Starting game...");
                TransitionTo<SinglePlayerState>();
            }
        }

        private void HandleGameplayInput(Keyboard keyboard)
        {
            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                Debug.Log("[Sample] Pausing...");
                TransitionTo<PauseState>();
            }
        }

        private void HandlePauseInput(Keyboard keyboard)
        {
            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                Debug.Log("[Sample] Resuming...");
                TransitionTo<SinglePlayerState>();
            }

            if (keyboard.backspaceKey.wasPressedThisFrame)
            {
                Debug.Log("[Sample] Returning to main menu...");
                ChangeState<MainMenuState>();
            }
        }

        private void HandleGlobalInput(Keyboard keyboard)
        {
            if (keyboard.f1Key.wasPressedThisFrame)
            {
                _debugOverlayActive = !_debugOverlayActive;
                if (_debugOverlayActive)
                    AttachExtension<DebugOverlayExtension>();
                else
                    DetachExtension<DebugOverlayExtension>();
                Debug.Log($"[Sample] DebugOverlay: {(_debugOverlayActive ? "ON" : "OFF")}");
            }

            if (keyboard.f2Key.wasPressedThisFrame)
            {
                _fpsCounterActive = !_fpsCounterActive;
                if (_fpsCounterActive)
                    AttachExtension<FpsCounterExtension>();
                else
                    DetachExtension<FpsCounterExtension>();
                Debug.Log($"[Sample] FpsCounter: {(_fpsCounterActive ? "ON" : "OFF")}");
            }
        }

        protected override void OnChangedState()
        {
            base.OnChangedState();
            var chain = string.Join(" → ", System.Linq.Enumerable.Select(CurrentStates, s => s.GetType().Name));
            Debug.Log($"[Sample] State chain: {chain}");
        }

        private void OnDestroy()
        {
            Dispose();
        }
    }
}
