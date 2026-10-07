using System;

// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM
{
    /// <summary>
    /// When applied to a controller group method, the source generator emits code that invokes
    /// the inner controllers in reverse declaration order (leaf-to-root).
    /// Used on exit/dispose paths to unwind in the opposite order of initialization.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class ReverseExecuteAttribute : Attribute { }
}
