// Procedural Pixel Creatures - render-time environment look: snow caps, sand, moss and ash on upward facing
// surfaces, mud and dust near the ground, wet glints and a colour grade.
//
// The overlay is part of the ViewSpec, not of the genome: the same creation can stand in fresh snow or
// walk out of a swamp. Rendering with an overlay needs the palette from CreatureModel.PaletteFor(overlay)
// (it contains the cover ramps and the graded colours); an inactive overlay leaves the output untouched.

#nullable enable

using System;
using System.Globalization;
using PixelCreatures.Core.Palettes;

namespace PixelCreatures.Core
{
    /// <summary>
    /// A material the environment puts on creations: a five step ramp (deep, shadow, base, light, highlight).
    /// The outline stays the creation's own outline colour.
    /// </summary>
    public sealed class OverlayMaterial
    {
        public string Id { get; }
        public string Label { get; }
        public Rgba Deep { get; }
        public Rgba Shadow { get; }
        public Rgba Base { get; }
        public Rgba Light { get; }
        public Rgba Highlight { get; }
        /// <summary>Sparkles: the brightest facets get the highlight (snow, ice).</summary>
        public bool Sparkle { get; }
        public ulong Key { get; }

        public OverlayMaterial(string id, string label, Rgba deep, Rgba shadow, Rgba baseColor, Rgba light, Rgba highlight, bool sparkle = false)
        {
            Id = id; Label = label; Deep = deep; Shadow = shadow; Base = baseColor; Light = light; Highlight = highlight; Sparkle = sparkle;
            ulong k = StableHash.Fnv1a64(id);
            foreach (var c in new[] { deep, shadow, baseColor, light, highlight }) k = StableHash.Combine(k, c.Packed);
            Key = StableHash.Combine(k, sparkle ? 1UL : 0UL);
        }

        public Rgba Shade(int level) => level switch
        {
            <= 1 => Deep,
            2 => Shadow,
            3 => Base,
            4 => Light,
            _ => Highlight,
        };

        public static readonly OverlayMaterial Snow = new OverlayMaterial("snow", "Snow",
            new Rgba(116, 136, 182), new Rgba(164, 184, 222), new Rgba(212, 225, 243), new Rgba(237, 243, 252), new Rgba(255, 255, 255), sparkle: true);
        public static readonly OverlayMaterial Sand = new OverlayMaterial("sand", "Sand",
            new Rgba(146, 108, 64), new Rgba(184, 146, 92), new Rgba(212, 180, 122), new Rgba(230, 204, 150), new Rgba(244, 226, 182));
        /// <summary>Pale dust on legs and feet.</summary>
        public static readonly OverlayMaterial Dust = new OverlayMaterial("dust", "Dust",
            new Rgba(128, 102, 74), new Rgba(160, 134, 100), new Rgba(190, 164, 126), new Rgba(210, 188, 150), new Rgba(226, 208, 172));
        public static readonly OverlayMaterial Moss = new OverlayMaterial("moss", "Moss",
            new Rgba(36, 66, 32), new Rgba(54, 94, 40), new Rgba(78, 124, 50), new Rgba(108, 152, 62), new Rgba(142, 180, 82));
        /// <summary>Dark algae and duckweed (swamps).</summary>
        public static readonly OverlayMaterial Algae = new OverlayMaterial("algae", "Algae",
            new Rgba(34, 52, 30), new Rgba(48, 72, 38), new Rgba(68, 96, 46), new Rgba(90, 118, 56), new Rgba(116, 140, 70));
        public static readonly OverlayMaterial Mud = new OverlayMaterial("mud", "Mud",
            new Rgba(46, 34, 26), new Rgba(66, 50, 36), new Rgba(88, 68, 48), new Rgba(110, 88, 62), new Rgba(132, 108, 78));
        public static readonly OverlayMaterial Ash = new OverlayMaterial("ash", "Ash",
            new Rgba(64, 60, 64), new Rgba(94, 90, 94), new Rgba(128, 124, 126), new Rgba(158, 154, 154), new Rgba(188, 184, 182));
        public static readonly OverlayMaterial Soot = new OverlayMaterial("soot", "Soot",
            new Rgba(28, 24, 26), new Rgba(42, 36, 38), new Rgba(58, 50, 50), new Rgba(76, 66, 64), new Rgba(96, 84, 80));

        public static readonly OverlayMaterial[] All = { Snow, Sand, Dust, Moss, Algae, Mud, Ash, Soot };

        public static OverlayMaterial? ById(string? id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var m in All) if (m.Id == id) return m;
            return null;
        }

        public override string ToString() => Id;
    }

    /// <summary>Environment look applied while rendering (see ViewSpec.Overlay). Default = no overlay.</summary>
    public struct BiomeOverlay
    {
        /// <summary>Material on upward facing surfaces (snow, sand, moss, ash).</summary>
        public OverlayMaterial? Cover;
        /// <summary>0..1: 0.2 dusts flat tops, 0.6 caps backs and heads, 1 covers everything facing up.</summary>
        public double CoverAmount;
        /// <summary>0..1: irregular, patchy edges of the cover.</summary>
        public double CoverPatchiness;
        /// <summary>Material near the ground (mud, dust, soot, snow on the legs).</summary>
        public OverlayMaterial? Grime;
        /// <summary>0..1: how far up the body the grime reaches (1 = about 40 % of the body height).</summary>
        public double GrimeAmount;
        /// <summary>0..1: extra specular glints (rain forest, swamp).</summary>
        public double Wetness;
        /// <summary>Colour grade: all colours move towards Grade by GradeAmount.</summary>
        public Rgba Grade;
        public double GradeAmount;

        public static BiomeOverlay None => default;

        public bool HasCover => Cover != null && CoverAmount > 1e-6;
        public bool HasGrime => Grime != null && GrimeAmount > 1e-6;
        public bool IsActive => HasCover || HasGrime || Wetness > 1e-6 || GradeAmount > 1e-6;
        /// <summary>Overlays that change palette colours (cover and grime ramps, grade) need CreatureModel.PaletteFor.</summary>
        public bool NeedsPalette => HasCover || HasGrime || GradeAmount > 1e-6;

        /// <summary>The same look at a different strength (0 = none, 1 = as defined, up to 2 = heavy).</summary>
        public BiomeOverlay Scaled(double intensity)
        {
            double k = DMath.Clamp(intensity, 0, 2);
            var o = this;
            o.CoverAmount = DMath.Clamp(CoverAmount * k, 0, 1);
            o.GrimeAmount = DMath.Clamp(GrimeAmount * k, 0, 1);
            o.Wetness = DMath.Clamp(Wetness * k, 0, 1);
            o.GradeAmount = DMath.Clamp(GradeAmount * k, 0, 0.5);
            return o;
        }

        /// <summary>Content key (for palette caches and render hashes); 0 for an inactive overlay.</summary>
        public ulong Key
        {
            get
            {
                if (!IsActive) return 0;
                ulong k = 0x9E3779B97F4A7C15UL;
                k = StableHash.Combine(k, HasCover ? Cover!.Key : 0);
                k = StableHash.Combine(k, Q(HasCover ? CoverAmount : 0));
                k = StableHash.Combine(k, Q(HasCover ? CoverPatchiness : 0));
                k = StableHash.Combine(k, HasGrime ? Grime!.Key : 0);
                k = StableHash.Combine(k, Q(HasGrime ? GrimeAmount : 0));
                k = StableHash.Combine(k, Q(Wetness));
                k = StableHash.Combine(k, GradeAmount > 1e-6 ? Grade.Packed : 0);
                k = StableHash.Combine(k, Q(GradeAmount));
                return k;
            }
        }

        private static ulong Q(double v) => (ulong)Math.Round(DMath.Clamp(v, 0, 4) * 100000.0);

        public override string ToString() =>
            !IsActive ? "none" : string.Format(CultureInfo.InvariantCulture, "cover {0} {1:0.##}, grime {2} {3:0.##}, wet {4:0.##}, grade {5:0.##}",
                HasCover ? Cover!.Id : "-", CoverAmount, HasGrime ? Grime!.Id : "-", GrimeAmount, Wetness, GradeAmount);
    }
}
