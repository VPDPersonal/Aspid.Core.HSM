using UnityEngine;

namespace Aspid.Core.HSM.Sample
{
    public class LoadingScreen : IGuiController
    {
        private readonly AssetLoadingController _loading;

        public LoadingScreen(AssetLoadingController loading) => _loading = loading;

        public void OnGui()
        {
            GameStyles.DrawBackground(GameStyles.Night);

            GUI.Label(GameStyles.Centered(600, 60, Screen.height * 0.35f), "ASPID QUEST", GameStyles.Title);

            var barRect = GameStyles.Centered(360, 26, Screen.height * 0.55f);
            GameStyles.DrawBar(barRect, _loading.Progress, GameStyles.Accent);

            GUI.Label(GameStyles.Centered(360, 24, barRect.yMax + 8),
                $"Loading... {_loading.Progress:P0}", GameStyles.Hint);
        }
    }
}
