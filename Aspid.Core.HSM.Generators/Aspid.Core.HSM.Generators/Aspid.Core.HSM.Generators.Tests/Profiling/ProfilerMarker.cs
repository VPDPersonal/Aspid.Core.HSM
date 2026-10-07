using System;
using System.Collections.Generic;

// ReSharper disable once CheckNamespace
namespace Unity.Profiling;

/// <summary>
/// Test stand-in for Unity's <c>ProfilerMarker</c>. The project defines <c>ENABLE_PROFILER</c>, so the
/// machine's profiling branch is compiled and run here; every sample is recorded as "begin"/"end" lines.
/// </summary>
public readonly struct ProfilerMarker(string name)
{
    [ThreadStatic] private static List<string>? _samples;

    public static List<string> Samples => _samples ??= [];

    public string Name { get; } = name;

    public AutoScope Auto()
    {
        Samples.Add("begin " + Name);
        return new AutoScope(Name);
    }

    public readonly struct AutoScope(string name) : IDisposable
    {
        public void Dispose() => Samples.Add("end " + name);
    }
}
