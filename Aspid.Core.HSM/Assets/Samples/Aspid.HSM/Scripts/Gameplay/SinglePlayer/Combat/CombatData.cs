namespace Aspid.Core.HSM.Sample
{
    public enum CombatResult
    {
        None,
        Victory,
        Defeat
    }

    /// <summary>
    /// Battle data. Lives in the session (not the Transient CombatState) so a fight
    /// survives a pause round-trip, and so CombatToExplorationTransition can guard on Result.
    /// </summary>
    public class CombatData
    {
        public int EnemyHp;
        public int EnemyMaxHp;
        public CombatResult Result;
        public string Log = "";

        // UI intent (attack button) consumed by PlayerCombatController
        public bool AttackRequested;

        // Visual feedback decayed by HitFlashController in LateUpdate
        public float EnemyHitFlash;
        public float PlayerHitFlash;
    }
}
