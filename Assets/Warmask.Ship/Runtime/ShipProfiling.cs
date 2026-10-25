using Unity.Profiling;
using UnityEngine;

namespace Warmask.Ship
{
    /// <summary>
    /// Static profiling toggles to isolate performance bottlenecks.
    /// Set these before running the FpsMeasurementTest.
    /// </summary>
    public static class ShipProfiling
    {
        public static bool EnableBoidSteering = true;
        public static bool EnableMovement = true;
        public static bool EnableTrails = true;
        public static bool EnableLasers = true;
        public static bool EnableAudio = true;
        public static bool EnableRendering = true;
        public static bool EnableHitFlash = true;
        public static bool EnableTargeting = true;
        public static bool EnableFiring = true;
        public static bool EnableDamage = true;
        public static bool EnableOffMaskCoarseTick = true;
        public static bool LogPhaseTimings = false;

        /// <summary>
        /// Manager-phase profiler markers, read by the editor Profiler's Scripts
        /// area and by <c>ProfilerRecorder</c>. To remove profiling entirely,
        /// delete this class and the six <c>using</c> scopes in
        /// <see cref="ShipManagerInstance.LateUpdate"/>.
        /// </summary>
        internal static class Phases
        {
            internal static readonly ProfilerMarker Snapshot = new ProfilerMarker("Ship.Snapshot");
            internal static readonly ProfilerMarker SpatialHash = new ProfilerMarker("Ship.SpatialHash");
            internal static readonly ProfilerMarker Neighbors = new ProfilerMarker("Ship.Neighbors");
            internal static readonly ProfilerMarker BoidJob = new ProfilerMarker("Ship.BoidJob");
            internal static readonly ProfilerMarker Tick = new ProfilerMarker("Ship.Tick");
            internal static readonly ProfilerMarker Deaths = new ProfilerMarker("Ship.Deaths");
        }
    }
}