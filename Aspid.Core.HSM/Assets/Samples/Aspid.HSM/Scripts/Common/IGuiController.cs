
namespace Aspid.Core.HSM.Sample
{
    /// <summary>
    /// Sample-level controller: a state (or extension) that owns a screen draws it here.
    /// SampleGameManager.OnGUI dispatches through the active chain via GetController,
    /// exactly like the framework dispatches IUpdateController.
    /// </summary>
    public interface IGuiController : IController
    {
        void OnGui();
    }
}
