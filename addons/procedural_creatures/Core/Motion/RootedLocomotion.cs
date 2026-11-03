// Procedural Pixel Creatures - "rooted" motion module for stationary creations (trees, plants, props).
//
// The object never travels. Wind moves it: the trunk (spine chain) leans with a slow, gusty sway and
// every branch, frond, banner or flame (appendage chains with the Tentacle role) carries a travelling
// sway wave on top of its spring dynamics. Strength and tempo come from the rig tuning:
//   wind.trunk   lean amplitude of the trunk in radians (default 0.03)
//   wind.branch  sway amplitude of the appendage chains (TentacleAmp, default 0.25)
//   wind.hz      gust frequency in Hz (default 0.35)
// Export mode snaps all frequencies to the loop length so idle animations loop seamlessly.

using System;
using PixelCreatures.Core.Anatomy;

namespace PixelCreatures.Core.Motion
{
    public sealed class RootedLocomotion : LocomotionModuleBase
    {
        public const string ModuleId = "rooted";
        public override string Id => ModuleId;

        private double _phase0, _phase1;

        public override void Initialize(MotorContext ctx)
        {
            _phase0 = ctx.Hash(71, 0) * DMath.TwoPi;
            _phase1 = ctx.Hash(72, 0) * DMath.TwoPi;
        }

        public override void Evaluate(MotorContext ctx, ref PoseParams p)
        {
            var rig = ctx.Rig;
            double trunk = rig.Tune("wind.trunk", 0.03);
            double branch = rig.Tune("wind.branch", 0.25);
            double hz = rig.Tune("wind.hz", 0.35);
            double t = ctx.Time;
            double h0 = hz, h1 = hz * 1.7, hb = hz * 1.3;
            if (ctx.ExportMode)
            {
                double loop = Math.Max(0.5, ctx.ExportLoop);
                h0 = Math.Max(1, Math.Round(h0 * loop)) / loop;
                h1 = Math.Max(1, Math.Round(h1 * loop)) / loop;
                hb = Math.Max(1, Math.Round(hb * loop)) / loop;
            }
            double ph0 = ctx.ExportMode ? 0 : _phase0, ph1 = ctx.ExportMode ? 1.3 : _phase1;
            // gusts: a slow lean plus a faster flutter, mostly along one wind direction
            double gust = DMath.Sin(DMath.TwoPi * h0 * t + ph0) * 0.75 + DMath.Sin(DMath.TwoPi * h1 * t + ph1) * 0.25;
            p.SpinePitch += gust * trunk;
            p.SpineYaw += DMath.Sin(DMath.TwoPi * h0 * t + ph0 + 1.7) * trunk * 0.45;
            p.BodyRoll += gust * trunk * 0.15;
            p.TentaclePhase = DMath.TwoPi * hb * t + ph1;
            p.TentacleAmp = branch * (0.75 + 0.25 * gust);
            // a living object (treant, mimic face) still blinks; nothing breathes
            p.Breath = 0;
        }
    }
}
