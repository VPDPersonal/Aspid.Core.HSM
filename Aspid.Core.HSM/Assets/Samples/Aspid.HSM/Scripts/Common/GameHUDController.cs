using UnityEngine;

namespace Aspid.Core.HSM.Sample
{
    /// <summary>Top bar with health, score and a pause button, shared by gameplay groups.</summary>
    public class GameHUDController : IEnterController, IExitController, IGuiController
    {
        private readonly GameSession _session;
        private readonly IGameActions _actions;

        public GameHUDController(GameSession session, IGameActions actions)
        {
            _session = session;
            _actions = actions;
        }

        public void OnEnter()
        {
            Debug.Log("[HSM] GameHUD: HUD shown");
        }

        public void OnGui()
        {
            GameStyles.DrawRect(new Rect(0, 0, Screen.width, 52), new Color(0f, 0f, 0f, 0.45f));

            var hp = Mathf.Max(0, _session.Player.Hp);
            GameStyles.DrawBar(new Rect(14, 13, 220, 26), (float)hp / PlayerData.MaxHp,
                GameStyles.HpGreen, $"HP {hp}/{PlayerData.MaxHp}");

            GUI.Label(new Rect(250, 13, 200, 26), $"Score: {_session.Player.Score}", GameStyles.Body);

            if (GUI.Button(new Rect(Screen.width - 60, 8, 48, 36), "II", GameStyles.Button))
                _actions.PauseGame();
        }

        public void OnExit()
        {
            Debug.Log("[HSM] GameHUD: HUD hidden");
        }
    }
}
