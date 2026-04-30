using System.Threading;
using Cysharp.Threading.Tasks;

// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM
{
    [AsyncOf(typeof(IEnterController))]
    public interface IAsyncEnterController : IController
    {
        public UniTask OnEnterAsync(CancellationToken cancellationToken);
    }
}
