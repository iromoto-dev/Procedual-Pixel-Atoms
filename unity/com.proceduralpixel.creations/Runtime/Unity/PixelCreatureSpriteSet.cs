// Procedural Pixel Creations for Unity - baked sprites of one creation (created by the editor baker).
//
// The asset holds one Sprite sub-asset per frame plus the clip structure (state, direction, timing), so baked
// creations can also be animated from code without an Animator: PixelCreatureSpriteSet.Find("walk", 7).

#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace PixelCreatures.Unity
{
    public sealed class PixelCreatureSpriteSet : ScriptableObject
    {
        [Serializable]
        public sealed class Clip
        {
            /// <summary>State name: idle, walk, run, action, hit, rest_enter, sleep, rest_exit.</summary>
            public string state = string.Empty;
            /// <summary>Facing direction 0..7 (0 = east, 2 = north, 6 = south).</summary>
            public int direction;
            public bool loop;
            public float fps = 12f;
            public Sprite[] frames = Array.Empty<Sprite>();
            /// <summary>Ground speed of the clip in ground pixels per second (root motion).</summary>
            public float groundSpeed;

            public float Duration => fps > 0 ? frames.Length / fps : 0f;

            /// <summary>The frame shown at 'time' seconds into the clip (wraps for loops, holds the last frame otherwise).</summary>
            public Sprite? FrameAt(float time)
            {
                if (frames.Length == 0) return null;
                int i = Mathf.FloorToInt(Mathf.Max(0f, time) * fps);
                i = loop ? i % frames.Length : Mathf.Min(i, frames.Length - 1);
                return frames[i];
            }
        }

        public string creationName = string.Empty;
        public string family = string.Empty;
        public string biome = string.Empty;
        /// <summary>Unity units: sprites use pixelsPerUnit * pixelDensity.</summary>
        public float pixelsPerUnit = 32f;
        public float pixelDensity = 2f;
        public List<Clip> clips = new List<Clip>();

        /// <summary>The clip of a state in a direction; falls back to the nearest baked direction.</summary>
        public Clip? Find(string state, int direction)
        {
            Clip? best = null;
            int bestDist = int.MaxValue;
            foreach (var c in clips)
            {
                if (!string.Equals(c.state, state, StringComparison.OrdinalIgnoreCase)) continue;
                int diff = ((c.direction - direction) % 8 + 8) % 8;
                int d = Math.Min(diff, 8 - diff);
                if (d < bestDist) { bestDist = d; best = c; }
            }
            return best;
        }
    }
}
