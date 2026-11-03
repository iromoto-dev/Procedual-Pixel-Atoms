// Procedural Pixel Creatures - engine independent test suite (runs in the console harness and inside
// Godot via --self-test). Everything here is deterministic.

#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using PixelCreatures.Core.Json;
using PixelCreatures.Core;
using PixelCreatures.Core.Anatomy;
using PixelCreatures.Core.Genetics;
using PixelCreatures.Core.Motion;
using PixelCreatures.Core.Rendering;
using PixelCreatures.Core.Export;
using PixelCreatures.Core.Serialization;

namespace PixelCreatures.Tests
{
    public static partial class CoreTests
    {
        public static void RunAll(TestRunner r, CreatureRegistry registry, int seedsPerFamily = 100)
        {
            var factory = new CreatureFactory(registry);
            r.Run("math.deterministic_kernels", ctx => DeterministicMath(ctx));
            r.Run("random.golden_sequences", ctx => RandomGolden(ctx));
            r.Run("genome.sampling_is_deterministic", ctx => SamplingDeterminism(ctx, registry));
            r.Run("genome.streams_are_independent", ctx => StreamIndependence(ctx, registry, factory));
            r.Run("genome.quantized_json_roundtrip", ctx => JsonRoundTrip(ctx, registry));
            r.Run("serializer.rejects_invalid_input", ctx => SerializerValidation(ctx, registry));
            r.Run("serializer.migration_chain", ctx => MigrationChain(ctx, registry));
            r.Run("genetics.mutation_deterministic_and_locked", ctx => MutationTests(ctx, registry));
            r.Run("genetics.crossover_and_compatibility", ctx => CrossoverTests(ctx, registry));
            r.Run("genetics.regenerate_respects_locks", ctx => RegenerateTests(ctx, registry));
            r.Run($"anatomy.valid_for_{seedsPerFamily}_seeds_per_family", ctx => AnatomyValidity(ctx, registry, factory, seedsPerFamily));
            r.Run("render.deterministic_and_golden", ctx => RenderDeterminism(ctx, registry, factory));
            r.Run("motion.framerate_independent", ctx => MotionFramerateIndependence(ctx, registry, factory));
            r.Run("motion.planted_feet_do_not_slide", ctx => FootSliding(ctx, registry, factory));
            r.Run("motion.states_and_directions_stay_in_canvas", ctx => CanvasSafety(ctx, registry, factory));
            r.Run("motion.turning_in_place_reorients_body", ctx => TurnInPlace(ctx, registry, factory));
            r.Run("anatomy.extreme_genes_build_move_and_render", ctx => ExtremeGenes(ctx, registry, factory));
            r.Run("biome.sampling_neutral_deterministic_and_biased", ctx => BiomeSampling(ctx, registry, factory, Math.Max(6, seedsPerFamily / 4)));
            r.Run("biome.overlay_render", ctx => BiomeOverlayRender(ctx, registry, factory));
            r.Run("export.page_wrap_compact_and_overlay", ctx => ExportLayout(ctx, registry, factory));
        }

        // ------------------------------------------------------------------ math & random

        private static void DeterministicMath(TestContext ctx)
        {
            // Bit exact reference values of the DMath kernels. They must be identical on every OS/CPU.
            ctx.Equal(0x3FEAED548F090CEEL, BitConverter.DoubleToInt64Bits(DMath.Sin(1.0)), "sin(1)");
            ctx.Equal(0x3FE14A280FB5068CL, BitConverter.DoubleToInt64Bits(DMath.Cos(1.0)), "cos(1)");
            // DMath.Exp(1) is one ulp above the correctly rounded value; determinism, not rounding, is what matters
            ctx.Equal(0x4005BF0A8B14576AL, BitConverter.DoubleToInt64Bits(DMath.Exp(1.0)), "exp(1)");
            ctx.Equal(0x3FE62E42FEFA39EFL, BitConverter.DoubleToInt64Bits(DMath.Log(2.0)), "log(2)");
            ctx.Equal(0x3FE921FB54442D18L, BitConverter.DoubleToInt64Bits(DMath.Atan2(1.0, 1.0)), "atan2(1,1)");
            double maxErr = 0;
            for (int i = -2000; i <= 2000; i++)
            {
                double x = i * 0.0137;
                maxErr = Math.Max(maxErr, Math.Abs(DMath.Sin(x) - Math.Sin(x)));
                maxErr = Math.Max(maxErr, Math.Abs(DMath.Cos(x) - Math.Cos(x)));
            }
            ctx.True(maxErr < 1e-15, $"sin/cos accuracy {maxErr}");
            ctx.Metric("maxTrigError", maxErr);
        }

        private static void RandomGolden(TestContext ctx)
        {
            var rng = new Rng(42);
            var seq = new uint[] { rng.NextUInt(), rng.NextUInt(), rng.NextUInt(), rng.NextUInt() };
            ctx.Note("pcg32(42): " + string.Join(",", seq));
            ctx.Equal(Golden.Pcg42[0], seq[0], "pcg32[0]");
            ctx.Equal(Golden.Pcg42[1], seq[1], "pcg32[1]");
            ctx.Equal(Golden.Pcg42[2], seq[2], "pcg32[2]");
            ctx.Equal(Golden.Pcg42[3], seq[3], "pcg32[3]");
            ctx.Equal(Golden.Fnv, StableHash.Fnv1a64("Procedural Pixel Creatures"), "fnv1a64");
            ctx.Equal(Golden.Derived, StableHash.Derive(123456789UL, "stream.anatomy"), "derive");
            // unbiased bounded ints stay in range
            var r2 = new Rng(7);
            for (int i = 0; i < 10000; i++)
            {
                int v = r2.NextInt(37);
                ctx.True(v >= 0 && v < 37, "NextInt range");
            }
        }

        // ------------------------------------------------------------------ genome

        private static void SamplingDeterminism(TestContext ctx, CreatureRegistry reg)
        {
            foreach (var f in reg.Families)
            {
                var a = GenomeFactory.Sample(f, 424242);
                var b = GenomeFactory.Sample(f, 424242);
                var c = GenomeFactory.Sample(f, 424243);
                ctx.True(a.ContentEquals(b), $"{f.Id}: same seed must give same genome");
                ctx.True(!a.ContentEquals(c), $"{f.Id}: different seed should differ");
                ctx.Equal(a.ContentHash, b.ContentHash, $"{f.Id}: content hash");
            }
        }

        private static void StreamIndependence(TestContext ctx, CreatureRegistry reg, CreatureFactory factory)
        {
            foreach (var f in reg.Families)
            {
                var g = GenomeFactory.Sample(f, 99);
                var colored = g.With("color.hue", (g.Get("color.hue") + 137) % 360).With("color.saturation", 1 - g.Get("color.saturation"));
                ctx.Equal(g.AnatomyKey, colored.AnatomyKey, $"{f.Id}: anatomy key must ignore colour genes");
                ctx.Equal(g.PatternKey, colored.PatternKey, $"{f.Id}: pattern key must ignore colour genes");
                ctx.True(g.ColorKey != colored.ColorKey, $"{f.Id}: colour key must change");
                var a1 = f.BuildAnatomy(g, reg);
                var a2 = f.BuildAnatomy(colored, reg);
                ctx.Equal(AnatomyHash(a1), AnatomyHash(a2), $"{f.Id}: anatomy geometry after colour change");
                var patterned = g.With("pattern.density", 1 - g.Get("pattern.density"));
                ctx.Equal(AnatomyHash(a1), AnatomyHash(f.BuildAnatomy(patterned, reg)), $"{f.Id}: anatomy after pattern change");
                // stationary families (flora, props) have no motion genes
                if (!g.Schema.Contains("motion.energy")) continue;
                var moved = g.With("motion.energy", 1 - g.Get("motion.energy"));
                ctx.Equal(g.AnatomyKey, moved.AnatomyKey, $"{f.Id}: anatomy key must ignore motion genes");
            }
        }

        public static ulong AnatomyHash(CreatureAnatomy a)
        {
            ulong h = 17;
            void D(double v) => h = StableHash.Combine(h, (ulong)BitConverter.DoubleToInt64Bits(v));
            foreach (var b in a.Skeleton.Bones)
            {
                h = StableHash.Combine(h, (ulong)(b.Parent + 7));
                D(b.RestOffset.X); D(b.RestOffset.Y); D(b.RestOffset.Z);
                D(b.RestRotation.X); D(b.RestRotation.Y); D(b.RestRotation.Z); D(b.RestRotation.W);
            }
            foreach (var p in a.Primitives)
            {
                h = StableHash.Combine(h, (ulong)p.Kind * 31 + (ulong)p.Material);
                D(p.LocalA.X); D(p.LocalA.Y); D(p.LocalA.Z); D(p.LocalB.X); D(p.LocalB.Y); D(p.LocalB.Z);
                D(p.RadiusA); D(p.RadiusB); D(p.Radii.X); D(p.Radii.Y); D(p.Radii.Z);
            }
            foreach (var fe in a.Features) { D(fe.LocalPosition.X); D(fe.LocalPosition.Y); D(fe.LocalPosition.Z); D(fe.Size); }
            return h;
        }

        private static void JsonRoundTrip(TestContext ctx, CreatureRegistry reg)
        {
            var ser = new GenomeSerializer(reg);
            foreach (var f in reg.Families)
            {
                for (ulong s = 1; s <= 20; s++)
                {
                    var g = GenomeFactory.Sample(f, s * 7919);
                    string json = ser.Serialize(g);
                    var res = ser.Deserialize(json);
                    ctx.True(res.Ok, $"{f.Id}/{s}: reload failed: {res.Describe()}");
                    ctx.True(res.Warnings.Count == 0, $"{f.Id}/{s}: unexpected warnings: {res.Describe()}");
                    ctx.True(res.Genome!.ContentEquals(g), $"{f.Id}/{s}: genome changed by save/load");
                    ctx.Equal(g.Name, res.Genome.Name, "name");
                    // second round trip is byte identical
                    ctx.Equal(json, ser.Serialize(res.Genome), $"{f.Id}/{s}: serializer output stable");
                }
            }
        }

        private static void SerializerValidation(TestContext ctx, CreatureRegistry reg)
        {
            var ser = new GenomeSerializer(reg);
            var f = reg.Families[0];
            var g = GenomeFactory.Sample(f, 5);
            string good = ser.Serialize(g);

            void ExpectError(string json, string contains, string what)
            {
                var r = ser.Deserialize(json);
                ctx.True(!r.Ok, $"{what}: must fail");
                ctx.True(r.Errors.Any(e => e.Contains(contains, StringComparison.OrdinalIgnoreCase)), $"{what}: error should mention '{contains}', got: {r.Describe()}");
            }

            JsonObject Mut(Action<JsonObject> change)
            {
                var o = (JsonObject)JsonNode.Parse(good)!;
                change(o);
                return o;
            }

            ExpectError("{ not json", "Invalid JSON", "broken json");
            ExpectError("[1,2]", "does not contain a JSON object", "array root");
            ExpectError(Mut(o => o["format"] = "something").ToJsonString(), "file format", "wrong format");
            ExpectError(Mut(o => o.Remove("formatVersion")).ToJsonString(), "formatVersion", "missing version");
            ExpectError(Mut(o => o["formatVersion"] = 99).ToJsonString(), "format version 99", "newer format");
            ExpectError(Mut(o => o["generatorVersion"] = 42).ToJsonString(), "generator version 42", "newer generator");
            ExpectError(Mut(o => o["family"] = "dragons-from-mars").ToJsonString(), "Unknown body-plan family", "unknown family");
            ExpectError(Mut(o => o["seeds"]!["color"] = "-5").ToJsonString(), "Seed 'color'", "negative seed");
            ExpectError(Mut(o => o["seeds"]!.AsObject().Remove("motion")).ToJsonString(), "Seed 'motion'", "missing seed");
            ExpectError(Mut(o => o["genes"]!["size"] = "big").ToJsonString(), "Expected a number", "string for float");
            ExpectError(Mut(o => o["genes"]!["pattern.dark"] = 3).ToJsonString(), "Boolean", "number for bool");

            // recoverable problems become warnings
            var w1 = ser.Deserialize(Mut(o => o["genes"]!["size"] = 5.0).ToJsonString());
            ctx.True(w1.Ok && w1.Warnings.Any(x => x.Contains("clamped")), "out of range clamps with warning: " + w1.Describe());
            ctx.Near(1.0, w1.Genome!.Get("size"), 1e-9, "clamped size");
            var w2 = ser.Deserialize(Mut(o => o["genes"]!.AsObject().Remove("mass")).ToJsonString());
            ctx.True(w2.Ok && w2.Warnings.Any(x => x.Contains("'mass'")), "missing gene defaults with warning");
            var w3 = ser.Deserialize(Mut(o => o["genes"]!["legacy.thing"] = 1).ToJsonString());
            ctx.True(w3.Ok && w3.Warnings.Any(x => x.Contains("legacy.thing")), "unknown gene ignored with warning");
            var w4 = ser.Deserialize(Mut(o => o["genes"]!["color.scheme"] = "neon").ToJsonString());
            ctx.True(w4.Ok && w4.Warnings.Any(x => x.Contains("neon")), "unknown option falls back with warning");
        }

        private sealed class TestMigrationV0 : IGenomeMigration
        {
            public int FromVersion => 0;
            public string Description => "Test migration: 'traits' -> 'genes', 'species' -> 'family'";
            public void Apply(JsonObject root)
            {
                if (root["traits"] is JsonNode t) { root.Remove("traits"); root["genes"] = t; }
                if (root["species"] is JsonNode s) { root.Remove("species"); root["family"] = s; }
            }
        }

        private static void MigrationChain(TestContext ctx, CreatureRegistry reg)
        {
            var ser = new GenomeSerializer(reg);
            var f = reg.Families[0];
            var g = GenomeFactory.Sample(f, 77);
            var o = (JsonObject)JsonNode.Parse(ser.Serialize(g))!;
            // build a synthetic legacy (format 0) file
            o["formatVersion"] = 0;
            var genes = o["genes"]!;
            o.Remove("genes");
            o["traits"] = genes;
            var fam = o["family"]!;
            o.Remove("family");
            o["species"] = fam;
            string legacy = o.ToJsonString();
            var noMigration = ser.Deserialize(legacy);
            ctx.True(!noMigration.Ok && noMigration.Errors.Any(e => e.Contains("No migration")), "missing migration must be an explicit error");
            ser.RegisterMigration(new TestMigrationV0());
            var migrated = ser.Deserialize(legacy);
            ctx.True(migrated.Ok, "migrated load: " + migrated.Describe());
            ctx.True(migrated.Migrations.Count == 1, "one migration step recorded");
            ctx.True(migrated.Genome!.ContentEquals(g), "migration preserves the creature");
            // gene alias (renamed gene)
            var ser2 = new GenomeSerializer(reg);
            ser2.RegisterGeneAlias(f.Id, "bodySize", "size");
            var o2 = (JsonObject)JsonNode.Parse(ser2.Serialize(g))!;
            var size = o2["genes"]!["size"]!.DeepClone();
            o2["genes"]!.AsObject().Remove("size");
            o2["genes"]!["bodySize"] = size;
            var aliased = ser2.Deserialize(o2.ToJsonString());
            ctx.True(aliased.Ok && aliased.Genome!.ContentEquals(g), "gene alias migration: " + aliased.Describe());
        }

        // ------------------------------------------------------------------ genetics

        private static void MutationTests(TestContext ctx, CreatureRegistry reg)
        {
            foreach (var f in reg.Families)
            {
                var parent = GenomeFactory.Sample(f, 1234);
                var settings = new MutationSettings { Strength = 0.5 };
                var a = GenomeOps.Mutate(parent, settings, 55);
                var b = GenomeOps.Mutate(parent, settings, 55);
                ctx.True(a.ContentEquals(b), $"{f.Id}: mutation deterministic");
                ctx.True(!a.ContentEquals(parent), $"{f.Id}: mutation changes something");
                var locks = new GeneLocks();
                locks.SetGroup("palette", true);
                locks.SetGene("size", true);
                var c = GenomeOps.Mutate(parent, new MutationSettings { Strength = 1.0 }, 56, locks);
                foreach (var def in f.Schema.Genes)
                {
                    if (def.Group == "palette" || def.Id == "size")
                        ctx.Equal(parent.Get(def.Id), c.Get(def.Id), $"{f.Id}: locked gene {def.Id}");
                }
                ctx.Equal(parent.ColorSeed, c.ColorSeed, $"{f.Id}: locked colour stream seed");
                var only = GenomeOps.Mutate(parent, new MutationSettings { Strength = 1.0, Anatomy = false, Pattern = false, Motion = false }, 57);
                ctx.Equal(parent.AnatomyKey, only.AnatomyKey, $"{f.Id}: colour-only mutation keeps anatomy");
                // strength 0 is identity for genes
                var zero = GenomeOps.Mutate(parent, new MutationSettings { Strength = 0 }, 58);
                ctx.True(zero.CopyValues().SequenceEqual(parent.CopyValues()), $"{f.Id}: zero strength keeps genes");
            }
        }

        private static void CrossoverTests(TestContext ctx, CreatureRegistry reg)
        {
            var fams = reg.Families;
            var f = fams[0];
            var a = GenomeFactory.Sample(f, 1);
            var b = GenomeFactory.Sample(f, 2);
            var r1 = GenomeOps.Cross(a, b, 9, new MutationSettings { Strength = 0.1 });
            var r2 = GenomeOps.Cross(a, b, 9, new MutationSettings { Strength = 0.1 });
            ctx.True(r1.Child.ContentEquals(r2.Child), "crossover deterministic");
            ctx.True(r1.Provenance.Count > 0, "provenance recorded");
            ctx.Equal("crossover", r1.Child.Lineage.Origin, "lineage origin");
            ctx.Equal(2, r1.Child.Lineage.ParentIds.Count, "two parents recorded");
            // without mutation every linked group comes from A, B or a blend of both
            var pure = GenomeOps.Cross(a, b, 10, new MutationSettings { Strength = 0 });
            foreach (var def in f.Schema.Genes)
            {
                double va = a.Get(def.Id), vb = b.Get(def.Id), vc = pure.Child.Get(def.Id);
                string src = pure.Provenance[def.Linkage];
                if (src == "A") ctx.Equal(va, vc, $"{def.Id} from A");
                else if (src == "B") ctx.Equal(vb, vc, $"{def.Id} from B");
                else if (def.Inheritance == InheritanceMode.Blend && def.Kind == GeneKind.Float)
                    ctx.True(vc >= Math.Min(va, vb) - 1e-6 && vc <= Math.Max(va, vb) + 1e-6, $"{def.Id} blended within parents");
            }
            if (fams.Count > 1)
            {
                var other = GenomeFactory.Sample(fams[1], 3);
                ctx.True(!GenomeOps.CanCross(a, other, out string reason) && reason.Length > 0, "different families are incompatible");
            }
        }

        private static void RegenerateTests(TestContext ctx, CreatureRegistry reg)
        {
            foreach (var f in reg.Families)
            {
                var g = GenomeFactory.Sample(f, 31337);
                var locks = new GeneLocks();
                string lockedGroup = f.Schema.Groups.First(x => x.Stream == GeneStream.Anatomy && x.Id != "body").Id;
                locks.SetGroup(lockedGroup, true);
                locks.SetGroup("palette", true);
                var n = GenomeFactory.RegenerateUnlocked(f, g, locks, 777);
                int changed = 0;
                foreach (var def in f.Schema.Genes)
                {
                    if (locks.IsLocked(def)) ctx.Equal(g.Get(def.Id), n.Get(def.Id), $"{f.Id}: locked {def.Id}");
                    else if (g.Get(def.Id) != n.Get(def.Id)) changed++;
                }
                ctx.True(changed > 3, $"{f.Id}: unlocked genes should change ({changed})");
                ctx.Equal(g.ColorSeed, n.ColorSeed, $"{f.Id}: colour seed kept with locked palette");
            }
        }

        // ------------------------------------------------------------------ anatomy & rendering

        private static void AnatomyValidity(TestContext ctx, CreatureRegistry reg, CreatureFactory factory, int seeds)
        {
            int total = 0, repairs = 0;
            int maxW = 0, maxH = 0;
            foreach (var f in reg.Families)
            {
                for (int i = 0; i < seeds; i++)
                {
                    var g = GenomeFactory.Sample(f, (ulong)(50000 + i * 131));
                    var m = factory.Build(g);
                    var a = m.Anatomy;
                    ctx.True(a.Primitives.Count >= 2, $"{f.Id}#{i}: primitives");
                    ctx.True(a.Skeleton.Count > 2, $"{f.Id}#{i}: bones");
                    foreach (var p in a.Primitives)
                    {
                        ctx.True(p.LocalA.IsFinite && p.LocalB.IsFinite && p.LocalC.IsFinite && p.Radii.IsFinite && DMath.IsFinite(p.RadiusA) && DMath.IsFinite(p.RadiusB), $"{f.Id}#{i}: finite primitive {p.Tag}");
                    }
                    ctx.True(a.Canvas.Width > 8 && a.Canvas.Width <= 320 && a.Canvas.Height > 8 && a.Canvas.Height <= 320, $"{f.Id}#{i}: canvas {a.Canvas}");
                    ctx.True(a.Metrics.BodyLength > 4 && DMath.IsFinite(a.Metrics.WalkSpeed) && a.Metrics.WalkSpeed > 0, $"{f.Id}#{i}: metrics");
                    maxW = Math.Max(maxW, a.Canvas.Width);
                    maxH = Math.Max(maxH, a.Canvas.Height);
                    repairs += a.Repairs.Count;
                    total++;
                }
            }
            ctx.Metric("anatomies", total);
            ctx.Metric("repairs", repairs);
            ctx.Metric("maxCanvasW", maxW);
            ctx.Metric("maxCanvasH", maxH);
        }

        public static ulong RenderHash(CreatureModel m, CreaturePose pose, double yaw, double density = 1)
        {
            var r = new CreatureRenderer();
            var c = m.Anatomy.Canvas.Scaled(density);
            var f = new PixelFrame(c.Width, c.Height) { OriginX = c.OriginX, OriginY = c.OriginY };
            r.Render(m.Anatomy, m.Palette, m.Surface, pose, ViewSpec.Default(yaw, density), f);
            return f.ContentHash();
        }

        /// <summary>Render hash under an environment overlay: frame indices plus the (graded) palette colours.</summary>
        public static ulong OverlayRenderHash(CreatureModel m, CreaturePose pose, double yaw, in BiomeOverlay overlay, double density = 1)
        {
            var frame = RenderOverlay(m, pose, yaw, overlay, density, out var palette);
            ulong h = frame.ContentHash();
            foreach (var col in palette.Colors) h = StableHash.Combine(h, col.Packed);
            return h;
        }

        private static PixelFrame RenderOverlay(CreatureModel m, CreaturePose pose, double yaw, in BiomeOverlay overlay, double density, out PixelCreatures.Core.Palettes.CreaturePalette palette)
        {
            var c = m.Anatomy.Canvas.Scaled(density);
            var f = new PixelFrame(c.Width, c.Height) { OriginX = c.OriginX, OriginY = c.OriginY };
            var view = ViewSpec.Default(yaw, density);
            view.Overlay = overlay;
            palette = m.PaletteFor(overlay);
            new CreatureRenderer().Render(m.Anatomy, palette, m.Surface, pose, view, f);
            return f;
        }

        /// <summary>
        /// Values checked against the golden table: genome content hash of seed 2024, the rest pose
        /// image and an image after 1.5 s of simulated walking (motion + rendering). Keys: family id,
        /// "id:genome", "id:walk".
        /// </summary>
        public static IEnumerable<(string key, ulong value)> GoldenValues(CreatureRegistry reg, CreatureFactory factory)
        {
            foreach (var f in reg.Families)
            {
                var g = GenomeFactory.Sample(f, 2024);
                var m = factory.Build(g);
                yield return ($"{f.Id}:genome", g.ContentHash);
                yield return (f.Id, RenderHash(m, new CreaturePose(m.Anatomy.Skeleton), PixelCamera.DirectionYaw(7)));
                // the HD path (pixel density 2 with high density anatomy) is pinned as well
                yield return ($"{f.Id}:hd", RenderHash(m, new CreaturePose(m.Anatomy.Skeleton), PixelCamera.DirectionYaw(7), CreatureFramework.HdDensity));
                var motor = new CreatureMotor(m, reg);
                double yaw = PixelCamera.DirectionYaw(7);
                motor.Teleport(Vec3.Zero, yaw);
                motor.FacingOverride = yaw;
                motor.DesiredVelocity = new Vec3(DMath.Cos(yaw), 0, -DMath.Sin(yaw)) * m.Motion.WalkSpeed;
                for (int i = 0; i < 90; i++) motor.Step(FixedClock.DefaultStep);
                yield return ($"{f.Id}:walk", RenderHash(m, motor.Pose, motor.Yaw));
            }
            // biomes: the biased genome of every family, and the overlay render of a creature, a tree and a prop
            foreach (var b in reg.Biomes)
            {
                if (b.IsNeutralSampling) continue;
                foreach (var f in reg.Families)
                {
                    var g = GenomeFactory.Sample(f, 2024, b);
                    yield return ($"{f.Id}:biome:{b.Id}", g.ContentHash);
                    if (f.Id != "quadruped" && f.Id != "flora" && f.Id != "prop") continue;
                    var m = factory.Build(g);
                    yield return ($"{f.Id}:biome:{b.Id}:render", OverlayRenderHash(m, new CreaturePose(m.Anatomy.Skeleton), PixelCamera.DirectionYaw(7), b.OverlayFor(m), CreatureFramework.HdDensity));
                }
            }
        }

        private static void RenderDeterminism(TestContext ctx, CreatureRegistry reg, CreatureFactory factory)
        {
            foreach (var f in reg.Families)
            {
                var m = factory.Build(GenomeFactory.Sample(f, 2024));
                var pose = new CreaturePose(m.Anatomy.Skeleton);
                ulong h1 = RenderHash(m, pose, PixelCamera.DirectionYaw(7));
                ulong h2 = RenderHash(m, pose, PixelCamera.DirectionYaw(7));
                ctx.Equal(h1, h2, $"{f.Id}: identical render twice");
                // a fresh factory (cold caches) produces the same image
                var m2 = new CreatureFactory(reg).Build(GenomeFactory.Sample(f, 2024));
                ctx.Equal(h1, RenderHash(m2, new CreaturePose(m2.Anatomy.Skeleton), PixelCamera.DirectionYaw(7)), $"{f.Id}: cold cache render");
            }
            // golden values (produced on Linux x64): identical results on every platform prove that
            // sampling, motion and rasterization are bit-exact (DMath instead of platform libm)
            var values = GoldenValues(reg, new CreatureFactory(reg)).ToList();
            int checkedCount = 0;
            foreach (var (key, value) in values)
            {
                if (!Golden.Values.TryGetValue(key, out ulong golden)) { ctx.Note($"no golden value for {key}"); continue; }
                ctx.Equal(golden, value, $"golden {key}");
                checkedCount++;
            }
            ctx.Metric("goldenChecked", checkedCount);
            ctx.Note("values: " + string.Join(" ", values.Select(v => $"{v.key}={v.value:x16}")));
        }

        private static CreatureMotor Scenario(CreatureRegistry reg, CreatureModel m, Func<int, double> frameDeltas, double seconds, out long steps)
        {
            var motor = new CreatureMotor(m, reg);
            motor.Teleport(Vec3.Zero, 0);
            var clock = new FixedClock();
            double t = 0;
            int frame = 0;
            while (t < seconds)
            {
                double d = frameDeltas(frame++);
                t += d;
                int n = clock.Advance(d);
                for (int i = 0; i < n; i++)
                {
                    double st = motor.Time;
                    motor.DesiredVelocity = st > 0.3 && st < 1.6 ? new Vec3(m.Motion.WalkSpeed, 0, -m.Motion.WalkSpeed * 0.4) : Vec3.Zero;
                    if (Math.Abs(st - 1.9) < 1e-9 || (st >= 1.9 && st < 1.9 + FixedClock.DefaultStep)) motor.TriggerAction();
                    motor.Step(clock.StepSize);
                }
            }
            steps = motor.StepCount;
            return motor;
        }

        private static ulong PoseHash(CreaturePose p)
        {
            ulong h = 1;
            foreach (var x in p.Skeleton.World)
            {
                h = StableHash.Combine(h, (ulong)BitConverter.DoubleToInt64Bits(x.Origin.X));
                h = StableHash.Combine(h, (ulong)BitConverter.DoubleToInt64Bits(x.Origin.Y));
                h = StableHash.Combine(h, (ulong)BitConverter.DoubleToInt64Bits(x.Origin.Z));
                h = StableHash.Combine(h, (ulong)BitConverter.DoubleToInt64Bits(x.Basis.C0.X));
            }
            return h;
        }

        private static void MotionFramerateIndependence(TestContext ctx, CreatureRegistry reg, CreatureFactory factory)
        {
            foreach (var f in reg.Families)
            {
                var m = factory.Build(GenomeFactory.Sample(f, 8080));
                // exactly 3 seconds of simulation fed with 60 fps, 144 fps and irregular frame times
                var a = Scenario(reg, m, i => 1.0 / 60.0, 3.0, out long sa);
                var b = Scenario(reg, m, i => 1.0 / 144.0, 3.0, out long sb);
                var c = Scenario(reg, m, i => (i % 3 == 0 ? 0.031 : 0.007), 3.0, out long sc);
                long steps = Math.Min(sa, Math.Min(sb, sc));
                // run all to the same step count
                void Advance(CreatureMotor motor, long have)
                {
                    for (long k = have; k < steps + 3; k++)
                    {
                        motor.DesiredVelocity = Vec3.Zero;
                        motor.Step(FixedClock.DefaultStep);
                    }
                }
                Advance(a, sa); Advance(b, sb); Advance(c, sc);
                ctx.Equal(a.StepCount, b.StepCount, $"{f.Id}: step count");
                ctx.Equal(PoseHash(a.Pose), PoseHash(b.Pose), $"{f.Id}: 60 vs 144 fps pose");
                ctx.Equal(PoseHash(a.Pose), PoseHash(c.Pose), $"{f.Id}: 60 fps vs irregular frames pose");
            }
        }

        private static void FootSliding(TestContext ctx, CreatureRegistry reg, CreatureFactory factory)
        {
            double worst = 0;
            int checkedContacts = 0;
            foreach (var f in reg.Families)
            {
                for (int s = 0; s < 6; s++)
                {
                    var m = factory.Build(GenomeFactory.Sample(f, (ulong)(300 + s)));
                    if (m.Anatomy.Rig.Locomotion != LocomotionKind.Legged || m.Anatomy.Rig.Legs.Count == 0) continue;
                    var motor = new CreatureMotor(m, reg);
                    motor.Teleport(Vec3.Zero, 0);
                    var prev = new Dictionary<int, Vec3>();
                    for (int i = 0; i < 360; i++)
                    {
                        double t = i / 60.0;
                        // walk, run, turn and stop
                        double speed = t < 2 ? m.Motion.WalkSpeed : (t < 4 ? m.Motion.RunSpeed : 0);
                        double yaw = t < 3 ? 0 : 1.2;
                        motor.DesiredVelocity = new Vec3(DMath.Cos(yaw), 0, -DMath.Sin(yaw)) * speed;
                        motor.Step(FixedClock.DefaultStep);
                        var ctxM = motor.Context;
                        foreach (var c in motor.Pose.Contacts)
                        {
                            if (!c.Planted) { prev.Remove(c.Leg); continue; }
                            Vec3 w = ctxM.CreatureToWorld(c.Position);
                            if (prev.TryGetValue(c.Leg, out Vec3 pw))
                            {
                                double slide = new Vec2(w.X - pw.X, w.Z - pw.Z).Length;
                                worst = Math.Max(worst, slide);
                                checkedContacts++;
                            }
                            prev[c.Leg] = w;
                        }
                    }
                }
            }
            ctx.Metric("maxPlantedSlidePx", worst);
            ctx.Metric("contactSamples", checkedContacts);
            ctx.True(worst < 0.05, $"planted feet slid by {worst:0.000} px");
            // A shallow world-space slope must also meet each planted foot when the root moves.
            var slopeModel = factory.Build(GenomeFactory.Sample(reg.GetFamily("quadruped"), 26));
            var slopeMotor = new CreatureMotor(slopeModel, reg);
            slopeMotor.Context.GroundHeightAt = (x, z) => x * 0.04 + z * 0.03;
            slopeMotor.DesiredVelocity = new Vec3(slopeModel.Motion.WalkSpeed, 0, 0);
            double worstHeight = 0;
            int slopeContacts = 0;
            for (int i = 0; i < 180; i++)
            {
                slopeMotor.Step(FixedClock.DefaultStep);
                var context = slopeMotor.Context;
                foreach (var c in slopeMotor.Pose.Contacts.Where(c => c.Planted))
                {
                    var world = context.CreatureToWorld(c.Position);
                    double rootHeight = context.GroundHeightAt(context.Position.X, context.Position.Z);
                    double groundHeight = context.GroundHeightAt(world.X, world.Z);
                    worstHeight = Math.Max(worstHeight, Math.Abs(world.Y + rootHeight - groundHeight));
                    slopeContacts++;
                }
            }
            ctx.Metric("maxSlopeContactErrorPx", worstHeight);
            ctx.True(slopeContacts > 100 && worstHeight < 0.05, $"feet must follow a shallow slope: {worstHeight:0.000} px");
        }

        private static void CanvasSafety(TestContext ctx, CreatureRegistry reg, CreatureFactory factory)
        {
            var renderer = new CreatureRenderer();
            int overflow = 0, frames = 0;
            var problems = new List<string>();
            foreach (var f in reg.Families)
            {
                for (int s = 0; s < 8; s++)
                {
                    var m = factory.Build(GenomeFactory.Sample(f, (ulong)(900 + s * 17)));
                    var c = m.Anatomy.Canvas;
                    var frame = new PixelFrame(c.Width, c.Height) { OriginX = c.OriginX, OriginY = c.OriginY };
                    var motor = new CreatureMotor(m, reg);
                    for (int i = 0; i < 60 * 7; i++)
                    {
                        double t = i / 60.0;
                        int dir = (int)(t * 1.5) % 8;
                        motor.FacingOverride = PixelCamera.DirectionYaw(dir);
                        motor.DesiredVelocity = t > 1 && t < 2.5 ? new Vec3(DMath.Cos(dir * 0.785), 0, -DMath.Sin(dir * 0.785)) * m.Motion.RunSpeed : Vec3.Zero;
                        if (i == 60 * 3) motor.TriggerAction();
                        if (i == 60 * 4) motor.TriggerHit(new Vec3(1, 0, 0));
                        motor.Resting = t > 4.6 && t < 6.2;
                        motor.Step(FixedClock.DefaultStep);
                        if (i % 6 != 0) continue;
                        var st = renderer.Render(m.Anatomy, m.Palette, m.Surface, motor.Pose, ViewSpec.Default(motor.Yaw), frame);
                        frames++;
                        if (st.Overflow)
                        {
                            overflow++;
                            if (problems.Count < 6) problems.Add($"{f.Id}/{s} t={t:0.0} L{st.OverflowLeft} R{st.OverflowRight} T{st.OverflowTop} B{st.OverflowBottom}");
                        }
                    }
                }
            }
            ctx.Metric("framesChecked", frames);
            ctx.Metric("overflowFrames", overflow);
            ctx.True(overflow == 0, "parts left the canvas: " + string.Join("; ", problems));
        }

        /// <summary>
        /// Every gene at its extremes (all minimum, all maximum, alternating) and every option of every
        /// choice gene: the creature must build, stay finite, move through all states without leaving
        /// its canvas and render visible pixels. Extremes are legal genomes (the editor allows them).
        /// </summary>
        /// <summary>Variant 0: all genes minimal, 1: all maximal, 2+: alternating; choices cycle through all options.</summary>
        public static CreatureGenome ExtremeGenome(ICreatureFamily f, int variant)
        {
            var schema = f.Schema;
            var baseGenome = GenomeFactory.Sample(f, (ulong)(900 + variant));
            var values = baseGenome.CopyValues();
            for (int gi = 0; gi < schema.Count; gi++)
            {
                var def = schema.Genes[gi];
                bool high = (variant % 3) switch { 0 => false, 1 => true, _ => ((gi + variant) & 1) == 0 };
                switch (def.Kind)
                {
                    case GeneKind.Float:
                    case GeneKind.Int:
                        values[gi] = high ? def.Max : def.Min;
                        break;
                    case GeneKind.Bool:
                        values[gi] = high ? 1 : 0;
                        break;
                    case GeneKind.Choice:
                        values[gi] = (variant + gi) % def.Options.Count;
                        break;
                }
            }
            return baseGenome.WithValues(values).WithName($"Extrem {variant}");
        }

        private static void ExtremeGenes(TestContext ctx, CreatureRegistry reg, CreatureFactory factory)
        {
            var renderer = new CreatureRenderer();
            var problems = new List<string>();
            int cases = 0, frames = 0, maxCanvas = 0;
            foreach (var f in reg.Families)
            {
                var schema = f.Schema;
                int maxOptions = schema.Genes.Where(d => d.Kind == GeneKind.Choice).Select(d => d.Options.Count).DefaultIfEmpty(1).Max();
                int variants = Math.Max(6, maxOptions);
                for (int variant = 0; variant < variants; variant++)
                {
                    var genome = ExtremeGenome(f, variant);
                    string label = $"{f.Id}/v{variant}({genome.GetChoice("form")})";
                    cases++;
                    try
                    {
                        var m = factory.Build(genome);
                        var a = m.Anatomy;
                        bool finite = a.Primitives.All(p => p.LocalA.IsFinite && p.LocalB.IsFinite && p.LocalC.IsFinite && p.Radii.IsFinite && DMath.IsFinite(p.RadiusA) && DMath.IsFinite(p.RadiusB));
                        if (!finite) { problems.Add(label + ": non-finite primitive"); continue; }
                        // all-maximum genomes are allowed to be larger than sampled creatures, but bounded
                        maxCanvas = Math.Max(maxCanvas, Math.Max(a.Canvas.Width, a.Canvas.Height));
                        if (a.Canvas.Width > 400 || a.Canvas.Height > 400) { problems.Add($"{label}: canvas {a.Canvas.Width}x{a.Canvas.Height}"); continue; }
                        var c = a.Canvas;
                        var frame = new PixelFrame(c.Width, c.Height) { OriginX = c.OriginX, OriginY = c.OriginY };
                        var motor = new CreatureMotor(m, reg);
                        double yaw = PixelCamera.DirectionYaw(variant % 8);
                        motor.Teleport(Vec3.Zero, yaw);
                        bool visible = false;
                        string? issue = null;
                        for (int i = 0; i < 60 * 6 && issue == null; i++)
                        {
                            double t = i / 60.0;
                            int dir = (variant + (int)(t * 2)) % 8;
                            double dyaw = PixelCamera.DirectionYaw(dir);
                            motor.FacingOverride = dyaw;
                            double speed = t < 1 ? 0 : (t < 2 ? m.Motion.WalkSpeed : (t < 3 ? m.Motion.RunSpeed : 0));
                            motor.DesiredVelocity = new Vec3(DMath.Cos(dyaw), 0, -DMath.Sin(dyaw)) * speed;
                            if (i == 60 * 3) motor.TriggerAction();
                            if (i == 60 * 4) motor.TriggerHit(new Vec3(1, 0, 0));
                            motor.Resting = t > 4.4 && t < 5.6;
                            motor.Step(FixedClock.DefaultStep);
                            if (i % 10 != 0) continue;
                            var world = motor.Pose.Skeleton.World;
                            for (int b = 0; b < world.Length; b++)
                                if (!world[b].Origin.IsFinite) { issue = $"non-finite bone at t={t:0.00}"; break; }
                            if (issue != null) break;
                            var st = renderer.Render(a, m.Palette, m.Surface, motor.Pose, ViewSpec.Default(motor.Yaw), frame);
                            frames++;
                            if (st.Overflow) issue = $"left the canvas at t={t:0.00} (L{st.OverflowLeft} R{st.OverflowRight} T{st.OverflowTop} B{st.OverflowBottom})";
                            if (frame.ContentBounds(out _, out _, out _, out _)) visible = true;
                        }
                        if (issue == null && !visible) issue = "no visible pixels";
                        if (issue != null) problems.Add(label + ": " + issue);
                    }
                    catch (Exception e)
                    {
                        problems.Add($"{label}: {e.GetType().Name} {e.Message}");
                    }
                }
            }
            ctx.Metric("cases", cases);
            ctx.Metric("framesChecked", frames);
            ctx.Metric("maxCanvas", maxCanvas);
            ctx.True(problems.Count == 0, $"{problems.Count} of {cases} extreme genomes failed: " + string.Join("; ", problems.Take(12)));
        }

        /// <summary>
        /// Facing changes while standing still must turn the whole creature. Trail based bodies
        /// (snakes, worms) cannot pivot, their head has to crawl around and the body must follow.
        /// </summary>
        private static void TurnInPlace(TestContext ctx, CreatureRegistry reg, CreatureFactory factory)
        {
            var problems = new List<string>();
            int creatures = 0, trailBodies = 0;
            Vec3 want = PixelCamera.YawRotation(DMath.Pi).Mul(Vec3.UnitX);
            foreach (var f in reg.Families)
            {
                for (int s = 0; s < 4; s++)
                {
                    var m = factory.Build(GenomeFactory.Sample(f, (ulong)(500 + s * 13)));
                    var rig = m.Anatomy.Rig;
                    var motor = new CreatureMotor(m, reg);
                    motor.Teleport(Vec3.Zero, 0);
                    motor.FacingOverride = 0;
                    for (int i = 0; i < 60; i++) motor.Step(FixedClock.DefaultStep);
                    motor.FacingOverride = DMath.Pi;
                    for (int i = 0; i < 60 * 6; i++) motor.Step(FixedClock.DefaultStep);
                    creatures++;
                    if (Math.Abs(DMath.WrapAngle(motor.Yaw - DMath.Pi)) > 0.05)
                        problems.Add($"{f.Id}/{s}: yaw {motor.Yaw:0.00}");
                    if (rig.LocomotionModule == "serpentine" && rig.Body != null && rig.Body.Bones.Length > 2)
                    {
                        trailBodies++;
                        var yawM = PixelCamera.YawRotation(motor.Yaw);
                        var world = motor.Pose.Skeleton.World;
                        Vec3 head = yawM.Mul(world[rig.Body.Bones[0]].Origin);
                        Vec3 mid = yawM.Mul(world[rig.Body.Bones[rig.Body.Bones.Length / 2]].Origin);
                        Vec3 d = new Vec3(head.X - mid.X, 0, head.Z - mid.Z).NormalizedOr(Vec3.Zero);
                        double dot = Vec3.Dot(d, want);
                        if (dot < 0.6) problems.Add($"{f.Id}/{s}: body not turned (dot {dot:0.00})");
                    }
                }
            }
            ctx.Metric("creatures", creatures);
            ctx.Metric("trailBodies", trailBodies);
            ctx.True(trailBodies > 0, "no trail based body was checked");
            ctx.True(problems.Count == 0, string.Join("; ", problems));
        }
    }

    /// <summary>Golden reference values (produced on Linux x64; must match on every platform).</summary>
    public static partial class CoreTests
    {
        // ------------------------------------------------------------------ biomes

        private static void BiomeSampling(TestContext ctx, CreatureRegistry reg, CreatureFactory factory, int seeds)
        {
            ctx.True(reg.Biomes.Count >= 6, $"built-in biomes registered ({reg.Biomes.Count})");
            ctx.True(reg.GetBiome("unknown") == Biomes.Temperate && reg.GetBiome(null) == Biomes.Temperate, "unknown ids fall back to temperate");
            foreach (var f in reg.Families)
            {
                for (int i = 0; i < 4; i++)
                {
                    ulong seed = 3000UL + (ulong)i * 977;
                    // the temperate biome (and null) must not change a single gene: existing seeds stay valid
                    ctx.Equal(GenomeFactory.Sample(f, seed).ContentHash, GenomeFactory.Sample(f, seed, Biomes.Temperate).ContentHash, $"{f.Id}/{seed}: temperate is neutral");
                }
            }
            foreach (var b in reg.Biomes)
            {
                if (b.IsNeutralSampling) continue;
                foreach (var f in reg.Families)
                {
                    for (int i = 0; i < seeds; i++)
                    {
                        ulong seed = 5000UL + (ulong)i * 7919;
                        var g = GenomeFactory.Sample(f, seed, b);
                        ctx.Equal(g.ContentHash, GenomeFactory.Sample(f, seed, b).ContentHash, $"{b.Id}/{f.Id}/{seed}: deterministic");
                        CreatureModel m;
                        try { m = factory.Build(g); }
                        catch (Exception e) { ctx.True(false, $"{b.Id}/{f.Id}/{seed}: build failed: {e.Message}"); continue; }
                        ctx.True(m.Anatomy.Primitives.Count > 0, $"{b.Id}/{f.Id}/{seed}: anatomy");
                    }
                }
            }
            // the biases do what the biomes promise
            double Mean(string family, Biome? b, Func<CreatureGenome, double> value, int n = 160)
            {
                var fam = reg.GetFamily(family);
                double sum = 0;
                for (int i = 0; i < n; i++) sum += value(GenomeFactory.Sample(fam, 900UL + (ulong)i * 31, b));
                return sum / n;
            }
            double Share(string family, Biome? b, string arch) => Mean(family, b, g => g.GetChoice("form") == arch ? 1 : 0);
            ctx.True(Share("flora", Biomes.Desert, "cactus") > Share("flora", null, "cactus") * 3, "desert: more cacti");
            ctx.True(Share("flora", Biomes.Snow, "pine") > Share("flora", null, "pine") * 1.8, "snow: more conifers");
            ctx.True(Share("flora", Biomes.Snow, "palm") == 0, "snow: no palms");
            ctx.True(Share("biped", Biomes.Snow, "yeti") > Share("biped", null, "yeti") * 3, "snow: more yetis");
            ctx.True(Share("serpent", Biomes.Desert, "sandworm") > Share("serpent", null, "sandworm") * 2.5, "desert: more sandworms");
            ctx.True(Share("reptile", Biomes.Swamp, "crocodile") > Share("reptile", null, "crocodile") * 2, "swamp: more crocodiles");
            ctx.True(Mean("flora", Biomes.Snow, g => g.Get("snow")) > 0.6, "snow: snowy crowns");
            ctx.True(Mean("quadruped", Biomes.Snow, g => g.Get("color.value")) > Mean("quadruped", null, g => g.Get("color.value")) + 0.12, "snow: lighter coats");
            ctx.True(Mean("quadruped", Biomes.Volcanic, g => g.Get("color.value")) < Mean("quadruped", null, g => g.Get("color.value")) - 0.08, "volcanic: darker coats");
            ctx.True(Mean("quadruped", Biomes.Desert, g => g.Get("head.earSize")) > Mean("quadruped", null, g => g.Get("head.earSize")) + 0.08, "desert: bigger ears");
            ctx.True(Mean("quadruped", Biomes.Jungle, g => g.Get("color.saturation")) > Mean("quadruped", null, g => g.Get("color.saturation")) + 0.08, "jungle: vivid coats");
            // a biome never empties a family: a scope excluding every archetype falls back to the family weights
            var none = new Biome("test-none", "None", "excludes every flora form");
            foreach (var a in reg.GetFamily("flora").Archetypes) none.Archetype("flora", a.Id, 0);
            ctx.True(GenomeFactory.Sample(reg.GetFamily("flora"), 7, none).Schema.Contains("form"), "all-zero archetype weights fall back");
        }

        private static void BiomeOverlayRender(TestContext ctx, CreatureRegistry reg, CreatureFactory factory)
        {
            double yaw = PixelCamera.DirectionYaw(7);
            foreach (var id in new[] { "quadruped", "flora", "prop", "biped" })
            {
                var m = factory.Build(GenomeFactory.Sample(reg.GetFamily(id), 2024));
                var pose = new CreaturePose(m.Anatomy.Skeleton);
                // no overlay: same palette instance, bit-identical image
                ctx.True(ReferenceEquals(m.PaletteFor(BiomeOverlay.None), m.Palette), $"{id}: no overlay keeps the palette");
                var plain = RenderOverlay(m, pose, yaw, BiomeOverlay.None, 2, out _);
                var c = m.Anatomy.Canvas.Scaled(2);
                var reference = new PixelFrame(c.Width, c.Height) { OriginX = c.OriginX, OriginY = c.OriginY };
                new CreatureRenderer().Render(m.Anatomy, m.Palette, m.Surface, pose, ViewSpec.Default(yaw, 2), reference);
                ctx.Equal(reference.ContentHash(), plain.ContentHash(), $"{id}: inactive overlay renders identically");
                foreach (var b in reg.Biomes)
                {
                    var o = b.OverlayFor(m);
                    if (!o.IsActive) continue;
                    var f = RenderOverlay(m, pose, yaw, o, 2, out var pal);
                    ctx.Equal(OverlayRenderHash(m, pose, yaw, o, 2), OverlayRenderHash(m, pose, yaw, o, 2), $"{id}/{b.Id}: deterministic");
                    ctx.True(pal.HasOverlay && ReferenceEquals(pal, m.PaletteFor(o)), $"{id}/{b.Id}: cached overlay palette");
                    // silhouettes are unchanged: the overlay only recolours covered pixels
                    int changedCoverage = 0, coverPixels = 0, grimePixels = 0;
                    var cover = pal.CoverRamp != null ? new HashSet<byte>(pal.CoverRamp.Skip(1)) : new HashSet<byte>();
                    var grime = pal.GrimeRamp != null ? new HashSet<byte>(pal.GrimeRamp.Skip(1)) : new HashSet<byte>();
                    for (int i = 0; i < f.Indices.Length; i++)
                    {
                        if ((f.Indices[i] == 0) != (reference.Indices[i] == 0)) changedCoverage++;
                        if (cover.Contains(f.Indices[i])) coverPixels++;
                        else if (grime.Contains(f.Indices[i])) grimePixels++;
                    }
                    ctx.Equal(0, changedCoverage, $"{id}/{b.Id}: silhouette unchanged");
                    if (o.HasCover && o.CoverAmount >= 0.3) ctx.True(coverPixels > 20, $"{id}/{b.Id}: cover visible ({coverPixels} px)");
                    if (o.HasGrime && o.GrimeAmount >= 0.3 && (id == "quadruped" || id == "biped")) ctx.True(grimePixels > 10, $"{id}/{b.Id}: grime visible ({grimePixels} px)");
                    ctx.True(pal.Colors.Count <= 255, $"{id}/{b.Id}: palette size");
                }
            }
            // eyes are drawn after the overlay: a creature's eye colours survive heavy snow
            var q = factory.Build(GenomeFactory.Sample(reg.GetFamily("quadruped"), 2024));
            var qp = new CreaturePose(q.Anatomy.Skeleton);
            var heavy = Biomes.Snow.Overlay.Scaled(2);
            var snowy = RenderOverlay(q, qp, yaw, heavy, 2, out _);
            var clean = RenderOverlay(q, qp, yaw, BiomeOverlay.None, 2, out _);
            var eyeIdx = new HashSet<byte>();
            for (int s2 = 0; s2 < 6; s2++) eyeIdx.Add(q.Palette.Index(PixelCreatures.Core.Anatomy.MaterialSlot.Eye, s2));
            int eyesClean = clean.Indices.Count(eyeIdx.Contains), eyesSnow = snowy.Indices.Count(eyeIdx.Contains);
            ctx.True(eyesSnow >= eyesClean * 0.9, $"eyes stay visible under snow ({eyesSnow}/{eyesClean})");
            // swimmers carry no snow
            var fish = factory.Build(GenomeFactory.Sample(reg.GetFamily("aquatic"), 5));
            ctx.True(!Biomes.Snow.OverlayFor(fish).HasCover, "swimmers get no cover");
        }

        private static void ExportLayout(TestContext ctx, CreatureRegistry reg, CreatureFactory factory)
        {
            var m = factory.Build(GenomeFactory.Sample(reg.GetFamily("prop"), 11));
            SpritesheetResult Run(bool compact, int maxPage, BiomeOverlay overlay)
            {
                var o = new SpritesheetOptions { Fps = 12, CompactFrames = compact, MaxPageSize = maxPage, Overlay = overlay };
                o.Clips.Clear(); o.Clips.Add(ClipKind.Idle); o.Clips.Add(ClipKind.Action);
                o.Directions.Clear(); o.Directions.Add(7); o.Directions.Add(2);
                return SpritesheetExporter.Export(m, reg, o, "t");
            }
            var full = Run(false, 8192, BiomeOverlay.None);
            var compact = Run(true, 8192, BiomeOverlay.None);
            ctx.Equal(full.Pages.Count, compact.Pages.Count, "compact: page count");
            for (int i = 0; i < full.Pages.Count; i++)
                ctx.True(full.Pages[i].rgba.AsSpan().SequenceEqual(compact.Pages[i].rgba), $"compact: page {i} identical");
            ctx.Equal(full.MetadataJson, compact.MetadataJson, "compact: metadata identical");
            // narrow pages: clips wrap onto further rows, every frame still lies inside its page
            int limit = full.FrameWidth * 5;
            var wrapped = Run(true, limit, BiomeOverlay.None);
            foreach (var (_, w, h) in wrapped.Pages) ctx.True(w <= limit, $"page width {w} <= {limit}");
            foreach (var clip in wrapped.Clips)
                foreach (var f in clip.Frames)
                {
                    var (_, pw, ph) = wrapped.Pages[f.Page];
                    ctx.True(f.X + wrapped.FrameWidth <= pw && f.Y + wrapped.FrameHeight <= ph, $"{clip.Name}: frame inside page");
                }
            // the same frames, only rearranged: compare one frame's pixels in both layouts
            var a = full.Clips[1].Frames[7];
            var b = wrapped.Clips[1].Frames[7];
            bool same = true;
            for (int y = 0; y < full.FrameHeight && same; y++)
                for (int x = 0; x < full.FrameWidth * 4; x++)
                {
                    var pa = full.Pages[a.Page]; var pb = wrapped.Pages[b.Page];
                    if (pa.rgba[((a.Y + y) * pa.width) * 4 + a.X * 4 + x] != pb.rgba[((b.Y + y) * pb.width) * 4 + b.X * 4 + x]) { same = false; break; }
                }
            ctx.True(same, "wrapped frame pixels identical");
            // overlay export: different colours, the biome is recorded
            var snowy = Run(true, 8192, Biomes.Snow.OverlayFor(m));
            ctx.True(!snowy.Pages[0].rgba.AsSpan().SequenceEqual(full.Pages[0].rgba), "overlay changes the sheet");
            ctx.True(snowy.MetadataJson.Contains("\"overlay\""), "overlay recorded in the metadata");
            ctx.True(!full.MetadataJson.Contains("\"overlay\"") && !full.MetadataJson.Contains("\"biome\""), "plain export metadata unchanged");
        }
    }

    public static class Golden
    {
        public static readonly uint[] Pcg42 = { 1210624746u, 4125136630u, 2074946571u, 3309376910u };
        public static readonly ulong Fnv = 114984965007321940UL;
        public static readonly ulong Derived = 16299427822044037963UL;
        /// <summary>Regenerate with: CoreHarness goldens (after an intended change of generation rules).</summary>
        public static readonly Dictionary<string, ulong> Values = new Dictionary<string, ulong>(StringComparer.Ordinal)
        {
            ["quadruped:genome"] = 0xb347eafec5ed47edUL,
            ["quadruped"] = 0xc24f96a97be5a1f5UL,
            ["quadruped:hd"] = 0x64e8e8ad7de7b9efUL,
            ["quadruped:walk"] = 0x37257c62ce5e5420UL,
            ["biped:genome"] = 0x5ef08293f551f95aUL,
            ["biped"] = 0x9d5f6ba037ea81f1UL,
            ["biped:hd"] = 0x4522d5dff5003069UL,
            ["biped:walk"] = 0x5f13d3534d41b54fUL,
            ["reptile:genome"] = 0x53993ac7a73b23b3UL,
            ["reptile"] = 0x5207d3574a0a7b7bUL,
            ["reptile:hd"] = 0x2bf9f9508b0d4873UL,
            ["reptile:walk"] = 0x56604dac7c734731UL,
            ["arthropod:genome"] = 0x17a527c0f16e660eUL,
            ["arthropod"] = 0x653189d69a32ee55UL,
            ["arthropod:hd"] = 0xeab2c1865c61e5d5UL,
            ["arthropod:walk"] = 0xa4df8330c029a0a1UL,
            ["winged:genome"] = 0x773625d175b173b9UL,
            ["winged"] = 0x1ddf0f2d627a3dc8UL,
            ["winged:hd"] = 0xaab178d09b037b4aUL,
            ["winged:walk"] = 0xd1ba207325472d3fUL,
            ["serpent:genome"] = 0xb6bd6dcb29822ee6UL,
            ["serpent"] = 0x76e6faaf990bc548UL,
            ["serpent:hd"] = 0x5f54e5c78bcd9547UL,
            ["serpent:walk"] = 0x228b7ca9a57b9021UL,
            ["aquatic:genome"] = 0x44b268859a62d96fUL,
            ["aquatic"] = 0xd8ea4b1e8583f131UL,
            ["aquatic:hd"] = 0xadaa62a0af5c90fdUL,
            ["aquatic:walk"] = 0x52c411ea737c831eUL,
            ["amorphous:genome"] = 0x96fa889d3f873a1eUL,
            ["amorphous"] = 0x29545e9a6eb3f823UL,
            ["amorphous:hd"] = 0xa71e7a4b75852f93UL,
            ["amorphous:walk"] = 0x62f57528386e2cbbUL,
            ["mythical:genome"] = 0xbcbaecd93437f83cUL,
            ["mythical"] = 0xd0fa3a771cc60eb5UL,
            ["mythical:hd"] = 0x76436701da97de94UL,
            ["mythical:walk"] = 0x32f26b862782e23bUL,
            ["monster:genome"] = 0x07a80d7f762120e3UL,
            ["monster"] = 0x8a3f0ca4a5b9e468UL,
            ["monster:hd"] = 0xd1931dc29e9e3e66UL,
            ["monster:walk"] = 0x658b33ddabd8772aUL,
            ["plantfolk:genome"] = 0x5e86703c03aa3251UL,
            ["plantfolk"] = 0x0365c2d8cc0518cbUL,
            ["plantfolk:hd"] = 0x1612d9af113af59aUL,
            ["plantfolk:walk"] = 0xacc0ea74aee3ec28UL,
            ["flora:genome"] = 0xd708d6ef4c122d91UL,
            ["flora"] = 0x6fc6748d2501495eUL,
            ["flora:hd"] = 0x4d6ef30baf4f75e0UL,
            ["flora:walk"] = 0xf554ed01d02460deUL,
            ["prop:genome"] = 0x2ec4d72d44ffa414UL,
            ["prop"] = 0x5b1ccd5f0e219b9eUL,
            ["prop:hd"] = 0xbfc9e0794ced2eb2UL,
            ["prop:walk"] = 0x441e784d8ef20b5bUL,
            ["quadruped:biome:desert"] = 0x5ee66a341ff4ccf3UL,
            ["quadruped:biome:desert:render"] = 0xcff9ae2099102e5bUL,
            ["biped:biome:desert"] = 0x8ff78225e572643cUL,
            ["reptile:biome:desert"] = 0xdf2f5a144c016c04UL,
            ["arthropod:biome:desert"] = 0x064167f74b1e23aeUL,
            ["winged:biome:desert"] = 0xb39098b659901858UL,
            ["serpent:biome:desert"] = 0xa69b296bb9182e52UL,
            ["aquatic:biome:desert"] = 0x5e36c5280707a894UL,
            ["amorphous:biome:desert"] = 0xe3dcca2a2f2f825fUL,
            ["mythical:biome:desert"] = 0x39dcf94873feb744UL,
            ["monster:biome:desert"] = 0x309fe3f2d5b50430UL,
            ["plantfolk:biome:desert"] = 0x2e6223b9db0c3949UL,
            ["flora:biome:desert"] = 0x0d05d047109dfd48UL,
            ["flora:biome:desert:render"] = 0x2db527e6268efc17UL,
            ["prop:biome:desert"] = 0x75bc5b0e071542feUL,
            ["prop:biome:desert:render"] = 0x93d656bae909ea06UL,
            ["quadruped:biome:snow"] = 0xf95ac0d840ee9802UL,
            ["quadruped:biome:snow:render"] = 0x15ead7cad8e2e685UL,
            ["biped:biome:snow"] = 0x350e7f7798a0a857UL,
            ["reptile:biome:snow"] = 0xe4b634fb619a71f6UL,
            ["arthropod:biome:snow"] = 0xfa3ab8f42f8989f2UL,
            ["winged:biome:snow"] = 0xd6024e253306036dUL,
            ["serpent:biome:snow"] = 0x7ae1119987c76fe9UL,
            ["aquatic:biome:snow"] = 0x5c64b5749e8eab95UL,
            ["amorphous:biome:snow"] = 0xa5638acd5d2c9e24UL,
            ["mythical:biome:snow"] = 0x5ebc2544825d191eUL,
            ["monster:biome:snow"] = 0xf09e0f447a2177d2UL,
            ["plantfolk:biome:snow"] = 0x7c38f112d931c19eUL,
            ["flora:biome:snow"] = 0x5a5f0e667e79971aUL,
            ["flora:biome:snow:render"] = 0xc5f772fb5b97da41UL,
            ["prop:biome:snow"] = 0xc0e5302a4a1617c5UL,
            ["prop:biome:snow:render"] = 0x7a7b82c5059f77cdUL,
            ["quadruped:biome:jungle"] = 0x923441c4fe5e2cddUL,
            ["quadruped:biome:jungle:render"] = 0xcb6ffc5326416b60UL,
            ["biped:biome:jungle"] = 0xc715c4213155a424UL,
            ["reptile:biome:jungle"] = 0x2e1453c4d2b74984UL,
            ["arthropod:biome:jungle"] = 0x0e05d7a405464010UL,
            ["winged:biome:jungle"] = 0x071e4cfcad2bd075UL,
            ["serpent:biome:jungle"] = 0x825afec513e7ab32UL,
            ["aquatic:biome:jungle"] = 0x5c83bd7ee2eba8e1UL,
            ["amorphous:biome:jungle"] = 0xdbfdbbfc44fa188fUL,
            ["mythical:biome:jungle"] = 0x0c0b06e0a813d037UL,
            ["monster:biome:jungle"] = 0x07ba390675489e80UL,
            ["plantfolk:biome:jungle"] = 0x8a5d421e9a8925e3UL,
            ["flora:biome:jungle"] = 0x75b56adc4383e92aUL,
            ["flora:biome:jungle:render"] = 0x9071fb4e623ab36cUL,
            ["prop:biome:jungle"] = 0x909ba9052ec89a30UL,
            ["prop:biome:jungle:render"] = 0xd8c78faca97fa4fcUL,
            ["quadruped:biome:swamp"] = 0xc22a6038352c163fUL,
            ["quadruped:biome:swamp:render"] = 0xc6f68576b7bba07fUL,
            ["biped:biome:swamp"] = 0x1ed4c7916b1cabfeUL,
            ["reptile:biome:swamp"] = 0x07f0ca1ab22dfab6UL,
            ["arthropod:biome:swamp"] = 0x02855e97ec751548UL,
            ["winged:biome:swamp"] = 0x0965534039e957aeUL,
            ["serpent:biome:swamp"] = 0xabc4a770f5d1bd3aUL,
            ["aquatic:biome:swamp"] = 0x3f86dd54ae770f33UL,
            ["amorphous:biome:swamp"] = 0x35a660ff33c73d9dUL,
            ["mythical:biome:swamp"] = 0x7c01b8865499583cUL,
            ["monster:biome:swamp"] = 0x22872997553f5ec8UL,
            ["plantfolk:biome:swamp"] = 0xc106a916c4fe41f0UL,
            ["flora:biome:swamp"] = 0xd83d774defdc997aUL,
            ["flora:biome:swamp:render"] = 0x94792bb7ffe6213cUL,
            ["prop:biome:swamp"] = 0x34dd66aeb0c0a41dUL,
            ["prop:biome:swamp:render"] = 0xb005c2c7de57a283UL,
            ["quadruped:biome:volcanic"] = 0xe4eef1fd3782402dUL,
            ["quadruped:biome:volcanic:render"] = 0xb96744bc700de830UL,
            ["biped:biome:volcanic"] = 0xb71b7fc9cdf2ac7cUL,
            ["reptile:biome:volcanic"] = 0x61df2f3b5a7de66fUL,
            ["arthropod:biome:volcanic"] = 0xbf98141b7b662c25UL,
            ["winged:biome:volcanic"] = 0xfee994ddb18f2f08UL,
            ["serpent:biome:volcanic"] = 0x8590272001157de8UL,
            ["aquatic:biome:volcanic"] = 0x26e9ac36cbb7340cUL,
            ["amorphous:biome:volcanic"] = 0x5d632eda98784893UL,
            ["mythical:biome:volcanic"] = 0x46cf076e4794646bUL,
            ["monster:biome:volcanic"] = 0x63f273f06b0a9531UL,
            ["plantfolk:biome:volcanic"] = 0x2bf0fb0c171021eeUL,
            ["flora:biome:volcanic"] = 0x443a4feba7e1e2e0UL,
            ["flora:biome:volcanic:render"] = 0xa17c6f7598c03ae6UL,
            ["prop:biome:volcanic"] = 0x28f9802ac120b060UL,
            ["prop:biome:volcanic:render"] = 0xb6d882689e1eff65UL,
        };
    }
}
