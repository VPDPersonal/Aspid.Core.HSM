using System;

// ReSharper disable once CheckNamespace
namespace Unity.Profiling
{
    /// <summary>
    /// The part of Unity's <c>ProfilerMarker</c> that the runtime uses, written in C# 9 so that the
    /// <c>ENABLE_PROFILER</c> branch compiles here as it does in the Editor.
    /// </summary>
    public readonly struct ProfilerMarker
    {
        public ProfilerMarker(string name) { }

        public AutoScope Auto() => default;

        public readonly struct AutoScope : IDisposable
        {
            public void Dispose() { }
        }
    }
}
