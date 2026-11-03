// Procedural Pixel Creations for Unity - batched rasterization of all actors.
//
// Actors that need a new frame enqueue themselves during Update. In LateUpdate the queue rasterizes the
// pending frames, in parallel on worker threads with one renderer per thread where threads exist, and
// then uploads the textures on the main thread. Rasterization only reads immutable models and a pose
// snapshot taken on the main thread, so the result is identical to serial rendering.

#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PixelCreatures.Core.Rendering;
using UnityEngine;

namespace PixelCreatures.Unity
{
    public static class PixelCreatureRenderQueue
    {
        private static readonly List<PixelCreatureActor> Pending = new List<PixelCreatureActor>();
        private static readonly List<PixelCreatureActor> Batch = new List<PixelCreatureActor>();
        private static ThreadLocal<CreatureRenderer> _renderers = new ThreadLocal<CreatureRenderer>(() => new CreatureRenderer());
        private static PixelCreatureRenderDriver? _driver;

        /// <summary>Rasterize on worker threads (ignored where threads are unavailable, e.g. WebGL).</summary>
        public static bool Parallel { get; set; } = true;

        /// <summary>Frames rasterized during the last flush (diagnostics).</summary>
        public static int LastBatchSize { get; private set; }

        /// <summary>Milliseconds of the last flush including uploads (diagnostics).</summary>
        public static double LastFlushMs { get; private set; }

        // "Enter Play Mode" without a domain reload keeps statics: start clean every session
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Pending.Clear();
            Batch.Clear();
            _driver = null;
            _renderers = new ThreadLocal<CreatureRenderer>(() => new CreatureRenderer());
            Parallel = true;
            LastBatchSize = 0;
            LastFlushMs = 0;
        }

        /// <summary>A renderer for the calling thread (renderers are not thread safe; one per thread).</summary>
        public static CreatureRenderer RendererForThisThread => _renderers.Value!;

        internal static void Enqueue(PixelCreatureActor actor)
        {
            if (!Pending.Contains(actor)) Pending.Add(actor);
            EnsureDriver();
        }

        internal static void Remove(PixelCreatureActor actor) => Pending.Remove(actor);

        private static void EnsureDriver()
        {
            if (_driver != null || !Application.isPlaying) return;
            // a hidden scene object (not DontSave): it lives in the DontDestroyOnLoad scene and ends with play mode
            var go = new GameObject("PixelCreatureRenderQueue") { hideFlags = HideFlags.HideInHierarchy | HideFlags.HideInInspector | HideFlags.NotEditable };
            UnityEngine.Object.DontDestroyOnLoad(go);
            _driver = go.AddComponent<PixelCreatureRenderDriver>();
        }

        /// <summary>Rasterizes and uploads every pending frame now (normally called automatically in LateUpdate).</summary>
        public static void Flush()
        {
            if (Pending.Count == 0) { LastBatchSize = 0; return; }
            var sw = System.Diagnostics.Stopwatch.StartNew();
            Batch.Clear();
            // take the pending list first: one failing actor must never block the queue for every frame after it
            var pending = Pending.ToArray();
            Pending.Clear();
            foreach (var a in pending)
            {
                if (a == null || !a.isActiveAndEnabled) continue;
                try { if (a.PrepareRaster()) Batch.Add(a); }
                catch (Exception e) { a.ReportRenderError(e.Message); }
            }
            LastBatchSize = Batch.Count;
            try
            {
                // Rasterize catches its own exceptions (reported in Upload on the main thread)
                if (Batch.Count > 1 && Parallel && PixelCreatureRuntime.ThreadsSupported)
                    System.Threading.Tasks.Parallel.For(0, Batch.Count, i => Batch[i].Rasterize(_renderers.Value!));
                else
                {
                    var r = _renderers.Value!;
                    for (int i = 0; i < Batch.Count; i++) Batch[i].Rasterize(r);
                }
                for (int i = 0; i < Batch.Count; i++)
                {
                    var a = Batch[i];
                    if (a == null) continue;
                    try { a.Upload(); }
                    catch (Exception e) { a.ReportRenderError(e.Message); }
                }
            }
            finally
            {
                Batch.Clear();
                LastFlushMs = sw.Elapsed.TotalMilliseconds;
            }
        }

        internal static void DriverDestroyed(PixelCreatureRenderDriver driver)
        {
            if (_driver == driver) _driver = null;
        }
    }
}
