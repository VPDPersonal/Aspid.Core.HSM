using UnityEngine;
using UnityEngine.InputSystem;

namespace Aspid.Core.HSM.Sample
{
    public class MatchTimerController : IUpdateController, IEnterController
    {
        private const float MatchDuration = 30f;

        private readonly GameSession _session;

        public MatchTimerController(GameSession session) => _session = session;

        public void OnEnter()
        {
            var match = _session.Match;
            match.Duration = MatchDuration;
            match.TimeLeft = MatchDuration;
            match.Goals = 0;
            match.Finished = false;
            match.GoalRequested = false;
            Debug.Log($"[HSM] Match: started, {MatchDuration:F0}s on the clock (Space = score)");
        }

        public void Update(float deltaTime)
        {
            var match = _session.Match;
            if (match.Finished) return;

            match.TimeLeft -= deltaTime;

            var keyboard = Keyboard.current;
            var shoot = match.GoalRequested ||
                        (keyboard != null && keyboard.spaceKey.wasPressedThisFrame);
            match.GoalRequested = false;

            if (shoot)
            {
                match.Goals++;
                _session.Player.Score += 10;
                Debug.Log($"[HSM] Match: GOAL! Match score = {match.Goals}");
            }

            if (match.TimeLeft <= 0f)
            {
                match.TimeLeft = 0f;
                match.Finished = true;
                _session.GameOverReason = $"Match finished — {match.Goals} goals scored";
                Debug.Log($"[HSM] Match: time is up, final score {match.Goals}");
            }
        }
    }
}
