// Adapted from DohunWi/3D-Action PerformanceLogger.cs (MIT), pinned in ThirdParty/SOURCES.md.
// Keeps frame samples, nearest-rank p95 and ProfilerRecorder; no singleton or global frame-rate changes.
using System;
using System.Collections.Generic;
using System.Globalization;
using Unity.Profiling;
namespace Milkfrog.CombatDemo
{
    public sealed class CombatPerformanceSampler : IDisposable
    {
        readonly List<float> frames = new List<float>(65536);
        ProfilerRecorder gc;
        double sum, gcSum;
        public bool GcAvailable => gc.Valid;
        public CombatPerformanceSampler() { gc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame"); }
        public void Clear() { frames.Clear(); sum = gcSum = 0; }
        public void Sample(float dt)
        {
            float ms = dt * 1000; frames.Add(ms); sum += ms;
            if (gc.Valid) gcSum += gc.LastValue;
        }
        public string Csv(string label, int repetition)
        {
            frames.Sort(); int count = frames.Count;
            if (count == 0) throw new InvalidOperationException("No performance samples.");
            int rank = Math.Min(count - 1, (int)Math.Ceiling(.95 * count) - 1);
            return string.Format(CultureInfo.InvariantCulture, "{0},{1},{2},{3:F4},{4:F4},{5:F2},{6}", label, repetition, count, sum/count, frames[rank], gcSum/count, gc.Valid);
        }
        public void Dispose() { if (gc.Valid) gc.Dispose(); }
    }
}
