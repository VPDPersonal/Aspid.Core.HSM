using System;

// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class ParentStateAttribute : Attribute { }
}
