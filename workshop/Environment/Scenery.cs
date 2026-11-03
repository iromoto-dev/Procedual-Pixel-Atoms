// Procedural Pixel Creature Workshop - procedural scenery of the test area. The prop spots of a map are
// filled with stationary creations of the "flora" and "prop" families (trees, bushes, rocks, lamp posts …)
// instead of static textures. A map samples a few genomes per form from its seed and builds their models
// while it is generated (core only, no engine calls, so this runs on the generator's worker thread). Spots
// pick a form by habitat - willows by the water, conifers and crystals on the hills, signposts and lanterns
// along the paths, mushrooms in dark patches - plus a facing and a wind phase, so neighbours that share a
// model still look and sway differently. The solid base of every placed creation (a share of its
// Metrics.HorizontalRadius) blocks the clearance map, like the footprints of the static props.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PixelCreatures.Core;
using PixelCreatures.Core.Anatomy;
using PixelCreatures.Core.Genetics;
using PixelCreatures.Core.Motion;
using PixelCreatures.Runtime;

namespace PixelCreatures.Workshop
{
    /// <summary>How the test area shows the trees, bushes and rocks of its map.</summary>
    public enum SceneryStyle : byte
    {
        /// <summary>Flora and prop creations: actors that sway in the wind and share the creatures' pixel density.</summary>
        Procedural,
        /// <summary>The static textures of <see cref="EnvironmentArt.Tree"/>, <see cref="EnvironmentArt.Bush"/> and <see cref="EnvironmentArt.Rock"/>.</summary>
        Classic,
    }

    /// <summary>One sampled flora or prop creation, shared by all spots that show it.</summary>
    public sealed class SceneryVariant
    {
        public string FamilyId = string.Empty;
        /// <summary>Archetype of the creation ("oak", "rock", "lantern" …).</summary>
        public string Form = string.Empty;
        public CreatureGenome Genome = null!;
        public CreatureModel Model = null!;
        /// <summary>Radius of the solid base on the ground plane (world units): what creatures cannot walk through.</summary>
        public double Footprint;
        /// <summary>
        /// True when the wind moves the creation (branches, fronds, banners, flames). Rocks, crates and the like hold
        /// still, so their actors only need to be rasterized when they come into view.
        /// </summary>
        public bool Sways;
    }

    public static partial class EnvironmentArt
    {
        /// <summary>Bounds of the solid footprint radius (world units).</summary>
        public const double MinFootprint = 1.5, MaxFootprint = 16;
        /// <summary>Elevation (screen pixels) from which a spot counts as hill country.</summary>
        private const float HighlandElevation = 22;

        [Flags]
        private enum Habitat
        {
            Meadow = 0,
            Shore = 1,
            Highland = 2,
            Wayside = 4,
            Shade = 8,
            Bloom = 16,
        }

        /// <summary>A form a spot can show: its family, how much of its reach is solid ground contact, whether it has a front.</summary>
        private readonly struct FormSpec
        {
            public readonly string Family, Form;
            /// <summary>
            /// Share of <c>Metrics.HorizontalRadius</c> that blocks the ground. Trees and giant mushrooms reach out with
            /// their crown but stand on a trunk or stem, so creatures may pass under the crown; plants block part of
            /// their spread; solid objects nearly all of it; posts (lamps, signs, banners) only their pole and base.
            /// </summary>
            public readonly double Solid;
            /// <summary>Objects with a front (signs, chests, gravestones) face the viewer; round things face anywhere.</summary>
            public readonly bool FacesViewer;
            /// <summary>Genomes sampled per map: common forms get more, so neighbours rarely share one.</summary>
            public readonly int Variants;

            public FormSpec(string family, string form, double solid, int variants, bool facesViewer = false)
            {
                Family = family;
                Form = form;
                Solid = solid;
                Variants = variants;
                FacesViewer = facesViewer;
            }
        }

        private static readonly FormSpec[] SceneryForms =
        {
            new FormSpec("flora", "oak", 0.22, 3), new FormSpec("flora", "pine", 0.22, 3), new FormSpec("flora", "birch", 0.22, 3),
            new FormSpec("flora", "willow", 0.2, 2), new FormSpec("flora", "palm", 0.18, 2), new FormSpec("flora", "dead", 0.22, 2),
            new FormSpec("flora", "blossom", 0.22, 2), new FormSpec("flora", "bush", 0.6, 3), new FormSpec("flora", "flowers", 0.45, 3),
            new FormSpec("flora", "mushroom", 0.3, 2),
            new FormSpec("prop", "rock", 0.85, 3), new FormSpec("prop", "crystal", 0.75, 2), new FormSpec("prop", "crate", 0.9, 2),
            new FormSpec("prop", "barrel", 0.9, 2), new FormSpec("prop", "chest", 0.9, 1, true), new FormSpec("prop", "pot", 0.9, 2),
            new FormSpec("prop", "gravestone", 0.9, 2, true), new FormSpec("prop", "pillar", 0.8, 2), new FormSpec("prop", "lantern", 0.5, 2, true),
            new FormSpec("prop", "signpost", 0.5, 2, true), new FormSpec("prop", "banner", 0.5, 2, true), new FormSpec("prop", "brazier", 0.8, 1),
            new FormSpec("prop", "campfire", 0.8, 2),
        };

        private static int FormIndex(string form)
        {
            for (int i = 0; i < SceneryForms.Length; i++)
                if (SceneryForms[i].Form == form) return i;
            throw new ArgumentException($"Unknown scenery form '{form}'.");
        }

        /// <summary>
        /// Weighted forms for a prop spot. The kinds keep their roles from the classic map (tree, undergrowth, rock),
        /// the habitat shifts the mix.
        /// </summary>
        private static void FormWeights(PropKind kind, Habitat h, List<(string Form, double Weight)> into)
        {
            into.Clear();
            bool shore = (h & Habitat.Shore) != 0, high = (h & Habitat.Highland) != 0, way = (h & Habitat.Wayside) != 0;
            switch (kind)
            {
                case PropKind.Tree:
                    into.Add(("oak", high ? 0.8 : shore ? 1.0 : 3.0));
                    into.Add(("birch", high ? 1.5 : shore ? 1.0 : 2.0));
                    into.Add(("pine", high ? 4.0 : shore ? 0.2 : 1.5));
                    into.Add(("blossom", high ? 0.2 : shore ? 0.5 : 1.2));
                    into.Add(("dead", high ? 0.5 : 0.3));
                    into.Add(("willow", shore ? 3.0 : 0.2));
                    into.Add(("palm", shore && !high ? 0.5 : 0));
                    break;
                case PropKind.Bush:
                    into.Add(("bush", 4.0));
                    into.Add(("flowers", (h & Habitat.Bloom) != 0 ? 5.0 : 2.0));
                    into.Add(("mushroom", (h & Habitat.Shade) != 0 ? 2.5 : 0.4));
                    break;
                default:
                    into.Add(("rock", high ? 8.0 : 6.0));
                    into.Add(("crystal", high ? 2.0 : 0.4));
                    into.Add(("pillar", high ? 0.8 : 0.25));
                    into.Add(("gravestone", way ? 0.4 : 0.25));
                    into.Add(("crate", way ? 1.0 : 0.12));
                    into.Add(("barrel", way ? 1.2 : 0.12));
                    into.Add(("pot", way ? 0.6 : 0.15));
                    into.Add(("signpost", way ? 2.0 : 0));
                    into.Add(("lantern", way ? 1.6 : 0));
                    into.Add(("campfire", way ? 0.8 : shore ? 0.4 : 0.1));
                    into.Add(("banner", way ? 0.4 : 0));
                    into.Add(("brazier", way ? 0.3 : 0));
                    into.Add(("chest", way ? 0.25 : 0.05));
                    break;
            }
        }

        /// <summary>Surroundings of a spot: water or sand, paths nearby, hill country, dark patches or flower meadows.</summary>
        private static Habitat HabitatAt(TestMap map, int x, int y)
        {
            var h = Habitat.Meadow;
            if (map.Elevation[y * map.Width + x] >= HighlandElevation) h |= Habitat.Highland;
            double m = Meadow(map.Seed, x, y);
            if (MeadowShade(m) > 0.3) h |= Habitat.Shade;
            if (MeadowLift(m) > 0.4) h |= Habitat.Bloom;
            for (int r = 14; r <= 38; r += 12)
            {
                for (int k = 0; k < 12; k++)
                {
                    double a = k * DMath.TwoPi / 12;
                    int xx = x + (int)Math.Round(DMath.Cos(a) * r), yy = y + (int)Math.Round(DMath.Sin(a) * r * 0.6);
                    if (!map.InBounds(xx, yy)) continue;
                    byte t = map.Terrain[yy * map.Width + xx];
                    if (t == TestMap.Water || t == TestMap.Sand) h |= Habitat.Shore;
                    else if (t == TestMap.Path && r <= 26) h |= Habitat.Wayside;
                }
            }
            return h;
        }

        /// <summary>
        /// Gives every prop spot a flora or prop creation (procedural style). First every spot picks its form, variant,
        /// facing and wind phase from one random stream; then the variants of the forms in use are sampled from the map
        /// seed - the same seed gives the same creations on every map size - and built in parallel into fixed slots,
        /// so the result does not depend on scheduling. The test area then only has to create the actors. Spots whose
        /// family is not registered keep their static texture.
        /// </summary>
        private static void AssignScenery(TestMap map, CancellationToken cancel)
        {
            var registry = CreatureRuntime.Registry;
            var families = new Dictionary<string, ICreatureFamily?>();
            ICreatureFamily? FamilyOf(string id)
            {
                if (families.TryGetValue(id, out var f)) return f;
                families[id] = f = registry.TryGetFamily(id, out var found) ? found : null;
                // the schema is created lazily: do it here, so the parallel sampling below only reads
                _ = f?.Schema;
                return f;
            }
            var firstSlot = new int[SceneryForms.Length];
            Array.Fill(firstSlot, -1);
            var jobs = new List<(int Form, int Variant, ICreatureFamily Family)>();
            var spotForm = new int[map.Props.Count];
            var spotPick = new int[map.Props.Count];
            var weights = new List<(string Form, double Weight)>();
            var w = new List<double>();
            var rng = new Rng(map.Seed ^ 0x5343454EUL);
            for (int i = 0; i < map.Props.Count; i++)
            {
                var p = map.Props[i];
                FormWeights(p.Kind, HabitatAt(map, p.X, p.Y), weights);
                w.Clear();
                foreach (var f in weights) w.Add(f.Weight);
                int form = FormIndex(weights[rng.Weighted(w)].Form);
                var spec = SceneryForms[form];
                spotForm[i] = form;
                spotPick[i] = rng.NextInt(spec.Variants);
                p.Facing = spec.FacesViewer ? 5 + rng.NextInt(3) : rng.NextInt(8);
                p.Phase = (float)rng.Range(0.0, 30.0);
                map.Props[i] = p;
                var family = firstSlot[form] < 0 ? FamilyOf(spec.Family) : null;
                if (family != null)
                {
                    firstSlot[form] = jobs.Count;
                    for (int k = 0; k < spec.Variants; k++) jobs.Add((form, k, family));
                }
            }
            var variants = new SceneryVariant[jobs.Count];
            Parallel.For(0, jobs.Count, new ParallelOptions { CancellationToken = cancel }, j =>
            {
                var (form, k, family) = jobs[j];
                variants[j] = BuildVariant(map.Seed, SceneryForms[form], family, k, cancel);
            });
            cancel.ThrowIfCancellationRequested();
            map.Scenery.AddRange(variants);
            for (int i = 0; i < map.Props.Count; i++)
            {
                int slot = firstSlot[spotForm[i]];
                if (slot < 0) continue;
                var p = map.Props[i];
                p.Scenery = slot + spotPick[i];
                map.Props[i] = p;
            }
        }

        /// <summary>Variant k of a form: the family picks its archetype from the seed, so seeds are drawn until it picks this form.</summary>
        private static SceneryVariant BuildVariant(ulong mapSeed, FormSpec spec, ICreatureFamily family, int k, CancellationToken cancel)
        {
            ulong formSeed = StableHash.Derive(mapSeed, "scenery." + spec.Form);
            CreatureGenome genome = GenomeFactory.Sample(family, StableHash.Derive(formSeed, "variant", k * 4096));
            for (int attempt = 1; attempt < 400 && genome.GetChoice("form") != spec.Form; attempt++)
                genome = GenomeFactory.Sample(family, StableHash.Derive(formSeed, "variant", k * 4096 + attempt));
            var model = CreatureRuntime.Factory.Build(genome, cancel);
            double footprint = Math.Clamp(model.Anatomy.Metrics.HorizontalRadius * spec.Solid, MinFootprint, MaxFootprint);
            return new SceneryVariant
            {
                FamilyId = spec.Family, Form = genome.GetChoice("form"), Genome = genome, Model = model, Footprint = footprint,
                Sways = MovesInWind(model),
            };
        }

        /// <summary>
        /// Whether the wind has anything to move. The rooted module leans the trunk (<c>wind.trunk</c>) and sways the
        /// appendage chains (bones with the Tentacle role: branches, fronds, leaf curtains, banners, lanterns, flames).
        /// Creations without either - rocks, crates, gravestones - hold still as scenery.
        /// </summary>
        private static bool MovesInWind(CreatureModel model)
        {
            var anatomy = model.Anatomy;
            if (anatomy.Rig.LocomotionModule != RootedLocomotion.ModuleId || anatomy.Rig.Tune("wind.trunk", 0.03) > 0) return true;
            var skeleton = anatomy.Skeleton;
            for (int i = 0; i < skeleton.Count; i++)
                if (skeleton[i].Role == BoneRole.Tentacle) return true;
            return false;
        }
    }
}
