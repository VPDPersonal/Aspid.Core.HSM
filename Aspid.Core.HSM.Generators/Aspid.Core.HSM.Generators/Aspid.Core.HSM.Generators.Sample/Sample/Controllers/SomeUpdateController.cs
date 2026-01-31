namespace Aspid.Core.HSM.Generators.Sample.Sample;

public class SomeUpdateController : IEnterController, IUpdateController
{
    public void OnEnter()
    {
        // Some code.
    }
    
    public void Update(float deltaTime)
    {
        // Some code.
    }
}
