namespace Aspid.Core.HSM.Sample
{
    /// <summary>Settings persist across runs — GameSession.ResetRun leaves this slice alone.</summary>
    public class SettingsData
    {
        public float MusicVolume = 0.8f;
        public float SfxVolume = 0.6f;
    }
}
