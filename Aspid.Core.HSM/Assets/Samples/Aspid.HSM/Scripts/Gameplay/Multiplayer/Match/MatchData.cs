namespace Aspid.Core.HSM.Sample
{
    /// <summary>Live match data written by MatchTimerController and drawn by MatchScreen.</summary>
    public class MatchData
    {
        public float Duration;
        public float TimeLeft;
        public int Goals;
        public bool Finished;

        // UI intent (shoot button) consumed by MatchTimerController
        public bool GoalRequested;
    }
}
