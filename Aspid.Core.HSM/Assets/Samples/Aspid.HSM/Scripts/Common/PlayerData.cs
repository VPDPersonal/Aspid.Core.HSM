namespace Aspid.Core.HSM.Sample
{
    /// <summary>Cross-feature player stats shown in the HUD and judged at game over.</summary>
    public class PlayerData
    {
        public const int MaxHp = 100;

        public int Hp = MaxHp;
        public int Score;
    }
}
