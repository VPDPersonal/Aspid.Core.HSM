using UnityEngine;

namespace Aspid.Core.HSM.Sample
{
    public class CombatScreen : IGuiController
    {
        private readonly GameSession _session;
        private readonly IGameActions _actions;

        public CombatScreen(GameSession session, IGameActions actions)
        {
            _session = session;
            _actions = actions;
        }

        public void OnGui()
        {
            var combat = _session.Combat;

            GameStyles.DrawBackground(GameStyles.BloodRed);

            // Enemy — flashes white when hit
            var enemySize = 120f;
            var enemyRect = GameStyles.Centered(enemySize, enemySize, Screen.height * 0.22f);
            var enemyColor = Color.Lerp(new Color(0.55f, 0.16f, 0.45f), Color.white, combat.EnemyHitFlash);
            GameStyles.DrawRect(enemyRect, enemyColor);

            // Angry eyes
            var eyeSize = new Vector2(22, 12);
            GameStyles.DrawRect(new Rect(enemyRect.x + 24, enemyRect.y + 38, eyeSize.x, eyeSize.y), GameStyles.Night);
            GameStyles.DrawRect(new Rect(enemyRect.xMax - 24 - eyeSize.x, enemyRect.y + 38, eyeSize.x, eyeSize.y), GameStyles.Night);
            GameStyles.DrawRect(new Rect(enemyRect.x + 34, enemyRect.yMax - 34, enemyRect.width - 68, 8), GameStyles.Night);

            var enemyFill = combat.EnemyMaxHp > 0 ? (float)Mathf.Max(0, combat.EnemyHp) / combat.EnemyMaxHp : 0f;
            GameStyles.DrawBar(GameStyles.Centered(260, 24, enemyRect.y - 40), enemyFill,
                GameStyles.HpRed, $"Monster  {Mathf.Max(0, combat.EnemyHp)}/{combat.EnemyMaxHp}");

            // Screen flashes red when the player is hit
            if (combat.PlayerHitFlash > 0f)
                GameStyles.DrawBackground(new Color(0.9f, 0.1f, 0.1f, combat.PlayerHitFlash * 0.35f));

            // Combat log line
            GUI.Label(GameStyles.Centered(500, 30, enemyRect.yMax + 26), combat.Log, GameStyles.Body);

            // Actions
            var attackRect = GameStyles.Centered(220, 54, Screen.height * 0.68f);
            if (GUI.Button(attackRect, "ATTACK  (Space)", GameStyles.Button))
                combat.AttackRequested = true;

            var slowLabel = _actions.IsSlowMotionActive ? "Slow-mo: ON" : "Slow-mo: OFF";
            if (GUI.Button(GameStyles.Centered(220, 40, attackRect.yMax + 14), slowLabel, GameStyles.Button))
                _actions.ToggleSlowMotion();
        }
    }
}
