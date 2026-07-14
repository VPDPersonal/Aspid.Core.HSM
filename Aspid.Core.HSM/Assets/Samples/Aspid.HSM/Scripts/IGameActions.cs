namespace Aspid.Core.HSM.Sample
{
    /// <summary>
    /// Navigation actions the screens can trigger. Implemented by SampleGameManager;
    /// in-state gameplay actions (attack, goal, dialogue advance) go through
    /// GameSession intent flags instead, so the owning controllers stay in charge.
    /// </summary>
    public interface IGameActions
    {
        void StartAdventure();
        void OpenMultiplayer();
        void OpenSettings();
        void OpenCredits();
        void BackToMenu();
        void PauseGame();
        void ResumeGame();
        void JoinMatch();
        void Retry();
        void ToggleSlowMotion();
        bool IsSlowMotionActive { get; }
    }
}
