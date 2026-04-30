using System.Threading;
using Aspid.Core.HSM;
using Cysharp.Threading.Tasks;

namespace _Scripts.Controllers
{
    public class TestAsyncController : IAsyncEnterController
    {
        public UniTask OnEnterAsync(CancellationToken cancellationToken)
        {
            throw new System.NotImplementedException();
        }
    }
}