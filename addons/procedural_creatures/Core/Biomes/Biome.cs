// Procedural Pixel Creatures - biomes: environment variants of every family.
//
// A biome works on two layers:
//   1. Sampling: it re-weights archetypes (cacti and palms in the desert, conifers and yetis in the snow),
//      coat colours, patterns and glow, and nudges genes (big ears in the desert, bulky bodies in the cold,
//      dense jungle crowns). The result is an ordinary genome, so presets, editing and breeding are unchanged.
//   2. Rendering: its BiomeOverlay dresses any rendered creation with snow, sand, moss or ash on upward
//      facing surfaces, mud or dust near the ground, wet glints and a colour grade (see BiomeOverlay).
// The temperate biome has no rules: sampling and rendering are bit-identical to not using a biome.

#nullable enable

using System;
using System.Collections.Generic;
using PixelCreatures.Core.Genetics;
using PixelCreatures.Core.Palettes;

namespace PixelCreatures.Core
{
    /// <summary>Which families a biome rule applies to: categories, optionally one family and one archetype.</summary>
    public readonly struct BiomeScope
    {
        private readonly int _categories;
        public readonly string? FamilyId;
        public readonly string? ArchetypeId;

        private BiomeScope(int categories, string? family, string? archetype)
        {
            _categories = categories; FamilyId = family; ArchetypeId = archetype;
        }

        private static int Bit(CreationCategory c) => 1 << (int)c;

        public static BiomeScope Categories(params CreationCategory[] categories)
        {
            int m = 0;
            foreach (var c in categories) m |= Bit(c);
            return new BiomeScope(m, null, null);
        }

        /// <summary>Every family.</summary>
        public static readonly BiomeScope All = new BiomeScope(-1, null, null);
        /// <summary>Living creatures: creatures, mythical creatures and monsters.</summary>
        public static readonly BiomeScope Animals = Categories(CreationCategory.Creature, CreationCategory.Mythical, CreationCategory.Monster);
        public static readonly BiomeScope Flora = Categories(CreationCategory.Flora);
        public static readonly BiomeScope Props = Categories(CreationCategory.Prop);

        public static BiomeScope Family(string familyId) => new BiomeScope(-1, familyId, null);
        public static BiomeScope Archetype(string familyId, string archetypeId) => new BiomeScope(-1, familyId, archetypeId);

        public bool Matches(ICreatureFamily family, string archetype)
        {
            if (_categories != -1 && (_categories & Bit(CreatureFamilies.CategoryOf(family))) == 0) return false;
            if (FamilyId != null && !string.Equals(FamilyId, family.Id, StringComparison.Ordinal)) return false;
            if (ArchetypeId != null && !string.Equals(ArchetypeId, archetype, StringComparison.Ordinal)) return false;
            return true;
        }
    }

    /// <summary>
    /// An environment (desert, snow, jungle ...). Built-in biomes are in <see cref="Biomes"/>; extensions can
    /// register their own with CreatureRegistry.RegisterBiome. Rules are added with the fluent methods and
    /// must not change after the biome is registered (biomes are shared between threads).
    /// </summary>
    public sealed class Biome
    {
        private readonly struct ArchetypeRule
        {
            public readonly string FamilyId, ArchetypeId;
            public readonly double Factor;
            public ArchetypeRule(string f, string a, double factor) { FamilyId = f; ArchetypeId = a; Factor = factor; }
        }

        private readonly struct WeightRule
        {
            public readonly BiomeScope Scope;
            public readonly string Id;
            public readonly double Factor;
            public WeightRule(BiomeScope s, string id, double f) { Scope = s; Id = id; Factor = f; }
        }

        private readonly struct ExtraCoat
        {
            public readonly BiomeScope Scope;
            public readonly CoatPrior Coat;
            public readonly double Share;
            public ExtraCoat(BiomeScope s, CoatPrior c, double share) { Scope = s; Coat = c; Share = share; }
        }

        private readonly struct GeneRule
        {
            public readonly BiomeScope Scope;
            public readonly string GeneId;
            public readonly double Target, Amount;
            public GeneRule(BiomeScope s, string g, double t, double a) { Scope = s; GeneId = g; Target = t; Amount = a; }
        }

        private readonly List<ArchetypeRule> _archetypes = new List<ArchetypeRule>();
        private readonly List<WeightRule> _coats = new List<WeightRule>();
        private readonly List<ExtraCoat> _extraCoats = new List<ExtraCoat>();
        private readonly List<WeightRule> _patterns = new List<WeightRule>();
        private readonly List<WeightRule> _glow = new List<WeightRule>();
        private readonly List<GeneRule> _genes = new List<GeneRule>();

        public string Id { get; }
        public string DisplayName { get; }
        public string Description { get; }
        /// <summary>Sort order in pickers.</summary>
        public int Order { get; set; }
        /// <summary>The look of the biome: cover, grime, wetness and grade drawn over rendered creations.</summary>
        public BiomeOverlay Overlay { get; set; }
        /// <summary>
        /// Cover amount on living creations (creatures, mythical creatures, monsters) relative to trees and props:
        /// snow settles on every back, moss mostly grows on stone and wood.
        /// </summary>
        public double AnimalCover { get; set; } = 1.0;
        /// <summary>Suggested ground colours for scenes (base, shade, highlight) and water.</summary>
        public Rgba Ground { get; set; } = new Rgba(98, 142, 72);
        public Rgba GroundShade { get; set; } = new Rgba(74, 116, 58);
        public Rgba GroundLight { get; set; } = new Rgba(128, 168, 86);
        public Rgba Water { get; set; } = new Rgba(52, 110, 150);

        public Biome(string id, string displayName, string description)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("Biome id must not be empty.", nameof(id));
            Id = id; DisplayName = displayName; Description = description;
        }

        /// <summary>True when the biome changes nothing in sampling (the temperate default).</summary>
        public bool IsNeutralSampling =>
            _archetypes.Count == 0 && _coats.Count == 0 && _extraCoats.Count == 0 && _patterns.Count == 0 && _glow.Count == 0 && _genes.Count == 0;

        // ------------------------------------------------------------------ rule builders

        /// <summary>Multiplies the sampling weight of an archetype ("*" as family id: any family).</summary>
        public Biome Archetype(string familyId, string archetypeId, double factor)
        {
            _archetypes.Add(new ArchetypeRule(familyId, archetypeId, Math.Max(0, factor)));
            return this;
        }

        /// <summary>Multiplies the weights of several archetypes of one family: ("cactus", 6), ("palm", 3) ...</summary>
        public Biome Archetypes(string familyId, params (string archetype, double factor)[] factors)
        {
            foreach (var (a, f) in factors) Archetype(familyId, a, f);
            return this;
        }

        /// <summary>
        /// Multiplies the weight of a coat colour (see Coats: "sand", "albino", "green" ...). The id "*" sets the
        /// factor of all coats without a rule of their own.
        /// </summary>
        public Biome Coat(BiomeScope scope, string coatId, double factor)
        {
            _coats.Add(new WeightRule(scope, coatId, Math.Max(0, factor)));
            return this;
        }

        public Biome Coats(BiomeScope scope, params (string coat, double factor)[] factors)
        {
            foreach (var (c, f) in factors) Coat(scope, c, f);
            return this;
        }

        /// <summary>
        /// Adds a coat colour a family does not have (polar white for wolves). 'share' is its weight relative to
        /// the family's total coat weight (0.3 = about a quarter of all samples).
        /// </summary>
        public Biome AddCoat(BiomeScope scope, CoatPrior coat, double share)
        {
            _extraCoats.Add(new ExtraCoat(scope, coat, Math.Max(0, share)));
            return this;
        }

        /// <summary>Multiplies the weight of a pattern (stripes, spots, rosettes, mottled ...).</summary>
        public Biome Pattern(BiomeScope scope, string patternId, double factor)
        {
            _patterns.Add(new WeightRule(scope, patternId, Math.Max(0, factor)));
            return this;
        }

        public Biome Patterns(BiomeScope scope, params (string pattern, double factor)[] factors)
        {
            foreach (var (p, f) in factors) Pattern(scope, p, f);
            return this;
        }

        /// <summary>Multiplies the chance of glowing (bioluminescent) colours.</summary>
        public Biome Glow(BiomeScope scope, double factor)
        {
            _glow.Add(new WeightRule(scope, "glow", Math.Max(0, factor)));
            return this;
        }

        /// <summary>
        /// Moves a gene towards 'target' by 'amount' (0..1) after sampling. Hue genes move along the shorter arc.
        /// Genes a family does not have are ignored.
        /// </summary>
        public Biome Gene(BiomeScope scope, string geneId, double target, double amount)
        {
            _genes.Add(new GeneRule(scope, geneId, target, DMath.Clamp(amount, 0, 1)));
            return this;
        }

        // ------------------------------------------------------------------ application

        /// <summary>Weight factor of an archetype in this biome (product of all matching rules, 1 without rules).</summary>
        public double ArchetypeFactor(ICreatureFamily family, string archetypeId)
        {
            double f = 1;
            foreach (var r in _archetypes)
            {
                if (!string.Equals(r.ArchetypeId, archetypeId, StringComparison.Ordinal)) continue;
                if (r.FamilyId != "*" && !string.Equals(r.FamilyId, family.Id, StringComparison.Ordinal)) continue;
                f *= r.Factor;
            }
            return f;
        }

        /// <summary>The family's style hints adjusted to this biome (a copy; the family's hints stay untouched).</summary>
        public FamilyStyleHints ApplyHints(FamilyStyleHints hints, ICreatureFamily family, string archetype)
        {
            var h = hints.Clone();
            if (h.Coats.Length > 0)
            {
                var coats = new List<CoatPrior>(h.Coats.Length + _extraCoats.Count);
                double total = 0;
                foreach (var c in h.Coats)
                {
                    double w = c.Weight;
                    bool listed = false;
                    foreach (var r in _coats)
                    {
                        if (r.Id != c.Id || !r.Scope.Matches(family, archetype)) continue;
                        w *= r.Factor;
                        listed = true;
                    }
                    // "*" applies to every coat the biome does not name (custom coats of extensions included)
                    if (!listed)
                        foreach (var r in _coats)
                            if (r.Id == "*" && r.Scope.Matches(family, archetype)) w *= r.Factor;
                    coats.Add(c.WithWeight(w));
                    total += c.Weight;
                }
                foreach (var e in _extraCoats)
                {
                    if (!e.Scope.Matches(family, archetype)) continue;
                    bool present = false;
                    for (int i = 0; i < coats.Count; i++)
                    {
                        if (coats[i].Id != e.Coat.Id) continue;
                        // already a coat of the family: make sure it gets at least the requested share
                        coats[i] = coats[i].WithWeight(Math.Max(coats[i].Weight, e.Share * total));
                        present = true;
                    }
                    if (!present) coats.Add(e.Coat.WithWeight(e.Share * total));
                }
                double sum = 0;
                foreach (var c in coats) sum += c.Weight;
                // a biome never removes every colour of a family
                if (sum > 1e-9) h.Coats = coats.ToArray();
            }
            if (_patterns.Count > 0)
            {
                var keys = new List<string>(h.Patterns.Keys);
                var scaled = new Dictionary<string, double>(h.Patterns);
                foreach (var r in _patterns)
                {
                    if (!r.Scope.Matches(family, archetype)) continue;
                    if (scaled.TryGetValue(r.Id, out double w)) scaled[r.Id] = w * r.Factor;
                }
                double sum = 0;
                foreach (var k in keys) sum += scaled[k];
                if (sum > 1e-9) h.Patterns = scaled;
            }
            foreach (var r in _glow)
                if (r.Scope.Matches(family, archetype)) h.GlowChance = Math.Min(0.85, h.GlowChance * r.Factor);
            return h;
        }

        /// <summary>Applies the gene nudges to freshly sampled values.</summary>
        public void ApplyGenes(GeneSchema schema, double[] values, ICreatureFamily family, string archetype)
        {
            foreach (var r in _genes)
            {
                if (!r.Scope.Matches(family, archetype)) continue;
                int i = schema.IndexOf(r.GeneId);
                if (i < 0) continue;
                var gene = schema.Genes[i];
                if (gene.Kind != GeneKind.Float) continue;
                double v = values[i];
                if (gene.Unit == DisplayUnit.Degrees && gene.Max - gene.Min >= 359)
                    v = ColorMath.WrapHue(v + ColorMath.HueDelta(v, r.Target) * r.Amount);
                else
                    v += (r.Target - v) * r.Amount;
                values[i] = gene.Quantize(v);
            }
        }

        /// <summary>
        /// The overlay for one creation: the biome's look at 'intensity' (0..2), without cover and grime for
        /// swimmers (they live under water) and with only half the grime for floating and flying bodies.
        /// </summary>
        public BiomeOverlay OverlayFor(CreatureModel? model, double intensity = 1.0)
        {
            var o = Overlay.Scaled(intensity);
            if (model == null || !o.IsActive) return o;
            var category = CreatureFamilies.CategoryOf(model.Family);
            if (category != CreationCategory.Flora && category != CreationCategory.Prop)
                o.CoverAmount = DMath.Clamp(o.CoverAmount * AnimalCover, 0, 1);
            switch (model.Anatomy.Rig.Locomotion)
            {
                case Anatomy.LocomotionKind.Swim:
                    o.Cover = null; o.CoverAmount = 0;
                    o.Grime = null; o.GrimeAmount = 0;
                    break;
                case Anatomy.LocomotionKind.Fly:
                case Anatomy.LocomotionKind.Float:
                    o.GrimeAmount *= 0.5;
                    break;
            }
            return o;
        }

        public override string ToString() => Id;
    }
}
