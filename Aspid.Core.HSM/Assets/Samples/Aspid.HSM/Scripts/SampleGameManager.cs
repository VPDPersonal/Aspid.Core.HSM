using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Aspid.Core.HSM.Sample
{
    /// <summary>
    /// A tiny game ("Aspid Quest") driving a deep HSM. Each leaf state gets its own screen;
    /// screens trigger navigation via <see cref="IGameActions"/> and gameplay via intent flags
    /// on the <see cref="GameSession"/> slices, which the owning state controllers consume.
    /// The manager polls result signals after dispatch (OnUpdated) — session flags for shared
    /// data, typed properties pattern-matched off the leaf state for state-owned data — and
    /// performs the transitions, so no state ever mutates the machine while it is dispatching.
    /// </summary>
    public class SampleGameManager : MonoStateMachine, IGameActions
    {
        private GameSession _session;
        private Type _resumeLeaf;

        private readonly List<IState> _guiStates = new();
        private readonly List<IExtensionState> _guiExtensions = new();

        private void Awake()
        {
            _session = new GameSession();
            Initialize(new SampleStateFactory(_session, this));
            RegisterTransitions();

            BootAsync().Forget();
        }

        private void RegisterTransitions()
        {
            RegisterTransition(new MenuToGameplayTransition());
            RegisterTransition(new ExplorationToPauseTransition());
            RegisterTransition(new PauseToExplorationTransition());
            RegisterTransition(new ExplorationToCombatTransition());
            RegisterTransition(new CombatToExplorationTransition(_session));
            RegisterTransition(new CombatToGameOverTransition());
            RegisterTransition(new DialogueToExplorationTransition());
            RegisterTransition(new LobbyToMatchTransition(_session));
            RegisterTransition(new MatchToGameOverTransition());
            RegisterTransition(new GameOverToMenuTransition(_session));
        }

        private async UniTaskVoid BootAsync()
        {
            await ChangeStateAsync<LoadingState>(destroyCancellationToken);
            TransitionTo<MainMenuState>();
        }

        #region IGameActions
        private bool CanNavigate => IsInitialized && !IsTransitioning;

        public void StartAdventure()
        {
            if (!CanNavigate) return;
            _session.ResetRun();
            TransitionTo<ExplorationState>();
        }

        public void OpenMultiplayer()
        {
            if (!CanNavigate) return;
            _session.ResetRun();
            ChangeState<LobbyState>();
        }

        public void OpenSettings()
        {
            if (CanNavigate)
                ChangeState<SettingsState>();
        }

        public void OpenCredits()
        {
            if (CanNavigate)
                ChangeState<CreditsState>();
        }

        public void BackToMenu()
        {
            if (CanNavigate)
                TransitionTo<MainMenuState>();
        }

        public void PauseGame()
        {
            if (!CanNavigate) return;
            _resumeLeaf = CurrentStates[^1].GetType();
            TransitionTo<PauseState>();
        }

        public void ResumeGame()
        {
            if (!CanNavigate) return;

            // Match is Transient — pausing it abandons the run,
            // so resuming lands back in the lobby instead.
            if (_resumeLeaf == typeof(MatchState))
                ChangeState<LobbyState>();
            else if (_resumeLeaf == typeof(DialogueState))
                ChangeState<DialogueState>();
            else if (_resumeLeaf == typeof(CombatState))
                ChangeState<CombatState>();
            else
                TransitionTo<ExplorationState>();
        }

        public void JoinMatch()
        {
            // TransitionVia looks the transition up by its type instead of the
            // (source, target) pair — the guard and hooks still run the same way.
            if (CanNavigate)
                TransitionVia<LobbyToMatchTransition>();
        }

        public void Retry()
        {
            if (!CanNavigate) return;
            _session.ResetRun();
            ChangeState<ExplorationState>();
        }

        public void ToggleSlowMotion() => ToggleExtension<SlowMotionExtension>();

        // Incompatible extensions are auto-detached by the machine after each state change,
        // so the active list is the single source of truth for the toggle state.
        public bool IsSlowMotionActive => HasExtension<SlowMotionExtension>();
        #endregion

        #region Extensions
        private bool HasExtension<T>() where T : IExtensionState =>
            ActiveExtensions.Any(extension => extension is T);

        private void ToggleExtension<T>() where T : IExtensionState
        {
            if (HasExtension<T>()) DetachExtension<T>();
            else AttachExtension<T>();
        }
        #endregion

        #region Input & flags
        protected override void OnUpdating()
        {
            base.OnUpdating();
            if (IsInitialized && !IsTransitioning)
                HandleHotkeys();
        }

        protected override void OnUpdated()
        {
            base.OnUpdated();
            if (IsInitialized && !IsTransitioning)
                PollResultSignals();
        }

        private void HandleHotkeys()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || CurrentStates.Count == 0) return;

            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                switch (CurrentStates[^1])
                {
                    case SettingsState:
                    case CreditsState:
                    case LobbyState:
                    case GameOverState:
                        BackToMenu();
                        break;
                    case ExplorationState:
                    case CombatState:
                    case DialogueState:
                    case MatchState:
                        PauseGame();
                        break;
                    case PauseState:
                        ResumeGame();
                        break;
                }
            }

            if (keyboard.enterKey.wasPressedThisFrame && CurrentStates[^1] is LobbyState)
                JoinMatch();

            // Hidden dev toggles for the extension system
            if (keyboard.f1Key.wasPressedThisFrame)
                ToggleExtension<DebugOverlayExtension>();

            if (keyboard.f2Key.wasPressedThisFrame)
                ToggleExtension<FpsCounterExtension>();
        }

        private void PollResultSignals()
        {
            switch (CurrentStates[^1])
            {
                case ExplorationState when _session.Exploration.EncounterTriggered:
                    _session.Exploration.EncounterTriggered = false;
                    TransitionTo<CombatState>();
                    break;

                case ExplorationState when _session.Exploration.DialogueRequested:
                    _session.Exploration.DialogueRequested = false;
                    TransitionTo<DialogueState>();
                    break;

                case CombatState when _session.Combat.Result == CombatResult.Victory:
                    TransitionTo<ExplorationState>();
                    break;

                case CombatState when _session.Combat.Result == CombatResult.Defeat:
                    TransitionTo<GameOverState>();
                    break;

                // State-owned signals are read straight off the typed leaf state
                case DialogueState dialogue when dialogue.IsFinished:
                    TransitionTo<ExplorationState>();
                    break;

                case MatchState when _session.Match.Finished:
                    TransitionTo<GameOverState>();
                    break;

                case CreditsState credits when credits.IsFinished:
                    ChangeState<MainMenuState>();
                    break;
            }
        }
        #endregion

        protected override void OnChangedState()
        {
            base.OnChangedState();
            var chain = string.Join(" → ", CurrentStates.Select(s => s.GetType().Name));
            Debug.Log($"[Sample] State chain: {chain}");
        }

        private void OnGUI()
        {
            if (!IsInitialized) return;

            // Dispatch through the active chain root→leaf, exactly like the framework
            // dispatches IUpdateController — each state owns and draws its own screen.
            // Screen buttons call IGameActions and can change state mid-draw, so iterate snapshots.
            _guiStates.Clear();
            _guiStates.AddRange(CurrentStates);
            foreach (var state in _guiStates)
                state.GetController<IGuiController>()?.OnGui();

            _guiExtensions.Clear();
            _guiExtensions.AddRange(ActiveExtensions);
            foreach (var extension in _guiExtensions)
                extension.GetController<IGuiController>()?.OnGui();
        }

        private void OnDestroy()
        {
            Dispose();
        }
    }
}
