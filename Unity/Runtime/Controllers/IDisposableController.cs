// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM
{
    public interface IDisposableController : IController
    { 
        [ReverseExecute]
        public void Dispose();
    }
}
