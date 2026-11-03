// Procedural Pixel Creatures - family "monster": monsters and horrors.
//
// Mimics (treasure chests that bite, on spider legs), cyclopes, minotaurs, skeletons, wraiths,
// gargoyles and eldritch horrors. Humanoid monsters share one compact upright body builder that uses
// the limb and head blocks (with their high density anatomy); the others are built from scratch:
// the mimic's lid is its jaw, the wraith is a floating hooded cloak whose tattered hem and sleeves are
// spring chains, the horror is a floating mass of eyes, a toothed maw and writhing tentacles.

#nullable enable

using System;
using System.Collections.Generic;
using PixelCreatures.Core.Anatomy;
using PixelCreatures.Core.Genetics;
using PixelCreatures.Core.Palettes;
using PixelCreatures.Core.Shapes;

namespace PixelCreatures.Core.Families
{
    public sealed class MonsterFamily : CreatureFamilyBase, ISurfaceCustomizer
    {
        public override string Id => "monster";
        public override string DisplayName => "Monsters";
        public override string Description => "Mimics, cyclopes, minotaurs, skeletons, wraiths, gargoyles and eldritch horrors: biting treasure chests on spider legs, one-eyed giants with clubs, bull-headed brutes, bony warriors, floating hooded spirits, winged stone guardians and many-eyed tentacled masses.";
        public override int Order => 95;
        public override CreationCategory Category => CreationCategory.Monster;

        private static readonly ArchetypeInfo[] ArchetypeList =
        {
            new ArchetypeInfo("mimic", "Mimic", 1.0),
            new ArchetypeInfo("cyclops", "Cyclops", 1.0),
            new ArchetypeInfo("minotaur", "Minotaur", 1.0),
            new ArchetypeInfo("skeleton", "Skeleton", 1.1),
            new ArchetypeInfo("wraith", "Wraith", 1.0),
            new ArchetypeInfo("gargoyle", "Gargoyle", 0.9),
            new ArchetypeInfo("horror", "Eldritch horror", 0.9),
        };

        public override IReadOnlyList<ArchetypeInfo> Archetypes => ArchetypeList;

        public static readonly (string, string)[] Forms =
        {
            ("mimic", "Mimic"), ("cyclops", "Cyclops"), ("minotaur", "Minotaur"), ("skeleton", "Skeleton"), ("wraith", "Wraith"), ("gargoyle", "Gargoyle"), ("horror", "Eldritch horror"),
        };
        public static readonly (string, string)[] Weapons = { ("none", "None"), ("club", "Club"), ("axe", "Axe"), ("sword", "Sword") };
        public static readonly (string, string)[] Metals = { ("steel", "Steel"), ("iron", "Iron"), ("bronze", "Bronze"), ("gold", "Gold") };

        protected override GeneSchema CreateSchema()
        {
            var b = new GeneSchemaBuilder(Id);
            b.Group("body", "Body", GeneStream.Anatomy)
             .Choice("form", "Monster", Forms, "skeleton", "Monster used as a template when rerolling.", 0.04)
             .Float("size", "Size", 0, 1, 0.5, "", 0.08)
             .Float("mass", "Build", 0, 1, 0.5, "Gaunt to hulking.", 0.1)
             .Float("limbs", "Limb length", 0, 1, 0.5, "", 0.1)
             .Float("hunch", "Hunch", 0, 1, 0.3, "Posture of humanoid monsters.", 0.1);
            b.Group("head", "Head", GeneStream.Anatomy)
             .Float("head.size", "Head size", 0, 1, 0.5, "", 0.1)
             .Float("eyes", "Eyes", 0, 1, 0.5, "Eye size, and the number of eyes of horrors and mimics.", 0.1)
             .Float("horns", "Horns", 0, 1, 0.4, "", 0.1)
             .Float("teeth", "Teeth", 0, 1, 0.5, "Size of teeth and fangs.", 0.1);
            b.Group("extras", "Extras", GeneStream.Anatomy)
             .Float("tentacles", "Tentacles", 0, 1, 0.5, "Tentacles, tatters and legs.", 0.12)
             .Choice("weapon", "Weapon", Weapons, "none", "", 0.06)
             .Float("wings", "Wing span", 0, 1, 0.5, "Gargoyle wings.", 0.1)
             .Float("tail", "Tail", 0, 1, 0.4, "", 0.1);
            b.Group("gearcolor", "Cloth & metal", GeneStream.Color)
             .Hue("color.clothHue", "Cloth hue", 260, "Cloaks, loincloths and wood of mimics.")
             .Float("color.clothSaturation", "Cloth saturation", 0, 1, 0.3, "", 0.12)
             .Float("color.clothValue", "Cloth brightness", 0, 1, 0.25, "", 0.12)
             .Choice("color.metal", "Metal", Metals, "iron", "Weapons, bands and rings.", 0.1);
            SharedGenes.AddColorGenes(b);
            SharedGenes.AddPatternGenes(b, "hide");
            SharedGenes.AddMotionGenes(b);
            return b.Build();
        }

        public override void SampleAnatomy(double[] v, Rng rng, string arch)
        {
            SetChoice(v, "form", arch);
            const double sp = 0.12;
            (double size, double mass, double limbs, double hunch, double head, double eyes, double horns, double teeth, double tent, double wings, double tail) p = arch switch
            {
                "mimic" => (0.4, 0.5, 0.5, 0.0, 0.5, 0.5, 0.0, 0.7, 0.5, 0, 0),
                "cyclops" => (0.65, 0.7, 0.55, 0.35, 0.55, 0.6, 0.35, 0.5, 0, 0, 0),
                "minotaur" => (0.6, 0.75, 0.55, 0.3, 0.5, 0.4, 0.7, 0.4, 0, 0, 0.5),
                "skeleton" => (0.45, 0.15, 0.55, 0.15, 0.5, 0.5, 0.1, 0.5, 0, 0, 0),
                "wraith" => (0.5, 0.3, 0.6, 0.35, 0.5, 0.5, 0.1, 0.3, 0.6, 0, 0),
                "gargoyle" => (0.45, 0.5, 0.45, 0.55, 0.5, 0.5, 0.6, 0.6, 0, 0.55, 0.6),
                _ => (0.55, 0.6, 0.5, 0.0, 0.5, 0.6, 0.2, 0.7, 0.6, 0, 0),
            };
            Around(v, rng, "size", p.size, 0.12);
            Around(v, rng, "mass", p.mass, sp);
            Around(v, rng, "limbs", p.limbs, sp);
            Around(v, rng, "hunch", p.hunch, sp);
            Around(v, rng, "head.size", p.head, sp);
            Around(v, rng, "eyes", p.eyes, sp * 1.5);
            Around(v, rng, "horns", p.horns, sp);
            Around(v, rng, "teeth", p.teeth, sp);
            Around(v, rng, "tentacles", p.tent, sp * 1.5);
            Around(v, rng, "wings", p.wings, sp);
            Around(v, rng, "tail", p.tail, sp);
            SetChoice(v, "weapon", arch switch
            {
                "cyclops" => PickWeighted(rng, ("club", 3.0), ("none", 1.0)),
                "minotaur" => PickWeighted(rng, ("axe", 3.0), ("club", 1.0), ("none", 0.5)),
                "skeleton" => PickWeighted(rng, ("sword", 3.0), ("axe", 1.0), ("none", 1.0)),
                _ => "none",
            });
        }

        public override FamilyStyleHints StyleHints(string arch)
        {
            var h = new FamilyStyleHints
            {
                Materials = new Dictionary<string, double> { ["hide"] = 1 },
                Patterns = new Dictionary<string, double> { ["none"] = 3, ["mottled"] = 1 },
                Accents = new Dictionary<string, double> { ["bone"] = 2, ["dark"] = 1 },
                SocksChance = 0, MaskChance = 0, PiebaldChance = 0, GlowChance = 0.2,
                CountershadeMin = 0.1, CountershadeMax = 0.5,
            };
            switch (arch)
            {
                case "mimic":
                    h.Materials = new Dictionary<string, double> { ["hide"] = 1 };
                    h.Coats = new[] { Coats.Crimson(1.5), Coats.Violet(1), Coats.Rust(1), Coats.Pink(0.6) };
                    h.Accents = new Dictionary<string, double> { ["bone"] = 1 };
                    h.GlowChance = 0.4;
                    break;
                case "cyclops":
                    h.Coats = new[] { Coats.Tan(1.5), Coats.Olive(1), Coats.Grey(1), Coats.Teal(0.6), Coats.Pink(0.5) };
                    break;
                case "minotaur":
                    h.Materials = new Dictionary<string, double> { ["fur"] = 1 };
                    h.Coats = new[] { Coats.Chocolate(2), Coats.Black(1.5), Coats.Brown(1.5), Coats.Red(0.6) };
                    h.Accents = new Dictionary<string, double> { ["bone"] = 2, ["dark"] = 1 };
                    break;
                case "skeleton":
                    h.Coats = new[] { Coats.Cream(3), Coats.Albino(1), Coats.Sand(1), Coats.Silver(0.5) };
                    h.Patterns = new Dictionary<string, double> { ["none"] = 3, ["mottled"] = 0.5 };
                    h.CountershadeMin = 0; h.CountershadeMax = 0.1;
                    h.GlowChance = 0.9;
                    break;
                case "wraith":
                    h.Coats = new[] { Coats.Slate(2), Coats.Black(1), Coats.Silver(0.6), Coats.Teal(0.4) };
                    h.GlowChance = 1.0;
                    break;
                case "gargoyle":
                    h.Materials = new Dictionary<string, double> { ["stone"] = 1 };
                    h.Coats = new[] { Coats.Grey(2), Coats.Slate(2), Coats.Sand(0.6), Coats.Olive(0.3) };
                    h.Patterns = new Dictionary<string, double> { ["mottled"] = 2, ["none"] = 1 };
                    h.GlowChance = 0.5;
                    h.CountershadeMin = 0; h.CountershadeMax = 0.2;
                    break;
                default:
                    h.Materials = new Dictionary<string, double> { ["slime"] = 2, ["hide"] = 1 };
                    h.Coats = new[] { Coats.Violet(2), Coats.Teal(1.2), Coats.Olive(1), Coats.Crimson(0.8), Coats.Muted(1) };
                    h.Patterns = new Dictionary<string, double> { ["mottled"] = 1.5, ["spots"] = 1, ["none"] = 1 };
                    h.GlowChance = 0.5;
                    break;
            }
            return h;
        }

        public override void AdjustSample(double[] values, Rng rng, string arch)
        {
            // cloth: dark cloaks for wraiths, worn loincloths, wooden chests for mimics
            var (hue, s0, s1, v0, v1) = arch switch
            {
                "mimic" => (55.0, 0.35, 0.55, 0.22, 0.38),
                "wraith" => (rng.Chance(0.5) ? 270.0 : 200.0, 0.1, 0.35, 0.1, 0.28),
                _ => (55.0, 0.2, 0.45, 0.2, 0.4),
            };
            Set(values, "color.clothHue", hue + rng.Gaussian(0, 8, 2));
            Set(values, "color.clothSaturation", rng.Range(s0, s1));
            Set(values, "color.clothValue", rng.Range(v0, v1));
            SetChoice(values, "color.metal", arch == "mimic" ? PickWeighted(rng, ("gold", 2.0), ("iron", 1.0)) : PickWeighted(rng, ("iron", 2.0), ("steel", 1.0), ("bronze", 0.6)));
            if (arch is "skeleton" or "wraith" || (arch == "gargoyle" && rng.Chance(0.5)))
            {
                Set(values, "color.glow", rng.Range(0.65, 1.0));
                Set(values, "color.eyeHue", PickWeighted(rng, ("200", 2.0), ("140", 1.0), ("30", 1.0), ("300", 0.7)) switch { var s => double.Parse(s, System.Globalization.CultureInfo.InvariantCulture) + rng.Gaussian(0, 8, 2) });
            }
        }

        public void CustomizeSurface(CreatureGenome genome, SurfaceSpec surface)
        {
            string form = genome.GetChoice("form");
            // mimics: the box is wood (cloth genes), the inside is flesh (primary)
            if (form == "mimic") surface.OverrideSlotStyle(MaterialSlot.Cloth, MaterialStyles.Wood);
            if (form == "wraith") surface.OverrideSlotStyle(MaterialSlot.Cloth, MaterialStyles.Membrane);
        }

        // ------------------------------------------------------------------ anatomy

        public override CreatureAnatomy BuildAnatomy(CreatureGenome g, CreatureRegistry registry)
        {
            var b = new AnatomyBuilder(Id, g.AnatomySeed);
            string form = g.GetChoice("form");
            b.Trait(form);
            double size = g.Get("size");
            switch (form)
            {
                case "mimic": BuildMimic(b, g, size); break;
                case "wraith": BuildWraith(b, g, size); break;
                case "horror": BuildHorror(b, g, size); break;
                default: BuildHumanoid(b, g, form, size); break;
            }
            return b.Build(g.AnatomyKey);
        }

        // ---- humanoid monsters: cyclops, minotaur, skeleton, gargoyle

        private static void BuildHumanoid(AnatomyBuilder b, CreatureGenome g, string form, double size)
        {
            double baseL = DMath.Lerp(18, 40, DMath.Pow(size, 1.05)) * (form == "cyclops" ? 1.15 : 1.0);
            double mass = g.Get("mass");
            bool bony = form == "skeleton";
            bool stone = form == "gargoyle";
            bool digi = form == "gargoyle";
            bool hooved = form == "minotaur";
            double hunch = g.Get("hunch");
            double torsoH = baseL * DMath.Lerp(0.75, 1.0, mass);
            double hipR = baseL * (bony ? 0.12 : DMath.Lerp(0.2, 0.33, mass));
            double chestR = baseL * (bony ? 0.2 : DMath.Lerp(0.26, 0.42, mass));
            double legLen = baseL * DMath.Lerp(0.7, 1.2, g.Get("limbs")) * (digi ? 0.85 : 1.0);
            int root = b.Bone("root", -1, Vec3.Zero, BoneRole.Root);
            double hipH = legLen * (digi ? 0.82 : 0.94);
            double lean = DMath.Lerp(4, 40, hunch) * DMath.Deg2Rad;
            Vec3 spineDir = new Vec3(DMath.Sin(lean), DMath.Cos(lean), 0);
            Vec3 back = new Vec3(-1, 0, 0);
            Vec3 P = new Vec3(0, hipH, 0);
            Vec3 C = P + spineDir * torsoH;
            int pelvis = b.BoneAlong("pelvis", root, P, spineDir, back, BoneRole.Pelvis, 0, torsoH * 0.5);
            int spine = b.BoneAlong("spine", pelvis, P + spineDir * (torsoH * 0.5), spineDir, back, BoneRole.Spine, 0, torsoH * 0.5);
            int chest = b.BoneAlong("chest", spine, C, spineDir, back, BoneRole.Chest, 0, chestR);
            b.Rig.Root = root; b.Rig.Pelvis = pelvis; b.Rig.Chest = chest; b.Rig.Upright = true;
            b.Rig.Spine = new ChainRig { Name = "spine", Bones = new[] { pelvis, spine, chest }, Lengths = new[] { torsoH * 0.5, torsoH * 0.5 }, TotalLength = torsoH };
            int body = b.Group("body", bony ? 0.6 : DMath.Clamp(chestR * 0.35, 1.5, 4.0), 0, stone ? GroupFlags.CreaseShade : GroupFlags.None);
            double dom = torsoH + hipR + chestR;
            PrimitiveDef E(int bone, Vec3 c, Vec3 r, double u0, double u1, string tag)
            {
                var e = b.Ellipsoid(body, bone, c, r, MaterialSlot.Primary, PatternDomain.Body, u0, u1);
                e.DomainLength = dom; e.Tag = tag;
                if (stone) { e.Flags |= PrimFlags.Faceted; e.FacetSeed = (uint)(tag.Length * 7919 + 3); }
                return e;
            }
            if (bony)
            {
                // spine column, pelvis, and a rib cage of arching ribs around an empty chest
                var col0 = b.Segment(body, pelvis, baseL * 0.035, chest, baseL * 0.04, MaterialSlot.Primary, PatternDomain.Body, 0, 1);
                col0.DomainLength = dom;
                E(pelvis, new Vec3(hipR * 0.1, 0, 0), new Vec3(hipR * 0.55, hipR * 0.7, hipR * 1.25), 0, 0.2, "pelvisbone");
                int rg = b.Group("ribs", 0.4, 0, GroupFlags.None, 0.1);
                for (int i = 0; i < 4; i++)
                {
                    double x = torsoH * DMath.Lerp(0.05, 0.42, i / 3.0);
                    double rr = chestR * DMath.Lerp(0.85, 1.05, Math.Sin(DMath.Pi * (i + 0.5) / 4));
                    for (int s = -1; s <= 1; s += 2)
                    {
                        // from the spine (back, +Y) around the side to the sternum (front, -Y)
                        Vec3 a = new Vec3(x, rr * 0.35, s * rr * 0.25), m = new Vec3(x - rr * 0.1, -rr * 0.05, s * rr * 0.95), f = new Vec3(x - rr * 0.18, -rr * 0.75, s * rr * 0.3);
                        var r1 = b.Cone(rg, chest, a, 0.55, chest, m, 0.5, MaterialSlot.Primary);
                        var r2 = b.Cone(rg, chest, m, 0.5, chest, f, 0.45, MaterialSlot.Primary);
                        foreach (var r in new[] { r1, r2 }) { r.Flags |= PrimFlags.NoPattern | PrimFlags.Hard; r.Tag = "rib"; }
                    }
                }
                var sternum = b.Cone(rg, chest, new Vec3(-torsoH * 0.02, -chestR * 0.75, 0), 0.6, chest, new Vec3(-torsoH * 0.4, -chestR * 0.75, 0), 0.5, MaterialSlot.Primary);
                sternum.Flags |= PrimFlags.NoPattern;
                // shoulder girdle
                E(chest, new Vec3(-chestR * 0.2, 0, 0), new Vec3(chestR * 0.25, chestR * 0.35, chestR * 1.15), 0.9, 1, "clavicles");
            }
            else
            {
                E(pelvis, new Vec3(hipR * 0.1, hipR * 0.05, 0), new Vec3(hipR * 0.95, hipR * 0.9, hipR * 1.15), 0, 0.3, "hips");
                E(spine, new Vec3(-torsoH * 0.05, -hipR * 0.15, 0), new Vec3(torsoH * 0.5, (hipR + chestR) * 0.5, (hipR + chestR) * 0.52), 0.25, 0.7, "belly");
                E(chest, new Vec3(-chestR * 0.3, chestR * 0.05, 0), new Vec3(chestR * 0.95, chestR * 0.82, chestR * 1.2), 0.6, 1.0, "chest");
                // high density: pectorals, trapezius and abdominal plates
                for (int s = -1; s <= 1; s += 2)
                {
                    var pec = E(chest, new Vec3(-chestR * 0.35, -chestR * 0.55, s * chestR * 0.48), new Vec3(chestR * 0.55, chestR * 0.35, chestR * 0.5), 0.75, 0.9, "pectoral");
                    pec.MinDensity = Limbs.HdDensity;
                    var trap = E(chest, new Vec3(chestR * 0.15, chestR * 0.35, s * chestR * 0.55), new Vec3(chestR * 0.45, chestR * 0.45, chestR * 0.45), 0.85, 1.0, "trapezius");
                    trap.MinDensity = Limbs.HdDensity;
                }
                for (int k = 0; k < 3; k++)
                {
                    var ab = E(spine, new Vec3(-torsoH * (0.25 - 0.15 * k), -(hipR + chestR) * 0.45, 0), new Vec3(torsoH * 0.09, (hipR + chestR) * 0.12, (hipR + chestR) * 0.28), 0.35, 0.6, "abs");
                    ab.MinDensity = Limbs.HdDensity;
                }
                if (form == "cyclops" || form == "minotaur")
                {
                    // loincloth: a skirt ring at the hips and a hanging flap
                    int cg = b.Group("loincloth", 0.8, 0, GroupFlags.None, 0.3);
                    var belt = b.Ellipsoid(cg, pelvis, new Vec3(hipR * 0.05, 0, 0), new Vec3(hipR * 0.5, hipR * 1.0, hipR * 1.2), MaterialSlot.Cloth, PatternDomain.Body);
                    belt.Flags |= PrimFlags.NoPattern;
                    var flap = Chains.Build(b, new ChainSpec
                    {
                        Name = "flap", ParentBone = pelvis, Base = P + new Vec3(hipR * 0.95, -hipR * 0.1, 0), Direction = new Vec3(0.15, -1, 0), Length = legLen * 0.38, Segments = 2,
                        RadiusBase = hipR * 0.45, RadiusTip = hipR * 0.35, Material = MaterialSlot.Cloth, Domain = PatternDomain.Appendage, Role = BoneRole.Antenna,
                        Stiffness = 0.3, Droop = 0.8, Flags = PrimFlags.NoPattern, Thin = true, Group = cg,
                    });
                    b.Rig.Appendages.Add(flap);
                }
            }

            // ---- head
            double skullL = baseL * DMath.Lerp(0.42, 0.66, g.Get("head.size")) * (form == "cyclops" ? 1.1 : 1.0);
            Vec3 neckDir = Vec3.Lerp(Vec3.UnitY, Vec3.UnitX, DMath.Lerp(0.12, 0.65, hunch)).Normalized();
            double neckLen = Math.Max(1.5, (0.24 * chestR + 0.3 * skullL * 0.8) * DMath.Lerp(1.0, 0.65, hunch)) * (bony ? 1.3 : 1.0);
            Vec3 N0 = C + spineDir * (chestR * 0.45) + new Vec3(chestR * 0.12, 0, 0);
            int neck = b.BoneAlong("neck", chest, N0, neckDir, back, BoneRole.Neck, 0, neckLen);
            double eyes = g.Get("eyes");
            double teeth = g.Get("teeth");
            var hs = new HeadSpec
            {
                ParentBone = neck, Position = N0 + neckDir * neckLen, Forward = new Vec3(1, DMath.Lerp(0.05, -0.12, hunch), 0),
                SkullLength = skullL, SkullHeight = skullL * 0.9, SkullWidth = skullL * 0.85, SkullOffset = 0.22,
                SnoutLength = skullL * 0.2, SnoutRadius = skullL * 0.27, SnoutTaper = 0.9, SnoutHeight = -0.18, Nose = NoseKind.Flat, NoseSize = 1.0,
                JawLength = 0.85, JawRadius = skullL * 0.24, JawDepth = 1.0, Brow = 0.6, Cheeks = 0.2,
                EyeSize = DMath.Clamp(DMath.Lerp(1.6, 4.0, eyes) * Math.Sqrt(skullL / 9.0), 1.4, 6.0), EyeStyle = EyeStyle.Round, EyeForward = 0.9, EyeHeight = 0.2,
                Teeth = teeth > 0.55 ? TeethKind.Fangs : TeethKind.Small, TeethSize = DMath.Clamp(skullL / 8.0, 0.8, 1.7) * DMath.Lerp(0.8, 1.3, teeth),
                Group = -1, BlendRadius = DMath.Clamp(skullL * 0.2, 1.2, 2.6), DomainLength = skullL * 1.8, JawOpenMax = 0.65,
            };
            switch (form)
            {
                case "cyclops":
                    hs.EyeCount = 1; hs.EyeSize = DMath.Clamp(skullL * DMath.Lerp(0.3, 0.45, eyes), 2.5, 9); hs.Brow = 0.9; hs.Teeth = TeethKind.Tusks; hs.Nose = NoseKind.Round;
                    break;
                case "minotaur":
                    hs.SkullLength = skullL * 0.85; hs.SnoutLength = skullL * 0.7; hs.SnoutRadius = skullL * 0.32; hs.SnoutWidth = 1.3; hs.Nose = NoseKind.Pad; hs.NoseSize = 1.25;
                    hs.EyeForward = 0.4; hs.EyeStyle = EyeStyle.Bead; hs.Teeth = TeethKind.Small; hs.Forward = new Vec3(1, -0.35, 0).Normalized();
                    break;
                case "skeleton":
                    // skull: hollow sockets with glowing points, bare teeth
                    hs.SkullHeight = skullL * 0.95; hs.SnoutLength = skullL * 0.12; hs.SnoutRadius = skullL * 0.22; hs.NoseSize = 0.3; hs.Nose = NoseKind.Flat;
                    hs.EyeStyle = EyeStyle.Glow; hs.EyeSize = DMath.Clamp(skullL * 0.16, 1.2, 3.5); hs.Brow = 0.9; hs.Teeth = TeethKind.Small; hs.TeethSize *= 1.3; hs.JawRadius = skullL * 0.18; hs.Structure = false;
                    break;
                case "gargoyle":
                    hs.SnoutLength = skullL * 0.45; hs.SnoutRadius = skullL * 0.24; hs.Nose = NoseKind.Pad; hs.EyeStyle = EyeStyle.Glow; hs.Teeth = TeethKind.Fangs; hs.Brow = 1.0; hs.Cheeks = 0.6;
                    break;
            }
            if (g.Get("color.glow") > 0.5 && form != "minotaur" && form != "cyclops") hs.EyeStyle = EyeStyle.Glow;
            var head = Heads.Build(b, hs);
            if (stone) foreach (var p in b.Primitives) if (p.Group == head.Group) { p.Flags |= PrimFlags.Faceted; p.FacetSeed = 977; }
            if (bony)
            {
                // dark eye sockets and nasal cavity behind the glowing pupils
                int sg = b.Group("sockets", 0.3, 0, GroupFlags.NoOutline, 0.15);
                for (int s = -1; s <= 1; s += 2)
                {
                    var sock = b.Ellipsoid(sg, head.Head, head.SkullCenterLocal + new Vec3(head.SkullRadii.X * 0.86, head.SkullRadii.Y * 0.1, s * head.SkullRadii.Z * 0.42),
                        new Vec3(skullL * 0.1, skullL * 0.17, skullL * 0.15), MaterialSlot.Marking, PatternDomain.None);
                    sock.Flags |= PrimFlags.NoPattern | PrimFlags.Hard | PrimFlags.NoShadow;
                    sock.ShadeBias = -3;
                }
            }
            var nk = b.Cone(body, neck, Vec3.Zero, bony ? baseL * 0.035 : Math.Max(1.2, chestR * DMath.Lerp(0.45, 0.62, mass)), head.Head, head.SkullCenterLocal * 0.4,
                bony ? baseL * 0.03 : Math.Max(1.0, skullL * 0.34), MaterialSlot.Primary, PatternDomain.Neck, 0, 1);
            nk.DomainLength = neckLen + 2;
            b.Rig.Neck = new ChainRig { Name = "neck", Bones = new[] { neck, head.Head }, Lengths = new[] { neckLen }, TotalLength = neckLen };
            double hornsG = g.Get("horns");
            if (form == "minotaur")
            {
                Horns.Build(b, new HornSpec { Kind = HornKind.Curved, HeadBone = head.Head, Base = head.SkullCenterLocal + new Vec3(0, skullL * 0.35, skullL * 0.3), Size = skullL * DMath.Lerp(0.45, 0.85, hornsG), Thickness = 0.85 });
                Ears.Build(b, new EarSpec { Kind = EarKind.Floppy, HeadBone = head.Head, Base = head.SkullCenterLocal + new Vec3(-skullL * 0.1, skullL * 0.2, skullL * 0.38), Size = skullL * 0.4 });
                // a nose ring
                int rgp = b.Group("nosering", 0.2, 0, GroupFlags.None, 0.4);
                var ring = b.Ellipsoid(rgp, head.Head, head.SnoutTipLocal + new Vec3(skullL * 0.12, -skullL * 0.08, 0), new Vec3(skullL * 0.1, skullL * 0.1, 0.4), MaterialSlot.Metal);
                ring.Flags |= PrimFlags.NoPattern | PrimFlags.Hard;
            }
            else if (form == "gargoyle" || (form == "cyclops" && hornsG > 0.4))
            {
                Horns.Build(b, new HornSpec { Kind = form == "cyclops" ? HornKind.Short : HornKind.Backswept, HeadBone = head.Head, Base = head.SkullCenterLocal + new Vec3(0, skullL * 0.38, skullL * 0.22), Size = skullL * DMath.Lerp(0.4, 1.0, hornsG) });
                if (form == "gargoyle") Ears.Build(b, new EarSpec { Kind = EarKind.Elf, HeadBone = head.Head, Base = head.SkullCenterLocal + new Vec3(-skullL * 0.05, skullL * 0.1, skullL * 0.42), Size = skullL * 0.55 });
            }
            else if (form == "cyclops")
                Ears.Build(b, new EarSpec { Kind = EarKind.Round, HeadBone = head.Head, Base = head.SkullCenterLocal + new Vec3(-skullL * 0.05, -skullL * 0.04, skullL * 0.42), Size = skullL * 0.4 });

            // ---- legs
            double legTop = bony ? baseL * 0.045 : Math.Max(1.3, hipR * DMath.Lerp(0.5, 0.75, mass));
            double footLen = Math.Max(2.0, baseL * 0.3);
            for (int side = -1; side <= 1; side += 2)
            {
                Vec3 hipJ = P + new Vec3(0, -hipR * 0.3, side * Math.Max(hipR * 0.62, baseL * 0.1));
                Vec3 contact = new Vec3(hipJ.X + footLen * 0.1, 0, side * Math.Max(hipR * 0.7, baseL * 0.12));
                var spec = new LegSpec
                {
                    Name = "leg" + (side < 0 ? "L" : "R"), Side = side, Front = false, AnchorBone = pelvis, Hip = hipJ, Contact = contact,
                    Style = hooved ? LegStyle.Unguligrade : (digi ? LegStyle.Digitigrade : LegStyle.Plantigrade), Cloven = hooved,
                    RadiusTop = legTop, RadiusKnee = Math.Max(0.8, legTop * (bony ? 0.9 : 0.7)), RadiusAnkle = Math.Max(0.7, legTop * (bony ? 0.8 : 0.5)), RadiusFoot = Math.Max(0.8, legTop * 0.6),
                    Haunch = bony ? 0 : 0.9, PawRadius = Math.Max(0.9, bony ? baseL * 0.05 : legTop * 0.7), FootMaterial = MaterialSlot.Primary,
                    Claws = digi || form == "cyclops" ? 3 : 0, ClawSize = DMath.Lerp(0.6, 1.4, g.Get("teeth")), Phase = side < 0 ? 0.0 : 0.5,
                    LiftHeight = Math.Max(1.5, legLen * 0.16), MetaAngle = 18, BlendRadius = bony ? 0.5 : DMath.Clamp(legTop * 0.5, 0.8, 2.2), DomainLength = legLen,
                    Muscle = bony ? 0 : DMath.Lerp(0.6, 1.0, mass), Feathering = hooved ? 0.5 : 0,
                };
                if (digi || hooved)
                {
                    spec.Length = hipJ.Y * 1.22; spec.Upper = 0.38; spec.Lower = 0.38; spec.Distal = 0.24; spec.PawLength = footLen * (hooved ? 0.45 : 0.8);
                }
                else
                {
                    double legPart = hipJ.Y * 1.03;
                    spec.Length = legPart + footLen; spec.Upper = legPart * 0.5 / spec.Length; spec.Lower = legPart * 0.5 / spec.Length; spec.Distal = footLen / spec.Length; spec.PawLength = footLen;
                }
                var leg = Limbs.Build(b, spec);
                if (stone) MarkFaceted(b, leg.Group);
                b.Rig.Legs.Add(leg);
            }

            // ---- arms
            double armLen = baseL * DMath.Lerp(0.85, 1.45, g.Get("limbs")) * (form == "gargoyle" ? 1.1 : 1.0);
            double armTop = bony ? baseL * 0.04 : Math.Max(1.1, chestR * DMath.Lerp(0.32, 0.52, mass));
            double handR = Math.Max(0.9, bony ? baseL * 0.045 : armTop * 0.85);
            LegRig? right = null;
            for (int side = -1; side <= 1; side += 2)
            {
                Vec3 sh = C + new Vec3(-chestR * 0.1, -chestR * 0.18, side * chestR * (bony ? 1.05 : 1.15));
                Vec3 wrist = sh + new Vec3(armLen * 0.12, -armLen * 0.8, side * armLen * 0.12);
                var arm = Limbs.Build(b, new LegSpec
                {
                    Name = "arm" + (side < 0 ? "L" : "R"), Side = side, Front = true, IsArm = true, Hand = true, AnchorBone = chest, Hip = sh, Contact = wrist,
                    Style = LegStyle.Plantigrade, Length = armLen, Upper = 0.44, Lower = 0.4, Distal = 0.16,
                    RadiusTop = armTop, RadiusKnee = Math.Max(0.8, armTop * (bony ? 0.9 : 0.72)), RadiusAnkle = Math.Max(0.7, armTop * (bony ? 0.8 : 0.58)), RadiusFoot = handR,
                    Haunch = bony ? 0 : (stone ? 1.3 : 1.0), PawLength = Math.Max(1.6, handR * 2.0), PawRadius = handR,
                    Claws = form == "gargoyle" || form == "skeleton" ? 3 : 0, ClawSize = 1.0, BlendRadius = bony ? 0.5 : DMath.Clamp(armTop * 0.5, 0.8, 2.0), DomainLength = armLen,
                    Muscle = bony ? 0 : DMath.Lerp(0.6, 1.0, mass),
                });
                arm.Phase = side < 0 ? 0.5 : 0.0;
                if (stone) MarkFaceted(b, arm.Group);
                b.Rig.Arms.Add(arm);
                if (side > 0) right = arm;
            }
            string weapon = g.GetChoice("weapon");
            if (weapon != "none" && right != null)
            {
                Weapon(b, right, weapon, baseL * (form == "cyclops" ? 1.1 : 0.9), handR);
                b.Rig.Tuning["action.armSide"] = 1;
                b.Trait("weapon:" + weapon);
            }

            // ---- tail and wings
            double tail = g.Get("tail");
            if ((form == "minotaur" || form == "gargoyle") && tail > 0.2)
            {
                b.Rig.Tail = Chains.Build(b, new ChainSpec
                {
                    Name = "tail", ParentBone = pelvis, Base = P + new Vec3(-hipR * 0.9, -hipR * 0.1, 0), Direction = new Vec3(-0.6, -0.8, 0), Length = baseL * DMath.Lerp(0.4, 1.0, tail),
                    Segments = 5, RadiusBase = Math.Max(0.8, hipR * 0.2), RadiusTip = 0.5, Curl = 0.8, Tip = form == "gargoyle" ? TailTip.Spade : TailTip.Tuft,
                    TipMaterial = form == "gargoyle" ? MaterialSlot.Primary : MaterialSlot.Hair, TipSize = 1.4, Group = body, Stiffness = 0.4,
                });
            }
            bool winged = form == "gargoyle" && g.Get("wings") > 0.15;
            double span = 0;
            if (winged)
            {
                span = baseL * DMath.Lerp(0.9, 1.7, g.Get("wings"));
                Wings.BuildPair(b, new WingSpec
                {
                    Kind = WingKind.Membrane, AnchorBone = chest, Root = C + new Vec3(-chestR * 0.6, chestR * 0.2, chestR * 0.6), Span = span, Chord = span * 0.5,
                    Raise = 0.5, SweepBack = 0.4, BoneRadius = Math.Max(0.8, chestR * 0.18), Fingers = 3, BodyBone = spine, BodyAttach = P + new Vec3(-hipR * 0.6, hipR * 0.5, hipR * 0.5),
                    ClawSize = 1.2, BoneMaterial = MaterialSlot.Primary, MembraneMaterial = MaterialSlot.Membrane,
                });
                b.Trait("wings");
            }

            // ---- rig & metrics
            b.Rig.Locomotion = winged ? LocomotionKind.Fly : LocomotionKind.Legged;
            b.Rig.LocomotionModule = winged ? "flight" : "legged";
            if (winged)
            {
                b.Rig.Tuning["flight.flapHz"] = 2.4;
                b.Rig.Altitude = Math.Max(12, hipH + baseL * 0.8);
            }
            b.Rig.Action = form switch { "minotaur" => weapon == "none" ? ActionStyle.Slam : ActionStyle.Strike, "cyclops" => ActionStyle.Slam, "gargoyle" => winged ? ActionStyle.WingBuffet : ActionStyle.Pounce, _ => ActionStyle.Strike };
            b.Rig.Rest = RestStyle.SitSlump;
            b.Rig.Tuning["rest.bodyHeight"] = Math.Max(hipR * 0.95, 1.5);
            b.Rig.Tuning["gait.walkDuty"] = 0.62;
            b.Rig.Tuning["gait.runDuty"] = 0.36;
            b.Metrics.LegLength = legLen;
            b.Metrics.HipHeight = hipH;
            b.Metrics.BackHeight = C.Y + chestR;
            b.Metrics.BodyLength = (C.Y + chestR + skullL) * 0.7;
            b.Metrics.Mass = DMath.Pow(baseL / 26.0, 3) * DMath.Lerp(0.5, 1.8, mass) * (bony ? 0.4 : 1.0);
            b.Metrics.WalkSpeed = Math.Sqrt(0.3 * 400 * legLen) * DMath.Lerp(0.8, 1.05, g.GetOr("motion.speed", 0.5));
            b.Metrics.RunSpeed = Math.Sqrt(1.8 * 400 * legLen) * DMath.Lerp(0.8, 1.2, g.GetOr("motion.speed", 0.5)) * (winged ? 1.4 : 1.0);
            b.Metrics.PatternScale = DMath.Clamp(Math.Sqrt(baseL / 26.0), 0.75, 1.5);
            // swung weapons sweep far around the body
            double weaponLen = weapon != "none" ? baseL * (form == "cyclops" ? 1.1 : 0.9) : 0;
            b.AnimationMargin = Math.Max(8, baseL * 0.45 + weaponLen * 0.6);
            b.ExtraHeadroom = winged ? Math.Max(8, span * 0.8 + b.Rig.Altitude) : Math.Max(6, skullL * 0.6 + (weapon != "none" ? baseL * 0.4 : 0));
        }

        private static void MarkFaceted(AnatomyBuilder b, int group)
        {
            foreach (var p in b.Primitives) if (p.Group == group) { p.Flags |= PrimFlags.Faceted; p.FacetSeed = (uint)(group * 131 + 7); }
        }

        /// <summary>A weapon held in the hand: club, axe or sword, along the hand's grip direction.</summary>
        private static void Weapon(AnatomyBuilder b, LegRig arm, string kind, double len, double handR)
        {
            int hand = arm.JointBones[2];
            int g = b.Group("weapon", 0.4, arm.Side, GroupFlags.NoFarShade, 0.6);
            // the grip runs across the fist (hand frame +X along the fingers): the weapon points down-forward
            Vec3 grip = new Vec3(handR * 0.3, -handR * 0.2, 0);
            Vec3 dir = new Vec3(0.35, -0.2, 1).Normalized() * (arm.Side < 0 ? -1 : 1);
            dir = new Vec3(0.55, 0.75, 0).Normalized();
            Vec3 tip = grip + dir * len;
            switch (kind)
            {
                case "club":
                {
                    var c = b.Cone(g, hand, grip - dir * (len * 0.1), Math.Max(0.6, handR * 0.45), hand, tip, Math.Max(1.2, len * 0.11), MaterialSlot.Cloth);
                    c.Tag = "club";
                    // knots and studs
                    for (int k = 0; k < 3; k++)
                    {
                        var stud = b.Ellipsoid(g, hand, grip + dir * (len * (0.6 + 0.12 * k)) + new Vec3(0, 0, (k - 1) * len * 0.05), new Vec3(0.6, 0.6, 0.6) * Math.Max(1, len * 0.04), MaterialSlot.Metal);
                        stud.Flags |= PrimFlags.Hard | PrimFlags.NoPattern;
                        stud.MinDensity = Limbs.HdDensity;
                    }
                    break;
                }
                case "axe":
                {
                    var shaft = b.Cone(g, hand, grip - dir * (len * 0.15), 0.6, hand, tip, 0.55, MaterialSlot.Cloth);
                    shaft.Tag = "haft";
                    // the blade spreads across the grip so it faces the viewer in side views
                    Vec3 side = Vec3.UnitZ * (arm.Side < 0 ? -1 : 1);
                    Vec3 h0 = grip + dir * (len * 0.78), h1 = grip + dir * (len * 0.98);
                    var t1 = b.Triangle(g, hand, h0, hand, h1, hand, (h0 + h1) * 0.5 + side * (len * 0.32) + dir * (len * 0.05), MaterialSlot.Metal, PatternDomain.None, 0);
                    var t2 = b.Triangle(g, hand, h0 + side * (len * 0.2) - dir * (len * 0.08), hand, h1 + side * (len * 0.22) + dir * (len * 0.1), hand, (h0 + h1) * 0.5, MaterialSlot.Metal, PatternDomain.None, 0);
                    t1.Flags |= PrimFlags.TwoSided | PrimFlags.NoPattern; t2.Flags |= PrimFlags.TwoSided | PrimFlags.NoPattern;
                    break;
                }
                default:
                {
                    // sword: grip, crossguard and a blade plate
                    var hilt = b.Cone(g, hand, grip - dir * (len * 0.12), 0.5, hand, grip + dir * (len * 0.1), 0.5, MaterialSlot.Cloth);
                    hilt.Tag = "hilt";
                    Vec3 cg = grip + dir * (len * 0.12);
                    Vec3 across = new Vec3(0, 0, 1);
                    var guard = b.Cone(g, hand, cg - across * (len * 0.12), 0.5, hand, cg + across * (len * 0.12), 0.5, MaterialSlot.Metal);
                    guard.Flags |= PrimFlags.NoPattern;
                    Vec3 w = Vec3.Cross(dir, across).NormalizedOr(Vec3.UnitX) * (len * 0.05);
                    var b1 = b.Triangle(g, hand, cg + w, hand, tip, hand, cg - w, MaterialSlot.Metal, PatternDomain.None, 0);
                    b1.Flags |= PrimFlags.TwoSided | PrimFlags.NoPattern;
                    var b2 = b.Triangle(g, hand, cg + across * 0.4, hand, tip, hand, cg - across * 0.4, MaterialSlot.Metal, PatternDomain.None, 0);
                    b2.Flags |= PrimFlags.TwoSided | PrimFlags.NoPattern;
                    break;
                }
            }
        }

        // ---- mimic: a treasure chest on spider legs; the lid is the jaw

        private static void BuildMimic(AnatomyBuilder b, CreatureGenome g, double size)
        {
            double w = DMath.Lerp(16, 28, size);
            double d = w * 0.62, h = w * 0.5;
            double legLen = w * DMath.Lerp(0.55, 0.95, g.Get("limbs"));
            double hipH = legLen * 0.55;
            int root = b.Bone("root", -1, Vec3.Zero, BoneRole.Root);
            Vec3 P = new Vec3(-d * 0.3, hipH, 0), C = new Vec3(d * 0.3, hipH, 0);
            int pelvis = b.BoneAlong("pelvis", root, P, Vec3.UnitX, Vec3.UnitY, BoneRole.Pelvis, 0, d * 0.3);
            int spine = b.BoneAlong("spine", pelvis, new Vec3(0, hipH, 0), Vec3.UnitX, Vec3.UnitY, BoneRole.Spine, 0, d * 0.3);
            int chest = b.BoneAlong("chest", spine, C, Vec3.UnitX, Vec3.UnitY, BoneRole.Chest, 0, d * 0.2);
            b.Rig.Root = root; b.Rig.Pelvis = pelvis; b.Rig.Chest = chest;
            b.Rig.Spine = new ChainRig { Name = "spine", Bones = new[] { pelvis, spine, chest }, Lengths = new[] { d * 0.3, d * 0.3 }, TotalLength = d * 0.6 };
            // the box (wood from the cloth genes) on the spine bone, so it bobs with the gait
            int box = b.Group("box", 0, 0, GroupFlags.None, 0);
            var wood = new SolidStyle { Material = MaterialSlot.Cloth, Tag = "box", Pattern = false };
            var metal = new SolidStyle { Material = MaterialSlot.Metal, Tag = "band", Pattern = false };
            double y0 = hipH * 0.15;
            Vec3 boxC = new Vec3(0, h * 0.5, 0) + new Vec3(0, y0, 0) - new Vec3(0, hipH, 0);
            Solids.Box(b, box, spine, boxC, new Vec3(d * 0.5, h * 0.5, w * 0.5), wood);
            for (int k = -1; k <= 1; k += 2)
                Solids.Box(b, box, spine, boxC + new Vec3(0, 0, k * w * 0.3), new Vec3(d * 0.5 + 0.25, h * 0.5 + 0.1, Math.Max(0.6, w * 0.03)), metal);
            // inside: flesh with rows of teeth along the front rim
            double rimY = y0 + h - hipH;
            int mouth = b.Group("maw", 0.8, 0, GroupFlags.NoOutline | GroupFlags.NoContour, -0.3);
            var flesh = b.Ellipsoid(mouth, spine, new Vec3(0, rimY - h * 0.1, 0), new Vec3(d * 0.42, h * 0.2, w * 0.45), MaterialSlot.Mouth);
            flesh.Flags |= PrimFlags.NoPattern | PrimFlags.NoShadow;
            double teeth = g.Get("teeth");
            int tg = b.Group("teeth", 0.3, 0, GroupFlags.NoContour, 0.2);
            int nTeeth = (int)Math.Round(DMath.Lerp(5, 9, teeth));
            for (int i = 0; i < nTeeth; i++)
            {
                double z = DMath.Lerp(-0.42, 0.42, i / (double)(nTeeth - 1)) * w;
                double tl = DMath.Lerp(1.2, 2.6, teeth) * (i % 2 == 0 ? 1.0 : 0.7);
                var t = b.Cone(tg, spine, new Vec3(d * 0.44, rimY - 0.2, z), Math.Max(0.5, tl * 0.32), spine, new Vec3(d * 0.46, rimY + tl * 0.8, z), 0.28, MaterialSlot.Teeth);
                t.Flags |= PrimFlags.Hard | PrimFlags.NoPattern | PrimFlags.NoShadow;
            }
            // lid on a hinge at the back top edge; bone +X forwards, +Y down (the jaw opens upwards)
            Vec3 hinge = new Vec3(-d * 0.5, y0 + h, 0);
            int lid = b.BoneAlong("lid", chest, hinge, Vec3.UnitX, Vec3.UnitY * -1, BoneRole.Jaw, 0, d);
            var lx = b.RestWorld(lid);
            int lidG = b.Group("lid", 0, 0, GroupFlags.None, 0.15);
            double lidH = h * 0.22;
            Solids.Box(b, lidG, lid, lx.InversePoint(new Vec3(0, y0 + h + lidH * 0.5, 0)), new Vec3(d * 0.5 + 0.2, lidH * 0.5, w * 0.5 + 0.2), wood);
            Solids.Vault(b, lidG, lid, lx.InversePoint(new Vec3(0, y0 + h + lidH, 0)), lx.InverseDir(Vec3.UnitZ), lx.InverseDir(Vec3.UnitY), w * 0.5 + 0.2, d * 0.5 + 0.2, 6, wood, 0.7);
            for (int i = 0; i < nTeeth; i++)
            {
                double z = DMath.Lerp(-0.4, 0.4, (i + 0.5) / nTeeth) * w;
                double tl = DMath.Lerp(1.2, 2.6, teeth);
                var t = b.Cone(tg, lid, lx.InversePoint(new Vec3(d * 0.46, y0 + h + 0.2, z)), Math.Max(0.5, tl * 0.32), lid, lx.InversePoint(new Vec3(d * 0.48, y0 + h - tl * 0.8, z)), 0.28, MaterialSlot.Teeth);
                t.Flags |= PrimFlags.Hard | PrimFlags.NoPattern | PrimFlags.NoShadow;
            }
            b.Rig.Jaw = lid;
            b.Rig.JawOpenMax = 1.0;
            b.Rig.Tuning["idle.jaw"] = 0.12;
            // eyes peeking from under the lid
            int eyes = 1 + (int)Math.Round(g.Get("eyes") * 3);
            double es = DMath.Clamp(w * 0.12, 1.5, 3.5);
            for (int i = 0; i < eyes; i++)
            {
                double z = eyes == 1 ? 0 : DMath.Lerp(-0.25, 0.25, i / (double)(eyes - 1)) * w;
                b.Feature(new FeatureDef { Kind = FeatureKind.Eye, Bone = lid, LocalPosition = lx.InversePoint(new Vec3(d * 0.52 + 0.4, y0 + h + lidH * 0.45, z)), LocalNormal = lx.InverseDir(new Vec3(1, 0.1, 0)).Normalized(), Size = es, Style = i % 2 == 0 ? EyeStyle.Slit : EyeStyle.Round, Group = lidG, Side = z > 0 ? 1 : (z < 0 ? -1 : 0) });
            }
            b.Rig.Head = lid;
            // a long tongue lolling out of the front
            var tongue = Chains.Build(b, new ChainSpec
            {
                Name = "tongue", ParentBone = spine, Base = new Vec3(d * 0.35, y0 + h * 0.9, 0) - new Vec3(0, 0, 0), Direction = new Vec3(1, -0.6, 0.2), Length = w * DMath.Lerp(0.4, 0.8, g.Get("tentacles")),
                Segments = 4, RadiusBase = Math.Max(0.9, w * 0.08), RadiusTip = Math.Max(0.6, w * 0.05), Curl = -0.6, Material = MaterialSlot.Mouth, Domain = PatternDomain.Appendage,
                Role = BoneRole.Tentacle, Stiffness = 0.25, Droop = 0.85, Flags = PrimFlags.NoPattern | PrimFlags.NoShadow,
            });
            b.Rig.Appendages.Add(tongue);
            // spider legs from under the box
            int pairs = g.Get("tentacles") > 0.55 ? 3 : 2;
            for (int pair = 0; pair < pairs; pair++)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    double x = DMath.Lerp(d * 0.3, -d * 0.3, pairs == 1 ? 0.5 : pair / (double)(pairs - 1));
                    Vec3 hip = new Vec3(x, y0 + h * 0.15, side * w * 0.42);
                    Vec3 contact = new Vec3(x * 1.4, 0, side * (w * 0.5 + legLen * 0.45));
                    var leg = Limbs.Build(b, new LegSpec
                    {
                        Name = $"leg{pair}{(side < 0 ? "L" : "R")}", Side = side, Pair = pair, Front = pair == 0, AnchorBone = pair == 0 ? chest : pelvis, Hip = hip, Contact = contact,
                        Style = LegStyle.Arthropod, Length = legLen * 1.25, Upper = 0.42, Lower = 0.42, Distal = 0.16,
                        RadiusTop = Math.Max(0.9, w * 0.06), RadiusKnee = Math.Max(0.8, w * 0.05), RadiusAnkle = Math.Max(0.6, w * 0.035), RadiusFoot = 0.5, Haunch = 0,
                        Material = MaterialSlot.Primary, LiftHeight = Math.Max(2, hipH * 0.5), BlendRadius = 0.8, DomainLength = legLen, Muscle = 0,
                    });
                    leg.Phase = ((pair + (side < 0 ? 0 : 1)) % 2) * 0.5;
                    b.Rig.Legs.Add(leg);
                }
            }
            b.Rig.Locomotion = LocomotionKind.Legged;
            b.Rig.LocomotionModule = "legged";
            b.Rig.Action = ActionStyle.Bite;
            b.Rig.Rest = RestStyle.Settle;
            b.Rig.Tuning["rest.bodyHeight"] = h * 0.5;
            b.Rig.Tuning["gait.gallop"] = 0;
            b.Metrics.LegLength = legLen;
            b.Metrics.HipHeight = hipH;
            b.Metrics.BackHeight = y0 + h + lidH + d * 0.35;
            b.Metrics.BodyLength = d + legLen;
            b.Metrics.Mass = DMath.Pow(w / 16.0, 3);
            b.Metrics.WalkSpeed = Math.Sqrt(0.35 * 400 * legLen);
            b.Metrics.RunSpeed = Math.Sqrt(2.0 * 400 * legLen);
            b.Metrics.PatternScale = 1;
            b.AnimationMargin = Math.Max(8, w * 0.5);
            b.ExtraHeadroom = Math.Max(6, d * 0.9);
        }

        // ---- wraith: a floating hooded cloak with glowing eyes and tattered hem

        private static void BuildWraith(AnatomyBuilder b, CreatureGenome g, double size)
        {
            double baseL = DMath.Lerp(18, 38, size);
            double H = baseL * DMath.Lerp(0.9, 1.3, g.Get("limbs"));
            double R = baseL * DMath.Lerp(0.18, 0.3, g.Get("mass"));
            int root = b.Bone("root", -1, Vec3.Zero, BoneRole.Root);
            Vec3 up = new Vec3(0.18, 1, 0).Normalized(), back = new Vec3(-1, 0, 0);
            Vec3 P = new Vec3(0, H * 0.35, 0);
            int pelvis = b.BoneAlong("pelvis", root, P, up, back, BoneRole.Pelvis, 0, H * 0.3);
            int spine = b.BoneAlong("spine", pelvis, P + up * (H * 0.3), up, back, BoneRole.Spine, 0, H * 0.3);
            Vec3 C = P + up * (H * 0.6);
            int chest = b.BoneAlong("chest", spine, C, up, back, BoneRole.Chest, 0, R);
            b.Rig.Root = root; b.Rig.Pelvis = pelvis; b.Rig.Chest = chest; b.Rig.Upright = true;
            b.Rig.Spine = new ChainRig { Name = "spine", Bones = new[] { pelvis, spine, chest }, Lengths = new[] { H * 0.3, H * 0.3 }, TotalLength = H * 0.6 };
            int cloak = b.Group("cloak", DMath.Clamp(R * 0.3, 1.2, 3), 0, GroupFlags.CreaseShade);
            // robe: widening from the shoulders to the hem
            var robe0 = b.Segment(cloak, chest, R * 0.85, spine, R * 1.0, MaterialSlot.Cloth, PatternDomain.Body, 0.6, 1);
            var robe1 = b.Segment(cloak, spine, R * 1.0, pelvis, R * 1.25, MaterialSlot.Cloth, PatternDomain.Body, 0.3, 0.6);
            robe0.DomainLength = robe1.DomainLength = H;
            var shoulders = b.Ellipsoid(cloak, chest, Vec3.Zero, new Vec3(R * 0.6, R * 0.75, R * 1.25), MaterialSlot.Cloth, PatternDomain.Body, 0.9, 1);
            shoulders.DomainLength = H;
            // tattered hem: strips hanging from the bottom of the robe (spring chains that ripple)
            int strips = 5 + (int)Math.Round(g.Get("tentacles") * 4);
            for (int i = 0; i < strips; i++)
            {
                double a = (i + 0.5) / strips * DMath.TwoPi;
                Vec3 at = P + new Vec3(DMath.Cos(a) * R * 1.05, -R * 0.2, DMath.Sin(a) * R * 1.15);
                var ch = Chains.Build(b, new ChainSpec
                {
                    Name = $"tatter{i}", ParentBone = pelvis, Base = at, Direction = new Vec3(DMath.Cos(a) * 0.25 - 0.35, -1, DMath.Sin(a) * 0.25), Length = H * DMath.Lerp(0.3, 0.55, (i * 0.37) % 1.0),
                    Segments = 3, RadiusBase = R * 0.42, RadiusTip = 0.45, Material = MaterialSlot.Cloth, Domain = PatternDomain.Appendage, Role = BoneRole.Tentacle,
                    Stiffness = 0.2, Droop = 0.5, Flags = PrimFlags.NoPattern | PrimFlags.NoShadow, Thin = true, Group = cloak,
                });
                b.Rig.Appendages.Add(ch);
                b.Rig.Tentacles.Add(ch);
            }
            // hood with a dark hollow and glowing eyes
            double hoodR = R * DMath.Lerp(0.75, 1.0, g.Get("head.size"));
            int neck = b.BoneAlong("neck", chest, C + up * (R * 0.4), new Vec3(0.6, 0.8, 0).Normalized(), back, BoneRole.Neck, 0, hoodR * 0.5);
            Vec3 hc = C + up * (R * 0.4) + new Vec3(0.6, 0.8, 0).Normalized() * (hoodR * 0.6);
            int head = b.BoneAlong("head", neck, hc, new Vec3(1, -0.15, 0), Vec3.UnitY, BoneRole.Head, 0, hoodR);
            int hg = b.Group("hood", 1.4, 0, GroupFlags.None, 0.2);
            var hood = b.Ellipsoid(hg, head, new Vec3(-hoodR * 0.15, hoodR * 0.1, 0), new Vec3(hoodR * 1.0, hoodR * 1.05, hoodR * 0.9), MaterialSlot.Cloth, PatternDomain.Head);
            hood.DomainLength = H;
            var peak = b.Cone(hg, head, new Vec3(-hoodR * 0.3, hoodR * 0.7, 0), hoodR * 0.5, head, new Vec3(-hoodR * 0.9, hoodR * 1.3, 0), 0.4, MaterialSlot.Cloth, PatternDomain.Head);
            peak.DomainLength = H;
            int fg = b.Group("hollow", 0.6, 0, GroupFlags.NoOutline | GroupFlags.NoContour, 0.5);
            var hollow = b.Ellipsoid(fg, head, new Vec3(hoodR * 0.62, -hoodR * 0.02, 0), new Vec3(hoodR * 0.32, hoodR * 0.62, hoodR * 0.55), MaterialSlot.Marking, PatternDomain.None);
            hollow.Flags |= PrimFlags.NoPattern | PrimFlags.NoShadow;
            hollow.ShadeBias = -4;
            for (int s = -1; s <= 1; s += 2)
                b.Feature(new FeatureDef { Kind = FeatureKind.Eye, Bone = head, LocalPosition = new Vec3(hoodR * 0.92, hoodR * 0.08, s * hoodR * 0.22), LocalNormal = new Vec3(1, 0, s * 0.2).Normalized(), Size = DMath.Clamp(hoodR * DMath.Lerp(0.18, 0.32, g.Get("eyes")), 1.2, 4), Style = EyeStyle.Glow, Side = s, Group = fg });
            b.Rig.Head = head;
            b.Rig.Neck = new ChainRig { Name = "neck", Bones = new[] { neck, head }, Lengths = new[] { hoodR * 0.5 }, TotalLength = hoodR * 0.5 };
            // long sleeves ending in bony claws
            for (int s = -1; s <= 1; s += 2)
            {
                Vec3 sh = C + new Vec3(0, -R * 0.1, s * R * 1.1);
                double armLen = H * 0.55;
                var arm = Limbs.Build(b, new LegSpec
                {
                    Name = "arm" + (s < 0 ? "L" : "R"), Side = s, Front = true, IsArm = true, Hand = true, AnchorBone = chest, Hip = sh, Contact = sh + new Vec3(armLen * 0.35, -armLen * 0.65, s * armLen * 0.2),
                    Style = LegStyle.Plantigrade, Length = armLen, Upper = 0.45, Lower = 0.42, Distal = 0.13, RadiusTop = R * 0.38, RadiusKnee = R * 0.42, RadiusAnkle = R * 0.48, RadiusFoot = R * 0.16,
                    Haunch = 0, PawLength = armLen * 0.15, PawRadius = R * 0.16, Material = MaterialSlot.Cloth, FootMaterial = MaterialSlot.Primary, Claws = 3, ClawSize = 1.2,
                    BlendRadius = 0.8, DomainLength = armLen, Muscle = 0, Pole = new Vec3(-0.5, 0.2, 0.7),
                });
                b.Rig.Arms.Add(arm);
            }
            b.Rig.Locomotion = LocomotionKind.Float;
            b.Rig.LocomotionModule = "float";
            b.Rig.Altitude = Math.Max(4, H * 0.25);
            b.Rig.Tuning["tail.gravity"] = 0;
            b.Rig.Action = ActionStyle.TentacleLash;
            b.Rig.Rest = RestStyle.Settle;
            b.Rig.Tuning["rest.bodyHeight"] = P.Y * 0.6;
            b.Metrics.LegLength = H * 0.3;
            b.Metrics.HipHeight = P.Y;
            b.Metrics.BackHeight = C.Y + R;
            b.Metrics.BodyLength = H * 0.7;
            b.Metrics.Mass = DMath.Pow(baseL / 26.0, 3) * 0.4;
            b.Metrics.WalkSpeed = Math.Sqrt(0.3 * 400 * H * 0.4);
            b.Metrics.RunSpeed = Math.Sqrt(1.6 * 400 * H * 0.4);
            b.Metrics.PatternScale = DMath.Clamp(Math.Sqrt(baseL / 26.0), 0.75, 1.4);
            b.AnimationMargin = Math.Max(8, baseL * 0.45);
            b.ExtraHeadroom = Math.Max(8, b.Rig.Altitude + hoodR);
        }

        // ---- eldritch horror: a floating mass of eyes, a toothed maw and tentacles

        private static void BuildHorror(AnatomyBuilder b, CreatureGenome g, double size)
        {
            double baseL = DMath.Lerp(16, 40, size);
            double R = baseL * DMath.Lerp(0.32, 0.45, g.Get("mass"));
            int root = b.Bone("root", -1, Vec3.Zero, BoneRole.Root);
            double alt = R * 1.2;
            Vec3 P = new Vec3(0, alt, 0);
            int pelvis = b.BoneAlong("pelvis", root, P, Vec3.UnitX, Vec3.UnitY, BoneRole.Pelvis, 0, R);
            int chest = b.BoneAlong("chest", pelvis, P + new Vec3(R * 0.3, R * 0.15, 0), Vec3.UnitX, Vec3.UnitY, BoneRole.Chest, 0, R);
            b.Rig.Root = root; b.Rig.Pelvis = pelvis; b.Rig.Chest = chest;
            b.Rig.Spine = new ChainRig { Name = "spine", Bones = new[] { pelvis, chest }, Lengths = new[] { R * 0.33 }, TotalLength = R * 0.33 };
            int body = b.Group("mass", DMath.Clamp(R * 0.3, 1.5, 4), 0, GroupFlags.CreaseShade);
            var rng = b.RngFor("horror");
            var core = b.Ellipsoid(body, pelvis, Vec3.Zero, new Vec3(R * 1.05, R * 0.9, R), MaterialSlot.Primary, PatternDomain.Body);
            core.DomainLength = R * 4;
            // lumpy growths
            int lumps = 4 + (int)Math.Round(g.Get("mass") * 4);
            for (int i = 0; i < lumps; i++)
            {
                double a = rng.Range(0.0, DMath.TwoPi), el = rng.Range(-0.5, 0.9);
                Vec3 d = new Vec3(DMath.Cos(a) * DMath.Cos(el), DMath.Sin(el), DMath.Sin(a) * DMath.Cos(el));
                double r = R * rng.Range(0.35, 0.6);
                var l = b.Ellipsoid(body, i % 2 == 0 ? pelvis : chest, d * (R * 0.75), new Vec3(r, r * 0.9, r), MaterialSlot.Primary, PatternDomain.Body);
                l.DomainLength = R * 4;
                l.Tag = "lump";
            }
            // a maw on the front: the head bone is the upper lip, the jaw opens downwards
            var hs = new HeadSpec
            {
                Name = "maw", ParentBone = chest, Position = P + new Vec3(R * 0.55, -R * 0.15, 0), Forward = new Vec3(1, -0.1, 0),
                SkullLength = R * 0.6, SkullHeight = R * 0.5, SkullWidth = R * 0.8, SkullOffset = 0.2, SnoutLength = R * 0.2, SnoutRadius = R * 0.25, NoseSize = 0, Nostrils = false,
                JawLength = 1.0, JawRadius = R * 0.24, JawDepth = 1.0, EyeCount = 0, EyeSize = 0, Teeth = TeethKind.Fangs, TeethSize = DMath.Clamp(R / 6.0, 1, 2.2) * DMath.Lerp(0.8, 1.4, g.Get("teeth")),
                Group = body, DomainLength = R * 4, JawOpenMax = 0.9, Structure = false,
            };
            var maw = Heads.Build(b, hs);
            b.Rig.Tuning["idle.jaw"] = 0.25;
            // many eyes of different sizes over the upper front
            int eyes = 3 + (int)Math.Round(g.Get("eyes") * 7);
            for (int i = 0; i < eyes; i++)
            {
                double a = DMath.Lerp(-1.1, 1.1, rng.NextDouble()), el = rng.Range(0.05, 0.95);
                Vec3 n = new Vec3(DMath.Cos(el) * DMath.Cos(a), DMath.Sin(el), DMath.Cos(el) * DMath.Sin(a)).Normalized();
                Vec3 pos = new Vec3(n.X * R * 1.02, n.Y * R * 0.92, n.Z * R * 1.0);
                double es = DMath.Clamp(R * rng.Range(0.1, 0.32), 1.2, 6.5);
                var style = rng.Chance(0.4) ? EyeStyle.Slit : (rng.Chance(0.5) ? EyeStyle.Round : EyeStyle.Glow);
                b.Feature(new FeatureDef { Kind = FeatureKind.Eye, Bone = pelvis, LocalPosition = pos, LocalNormal = n, Size = es, Style = style, Side = n.Z > 0.1 ? 1 : (n.Z < -0.1 ? -1 : 0), Group = body });
            }
            b.Rig.Head = maw.Head;
            // writhing tentacles from the underside and the back
            int tentacles = 4 + (int)Math.Round(g.Get("tentacles") * 6);
            for (int i = 0; i < tentacles; i++)
            {
                double a = (i + 0.5) / tentacles * DMath.TwoPi + 0.2;
                bool under = i % 3 != 2;
                Vec3 at = new Vec3(DMath.Cos(a) * R * 0.7, under ? -R * 0.55 : R * 0.2, DMath.Sin(a) * R * 0.75);
                Vec3 dir = under ? new Vec3(DMath.Cos(a) * 0.4, -1, DMath.Sin(a) * 0.4) : new Vec3(DMath.Cos(a), 0.5, DMath.Sin(a));
                var ch = Chains.Build(b, new ChainSpec
                {
                    Name = $"tentacle{i}", ParentBone = pelvis, Base = P + at, Direction = dir, Length = R * rng.Range(1.1, 1.8) * (under ? 1.0 : 0.8),
                    Segments = 5, RadiusBase = Math.Max(0.9, R * 0.17), RadiusTip = 0.45, Curl = under ? 1.2 : -1.4, Material = MaterialSlot.Primary, Domain = PatternDomain.Appendage,
                    Role = BoneRole.Tentacle, Side = Math.Sin(a) >= 0 ? 1 : -1, Stiffness = 0.3, Droop = under ? 0.6 : 0.2, Flags = PrimFlags.NoShadow,
                });
                b.Rig.Appendages.Add(ch);
                b.Rig.Tentacles.Add(ch);
            }
            b.Rig.Locomotion = LocomotionKind.Float;
            b.Rig.LocomotionModule = "float";
            b.Rig.Altitude = Math.Max(3, R * 0.4);
            b.Rig.Tuning["float.pulse"] = 1;
            b.Rig.Action = ActionStyle.TentacleLash;
            b.Rig.Rest = RestStyle.Settle;
            b.Rig.Tuning["rest.bodyHeight"] = alt * 0.6;
            b.Metrics.LegLength = R;
            b.Metrics.HipHeight = alt;
            b.Metrics.BackHeight = alt + R;
            b.Metrics.BodyLength = R * 2.4;
            b.Metrics.Mass = DMath.Pow(baseL / 26.0, 3);
            b.Metrics.WalkSpeed = Math.Sqrt(0.25 * 400 * R);
            b.Metrics.RunSpeed = Math.Sqrt(1.2 * 400 * R);
            b.Metrics.PatternScale = DMath.Clamp(Math.Sqrt(baseL / 26.0), 0.75, 1.5);
            b.AnimationMargin = Math.Max(8, R * 0.9);
            b.ExtraHeadroom = Math.Max(8, b.Rig.Altitude + R * 0.6);
        }
    }
}
