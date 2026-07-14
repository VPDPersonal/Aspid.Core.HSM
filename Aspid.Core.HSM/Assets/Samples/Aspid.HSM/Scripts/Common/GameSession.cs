namespace Aspid.Core.HSM.Sample
{
    /// <summary>
    /// Shared mutable context injected into states, controllers and transitions by the factory.
    /// Composed of per-feature slices that live next to their feature (see Combat/CombatData.cs etc.).
    /// Data that only one state needs (loading progress, dialogue lines, credits timer) is owned
    /// by that state instead and never appears here.
    /// </summary>
    public class GameSession
    {
        // Run-scoped slices — recreated by ResetRun, so defaults live in the field initializers
        public PlayerData Player { get; private set; } = new();
        public ExplorationData Exploration { get; private set; } = new();
        public CombatData Combat { get; private set; } = new();
        public LobbyData Lobby { get; private set; } = new();
        public MatchData Match { get; private set; } = new();
        public string GameOverReason = "";

        // Persistent across runs
        public SettingsData Settings { get; } = new();

        public void ResetRun()
        {
            Player = new PlayerData();
            Exploration = new ExplorationData();
            Combat = new CombatData();
            Lobby = new LobbyData();
            Match = new MatchData();
            GameOverReason = "";
        }
    }
}
