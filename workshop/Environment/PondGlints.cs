// Procedural Pixel Creature Workshop - animated water glints of one pond. The map stores them as short
// sparkle runs per frame (PondSpec.GlintFrames, in ground texels); this node draws the current frame as
// rectangles. Unlike pre-rendered frame textures it costs no image memory, which matters for HD maps.

using System;
using Godot;

namespace PixelCreatures.Workshop
{
    public partial class PondGlints : Node2D
    {
        private GlintRun[][] _frames = Array.Empty<GlintRun[]>();
        private float _texel = 1;
        private int _frame = -1;

        /// <summary>Shows the glints of a pond; <paramref name="resolution"/> is the map's texels per map unit.</summary>
        public void SetGlints(GlintRun[][] frames, int resolution)
        {
            _frames = frames;
            _texel = 1f / Math.Max(1, resolution);
            QueueRedraw();
        }

        /// <summary>Frame of the loop to show (0..3).</summary>
        public int Frame
        {
            get => _frame;
            set
            {
                if (value == _frame) return;
                _frame = value;
                QueueRedraw();
            }
        }

        public override void _Draw()
        {
            if (_frame < 0 || _frame >= _frames.Length) return;
            float k = _texel;
            foreach (var g in _frames[_frame])
                DrawRect(new Rect2(g.X * k, g.Y * k, g.Length * k, k), g.Bright ? EnvironmentArt.Foam : EnvironmentArt.Water2);
        }
    }
}
