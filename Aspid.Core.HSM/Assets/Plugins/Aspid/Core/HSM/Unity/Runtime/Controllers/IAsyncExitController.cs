using System.Threading;
using Cysharp.Threading.Tasks;

// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM
{
    [AsyncOf(typeof(IExitController))]
    public interface IAsyncExitController : IController
    {
        [ReverseExecute]
        public UniTask OnExitAsync(CancellationToken cancellationToken);
    }
}
