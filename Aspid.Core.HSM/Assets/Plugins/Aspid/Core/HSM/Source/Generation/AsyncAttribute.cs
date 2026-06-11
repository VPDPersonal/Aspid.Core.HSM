using System;

// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM
{
    /// <summary>
    /// Marks a controller group method for asynchronous code generation.
    /// The source generator emits a <c>UniTask</c>-returning variant for the decorated method.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class AsyncAttribute : Attribute
    {
        /// <summary>
        /// When <c>true</c> (default), the generated caller awaits the async method before proceeding.
        /// When <c>false</c>, the task is fire-and-forget.
        /// </summary>
        public bool IsWait { get; private set; } = true;
    }
}
