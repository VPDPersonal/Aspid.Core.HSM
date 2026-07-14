using UnityEngine;

namespace Aspid.Core.HSM.Sample
{
    /// <summary>
    /// Exploration world data. Lives in the session (not the state) so the player returns
    /// to the same spot after a combat or dialogue detour recreates the Transient leaf states.
    /// Positions are normalized (0..1) inside the field.
    /// </summary>
    public class ExplorationData
    {
        public Vector2 PlayerPos = new(0.2f, 0.5f);
        public Vector2 NpcPos = new(0.75f, 0.35f);
        public bool NearNpc;
        public float DistanceWalked;

        // Written by PlayerMovementController each frame, read by WildEncounterController
        // later the same frame (controllers dispatch in registration order)
        public bool IsMoving;

        // Intent flags raised during Update; SampleGameManager polls them after dispatch
        public bool EncounterTriggered;
        public bool DialogueRequested;
    }
}
