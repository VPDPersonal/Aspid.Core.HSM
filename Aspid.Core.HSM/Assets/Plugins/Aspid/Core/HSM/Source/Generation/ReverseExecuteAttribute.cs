using System;

// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM
{
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class ReverseExecuteAttribute  : Attribute { }
}
