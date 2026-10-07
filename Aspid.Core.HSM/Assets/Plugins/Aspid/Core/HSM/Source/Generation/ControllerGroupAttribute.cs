using System;

// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM
{
    /// <summary>
    /// Marks a partial class as a controller group. The source generator emits aggregation
    /// plumbing that dispatches to all inner <see cref="IController"/> implementations,
    /// honoring <see cref="ReverseExecuteAttribute"/> and <see cref="AsyncAttribute"/> on group methods.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class ControllerGroupAttribute : Attribute { }
}
