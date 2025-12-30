namespace Aspid.Core.HSM.Generators.ControllerGroup.Data.Interfaces;

public readonly struct AsyncMethodData(bool isWait)
{
    public readonly bool IsWait = isWait;
}
