// Procedural Pixel Creatures - family "flora": procedural trees and plants.
//
// Trees grow from a curved, tapering trunk with a root flare. Main branches spread in a golden-angle
// spiral and are spring chains, so wind and hits make them sway with follow-through; side branches,
// twigs, leaf clumps, fruit and blossoms ride on them. Canopies are clumps of foliage (their own
// surface style: leaf clusters and, at high pixel density, a leafy silhouette) that blend into one
// lumpy crown. Conifers stack tiered skirts, palms carry feathered fronds, willows hang curtains of
// leaves, and the smaller archetypes (bushes, giant mushrooms, cacti, flowers) reuse the same parts.
// All flora is stationary and uses the "rooted" motion module.

using System;
using System.Collections.Generic;
using PixelCreatures.Core.Anatomy;
using PixelCreatures.Core.Genetics;
using PixelCreatures.Core.Motion;
using PixelCreatures.Core.Palettes;
using PixelCreatures.Core.Shapes;

namespace PixelCreatures.Core.Families
{
    public sealed class FloraFamily : CreatureFamilyBase, ISurfaceCustomizer
    {
        public override string Id => "flora";
        public override string DisplayName => "Trees & Flora";
        public override string Description => "Procedural trees and plants: oaks, pines, birches, willows, palms, dead trees, blossom trees, bushes, giant mushrooms, cacti and flowers. Branches, fronds and leaf curtains sway in the wind.";
        public override int Order => 300;
        public override CreationCategory Category => CreationCategory.Flora;
        public override string PaletteRuleId => FloraPaletteRule.RuleId;

        private static readonly ArchetypeInfo[] ArchetypeList =
        {
            new ArchetypeInfo("oak", "Broadleaf tree", 1.3),
            new ArchetypeInfo("pine", "Conifer", 1.2),
            new ArchetypeInfo("birch", "Birch", 0.9),
            new ArchetypeInfo("willow", "Willow", 0.8),
            new ArchetypeInfo("palm", "Palm", 0.8),
            new ArchetypeInfo("dead", "Dead tree", 0.8),
            new ArchetypeInfo("blossom", "Blossom tree", 0.8),
            new ArchetypeInfo("bush", "Bush", 1.0),
            new ArchetypeInfo("mushroom", "Giant mushroom", 0.8),
            new ArchetypeInfo("cactus", "Cactus", 0.7),
            new ArchetypeInfo("flowers", "Flowers", 0.8),
        };

        public override IReadOnlyList<ArchetypeInfo> Archetypes => ArchetypeList;

        public static readonly (string, string)[] Forms =
        {
            ("oak", "Broadleaf tree"), ("pine", "Conifer"), ("birch", "Birch"), ("willow", "Willow"), ("palm", "Palm"), ("dead", "Dead tree"),
            ("blossom", "Blossom tree"), ("bush", "Bush"), ("mushroom", "Giant mushroom"), ("cactus", "Cactus"), ("flowers", "Flowers"),
        };

        protected override GeneSchema CreateSchema()
        {
            var b = new GeneSchemaBuilder(Id);
            b.Group("growth", "Growth", GeneStream.Anatomy)
             .Choice("form", "Plant type", Forms, "oak", "Growth form used as a template when rerolling.", 0.04)
             .Float("size", "Size", 0, 1, 0.5, "Overall size.", 0.08)
             .Float("trunk.height", "Trunk height", 0, 1, 0.5, "Height of the trunk or stem below the crown.", 0.1)
             .Float("trunk.thickness", "Trunk thickness", 0, 1, 0.5, "", 0.1)
             .Float("trunk.lean", "Lean", 0, 1, 0.2, "How far the trunk leans and curves.", 0.1)
             .Float("trunk.gnarl", "Gnarl", 0, 1, 0.3, "Crooked growth, knots and twisted branches.", 0.1)
             .Float("roots", "Root flare", 0, 1, 0.5, "Visible roots spreading at the base.", 0.1);
            b.Group("branches", "Branches", GeneStream.Anatomy)
             .Float("branch.count", "Branch count", 0, 1, 0.5, "", 0.1)
             .Float("branch.angle", "Branch angle", 0, 1, 0.5, "Upright (0) to spreading (1).", 0.1)
             .Float("branch.length", "Branch length", 0, 1, 0.5, "", 0.1)
             .Float("branch.depth", "Twigginess", 0, 1, 0.5, "Side branches and twigs.", 0.1);
            b.Group("crown", "Crown", GeneStream.Anatomy)
             .Float("canopy.size", "Crown size", 0, 1, 0.5, "Size of the leaf clumps, tiers or fronds.", 0.1)
             .Float("canopy.density", "Crown density", 0, 1, 0.5, "Number of leaf clumps.", 0.1)
             .Float("canopy.droop", "Droop", 0, 1, 0.3, "Hanging leaves, drooping fronds and tiers.", 0.1)
             .Float("fruit", "Fruit & blossoms", 0, 1, 0.2, "Fruit, berries, blossoms or flowers.", 0.12)
             .Float("snow", "Snow", 0, 1, 0, "Snow on the crown.", 0.1);
            b.Group("floracolor", "Foliage & blossom colors", GeneStream.Color)
             .Hue("color.leafHue", "Foliage hue", 132, "Spring green, deep evergreen, autumn gold and red, or fantasy hues.")
             .Float("color.leafSaturation", "Foliage saturation", 0, 1, 0.55, "", 0.12)
             .Float("color.leafValue", "Foliage brightness", 0, 1, 0.5, "", 0.12)
             .Hue("color.bloomHue", "Blossom hue", 350, "Blossoms, fruit, berries and mushroom caps.")
             .Float("color.bloomSaturation", "Blossom saturation", 0, 1, 0.7, "", 0.12)
             .Float("color.bloomValue", "Blossom brightness", 0, 1, 0.6, "", 0.12);
            SharedGenes.AddColorGenes(b);
            SharedGenes.AddPatternGenes(b, "bark");
            return b.Build();
        }

        private sealed class Prior
        {
            public double Size = 0.5, Height = 0.5, Thick = 0.5, Lean = 0.15, Gnarl = 0.3, Roots = 0.5;
            public double Count = 0.5, Angle = 0.5, Length = 0.5, Depth = 0.5, Canopy = 0.5, Density = 0.5, Droop = 0.2, Fruit = 0.1, Snow = 0;
            public double FruitChance = 0.2, SnowChance = 0;
        }

        private static Prior PriorFor(string a) => a switch
        {
            "pine" => new Prior { Size = 0.6, Height = 0.35, Thick = 0.45, Lean = 0.06, Gnarl = 0.1, Roots = 0.4, Count = 0.55, Canopy = 0.55, Density = 0.55, Droop = 0.3, SnowChance = 0.25, FruitChance = 0 },
            "birch" => new Prior { Size = 0.5, Height = 0.65, Thick = 0.25, Lean = 0.15, Gnarl = 0.15, Roots = 0.2, Count = 0.55, Angle = 0.25, Length = 0.45, Depth = 0.5, Canopy = 0.4, Density = 0.55, FruitChance = 0.05 },
            "willow" => new Prior { Size = 0.6, Height = 0.4, Thick = 0.6, Lean = 0.25, Gnarl = 0.45, Roots = 0.6, Count = 0.6, Angle = 0.55, Length = 0.6, Depth = 0.3, Canopy = 0.45, Density = 0.6, Droop = 0.85, FruitChance = 0 },
            "palm" => new Prior { Size = 0.55, Height = 0.75, Thick = 0.35, Lean = 0.55, Gnarl = 0.1, Roots = 0.3, Count = 0.55, Canopy = 0.55, Droop = 0.55, Fruit = 0.5, FruitChance = 0.6 },
            "dead" => new Prior { Size = 0.5, Height = 0.5, Thick = 0.5, Lean = 0.3, Gnarl = 0.75, Roots = 0.7, Count = 0.55, Angle = 0.6, Length = 0.55, Depth = 0.85, Canopy = 0.0, Density = 0, FruitChance = 0 },
            "blossom" => new Prior { Size = 0.45, Height = 0.4, Thick = 0.5, Lean = 0.25, Gnarl = 0.5, Roots = 0.4, Count = 0.55, Angle = 0.65, Length = 0.55, Depth = 0.55, Canopy = 0.45, Density = 0.55, Fruit = 0.8, FruitChance = 1.0 },
            "bush" => new Prior { Size = 0.3, Height = 0.1, Thick = 0.3, Lean = 0.05, Gnarl = 0.2, Roots = 0.1, Count = 0.5, Canopy = 0.5, Density = 0.6, Fruit = 0.4, FruitChance = 0.45 },
            "mushroom" => new Prior { Size = 0.4, Height = 0.5, Thick = 0.6, Lean = 0.2, Gnarl = 0.1, Roots = 0.3, Count = 0.5, Canopy = 0.6, Density = 0.5, Droop = 0.2, Fruit = 0.6, FruitChance = 0.7 },
            "cactus" => new Prior { Size = 0.4, Height = 0.6, Thick = 0.6, Lean = 0.05, Gnarl = 0.05, Roots = 0.0, Count = 0.45, Canopy = 0.4, Fruit = 0.4, FruitChance = 0.5 },
            "flowers" => new Prior { Size = 0.2, Height = 0.5, Thick = 0.3, Lean = 0.3, Gnarl = 0.1, Roots = 0.0, Count = 0.5, Canopy = 0.55, Density = 0.5, Fruit = 1.0, FruitChance = 1.0 },
            _ => new Prior { Size = 0.55, Height = 0.4, Thick = 0.6, Lean = 0.15, Gnarl = 0.4, Roots = 0.6, Count = 0.55, Angle = 0.6, Length = 0.55, Depth = 0.5, Canopy = 0.55, Density = 0.6, Fruit = 0.4, FruitChance = 0.25 },
        };

        public override void SampleAnatomy(double[] v, Rng rng, string arch)
        {
            var p = PriorFor(arch);
            SetChoice(v, "form", arch);
            const double sp = 0.12;
            Around(v, rng, "size", p.Size, 0.13);
            Around(v, rng, "trunk.height", p.Height, sp);
            Around(v, rng, "trunk.thickness", p.Thick, sp);
            Around(v, rng, "trunk.lean", p.Lean, sp);
            Around(v, rng, "trunk.gnarl", p.Gnarl, sp);
            Around(v, rng, "roots", p.Roots, sp);
            Around(v, rng, "branch.count", p.Count, sp);
            Around(v, rng, "branch.angle", p.Angle, sp);
            Around(v, rng, "branch.length", p.Length, sp);
            Around(v, rng, "branch.depth", p.Depth, sp);
            Around(v, rng, "canopy.size", p.Canopy, sp);
            Around(v, rng, "canopy.density", p.Density, sp);
            Around(v, rng, "canopy.droop", p.Droop, sp);
            Set(v, "fruit", rng.Chance(p.FruitChance) ? DMath.Clamp(p.Fruit + rng.Gaussian(0, 0.2, 2), 0.25, 1) : 0);
            Set(v, "snow", rng.Chance(p.SnowChance) ? rng.Range(0.5, 1.0) : 0);
        }

        public override FamilyStyleHints StyleHints(string arch)
        {
            var h = new FamilyStyleHints
            {
                Materials = new Dictionary<string, double> { ["bark"] = 1 },
                Patterns = new Dictionary<string, double> { ["none"] = 4, ["mottled"] = 1 },
                Accents = new Dictionary<string, double> { ["bone"] = 1 },
                Coats = new[] { Coats.Brown(2.5), Coats.Chocolate(1.5), Coats.Grey(0.3), Coats.Olive(0.3), Coats.Rust(0.4) },
                Schemes = new Dictionary<string, double> { ["natural"] = 4, ["analogous"] = 1 },
                // no countershading: the secondary ramp is the foliage
                CountershadeMin = 0, CountershadeMax = 0,
                SocksChance = 0, MaskChance = 0, PiebaldChance = 0, GlowChance = 0.03,
            };
            switch (arch)
            {
                case "birch":
                    h.Coats = new[] { Coats.Cream(3), Coats.Silver(1) };
                    h.Patterns = new Dictionary<string, double> { ["bands"] = 3, ["blotches"] = 1 };
                    break;
                case "dead":
                    h.Coats = new[] { Coats.Grey(2), Coats.Slate(1), Coats.Chocolate(1), Coats.Black(0.4) };
                    break;
                case "palm":
                    h.Coats = new[] { Coats.Tan(2), Coats.Brown(1), Coats.Sand(1) };
                    break;
                case "mushroom":
                    h.Materials = new Dictionary<string, double> { ["hide"] = 1 };
                    h.Coats = new[] { Coats.Cream(3), Coats.Sand(1), Coats.Tan(0.5) };
                    h.GlowChance = 0.3;
                    h.Patterns = new Dictionary<string, double> { ["none"] = 1 };
                    break;
                case "cactus":
                case "flowers":
                case "bush":
                    h.Materials = new Dictionary<string, double> { ["leaf"] = 1 };
                    h.Coats = arch == "bush" ? new[] { Coats.Brown(1), Coats.Chocolate(1) } : new[] { Coats.Green(2), Coats.Olive(1), Coats.Teal(0.5) };
                    h.Patterns = new Dictionary<string, double> { ["none"] = 1 };
                    break;
            }
            return h;
        }

        public override void AdjustSample(double[] values, Rng rng, string arch)
        {
            // foliage: spring green, evergreen, autumn gold/orange/red, rarely fantasy violet or teal
            double leaf = arch switch
            {
                "pine" => PickHue(rng, (150, 3), (165, 1.5), (135, 1)),
                "palm" => PickHue(rng, (128, 3), (110, 1)),
                "cactus" => PickHue(rng, (140, 2), (160, 1.5), (120, 1)),
                "dead" => PickHue(rng, (75, 1)),
                "birch" => PickHue(rng, (120, 3), (95, 1.2), (75, 1)),
                _ => PickHue(rng, (132, 4), (145, 2), (95, 1.2), (65, 1.0), (40, 0.8), (28, 0.6), (300, 0.25), (195, 0.2)),
            };
            Set(values, "color.leafHue", leaf + rng.Gaussian(0, 5, 2));
            bool autumn = leaf < 100 && leaf > 20;
            Set(values, "color.leafSaturation", autumn ? rng.Range(0.6, 0.9) : rng.Range(0.4, 0.75));
            Set(values, "color.leafValue", arch == "pine" ? rng.Range(0.25, 0.45) : rng.Range(0.38, 0.62));
            // blossoms and fruit: cherry pink, apple red, orange, lemon, plum violet, white
            double bloom = arch switch
            {
                "mushroom" => PickHue(rng, (27, 3), (55, 1.2), (78, 1), (285, 0.6), (205, 0.5)),
                "cactus" => PickHue(rng, (355, 2), (30, 1), (95, 1), (320, 1)),
                "blossom" => PickHue(rng, (355, 4), (340, 2), (10, 1), (90, 0.8)),
                "palm" => PickHue(rng, (60, 2), (95, 1)),
                _ => PickHue(rng, (27, 2), (355, 2), (60, 1), (95, 1), (300, 1), (250, 0.6)),
            };
            Set(values, "color.bloomHue", bloom + rng.Gaussian(0, 5, 2));
            Set(values, "color.bloomSaturation", rng.Range(0.6, 0.95));
            Set(values, "color.bloomValue", arch == "palm" ? rng.Range(0.3, 0.45) : rng.Range(0.5, 0.75));
            if (arch == "mushroom" && rng.Chance(0.3)) Set(values, "color.glow", rng.Range(0.6, 1.0));
        }

        private static double PickHue(Rng rng, params (double hue, double w)[] options)
        {
            var w = new List<double>();
            foreach (var o in options) w.Add(o.w);
            return options[rng.Weighted(w)].hue;
        }

        public void CustomizeSurface(CreatureGenome genome, SurfaceSpec surface)
        {
            // leaves and needles are their own material: clumped foliage on the canopy, glossy fruit
            string form = genome.GetChoice("form");
            if (form != "cactus" && form != "flowers") surface.OverrideSlotStyle(MaterialSlot.Secondary, MaterialStyles.Foliage);
            surface.OverrideSlotStyle(MaterialSlot.Accent, form == "blossom" ? MaterialStyles.Foliage : MaterialStyles.Glossy);
            surface.OverrideSlotStyle(MaterialSlot.Cloth, MaterialStyles.Glossy);
        }

        // ------------------------------------------------------------------ anatomy

        private sealed class Ctx
        {
            public AnatomyBuilder B = null!;
            public int Wood, Canopy;
            public double Scale;
            public double Gnarl;
            public double ClumpR;
            public double Fruit;
            public string Form = "oak";
            public Rng Rng = null!;
            public double WoodDomain = 40;
            public int BranchCount;
            public double Snow;
            public readonly List<(int bone, Vec3 local, double r)> Clumps = new List<(int, Vec3, double)>();
        }

        public override CreatureAnatomy BuildAnatomy(CreatureGenome g, CreatureRegistry registry)
        {
            var b = new AnatomyBuilder(Id, g.AnatomySeed);
            string form = g.GetChoice("form");
            b.Trait(form);
            var c = new Ctx { B = b, Form = form, Rng = b.RngFor("growth"), Gnarl = g.Get("trunk.gnarl"), Fruit = g.Get("fruit"), Snow = g.Get("snow") };
            double size = g.Get("size");
            int root = b.Bone("root", -1, Vec3.Zero, BoneRole.Root);
            b.Rig.Root = root;
            b.Rig.Upright = true;
            b.Rig.Stationary = true;
            b.Rig.Locomotion = LocomotionKind.Legged;
            b.Rig.LocomotionModule = RootedLocomotion.ModuleId;
            b.Rig.Action = ActionStyle.Shake;
            b.Rig.Rest = RestStyle.Dormant;
            double top;
            switch (form)
            {
                case "pine": top = BuildConifer(c, g, size, root); break;
                case "palm": top = BuildPalm(c, g, size, root); break;
                case "bush": top = BuildBush(c, g, size, root); break;
                case "mushroom": top = BuildMushroom(c, g, size, root); break;
                case "cactus": top = BuildCactus(c, g, size, root); break;
                case "flowers": top = BuildFlowers(c, g, size, root); break;
                default: top = BuildBranchingTree(c, g, size, root); break;
            }
            // metrics: stationary, but the motor and exporters still read speeds and sizes
            double reach = Math.Max(4, top);
            b.Rig.Tuning["rest.bodyHeight"] = 1e6;
            b.Rig.Tuning["wind.hz"] = DMath.Lerp(0.45, 0.25, size);
            b.Metrics.LegLength = Math.Max(3, reach * 0.3);
            b.Metrics.HipHeight = 0;
            b.Metrics.BackHeight = top;
            b.Metrics.BodyLength = Math.Max(8, top * 0.8);
            b.Metrics.Mass = DMath.Pow(Math.Max(8, top) / 30.0, 3);
            b.Metrics.WalkSpeed = 1;
            b.Metrics.RunSpeed = 2;
            b.Metrics.PatternScale = DMath.Clamp(Math.Sqrt(Math.Max(8, top) / 40.0), 0.7, 1.4);
            // swaying branches, fronds and curtains swing a little beyond the rest silhouette
            b.AnimationMargin = Math.Max(6, top * 0.17);
            b.ExtraHeadroom = 2;
            return b.Build(g.AnatomyKey);
        }

        // ---- branching trees: oak, birch, willow, dead, blossom

        private double BuildBranchingTree(Ctx c, CreatureGenome g, double size, int root)
        {
            var b = c.B;
            string form = c.Form;
            double H = DMath.Lerp(22, 70, DMath.Pow(size, 1.1)) * DMath.Lerp(0.55, 1.15, g.Get("trunk.height"));
            double R0 = H * DMath.Lerp(0.045, 0.11, g.Get("trunk.thickness")) * (form == "birch" ? 0.75 : 1.0);
            double crownBase = form == "birch" ? 0.45 : (form == "willow" ? 0.55 : 0.6);
            c.Wood = b.Group("wood", DMath.Clamp(R0 * 0.35, 0.8, 2.6), 0, GroupFlags.CreaseShade);
            c.WoodDomain = H * 1.4;
            double leanAmt = g.Get("trunk.lean");
            double leanAz = c.Rng.Range(0.0, DMath.TwoPi);
            Vec3 leanDir = new Vec3(DMath.Cos(leanAz), 0, DMath.Sin(leanAz));
            var trunk = BuildTrunk(c, root, H, R0, form == "birch" ? 0.55 : 0.5, leanDir * (leanAmt * 0.18), leanAmt * 0.12);
            int topBone = trunk[trunk.Length - 1];
            BuildRoots(c, root, R0, g.Get("roots"));

            bool leafy = form != "dead";
            double clump = H * DMath.Lerp(0.12, 0.22, g.Get("canopy.size")) * (form == "birch" ? 0.75 : 1.0);
            c.ClumpR = clump;
            if (leafy) c.Canopy = b.Group("canopy", DMath.Clamp(clump * 0.22, 1.0, 3.0), 0, GroupFlags.CreaseShade, 0.2);
            int count = (int)Math.Round(DMath.Lerp(3, 7, g.Get("branch.count")));
            double angle = DMath.Lerp(25, 70, g.Get("branch.angle")) * DMath.Deg2Rad;
            if (form == "birch") angle *= 0.75;
            double blen = H * DMath.Lerp(0.28, 0.55, g.Get("branch.length"));
            double depth = g.Get("branch.depth");
            double droop = g.Get("canopy.droop");
            double az0 = c.Rng.Range(0.0, DMath.TwoPi);
            double maxReach = R0;
            for (int i = 0; i < count; i++)
            {
                double t = DMath.Lerp(crownBase, 0.94, count == 1 ? 0.5 : i / (double)(count - 1)) + c.Rng.Range(-0.04, 0.04);
                double az = az0 + i * 2.39996 + c.Rng.Range(-0.3, 0.3);
                double el = angle * DMath.Lerp(1.15, 0.7, t) + c.Rng.Range(-0.12, 0.12) * (1 + c.Gnarl);
                Vec3 dir = new Vec3(DMath.Sin(el) * DMath.Cos(az), DMath.Cos(el), DMath.Sin(el) * DMath.Sin(az));
                var (attachBone, attach) = TrunkPoint(c, trunk, t);
                double len = blen * DMath.Lerp(1.05, 0.65, (t - crownBase) / Math.Max(0.05, 1 - crownBase)) * c.Rng.Range(0.85, 1.12);
                double r0 = R0 * DMath.Lerp(0.6, 0.38, t);
                double curve = form == "willow" ? 0.25 : (form == "dead" ? -0.1 : 0.35);
                var br = Branch(c, $"branch{i}", attachBone, attach, dir, len, r0, Math.Max(0.45, r0 * 0.35), curve, 3, true, form == "willow" ? 0.1 : 0.0);
                Vec3 end = b.RestWorld(br[br.Length - 1]).Origin;
                maxReach = Math.Max(maxReach, Math.Sqrt(end.X * end.X + end.Z * end.Z));
                // side branches and twigs
                int subs = (int)Math.Round(DMath.Lerp(0, form == "dead" ? 4 : 3, depth));
                for (int k = 0; k < subs; k++)
                {
                    int segBone = br[Math.Min(br.Length - 2, 1 + k % 2)];
                    var xf = b.RestWorld(segBone);
                    double side = (k & 1) == 0 ? 1 : -1;
                    Vec3 sdir = (xf.Dir(Vec3.UnitX) * 0.6 + Vec3.Cross(xf.Dir(Vec3.UnitX), Vec3.UnitY).NormalizedOr(Vec3.UnitZ) * (side * 0.7) + Vec3.UnitY * 0.35).NormalizedOr(Vec3.UnitY);
                    Vec3 sp = xf.Point(new Vec3(len * 0.12 * c.Rng.Range(0.3, 1.0), 0, 0));
                    var sb = Branch(c, $"branch{i}.{k}", segBone, sp, sdir, len * c.Rng.Range(0.35, 0.55), r0 * 0.4, 0.4, 0.3, 2, false, 0);
                    if (leafy) AddClump(c, sb[sb.Length - 1], clump * c.Rng.Range(0.65, 0.85));
                    else Twigs(c, sb[sb.Length - 1], len * 0.18);
                }
                if (leafy)
                {
                    AddClump(c, br[br.Length - 1], clump * c.Rng.Range(0.95, 1.2));
                    AddClump(c, br[br.Length - 2], clump * c.Rng.Range(0.75, 0.95));
                }
                else Twigs(c, br[br.Length - 1], len * 0.25);
                if (form == "willow") HangingCurtains(c, br, clump, droop);
            }
            double crownTop = H;
            if (leafy)
            {
                // fill clumps around the top of the trunk and the crown centre
                int fill = (int)Math.Round(DMath.Lerp(2, 6, g.Get("canopy.density")));
                var tx = b.RestWorld(topBone);
                for (int k = 0; k < fill; k++)
                {
                    double a = az0 + k * 2.39996 + 1.1;
                    double rr = maxReach * c.Rng.Range(0.15, 0.55);
                    Vec3 w = tx.Origin + new Vec3(DMath.Cos(a) * rr, clump * c.Rng.Range(0.1, 0.8), DMath.Sin(a) * rr);
                    c.Clumps.Add((topBone, tx.InversePoint(w), clump * c.Rng.Range(1.0, 1.3)));
                }
                c.Clumps.Add((topBone, tx.InversePoint(tx.Origin + new Vec3(0, clump * 0.9, 0)), clump * 1.25));
                crownTop = EmitClumps(c);
            }
            else crownTop = H + blen * 0.5;
            b.Trait(leafy ? "canopy" : "bare");
            b.Rig.Tuning["wind.trunk"] = form == "willow" ? 0.035 : 0.022;
            b.Rig.Tuning["wind.branch"] = form == "dead" ? 0.1 : 0.16;
            return Math.Max(crownTop, H);
        }

        /// <summary>Curved, tapering trunk as a spine chain. Returns the bones from base to top.</summary>
        private static int[] BuildTrunk(Ctx c, int root, double H, double R0, double taper, Vec3 lean, double curve)
        {
            var b = c.B;
            int n = 4;
            var pts = new Vec3[n + 1];
            double side = c.Rng.Range(0.0, DMath.TwoPi);
            for (int i = 0; i <= n; i++)
            {
                double t = i / (double)n;
                Vec3 wobble = new Vec3(DMath.Cos(side + t * 3), 0, DMath.Sin(side + t * 3)) * (c.Gnarl * H * 0.035 * DMath.Sin(DMath.Pi * t));
                pts[i] = new Vec3(0, H * t, 0) + lean * (H * t * t) + new Vec3(lean.X, 0, lean.Z) * (curve * H * DMath.Sin(DMath.Pi * t)) + wobble;
            }
            var bones = new int[n + 1];
            int parent = root;
            for (int i = 0; i <= n; i++)
            {
                Vec3 dir = i < n ? pts[i + 1] - pts[i] : pts[i] - pts[i - 1];
                bones[i] = b.BoneAlong(i == 0 ? "trunk" : (i == n ? "trunk.top" : $"trunk.{i}"), parent, pts[i], dir, Vec3.UnitX * -1,
                    i == 0 ? BoneRole.Pelvis : (i == n ? BoneRole.Chest : BoneRole.Spine), 0, dir.Length);
                parent = bones[i];
            }
            for (int i = 0; i < n; i++)
            {
                double t0 = i / (double)n, t1 = (i + 1) / (double)n;
                var seg = b.Segment(c.Wood, bones[i], R0 * DMath.Lerp(1, taper, t0), bones[i + 1], R0 * DMath.Lerp(1, taper, t1), MaterialSlot.Primary, PatternDomain.Body, t0 * 0.7, t1 * 0.7);
                seg.DomainLength = c.WoodDomain;
                seg.Tag = "trunk";
                seg.Flags |= PrimFlags.ShadowCaster;
            }
            // knots and burls on gnarled trunks
            if (c.Gnarl > 0.35)
            {
                int knots = (int)Math.Round(DMath.Lerp(1, 4, c.Gnarl));
                for (int k = 0; k < knots; k++)
                {
                    int bi = 1 + k % (n - 1);
                    double a = c.Rng.Range(0.0, DMath.TwoPi);
                    double r = R0 * DMath.Lerp(1, taper, bi / (double)n);
                    var kn = b.Ellipsoid(c.Wood, bones[bi], new Vec3(c.Rng.Range(0.0, 1.0) * (pts[1] - pts[0]).Length * 0.5, DMath.Cos(a) * r * 0.85, DMath.Sin(a) * r * 0.85),
                        new Vec3(r * 0.45, r * 0.42, r * 0.42), MaterialSlot.Primary, PatternDomain.Body, 0.2, 0.5);
                    kn.DomainLength = c.WoodDomain;
                    kn.Tag = "knot";
                }
            }
            b.Rig.Pelvis = bones[0];
            b.Rig.Chest = bones[n];
            b.Rig.Spine = new ChainRig { Name = "trunk", Bones = bones, Lengths = Lengths(b, bones), TotalLength = H };
            return bones;
        }

        private static double[] Lengths(AnatomyBuilder b, int[] bones)
        {
            var l = new double[bones.Length - 1];
            for (int i = 0; i + 1 < bones.Length; i++) l[i] = (b.RestWorld(bones[i + 1]).Origin - b.RestWorld(bones[i]).Origin).Length;
            return l;
        }

        private static (int bone, Vec3 world) TrunkPoint(Ctx c, int[] trunk, double t)
        {
            int n = trunk.Length - 1;
            double f = DMath.Clamp(t, 0, 1) * n;
            int i = Math.Min(n - 1, (int)Math.Floor(f));
            Vec3 a = c.B.RestWorld(trunk[i]).Origin, d = c.B.RestWorld(trunk[i + 1]).Origin;
            return (trunk[i], Vec3.Lerp(a, d, f - i));
        }

        /// <summary>Roots spreading from the base: cones that dive into the ground.</summary>
        private static void BuildRoots(Ctx c, int root, double R0, double amount)
        {
            if (amount < 0.1) return;
            int n = 3 + (int)Math.Round(amount * 2);
            double a0 = c.Rng.Range(0.0, DMath.TwoPi);
            for (int i = 0; i < n; i++)
            {
                double a = a0 + i * DMath.TwoPi / n + c.Rng.Range(-0.3, 0.3);
                Vec3 d = new Vec3(DMath.Cos(a), 0, DMath.Sin(a));
                double len = R0 * DMath.Lerp(1.4, 2.6, amount) * c.Rng.Range(0.8, 1.2);
                var r = c.B.Cone(c.Wood, root, d * (R0 * 0.3) + new Vec3(0, R0 * 0.55, 0), R0 * 0.55, root, d * len + new Vec3(0, -R0 * 0.1, 0), Math.Max(0.4, R0 * 0.15), MaterialSlot.Primary, PatternDomain.Body, 0, 0.08);
                r.DomainLength = c.WoodDomain;
                r.Tag = "root";
                r.Flags |= PrimFlags.ShadowCaster;
            }
            var flare = c.B.Ellipsoid(c.Wood, root, new Vec3(0, R0 * 0.4, 0), new Vec3(R0 * 1.35, R0 * 0.75, R0 * 1.35), MaterialSlot.Primary, PatternDomain.Body, 0, 0.05);
            flare.DomainLength = c.WoodDomain;
            flare.Tag = "flare";
        }

        /// <summary>
        /// A branch from 'start' along 'dir' that bends upwards ('curve' > 0) or sags (droop). Chains sway in
        /// the wind (spring simulated); non-chain branches are rigid children of their parent.
        /// </summary>
        private static int[] Branch(Ctx c, string name, int parent, Vec3 start, Vec3 dir, double len, double r0, double r1, double curve, int segs, bool chain, double droop)
        {
            var b = c.B;
            var bones = new int[segs + 1];
            Vec3 p = start, d = dir.NormalizedOr(Vec3.UnitY);
            double seg = len / segs;
            var pts = new Vec3[segs + 1];
            pts[0] = p;
            for (int i = 1; i <= segs; i++)
            {
                // bend towards the light (up), sag under weight, plus a little gnarl
                d = (d + Vec3.UnitY * (curve * 0.5) - Vec3.UnitY * (droop * 0.6) + new Vec3(c.Rng.Range(-1.0, 1.0), c.Rng.Range(-0.5, 0.5), c.Rng.Range(-1.0, 1.0)) * (c.Gnarl * 0.25)).NormalizedOr(d);
                p = p + d * seg;
                pts[i] = p;
            }
            int par = parent;
            for (int i = 0; i <= segs; i++)
            {
                Vec3 fwd = i < segs ? pts[i + 1] - pts[i] : pts[i] - pts[i - 1];
                Vec3 upHint = Math.Abs(fwd.NormalizedOr(Vec3.UnitY).Y) > 0.9 ? Vec3.UnitX : Vec3.UnitY;
                bones[i] = b.BoneAlong($"{name}.{i}", par, pts[i], fwd, upHint, chain ? BoneRole.Tentacle : BoneRole.Other, 0, fwd.Length);
                par = bones[i];
            }
            for (int i = 0; i < segs; i++)
            {
                var cone = b.Segment(c.Wood, bones[i], DMath.Lerp(r0, r1, i / (double)segs), bones[i + 1], DMath.Lerp(r0, r1, (i + 1) / (double)segs),
                    MaterialSlot.Primary, PatternDomain.Body, 0.7, 1.0);
                cone.DomainLength = c.WoodDomain;
                cone.Tag = "branch";
                if (r0 < 1.0) cone.Flags |= PrimFlags.Thin;
            }
            if (chain)
            {
                var rad = new double[segs + 1];
                for (int i = 0; i <= segs; i++) rad[i] = DMath.Lerp(r0, r1, i / (double)segs);
                b.Rig.Appendages.Add(new ChainRig
                {
                    Name = name, Bones = bones, Lengths = Lengths(b, bones), TotalLength = len, Radii = rad,
                    Stiffness = DMath.Lerp(0.95, 0.75, c.Gnarl * 0.3 + droop), Droop = droop * 0.2,
                    RestPitch = new double[segs], RestYaw = new double[segs],
                });
                c.BranchCount++;
            }
            return bones;
        }

        private static void Twigs(Ctx c, int bone, double len)
        {
            var b = c.B;
            for (int k = 0; k < 3; k++)
            {
                Vec3 tip = new Vec3(len * c.Rng.Range(0.6, 1.0), len * c.Rng.Range(-0.5, 0.6), len * c.Rng.Range(-0.6, 0.6));
                var tw = b.Cone(c.Wood, bone, Vec3.Zero, 0.55, bone, tip, 0.3, MaterialSlot.Primary, PatternDomain.None, 0, 1);
                tw.Flags |= PrimFlags.Thin | PrimFlags.NoPattern;
                tw.Tag = "twig";
            }
        }

        private static void AddClump(Ctx c, int bone, double r)
        {
            var xf = c.B.RestWorld(bone);
            Vec3 w = xf.Origin + new Vec3(c.Rng.Range(-0.2, 0.2) * r, r * 0.25, c.Rng.Range(-0.2, 0.2) * r);
            c.Clumps.Add((bone, xf.InversePoint(w), r));
        }

        /// <summary>Turns the collected clumps into foliage, fruit or blossoms and snow caps. Returns the crown top.</summary>
        private static double EmitClumps(Ctx c)
        {
            var b = c.B;
            double topY = 0;
            int fruitGroup = c.Fruit > 0.05 && c.Form != "blossom" ? b.Group("fruit", 0.3, 0, GroupFlags.None, 0.6) : -1;
            int snowGroup = c.Snow > 0.1 ? b.Group("snow", 1.0, 0, GroupFlags.NoContour, 0.4) : -1;
            for (int i = 0; i < c.Clumps.Count; i++)
            {
                var (bone, local, r) = c.Clumps[i];
                var xf = b.RestWorld(bone);
                Vec3 w = xf.Point(local);
                topY = Math.Max(topY, w.Y + r);
                // blossom trees: most clumps are blossoms, a few stay green
                bool bloom = c.Form == "blossom" && (i % 4 != 3 || c.Fruit > 0.85);
                var slot = bloom ? MaterialSlot.Accent : MaterialSlot.Secondary;
                var e = b.Ellipsoid(c.Canopy, bone, local, new Vec3(r * 0.92, r * 0.8, r * 0.92), slot, PatternDomain.Body, 0.5, 1.0);
                e.Flags |= PrimFlags.NoPattern | PrimFlags.ShadowCaster;
                e.DomainLength = c.ClumpR * 6;
                e.Tag = bloom ? "blossom" : "foliage";
                // cauliflower crown: smaller lobes on the upper half of every clump give the silhouette its bumps
                int lobes = c.Form == "bush" ? 2 : 3;
                double la0 = c.Rng.Range(0.0, DMath.TwoPi);
                for (int k = 0; k < lobes; k++)
                {
                    double a = la0 + k * DMath.TwoPi / lobes + c.Rng.Range(-0.4, 0.4);
                    double el = c.Rng.Range(0.15, 0.85);
                    Vec3 d = new Vec3(DMath.Cos(a) * DMath.Cos(el), DMath.Sin(el), DMath.Sin(a) * DMath.Cos(el));
                    double lr = r * c.Rng.Range(0.42, 0.56);
                    var lobe = b.Ellipsoid(c.Canopy, bone, xf.InversePoint(w + d * (r * 0.62)), new Vec3(lr, lr * 0.9, lr), slot, PatternDomain.Body, 0.5, 1.0);
                    lobe.Flags |= PrimFlags.NoPattern;
                    lobe.DomainLength = c.ClumpR * 6;
                    lobe.Tag = "lobe";
                    topY = Math.Max(topY, w.Y + d.Y * r * 0.62 + lr);
                }
                if (fruitGroup >= 0)
                {
                    int n = (int)Math.Round(c.Fruit * 3);
                    for (int k = 0; k < n; k++)
                    {
                        double a = c.Rng.Range(0.0, DMath.TwoPi), el = c.Rng.Range(-0.6, 0.5);
                        Vec3 d = new Vec3(DMath.Cos(a) * DMath.Cos(el), DMath.Sin(el), DMath.Sin(a) * DMath.Cos(el));
                        Vec3 pw = w + d * (r * 0.92);
                        double fr = Math.Max(0.7, c.ClumpR * 0.13);
                        var f = b.Ellipsoid(fruitGroup, bone, xf.InversePoint(pw), new Vec3(fr, fr * 1.05, fr), MaterialSlot.Accent, PatternDomain.None);
                        f.Flags |= PrimFlags.NoPattern | PrimFlags.Hard | PrimFlags.NoShadow;
                        f.Tag = "fruit";
                    }
                }
                if (snowGroup >= 0 && w.Y > topY * 0.4)
                {
                    var s = b.Ellipsoid(snowGroup, bone, xf.InversePoint(w + new Vec3(-r * 0.1, r * 0.45, 0)), new Vec3(r * 0.8 * c.Snow, r * 0.42, r * 0.8 * c.Snow), MaterialSlot.White, PatternDomain.None);
                    s.Flags |= PrimFlags.NoPattern | PrimFlags.NoShadow;
                    s.Tag = "snow";
                }
            }
            return topY;
        }

        /// <summary>Willow curtains: hanging strands of leaves along each branch, simulated so they swing.</summary>
        private static void HangingCurtains(Ctx c, int[] branch, double clump, double droop)
        {
            var b = c.B;
            int strands = 3 + (int)Math.Round(droop * 3);
            for (int k = 0; k < strands; k++)
            {
                int bone = branch[Math.Min(branch.Length - 1, 1 + k)];
                var xf = b.RestWorld(bone);
                Vec3 start = xf.Origin + new Vec3(c.Rng.Range(-1.0, 1.0), 0, c.Rng.Range(-1.0, 1.0)) * (clump * 0.4);
                double len = Math.Min(start.Y * 0.85, clump * DMath.Lerp(2.0, 4.0, droop) * c.Rng.Range(0.8, 1.15));
                if (len < 3) continue;
                int segs = 3;
                var bones = new int[segs + 1];
                int par = bone;
                for (int i = 0; i <= segs; i++)
                {
                    Vec3 p = start + new Vec3(0, -len * i / segs, 0);
                    bones[i] = b.BoneAlong($"strand.{bone}.{k}.{i}", par, p, new Vec3(0, -1, 0), Vec3.UnitX, BoneRole.Tentacle, 0, len / segs);
                    par = bones[i];
                }
                for (int i = 0; i < segs; i++)
                {
                    double r = clump * DMath.Lerp(0.26, 0.14, i / (double)segs);
                    var leaf = b.Cone(c.Canopy, bones[i], Vec3.Zero, r, bones[i + 1], Vec3.Zero, r * 0.75, MaterialSlot.Secondary, PatternDomain.Body, 0.5, 1.0);
                    leaf.Flags |= PrimFlags.NoPattern;
                    leaf.DomainLength = clump * 6;
                    leaf.Tag = "strand";
                }
                b.Rig.Appendages.Add(new ChainRig
                {
                    Name = $"strand{bone}.{k}", Bones = bones, Lengths = Lengths(b, bones), TotalLength = len,
                    Radii = new[] { clump * 0.4, clump * 0.3, clump * 0.25, clump * 0.2 }, Stiffness = 0.35, Droop = 0.9,
                    RestPitch = new double[segs], RestYaw = new double[segs],
                });
            }
        }

        // ---- conifers

        private double BuildConifer(Ctx c, CreatureGenome g, double size, int root)
        {
            var b = c.B;
            double H = DMath.Lerp(26, 80, DMath.Pow(size, 1.1)) * DMath.Lerp(0.8, 1.2, g.Get("trunk.height"));
            double R0 = H * DMath.Lerp(0.035, 0.07, g.Get("trunk.thickness"));
            c.Wood = b.Group("wood", 1.2, 0, GroupFlags.CreaseShade);
            c.WoodDomain = H * 1.2;
            var trunk = BuildTrunk(c, root, H, R0, 0.35, new Vec3(c.Rng.Range(-1.0, 1.0), 0, c.Rng.Range(-1.0, 1.0)) * (g.Get("trunk.lean") * 0.08), 0);
            BuildRoots(c, root, R0, g.Get("roots") * 0.6);
            double bare = DMath.Lerp(0.08, 0.35, g.Get("trunk.height"));
            int tiers = (int)Math.Round(DMath.Lerp(4, 7, g.Get("canopy.density")));
            double width = H * DMath.Lerp(0.22, 0.36, g.Get("canopy.size")) * DMath.Lerp(0.85, 1.15, g.Get("branch.angle"));
            double droop = g.Get("canopy.droop");
            double y0 = H * bare, span = H * 1.04 - y0;
            int snowG = c.Snow > 0.1 ? b.Group("snow", 0.8, 0, GroupFlags.NoContour, 0.5) : -1;
            for (int i = 0; i < tiers; i++)
            {
                double t = i / (double)Math.Max(1, tiers - 1);
                double yb = y0 + span * t * 0.84;
                double tierH = span / tiers * DMath.Lerp(1.35, 1.2, t);
                double r = width * DMath.Lerp(1.0, 0.32, t) * c.Rng.Range(0.92, 1.06);
                var (bone, at) = TrunkPoint(c, trunk, DMath.Clamp(yb / H, 0, 1));
                var xf = b.RestWorld(bone);
                // each tier is its own group, so the lower edge of the tier above draws a contour on it
                int grp = b.Group($"tier{i}", 1.4, 0, GroupFlags.CreaseShade, 0.05 * i);
                Vec3 baseW = new Vec3(at.X, yb - tierH * 0.1 * (1 + droop), at.Z);
                double brimH = Math.Max(1.0, r * DMath.Lerp(0.16, 0.26, droop));
                var cone = b.Cone(grp, bone, xf.InversePoint(baseW + new Vec3(0, brimH * 0.6, 0)), r * 0.52, bone, xf.InversePoint(baseW + new Vec3(0, tierH, 0)), Math.Max(0.6, r * 0.12), MaterialSlot.Secondary, PatternDomain.Body, 0.4, 1.0);
                cone.Flags |= PrimFlags.NoPattern | PrimFlags.ShadowCaster;
                cone.DomainLength = width * 5;
                cone.Tag = "tier";
                // the wide drooping brim of the tier
                var skirt = b.Ellipsoid(grp, bone, xf.InversePoint(baseW + new Vec3(0, brimH * 0.4, 0)), new Vec3(r, brimH, r), MaterialSlot.Secondary, PatternDomain.Body, 0.4, 0.6);
                skirt.Flags |= PrimFlags.NoPattern | PrimFlags.ShadowCaster;
                skirt.DomainLength = width * 5;
                skirt.Tag = "brim";
                if (snowG >= 0)
                {
                    var sn = b.Cone(snowG, bone, xf.InversePoint(baseW + new Vec3(0, tierH * 0.42, 0)), r * 0.55 * c.Snow, bone, xf.InversePoint(baseW + new Vec3(0, tierH * 0.9, 0)), Math.Max(0.5, r * 0.15), MaterialSlot.White, PatternDomain.None);
                    sn.Flags |= PrimFlags.NoPattern | PrimFlags.NoShadow;
                }
            }
            var topX = b.RestWorld(trunk[trunk.Length - 1]);
            if (c.Fruit > 0.1)
            {
                // pine cones hanging under the tiers
                int cg = b.Group("cones", 0.3, 0, GroupFlags.None, 0.4);
                for (int k = 0; k < 3; k++)
                {
                    double a = c.Rng.Range(0.0, DMath.TwoPi);
                    double yy = y0 + span * c.Rng.Range(0.2, 0.6);
                    double rr = width * DMath.Lerp(0.8, 0.4, (yy - y0) / span);
                    Vec3 p = new Vec3(DMath.Cos(a) * rr, yy, DMath.Sin(a) * rr);
                    var cn = b.Ellipsoid(cg, root, p, new Vec3(0.9, 1.4, 0.9), MaterialSlot.Primary, PatternDomain.None);
                    cn.Flags |= PrimFlags.NoPattern | PrimFlags.Hard | PrimFlags.NoShadow;
                    cn.ShadeBias = -1;
                }
            }
            b.Rig.Tuning["wind.trunk"] = 0.018;
            b.Rig.Tuning["wind.branch"] = 0.1;
            b.Trait("tiers:" + tiers);
            _ = topX;
            return H * 1.05;
        }

        // ---- palms

        private double BuildPalm(Ctx c, CreatureGenome g, double size, int root)
        {
            var b = c.B;
            double H = DMath.Lerp(26, 72, DMath.Pow(size, 1.1)) * DMath.Lerp(0.75, 1.2, g.Get("trunk.height"));
            double R0 = H * DMath.Lerp(0.035, 0.06, g.Get("trunk.thickness"));
            c.Wood = b.Group("wood", 0.8, 0, GroupFlags.CreaseShade);
            c.WoodDomain = H;
            double lean = g.Get("trunk.lean");
            double az = c.Rng.Range(0.0, DMath.TwoPi);
            Vec3 ld = new Vec3(DMath.Cos(az), 0, DMath.Sin(az));
            var trunk = BuildTrunk(c, root, H, R0, 0.78, ld * (lean * 0.32), -lean * 0.1);
            BuildRoots(c, root, R0, g.Get("roots") * 0.5);
            // ring scars: slightly wider bands stacked along the trunk
            int rings = (int)Math.Round(H / Math.Max(2.2, R0 * 1.4));
            for (int i = 1; i < rings; i++)
            {
                double t = i / (double)rings;
                var (bone, at) = TrunkPoint(c, trunk, t);
                var xf = b.RestWorld(bone);
                double r = R0 * DMath.Lerp(1.0, 0.78, t) * 1.12;
                var ring = b.Ellipsoid(c.Wood, bone, xf.InversePoint(at), new Vec3(Math.Max(0.6, R0 * 0.35), r, r), MaterialSlot.Primary, PatternDomain.Body, t * 0.8, t * 0.8 + 0.02);
                ring.DomainLength = c.WoodDomain;
                ring.Tag = "ring";
            }
            int top = trunk[trunk.Length - 1];
            var tx = b.RestWorld(top);
            int fronds = (int)Math.Round(DMath.Lerp(5, 10, g.Get("branch.count")));
            double flen = H * DMath.Lerp(0.3, 0.5, g.Get("canopy.size"));
            double droop = g.Get("canopy.droop");
            c.Canopy = b.Group("fronds", 0.4, 0, GroupFlags.NoFarShade, 0.3);
            double reach = 0;
            for (int i = 0; i < fronds; i++)
            {
                double a = i * DMath.TwoPi / fronds + c.Rng.Range(-0.2, 0.2);
                double el = DMath.Lerp(0.7, -0.1, (i % 3) / 2.0) * DMath.Lerp(1.0, 0.6, droop);
                Vec3 dir = new Vec3(DMath.Cos(a) * DMath.Cos(el), DMath.Sin(el), DMath.Sin(a) * DMath.Cos(el));
                reach = Math.Max(reach, Frond(c, $"frond{i}", top, tx.Origin, dir, flen * c.Rng.Range(0.85, 1.1), droop));
            }
            if (c.Fruit > 0.1)
            {
                int cg = b.Group("coconuts", 0.6, 0, GroupFlags.None, 0.2);
                int n = 2 + (int)Math.Round(c.Fruit * 3);
                for (int k = 0; k < n; k++)
                {
                    double a = k * DMath.TwoPi / n + 0.4;
                    double r = Math.Max(1.0, R0 * 0.55);
                    var nut = b.Ellipsoid(cg, top, tx.InversePoint(tx.Origin + new Vec3(DMath.Cos(a) * R0 * 0.9, -r * 0.9, DMath.Sin(a) * R0 * 0.9)), new Vec3(r, r, r), MaterialSlot.Accent, PatternDomain.None);
                    nut.Flags |= PrimFlags.NoPattern;
                }
            }
            b.Rig.Tuning["wind.trunk"] = 0.03;
            b.Rig.Tuning["wind.branch"] = 0.22;
            return tx.Origin.Y + flen * 0.5;
        }

        /// <summary>A palm frond: a drooping rachis chain with leaflets on both sides (two sided plates).</summary>
        private static double Frond(Ctx c, string name, int parent, Vec3 start, Vec3 dir, double len, double droop)
        {
            var b = c.B;
            int segs = 4;
            var pts = new Vec3[segs + 1];
            Vec3 d = dir.NormalizedOr(Vec3.UnitX);
            pts[0] = start;
            for (int i = 1; i <= segs; i++)
            {
                d = (d - Vec3.UnitY * DMath.Lerp(0.18, 0.45, droop)).NormalizedOr(d);
                pts[i] = pts[i - 1] + d * (len / segs);
            }
            var bones = new int[segs + 1];
            int par = parent;
            for (int i = 0; i <= segs; i++)
            {
                Vec3 fwd = i < segs ? pts[i + 1] - pts[i] : pts[i] - pts[i - 1];
                bones[i] = b.BoneAlong($"{name}.{i}", par, pts[i], fwd, Vec3.UnitY, BoneRole.Tentacle, 0, fwd.Length);
                par = bones[i];
            }
            for (int i = 0; i < segs; i++)
            {
                var rib = b.Segment(c.Canopy, bones[i], 0.55, bones[i + 1], 0.4, MaterialSlot.Secondary, PatternDomain.None);
                rib.Flags |= PrimFlags.Thin | PrimFlags.NoPattern | PrimFlags.NoShadow;
                // leaflets: zig-zag plates from the rachis out and down to both sides
                double w = len * 0.2 * DMath.Sin(DMath.Pi * (i + 0.5) / segs + 0.3);
                for (int s = -1; s <= 1; s += 2)
                {
                    Vec3 side = new Vec3(0, -0.45, s).NormalizedOr(Vec3.UnitZ) * w;
                    Vec3 mid = new Vec3((pts[i + 1] - pts[i]).Length * 0.55, 0, 0);
                    var t1 = b.Triangle(c.Canopy, bones[i], Vec3.Zero, bones[i], mid + side, bones[i + 1], Vec3.Zero, MaterialSlot.Secondary, PatternDomain.None, 0);
                    var t2 = b.Triangle(c.Canopy, bones[i], mid * 0.4, bones[i], mid * 0.5 + side * 0.8, bones[i], mid * 0.95, MaterialSlot.Secondary, PatternDomain.None, 0);
                    foreach (var t in new[] { t1, t2 }) { t.Flags |= PrimFlags.TwoSided | PrimFlags.NoPattern | PrimFlags.NoShadow; t.Tag = "leaflet"; }
                }
            }
            b.Rig.Appendages.Add(new ChainRig
            {
                Name = name, Bones = bones, Lengths = Lengths(b, bones), TotalLength = len, Radii = new double[segs + 1],
                Stiffness = 0.6, Droop = 0.15, RestPitch = new double[segs], RestYaw = new double[segs],
            });
            double reach = 0;
            foreach (var p in pts) reach = Math.Max(reach, Math.Sqrt(p.X * p.X + p.Z * p.Z));
            return reach;
        }

        // ---- bushes

        private double BuildBush(Ctx c, CreatureGenome g, double size, int root)
        {
            var b = c.B;
            double R = DMath.Lerp(6, 18, size) * DMath.Lerp(0.8, 1.25, g.Get("canopy.size"));
            c.Wood = b.Group("wood", 0.8, 0, GroupFlags.CreaseShade);
            c.WoodDomain = R * 3;
            int body = b.BoneAlong("trunk", root, new Vec3(0, R * 0.2, 0), Vec3.UnitY, Vec3.UnitX * -1, BoneRole.Pelvis, 0, R);
            int top = b.BoneAlong("trunk.top", body, new Vec3(0, R * 0.9, 0), Vec3.UnitY, Vec3.UnitX * -1, BoneRole.Chest, 0, R * 0.5);
            b.Rig.Pelvis = body; b.Rig.Chest = top;
            b.Rig.Spine = new ChainRig { Name = "trunk", Bones = new[] { body, top }, Lengths = new[] { R * 0.7 }, TotalLength = R * 0.7 };
            c.Canopy = b.Group("canopy", DMath.Clamp(R * 0.18, 1.2, 3.5), 0, GroupFlags.CreaseShade, 0.1);
            c.ClumpR = R * 0.45;
            int n = (int)Math.Round(DMath.Lerp(4, 9, g.Get("canopy.density")));
            double wide = DMath.Lerp(0.8, 1.3, g.Get("branch.angle"));
            // a few springy stems carry the outer clumps so the bush rustles
            int stems = Math.Max(3, n / 2);
            for (int i = 0; i < n; i++)
            {
                double a = i * 2.39996 + c.Rng.Range(-0.2, 0.2);
                double rr = R * wide * (i < stems ? c.Rng.Range(0.45, 0.65) : c.Rng.Range(0.1, 0.4));
                double y = R * (i < stems ? c.Rng.Range(0.45, 0.7) : c.Rng.Range(0.7, 1.1));
                Vec3 p = new Vec3(DMath.Cos(a) * rr, y, DMath.Sin(a) * rr);
                double cr = R * c.Rng.Range(0.42, 0.58);
                if (i < stems)
                {
                    var st = Branch(c, $"stem{i}", body, new Vec3(0, R * 0.25, 0), p - new Vec3(0, R * 0.25, 0), (p - new Vec3(0, R * 0.25, 0)).Length, 0.8, 0.5, 0.2, 2, true, 0);
                    var xf = b.RestWorld(st[st.Length - 1]);
                    c.Clumps.Add((st[st.Length - 1], xf.InversePoint(p), cr));
                }
                else
                {
                    var xf = b.RestWorld(top);
                    c.Clumps.Add((top, xf.InversePoint(p), cr));
                }
            }
            c.Clumps.Add((top, b.RestWorld(top).InversePoint(new Vec3(0, R * 0.75, 0)), R * 0.62));
            double topY = EmitClumps(c);
            b.Rig.Tuning["wind.trunk"] = 0.02;
            b.Rig.Tuning["wind.branch"] = 0.2;
            return topY;
        }

        // ---- giant mushrooms

        private double BuildMushroom(Ctx c, CreatureGenome g, double size, int root)
        {
            var b = c.B;
            double H = DMath.Lerp(12, 40, size) * DMath.Lerp(0.7, 1.3, g.Get("trunk.height"));
            double R0 = H * DMath.Lerp(0.1, 0.2, g.Get("trunk.thickness"));
            c.Wood = b.Group("stem", DMath.Clamp(R0 * 0.4, 1, 3), 0, GroupFlags.CreaseShade);
            c.WoodDomain = H;
            double lean = g.Get("trunk.lean");
            var trunk = BuildTrunk(c, root, H, R0, 0.85, new Vec3(c.Rng.Range(-1.0, 1.0), 0, c.Rng.Range(-1.0, 1.0)) * (lean * 0.2), lean * 0.15);
            var bulb = b.Ellipsoid(c.Wood, root, new Vec3(0, R0 * 0.7, 0), new Vec3(R0 * 1.3, R0 * 0.9, R0 * 1.3), MaterialSlot.Primary, PatternDomain.Body, 0, 0.1);
            bulb.DomainLength = c.WoodDomain;
            double top = Cap(c, "cap", trunk[trunk.Length - 1], R0 * DMath.Lerp(2.4, 4.0, g.Get("canopy.size")), g.Get("canopy.droop"), g, true);
            // a ring (annulus) under the cap
            var (rb, ra) = TrunkPoint(c, trunk, 0.72);
            var ring = b.Ellipsoid(c.Wood, rb, b.RestWorld(rb).InversePoint(ra), new Vec3(R0 * 0.3, R0 * 1.25, R0 * 1.25), MaterialSlot.White, PatternDomain.None);
            ring.Flags |= PrimFlags.NoPattern;
            ring.Tag = "annulus";
            // small mushrooms around the base
            int smalls = (int)Math.Round(g.Get("branch.count") * 3);
            for (int i = 0; i < smalls; i++)
            {
                double a = c.Rng.Range(0.0, DMath.TwoPi), d = R0 * c.Rng.Range(2.0, 3.2);
                double h = H * c.Rng.Range(0.2, 0.38);
                Vec3 at = new Vec3(DMath.Cos(a) * d, 0, DMath.Sin(a) * d);
                int s0 = b.BoneAlong($"small{i}", root, at, Vec3.UnitY, Vec3.UnitX * -1, BoneRole.Other, 0, h);
                int s1 = b.BoneAlong($"small{i}.top", s0, at + new Vec3(0, h, 0), Vec3.UnitY, Vec3.UnitX * -1, BoneRole.Other, 0, 1);
                var st = b.Segment(c.Wood, s0, R0 * 0.35, s1, R0 * 0.3, MaterialSlot.Primary, PatternDomain.Body, 0, 0.5);
                st.DomainLength = c.WoodDomain;
                Cap(c, $"cap{i}", s1, R0 * c.Rng.Range(0.8, 1.2), g.Get("canopy.droop"), g, false);
            }
            b.Rig.Tuning["wind.trunk"] = 0.015;
            b.Rig.Tuning["wind.branch"] = 0.05;
            return top;
        }

        private static double Cap(Ctx c, string name, int bone, double capR, double droop, CreatureGenome g, bool main)
        {
            var b = c.B;
            var xf = b.RestWorld(bone);
            int grp = b.Group(name, 1.6, 0, GroupFlags.None, 0.3);
            double capH = capR * DMath.Lerp(0.5, 0.75, droop);
            // bone frame of trunk bones: +X up the trunk
            var cap = b.Ellipsoid(grp, bone, new Vec3(capH * 0.3, 0, 0), new Vec3(capH, capR, capR), MaterialSlot.Cloth, PatternDomain.Body, 0, 1);
            cap.Flags |= PrimFlags.NoPattern | PrimFlags.ShadowCaster;
            cap.DomainLength = capR * 4;
            cap.Tag = "cap";
            var gills = b.Ellipsoid(grp, bone, new Vec3(-capH * 0.25, 0, 0), new Vec3(capH * 0.32, capR * 0.9, capR * 0.9), MaterialSlot.Marking, PatternDomain.None);
            gills.Flags |= PrimFlags.NoPattern;
            gills.Tag = "gills";
            bool glow = g.Get("color.glow") > 0.3;
            int spots = (int)Math.Round(DMath.Lerp(2, 9, c.Fruit)) + (main ? 2 : 0);
            if (c.Fruit > 0.15 || glow)
            {
                for (int i = 0; i < spots; i++)
                {
                    double a = c.Rng.Range(0.0, DMath.TwoPi), el = c.Rng.Range(0.2, 1.1);
                    Vec3 n = new Vec3(DMath.Cos(el), DMath.Sin(el) * DMath.Cos(a), DMath.Sin(el) * DMath.Sin(a));
                    Vec3 pos = new Vec3(capH * 0.3, 0, 0) + new Vec3(n.X * capH * 0.97, n.Y * capR * 0.97, n.Z * capR * 0.97);
                    double sr = capR * c.Rng.Range(0.09, 0.15);
                    var sp = b.Ellipsoid(grp, bone, pos, new Vec3(Math.Max(0.45, sr * 0.3), sr, sr), glow ? MaterialSlot.Glow : MaterialSlot.White, PatternDomain.None);
                    sp.Flags |= PrimFlags.NoPattern | PrimFlags.Hard;
                    if (glow) sp.Flags |= PrimFlags.Emissive;
                    sp.Tag = "spot";
                }
            }
            return xf.Origin.Y + capH * 1.3;
        }

        // ---- cacti

        private double BuildCactus(Ctx c, CreatureGenome g, double size, int root)
        {
            var b = c.B;
            double H = DMath.Lerp(14, 48, size) * DMath.Lerp(0.75, 1.25, g.Get("trunk.height"));
            double R0 = H * DMath.Lerp(0.08, 0.14, g.Get("trunk.thickness"));
            c.Wood = b.Group("cactus", DMath.Clamp(R0 * 0.35, 1, 3), 0, GroupFlags.CreaseShade);
            c.WoodDomain = H;
            var trunk = BuildTrunk(c, root, H, R0, 0.92, Vec3.Zero, 0);
            var cap = b.Ellipsoid(c.Wood, trunk[trunk.Length - 1], Vec3.Zero, new Vec3(R0 * 0.9, R0 * 0.92, R0 * 0.92), MaterialSlot.Primary, PatternDomain.Body, 0.95, 1);
            cap.DomainLength = c.WoodDomain;
            int arms = (int)Math.Round(DMath.Lerp(1, 3, g.Get("branch.count")));
            double az0 = c.Rng.Range(0.0, DMath.TwoPi);
            var columns = new List<(int bone, double h, double r)> { (trunk[0], H, R0) };
            for (int i = 0; i < arms; i++)
            {
                double az = az0 + i * DMath.TwoPi / Math.Max(2, arms) + c.Rng.Range(-0.4, 0.4);
                double t = c.Rng.Range(0.3, 0.6);
                var (bone, at) = TrunkPoint(c, trunk, t);
                Vec3 out1 = new Vec3(DMath.Cos(az), 0, DMath.Sin(az));
                double ar = R0 * c.Rng.Range(0.55, 0.75);
                double outLen = R0 * c.Rng.Range(1.3, 2.0);
                double upLen = H * c.Rng.Range(0.2, 0.4);
                int a0 = b.BoneAlong($"arm{i}", bone, at, out1, Vec3.UnitY, BoneRole.Other, 0, outLen);
                Vec3 elbow = at + out1 * outLen + new Vec3(0, ar * 0.6, 0);
                int a1 = b.BoneAlong($"arm{i}.1", a0, elbow, Vec3.UnitY, out1, BoneRole.Other, 0, upLen);
                int a2 = b.BoneAt($"arm{i}.2", a1, elbow + new Vec3(0, upLen, 0), BoneRole.Other, 0);
                var p1 = b.Segment(c.Wood, a0, ar, a1, ar, MaterialSlot.Primary, PatternDomain.Body, 0.2, 0.3);
                var p2 = b.Segment(c.Wood, a1, ar, a2, ar * 0.95, MaterialSlot.Primary, PatternDomain.Body, 0.3, 0.6);
                p1.DomainLength = c.WoodDomain; p2.DomainLength = c.WoodDomain;
                columns.Add((a1, upLen, ar));
            }
            // high density: vertical ribs and rows of spines
            int spineG = b.Group("spines", 0.2, 0, GroupFlags.NoContour, 0.3);
            foreach (var (bone, h, r) in columns)
            {
                bool main = bone == trunk[0];
                int ribs = main ? 8 : 6;
                for (int k = 0; k < ribs; k++)
                {
                    double a = k * DMath.TwoPi / ribs;
                    if (main)
                    {
                        for (int s = 0; s < 4; s++)
                        {
                            var (bb, at) = TrunkPoint(c, trunk, (s + 0.5) / 4.0 * 0.95);
                            var xf = b.RestWorld(bb);
                            Vec3 n = new Vec3(DMath.Cos(a), 0, DMath.Sin(a));
                            Vec3 p = at + n * (r * 0.98);
                            Rib(c, spineG, bb, xf, p, n, r, s);
                        }
                    }
                    else
                    {
                        var xf = b.RestWorld(bone);
                        Vec3 n = new Vec3(DMath.Cos(a), 0, DMath.Sin(a));
                        Vec3 p = xf.Origin + new Vec3(0, h * 0.5, 0) + n * (r * 0.98);
                        Rib(c, spineG, bone, xf, p, n, r, k);
                    }
                }
            }
            if (c.Fruit > 0.1)
            {
                var tx = b.RestWorld(trunk[trunk.Length - 1]);
                Flower(c, "bloom", trunk[trunk.Length - 1], tx.Origin + new Vec3(0, R0 * 0.85, 0), R0 * DMath.Lerp(0.5, 0.9, c.Fruit), 7, Vec3.UnitY);
            }
            b.Rig.Tuning["wind.trunk"] = 0.004;
            b.Rig.Tuning["wind.branch"] = 0.02;
            return H + R0;
        }

        private static void Rib(Ctx c, int spineG, int bone, Xform xf, Vec3 p, Vec3 n, double r, int s)
        {
            var b = c.B;
            var rib = b.Ellipsoid(c.Wood, bone, xf.InversePoint(p - n * (r * 0.12)), new Vec3(r * 0.9, r * 0.2, r * 0.2), MaterialSlot.Primary, PatternDomain.Body, 0.3, 0.7,
                AnatomyBuilder.QuatFromBasis(xf.Basis.Transposed * Mat3.LookAlong(Vec3.UnitY, n)));
            rib.MinDensity = Limbs.HdDensity;
            rib.DomainLength = c.WoodDomain;
            rib.Tag = "rib";
            if ((s & 1) == 0)
            {
                Vec3 tip = p + n * Math.Max(1.0, r * 0.3) + new Vec3(0, 0.4, 0);
                var sp = b.Cone(spineG, bone, xf.InversePoint(p), 0.35, bone, xf.InversePoint(tip), 0.25, MaterialSlot.Teeth, PatternDomain.None);
                sp.Flags |= PrimFlags.Thin | PrimFlags.NoPattern | PrimFlags.NoShadow | PrimFlags.Hard;
                sp.MinDensity = Limbs.HdDensity;
                sp.Tag = "spine";
            }
        }

        // ---- flowers

        private double BuildFlowers(Ctx c, CreatureGenome g, double size, int root)
        {
            var b = c.B;
            double H = DMath.Lerp(10, 26, size) * DMath.Lerp(0.7, 1.3, g.Get("trunk.height"));
            c.Wood = b.Group("stems", 0.6, 0, GroupFlags.None);
            c.Canopy = b.Group("leaves", 0.5, 0, GroupFlags.NoFarShade, 0.1);
            c.WoodDomain = H;
            int body = b.BoneAlong("trunk", root, new Vec3(0, 0.5, 0), Vec3.UnitY, Vec3.UnitX * -1, BoneRole.Pelvis, 0, 1);
            int top = b.BoneAlong("trunk.top", body, new Vec3(0, 1.5, 0), Vec3.UnitY, Vec3.UnitX * -1, BoneRole.Chest, 0, 1);
            b.Rig.Pelvis = body; b.Rig.Chest = top;
            b.Rig.Spine = new ChainRig { Name = "trunk", Bones = new[] { body, top }, Lengths = new[] { 1.0 }, TotalLength = 1 };
            int stems = (int)Math.Round(DMath.Lerp(2, 7, g.Get("branch.count")));
            double spread = H * DMath.Lerp(0.15, 0.45, g.Get("branch.angle"));
            double head = H * DMath.Lerp(0.12, 0.24, g.Get("canopy.size"));
            double topY = 0;
            for (int i = 0; i < stems; i++)
            {
                double a = i * 2.39996 + c.Rng.Range(-0.3, 0.3);
                double rr = i == 0 ? 0 : spread * Math.Sqrt(i / (double)stems);
                Vec3 at = new Vec3(DMath.Cos(a) * rr, 0, DMath.Sin(a) * rr);
                double h = H * c.Rng.Range(0.65, 1.0);
                Vec3 dir = (Vec3.UnitY + new Vec3(DMath.Cos(a), 0, DMath.Sin(a)) * (g.Get("trunk.lean") * 0.5)).NormalizedOr(Vec3.UnitY);
                var st = Branch(c, $"stem{i}", body, at, dir, h, 0.7, 0.55, 0.0, 2, true, 0);
                // two leaves on the stem
                for (int s = -1; s <= 1; s += 2)
                {
                    var xf = b.RestWorld(st[0]);
                    Vec3 lb = new Vec3(h * 0.25, 0, 0);
                    Vec3 tipL = lb + new Vec3(h * 0.12, s * h * 0.28, s * h * 0.1);
                    Vec3 w = Vec3.Cross(tipL - lb, Vec3.UnitX).NormalizedOr(Vec3.UnitZ) * (h * 0.06);
                    var l1 = b.Triangle(c.Canopy, st[0], lb, st[0], (lb + tipL) * 0.5 + w, st[0], tipL, MaterialSlot.Secondary, PatternDomain.None, 0);
                    var l2 = b.Triangle(c.Canopy, st[0], lb, st[0], tipL, st[0], (lb + tipL) * 0.5 - w, MaterialSlot.Secondary, PatternDomain.None, 0);
                    l1.Flags |= PrimFlags.TwoSided | PrimFlags.NoPattern | PrimFlags.NoShadow;
                    l2.Flags |= PrimFlags.TwoSided | PrimFlags.NoPattern | PrimFlags.NoShadow;
                    _ = xf;
                }
                var ex = b.RestWorld(st[st.Length - 1]);
                Flower(c, $"flower{i}", st[st.Length - 1], ex.Origin, head * c.Rng.Range(0.8, 1.15), 5 + (i % 3), (Vec3.UnitY * 0.6 + new Vec3(0.5, 0, -0.3)).Normalized());
                topY = Math.Max(topY, ex.Origin.Y + head);
            }
            b.Rig.Tuning["wind.trunk"] = 0.01;
            b.Rig.Tuning["wind.branch"] = 0.35;
            return topY;
        }

        /// <summary>Blossom: petals (two sided plates) around a disc, facing 'facing' (creature space).</summary>
        private static void Flower(Ctx c, string name, int bone, Vec3 center, double radius, int petals, Vec3 facing)
        {
            var b = c.B;
            int head = b.BoneAlong(name, bone, center, facing, Math.Abs(facing.Y) > 0.9 ? Vec3.UnitX : Vec3.UnitY, BoneRole.Other, 0, radius);
            int pg = b.Group(name + ".petals", 0.4, 0, GroupFlags.NoFarShade, 0.2);
            for (int i = 0; i < petals; i++)
            {
                double a = i / (double)petals * DMath.TwoPi;
                Vec3 dir = new Vec3(-0.12, DMath.Cos(a), DMath.Sin(a));
                Vec3 side = new Vec3(0, -DMath.Sin(a), DMath.Cos(a)) * (radius * 0.45);
                Vec3 tip = dir * (radius * 1.5), mid = dir * (radius * 0.85);
                var t1 = b.Triangle(pg, head, Vec3.Zero, head, mid + side, head, tip, MaterialSlot.Accent, PatternDomain.None);
                var t2 = b.Triangle(pg, head, Vec3.Zero, head, tip, head, mid - side, MaterialSlot.Accent, PatternDomain.None);
                t1.Flags |= PrimFlags.TwoSided | PrimFlags.NoShadow | PrimFlags.NoPattern;
                t2.Flags |= PrimFlags.TwoSided | PrimFlags.NoShadow | PrimFlags.NoPattern;
                t1.Tag = t2.Tag = "petal";
            }
            int dg = b.Group(name + ".disc", 0.6, 0, GroupFlags.None, 0.4);
            var disc = b.Ellipsoid(dg, head, new Vec3(radius * 0.08, 0, 0), new Vec3(radius * 0.25, radius * 0.42, radius * 0.42), MaterialSlot.Marking, PatternDomain.None);
            disc.Flags |= PrimFlags.NoPattern;
            disc.Tag = "disc";
        }
    }

    /// <summary>
    /// Flora palette: bark from the main colour genes, foliage from the leaf genes (its own outline so the
    /// crown reads against the sky), blossoms, fruit and mushroom caps from the blossom genes.
    /// </summary>
    public sealed class FloraPaletteRule : DefaultPaletteRule
    {
        public const string RuleId = "flora";
        public override string Id => RuleId;

        public override CreaturePalette Build(CreatureGenome genome)
        {
            var cg = ColorGenes.Read(genome);
            double bh = genome.GetOr("color.bloomHue", 350), bs = genome.GetOr("color.bloomSaturation", 0.7), bv = genome.GetOr("color.bloomValue", 0.6);
            cg.ClothHue = bh; cg.ClothSaturation = bs; cg.ClothValue = bv;
            var pal = new CreaturePalette { ColorKey = genome.ColorKey };
            BuildInto(pal, cg);
            var leaf = PrimaryRamp(new ColorGenes
            {
                Hue = genome.GetOr("color.leafHue", 132), Saturation = genome.GetOr("color.leafSaturation", 0.55), Value = genome.GetOr("color.leafValue", 0.5),
                HueShift = Math.Max(0.6, cg.HueShift), Outline = cg.Outline,
            });
            var li = new int[6];
            for (int i = 0; i < 6; i++) li[i] = pal.Add(ColorMath.ToRgba(leaf[i]), i == 0 ? "foliage outline" : "foliage");
            pal.SetRamp(MaterialSlot.Secondary, li[0], li[1], li[2], li[3], li[4], li[5]);
            var bloom = PrimaryRamp(new ColorGenes { Hue = bh, Saturation = bs, Value = bv, HueShift = cg.HueShift, Outline = cg.Outline });
            var bi = new int[6];
            for (int i = 0; i < 6; i++) bi[i] = pal.Add(ColorMath.ToRgba(bloom[i]), i == 0 ? "blossom outline" : "blossom");
            pal.SetRamp(MaterialSlot.Accent, bi[0], bi[1], bi[2], bi[3], bi[4], bi[5]);
            return pal;
        }
    }
}
