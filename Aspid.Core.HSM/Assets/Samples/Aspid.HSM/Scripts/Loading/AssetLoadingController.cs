using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Aspid.Core.HSM.Sample
{
    // An async member makes the whole group's enter phase async: the generated group
    // implements IAsyncEnterController and awaits this controller during ChangeStateAsync.
    public class AssetLoadingController : IAsyncEnterController
    {
        private const int Steps = 10;

        public float Progress { get; private set; }

        public async UniTask OnEnterAsync(CancellationToken cancellationToken)
        {
            Progress = 0f;
            Debug.Log("[HSM] AssetLoading: loading assets...");

            for (var i = 1; i <= Steps; i++)
            {
                await UniTask.Delay(150, cancellationToken: cancellationToken);
                Progress = (float)i / Steps;
            }

            Debug.Log("[HSM] AssetLoading: assets loaded");
        }
    }
}
