using System;
using System.Collections.Generic;

// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM;

public class StateTree
{
    public IEnumerable<IState> GetState<T>()
        where T : IState
    {
        throw new NotImplementedException();
    }
}
