// Procedural Pixel Creatures - family "mythical": creatures of myth and legend.
//
// Griffins, unicorns and pegasi, hydras, cerberi, kirin, manticores and centaurs share one beast body
// builder (rump, barrel and chest masses with high density shoulder and thigh detail, a neck chain,
// four legs from the limb block) and add their signature parts from the shared shape blocks: beaked
// eagle heads, feathered or membrane wings (they take off with the flight module), unicorn horns and
// antlers, flowing manes that sway (spring chains), scorpion tails, additional heads on long necks
// that weave independently (appendage chains), and a humanoid upper body with arms for centaurs.

#nullable enable

using System;
using System.Collections.Generic;
using PixelCreatures.Core.Anatomy;
using PixelCreatures.Core.Genetics;
using PixelCreatures.Core.Palettes;
using PixelCreatures.Core.Shapes;

namespace PixelCreatures.Core.Families
{
    public sealed class MythicalFamily : CreatureFamilyBase, ISurfaceCustomizer
    {
        public override string Id => "mythical";
        public override string DisplayName => "Mythical Creatures";
        public override string Description => "Griffins, unicorns and pegasi, hydras, cerberi, kirin, manticores and centaurs: legendary beasts with beaks, horns, flowing manes, wings, extra heads and humanoid torsos. Winged ones take off when running.";
        public override int Order => 90;
        public override CreationCategory Category => CreationCategory.Mythical;

        private static readonly ArchetypeInfo[] ArchetypeList =
        {
            new ArchetypeInfo("griffin", "Griffin", 1.1),
            new ArchetypeInfo("unicorn", "Unicorn & pegasus", 1.1),
            new ArchetypeInfo("hydra", "Hydra", 0.9),
            new ArchetypeInfo("cerberus", "Cerberus", 0.9),
            new ArchetypeInfo("kirin", "Kirin", 0.8),
            new ArchetypeInfo("manticore", "Manticore", 0.9),
            new ArchetypeInfo("centaur", "Centaur", 0.9),
        };

        public override IReadOnlyList<ArchetypeInfo> Archetypes => ArchetypeList;

        public static readonly (string, string)[] Forms =
        {
            ("griffin", "Griffin"), ("unicorn", "Unicorn & pegasus"), ("hydra", "Hydra"), ("cerberus", "Cerberus"), ("kirin", "Kirin"), ("manticore", "Manticore"), ("centaur", "Centaur"),
        };
        public static readonly (string, string)[] WingKinds = { ("none", "None"), ("feathered", "Feathered"), ("membrane", "Membrane") };
        public static readonly (string, string)[] EyeStyles = { ("round", "Round"), ("slit", "Slit"), ("bead", "Bead"), ("glow", "Glowing") };

        protected override GeneSchema CreateSchema()
        {
            var b = new GeneSchemaBuilder(Id);
            b.Group("body", "Body", GeneStream.Anatomy)
             .Choice("form", "Creature", Forms, "griffin", "Legend used as a template when rerolling.", 0.04)
             .Float("size", "Size", 0, 1, 0.5, "", 0.08)
             .Float("mass", "Build", 0, 1, 0.5, "Slender to massive.", 0.1)
             .Float("torso.length", "Torso length", 0, 1, 0.5, "", 0.1)
             .Float("legs.length", "Leg length", 0, 1, 0.5, "", 0.1)
             .Float("neck.length", "Neck length", 0, 1, 0.5, "", 0.1)
             .Float("tail.length", "Tail length", 0, 1, 0.5, "", 0.1);
            b.Group("head", "Heads", GeneStream.Anatomy)
             .Float("head.size", "Head size", 0, 1, 0.5, "", 0.1)
             .Float("heads", "Extra heads", 0, 1, 0.5, "Number of heads of hydras and cerberi.", 0.12)
             .Float("head.hornSize", "Horn size", 0, 1, 0.5, "Unicorn horn, antlers and goat horns.", 0.1)
             .Float("head.eyeSize", "Eye size", 0, 1, 0.5, "", 0.1)
             .Choice("head.eyeStyle", "Eye style", EyeStyles, "round", "", 0.08);
            b.Group("legend", "Legendary parts", GeneStream.Anatomy)
             .Choice("wings", "Wings", WingKinds, "feathered", "Feathered wings (griffins, pegasi) or membrane wings (manticores).", 0.06)
             .Float("wings.span", "Wing span", 0, 1, 0.5, "", 0.1)
             .Float("mane", "Mane", 0, 1, 0.6, "Length of manes, ruffs and hair.", 0.1)
             .Float("claws", "Claws", 0, 1, 0.5, "", 0.1);
            b.Group("haircolor", "Mane color", GeneStream.Color)
             .Hue("color.hairHue", "Mane hue", 50, "Mane, hair and tail tuft.")
             .Float("color.hairSaturation", "Mane saturation", 0, 1, 0.4, "", 0.12)
             .Float("color.hairValue", "Mane brightness", 0, 1, 0.4, "", 0.12);
            SharedGenes.AddColorGenes(b);
            SharedGenes.AddPatternGenes(b, "fur");
            SharedGenes.AddMotionGenes(b);
            return b.Build();
        }

        private sealed class Prior
        {
            public double Size = 0.5, Mass = 0.5, Torso = 0.5, Legs = 0.5, Neck = 0.5, Tail = 0.5, Head = 0.5, Heads = 0.5, Horn = 0.5, Eye = 0.5, Span = 0.5, Mane = 0.5, Claws = 0.5;
            public (string, double)[] Wings = { ("none", 1) };
            public (string, double)[] Eyes = { ("round", 1) };
        }

        private static Prior PriorFor(string a) => a switch
        {
            "unicorn" => new Prior { Size = 0.55, Mass = 0.4, Torso = 0.5, Legs = 0.65, Neck = 0.7, Tail = 0.6, Head = 0.5, Horn = 0.6, Mane = 0.75, Claws = 0, Wings = new[] { ("none", 2.0), ("feathered", 1.2) }, Eyes = new[] { ("round", 3.0), ("bead", 1.0) } },
            "hydra" => new Prior { Size = 0.6, Mass = 0.6, Torso = 0.45, Legs = 0.3, Neck = 0.85, Tail = 0.6, Head = 0.45, Heads = 0.6, Horn = 0.4, Mane = 0.1, Claws = 0.6, Eyes = new[] { ("slit", 3.0), ("glow", 1.0) } },
            "cerberus" => new Prior { Size = 0.55, Mass = 0.6, Torso = 0.5, Legs = 0.55, Neck = 0.35, Tail = 0.5, Head = 0.5, Heads = 0.7, Horn = 0.2, Mane = 0.55, Claws = 0.7, Eyes = new[] { ("glow", 2.0), ("slit", 1.0), ("round", 1.0) } },
            "kirin" => new Prior { Size = 0.5, Mass = 0.4, Torso = 0.5, Legs = 0.6, Neck = 0.6, Tail = 0.6, Head = 0.5, Horn = 0.55, Mane = 0.75, Claws = 0, Eyes = new[] { ("round", 2.0), ("slit", 1.0) } },
            "manticore" => new Prior { Size = 0.55, Mass = 0.6, Torso = 0.5, Legs = 0.5, Neck = 0.3, Tail = 0.7, Head = 0.55, Horn = 0.3, Span = 0.45, Mane = 0.8, Claws = 0.8, Wings = new[] { ("membrane", 3.0), ("none", 1.0) }, Eyes = new[] { ("slit", 2.0), ("glow", 1.0) } },
            "centaur" => new Prior { Size = 0.55, Mass = 0.5, Torso = 0.45, Legs = 0.6, Neck = 0.5, Tail = 0.55, Head = 0.5, Horn = 0.2, Mane = 0.65, Claws = 0, Eyes = new[] { ("round", 3.0), ("bead", 1.0) } },
            _ => new Prior { Size = 0.55, Mass = 0.55, Torso = 0.5, Legs = 0.5, Neck = 0.45, Tail = 0.55, Head = 0.5, Horn = 0.3, Span = 0.6, Mane = 0.5, Claws = 0.7, Wings = new[] { ("feathered", 1.0) }, Eyes = new[] { ("round", 2.0), ("bead", 1.0), ("slit", 0.5) } },
        };

        public override void SampleAnatomy(double[] v, Rng rng, string arch)
        {
            var p = PriorFor(arch);
            SetChoice(v, "form", arch);
            const double sp = 0.11;
            Around(v, rng, "size", p.Size, 0.12);
            Around(v, rng, "mass", p.Mass, sp);
            Around(v, rng, "torso.length", p.Torso, sp);
            Around(v, rng, "legs.length", p.Legs, sp);
            Around(v, rng, "neck.length", p.Neck, sp);
            Around(v, rng, "tail.length", p.Tail, sp);
            Around(v, rng, "head.size", p.Head, sp);
            Around(v, rng, "heads", p.Heads, 0.2);
            Around(v, rng, "head.hornSize", p.Horn, sp);
            Around(v, rng, "head.eyeSize", p.Eye, sp);
            SetChoice(v, "head.eyeStyle", PickWeighted(rng, p.Eyes));
            SetChoice(v, "wings", PickWeighted(rng, p.Wings));
            Around(v, rng, "wings.span", p.Span, sp);
            Around(v, rng, "mane", p.Mane, sp);
            Around(v, rng, "claws", p.Claws, sp);
        }

        public override FamilyStyleHints StyleHints(string arch)
        {
            var h = new FamilyStyleHints
            {
                Materials = new Dictionary<string, double> { ["fur"] = 3, ["hide"] = 1 },
                Patterns = new Dictionary<string, double> { ["none"] = 3, ["dorsal"] = 0.6, ["spots"] = 0.4 },
                Accents = new Dictionary<string, double> { ["bone"] = 2, ["gold"] = 1.2, ["dark"] = 1 },
                Coats = new[] { Coats.Golden(2), Coats.Tan(1.5), Coats.Brown(1), Coats.Cream(0.6) },
                SocksChance = 0.15, MaskChance = 0, PiebaldChance = 0.03, GlowChance = 0.12,
            };
            switch (arch)
            {
                case "unicorn":
                    h.Coats = new[] { Coats.Albino(3), Coats.Cream(1.5), Coats.Silver(1.2), Coats.Pink(0.5), Coats.Black(0.6), Coats.Violet(0.4) };
                    h.Accents = new Dictionary<string, double> { ["gold"] = 2, ["bone"] = 1, ["vivid"] = 1 };
                    h.GlowChance = 0.25;
                    break;
                case "hydra":
                    h.Materials = new Dictionary<string, double> { ["scales"] = 1 };
                    h.Coats = new[] { Coats.Green(2), Coats.Teal(1.5), Coats.Olive(1), Coats.Crimson(0.6), Coats.Violet(0.5) };
                    h.Patterns = new Dictionary<string, double> { ["dorsal"] = 1.5, ["bands"] = 1, ["none"] = 1 };
                    h.Accents = new Dictionary<string, double> { ["bone"] = 2, ["dark"] = 1 };
                    break;
                case "cerberus":
                    h.Coats = new[] { Coats.Black(3), Coats.Chocolate(1), Coats.Slate(1), Coats.Crimson(0.5) };
                    h.Accents = new Dictionary<string, double> { ["dark"] = 1, ["bone"] = 1 };
                    h.GlowChance = 0.6;
                    break;
                case "kirin":
                    h.Materials = new Dictionary<string, double> { ["scales"] = 2, ["fur"] = 1 };
                    h.Coats = new[] { Coats.Golden(2), Coats.Teal(1), Coats.Crimson(1), Coats.Cream(1) };
                    h.Accents = new Dictionary<string, double> { ["gold"] = 2, ["vivid"] = 1 };
                    h.GlowChance = 0.35;
                    break;
                case "manticore":
                    h.Coats = new[] { Coats.Red(2), Coats.Rust(1.5), Coats.Golden(1), Coats.Chocolate(0.8) };
                    h.Accents = new Dictionary<string, double> { ["dark"] = 2, ["bone"] = 1 };
                    break;
                case "centaur":
                    h.Coats = new[] { Coats.Brown(2), Coats.Chocolate(1.5), Coats.Tan(1), Coats.Black(0.6), Coats.Grey(0.5) };
                    h.SocksChance = 0.35;
                    h.PiebaldChance = 0.12;
                    break;
            }
            return h;
        }

        public override void AdjustSample(double[] values, Rng rng, string arch)
        {
            // manes: dark, blond, white or fantasy hues
            var (hue, s0, s1, v0, v1) = arch switch
            {
                "unicorn" => rng.Chance(0.5) ? (rng.Range(0.0, 360.0), 0.4, 0.8, 0.6, 0.85) : (85.0, 0.05, 0.2, 0.82, 0.95),
                "kirin" => (rng.Chance(0.5) ? 30.0 : 200.0, 0.6, 0.9, 0.45, 0.65),
                "cerberus" => (rng.Chance(0.5) ? 25.0 : 270.0, 0.3, 0.7, 0.1, 0.25),
                "manticore" => (45.0, 0.5, 0.8, 0.2, 0.4),
                _ => (55.0, 0.3, 0.6, 0.18, 0.5),
            };
            Set(values, "color.hairHue", hue + rng.Gaussian(0, 6, 2));
            Set(values, "color.hairSaturation", rng.Range(s0, s1));
            Set(values, "color.hairValue", rng.Range(v0, v1));
            if (Schema.Get("head.eyeStyle").Options[(int)values[Schema.IndexOf("head.eyeStyle")]].Id == "glow")
                Set(values, "color.glow", Math.Max(values[Schema.IndexOf("color.glow")], 0.7));
        }

        public void CustomizeSurface(CreatureGenome genome, SurfaceSpec surface)
        {
            // griffins: the eagle half (head, neck, chest) is feathered and painted with the White ramp
            if (genome.GetChoice("form") == "griffin") surface.OverrideSlotStyle(MaterialSlot.White, MaterialStyles.Feather);
        }

        // ------------------------------------------------------------------ anatomy

        private sealed class Body
        {
            public int Root, Pelvis, Spine, Chest, Neck0, Neck1, Group;
            public Vec3 P, C, N0, NeckDir;
            public double L, HipR, ChestR, Wz, BaseL, NeckLen, Nr0, Nr1, HipH, ShoulderH, MidR, Drop;
        }

        public override CreatureAnatomy BuildAnatomy(CreatureGenome g, CreatureRegistry registry)
        {
            var b = new AnatomyBuilder(Id, g.AnatomySeed);
            string form = g.GetChoice("form");
            b.Trait(form);
            var rng = b.RngFor("legend");
            double size = g.Get("size");
            double baseL = DMath.Lerp(18, 42, DMath.Pow(size, 1.05)) * (form == "hydra" ? 1.1 : 1.0);
            bool equine = form == "unicorn" || form == "kirin" || form == "centaur";
            bool feline = form == "griffin" || form == "manticore";
            bool reptile = form == "hydra";
            var body = BuildBody(b, g, form, baseL);

            // ---- heads
            double headSize = g.Get("head.size");
            double skullL = baseL * DMath.Lerp(0.26, 0.38, headSize) * (equine ? 1.05 : 1.0);
            string eyeStyle = g.GetChoice("head.eyeStyle");
            var eye = eyeStyle switch { "slit" => EyeStyle.Slit, "bead" => EyeStyle.Bead, "glow" => EyeStyle.Glow, _ => EyeStyle.Round };
            HeadResult head;
            int extraHeads = 0;
            if (form == "centaur")
            {
                head = BuildCentaurTorso(b, g, body, skullL, eye);
            }
            else
            {
                var hs = HeadFor(form, body, skullL, eye, g);
                hs.ParentBone = body.Neck1;
                hs.Position = body.N0 + body.NeckDir * body.NeckLen;
                head = Heads.Build(b, hs);
                var nk1 = b.Cone(body.Group, body.Neck1, Vec3.Zero, (body.Nr0 + body.Nr1) * 0.5, head.Head, head.SkullCenterLocal * 0.5, body.Nr1, form == "griffin" ? MaterialSlot.White : MaterialSlot.Primary, PatternDomain.Neck, 0.5, 1.0);
                nk1.DomainLength = body.NeckLen + 2;
                b.Rig.Neck = new ChainRig { Name = "neck", Bones = new[] { body.Neck0, body.Neck1, head.Head }, Lengths = new[] { body.NeckLen * 0.5, body.NeckLen * 0.5 }, TotalLength = body.NeckLen };
                HeadExtras(b, g, form, head, skullL, rng);
                if (form == "hydra" || form == "cerberus")
                {
                    int mainHead = b.Rig.Head, mainJaw = b.Rig.Jaw;
                    double jawMax = b.Rig.JawOpenMax;
                    extraHeads = form == "hydra" ? 2 + (int)Math.Round(g.Get("heads") * 4) : 1 + (int)Math.Round(g.Get("heads"));
                    for (int i = 0; i < extraHeads; i++) ExtraHead(b, g, form, body, skullL, eye, i, extraHeads);
                    b.Rig.Head = mainHead; b.Rig.Jaw = mainJaw; b.Rig.JawOpenMax = jawMax;
                    b.Trait($"heads:{extraHeads + 1}");
                }
            }

            // ---- mane
            double mane = g.Get("mane");
            if (mane > 0.15 && form != "hydra" && form != "griffin") BuildMane(b, body, form, mane, skullL, head, rng);

            // ---- tail
            double tailLen = baseL * DMath.Lerp(0.3, 1.4, g.Get("tail.length")) * (reptile ? 1.3 : 1.0);
            BuildTail(b, g, form, body, tailLen);

            // ---- legs
            BuildLegs(b, g, form, body);

            // ---- wings
            string wings = form == "griffin" ? "feathered" : (form == "unicorn" || form == "manticore" ? g.GetChoice("wings") : "none");
            double span = 0;
            if (wings != "none")
            {
                span = baseL * DMath.Lerp(1.1, 2.1, g.Get("wings.span"));
                bool feathered = wings == "feathered";
                Wings.BuildPair(b, new WingSpec
                {
                    Kind = feathered ? WingKind.Feathered : WingKind.Membrane,
                    AnchorBone = body.Chest,
                    Root = body.C + new Vec3(-body.ChestR * 0.15, body.ChestR * 0.6, body.ChestR * body.Wz * 0.45),
                    Span = span,
                    Chord = span * (feathered ? 0.4 : 0.45),
                    Raise = 0.35,
                    SweepBack = 0.35,
                    BoneRadius = Math.Max(0.8, body.ChestR * 0.2),
                    Fingers = 3,
                    BodyBone = body.Pelvis,
                    BodyAttach = body.P + new Vec3(body.HipR * 0.3, body.HipR * 0.5, body.HipR * body.Wz * 0.5),
                    ClawSize = DMath.Clamp(baseL / 20.0, 0.7, 1.5),
                    MembraneMaterial = feathered ? MaterialSlot.White : MaterialSlot.Membrane,
                    BoneMaterial = feathered && form == "griffin" ? MaterialSlot.Primary : MaterialSlot.Primary,
                    TipMaterial = MaterialSlot.Marking,
                    Primaries = 4,
                });
                b.Trait("wings:" + wings);
            }

            // ---- rig semantics & metrics
            bool winged = wings != "none";
            b.Rig.Locomotion = winged ? LocomotionKind.Fly : LocomotionKind.Legged;
            b.Rig.LocomotionModule = winged ? "flight" : "legged";
            if (winged)
            {
                b.Rig.Tuning["flight.flapHz"] = DMath.Lerp(2.6, 1.6, size);
                b.Rig.Altitude = Math.Max(12, body.HipH + baseL * 0.9);
            }
            b.Rig.Action = form switch
            {
                "unicorn" => ActionStyle.Rear,
                "kirin" => ActionStyle.Rear,
                "centaur" => ActionStyle.Strike,
                "manticore" => ActionStyle.Sting,
                "hydra" => ActionStyle.Roar,
                "cerberus" => ActionStyle.Bite,
                _ => winged ? ActionStyle.WingBuffet : ActionStyle.Pounce,
            };
            b.Rig.Rest = RestStyle.LieDown;
            b.Rig.Tuning["gait.walkDuty"] = 0.64;
            b.Rig.Tuning["gait.runDuty"] = equine ? 0.38 : 0.34;
            if (form == "manticore") b.Rig.Tuning["tail.gravity"] = 8;
            b.Metrics.LegLength = (body.HipH + body.ShoulderH) * 0.5;
            b.Metrics.HipHeight = body.HipH;
            b.Metrics.BackHeight = Math.Max(body.HipH + body.HipR, body.ShoulderH + body.ChestR);
            b.Metrics.BodyLength = body.L + body.HipR + body.ChestR + body.NeckLen + skullL + tailLen * 0.7;
            b.Metrics.Mass = DMath.Pow(baseL / 26.0, 3) * DMath.Lerp(0.7, 1.6, g.Get("mass"));
            double legL = b.Metrics.LegLength;
            b.Metrics.WalkSpeed = Math.Sqrt(0.3 * 400 * legL) * DMath.Lerp(0.8, 1.05, g.GetOr("motion.speed", 0.5));
            b.Metrics.RunSpeed = Math.Sqrt(2.2 * 400 * legL) * DMath.Lerp(0.8, 1.2, g.GetOr("motion.speed", 0.5)) * (winged ? 1.4 : 1.0);
            b.Metrics.PatternScale = DMath.Clamp(Math.Sqrt(baseL / 26.0), 0.75, 1.5);
            b.AnimationMargin = Math.Max(8, baseL * 0.34 + skullL * 0.3 + (extraHeads > 0 ? body.NeckLen * 0.3 : 0) + tailLen * 0.05);
            b.ExtraHeadroom = winged ? Math.Max(8, span * 0.9 + b.Rig.Altitude) : Math.Max(5, skullL * 0.9 + body.NeckLen * 0.3 + (form == "centaur" ? baseL * 0.6 : 0));
            return b.Build(g.AnatomyKey);
        }

        private static Body BuildBody(AnatomyBuilder b, CreatureGenome g, string form, double baseL)
        {
            var o = new Body { BaseL = baseL };
            double mass = g.Get("mass");
            o.L = baseL * DMath.Lerp(0.85, 1.3, g.Get("torso.length")) * (form == "hydra" ? 1.15 : 1.0);
            double R = baseL * DMath.Lerp(0.19, 0.31, mass);
            bool feline = form == "griffin" || form == "manticore" || form == "cerberus";
            o.ChestR = R * (feline ? 1.12 : 1.05);
            o.HipR = R * (feline ? 0.88 : 0.98);
            o.Wz = DMath.Lerp(0.92, 1.2, mass);
            double legs = g.Get("legs.length");
            o.HipH = baseL * DMath.Lerp(0.45, 1.05, legs) * (form == "hydra" ? 0.6 : 1.0);
            o.HipH = Math.Max(o.HipH, o.HipR * 0.92 + 1.5);
            o.ShoulderH = Math.Max(o.HipH * (form == "griffin" ? 1.04 : 1.02), o.ChestR * 0.92 + 1.5);
            o.Root = b.Bone("root", -1, Vec3.Zero, BoneRole.Root);
            o.P = new Vec3(-o.L / 2, o.HipH, 0);
            o.C = new Vec3(o.L / 2, o.ShoulderH, 0);
            Vec3 spineDir = (o.C - o.P).Normalized();
            o.Pelvis = b.BoneAlong("pelvis", o.Root, o.P, spineDir, Vec3.UnitY, BoneRole.Pelvis, 0, o.L / 2);
            o.Spine = b.BoneAlong("spine", o.Pelvis, (o.P + o.C) * 0.5, spineDir, Vec3.UnitY, BoneRole.Spine, 0, o.L / 2);
            o.Chest = b.BoneAlong("chest", o.Spine, o.C, spineDir, Vec3.UnitY, BoneRole.Chest, 0, o.ChestR);
            b.Rig.Root = o.Root; b.Rig.Pelvis = o.Pelvis; b.Rig.Chest = o.Chest;
            b.Rig.Spine = new ChainRig { Name = "spine", Bones = new[] { o.Pelvis, o.Spine, o.Chest }, Lengths = new[] { o.L / 2, o.L / 2 }, TotalLength = o.L };

            o.Group = b.Group("body", DMath.Clamp(R * 0.32, 1.6, 4.5), 0, GroupFlags.None);
            double dom = o.L + o.HipR + o.ChestR;
            // griffins: the eagle half (chest) is feathered (White ramp, feather style)
            var chestMat = form == "griffin" ? MaterialSlot.White : MaterialSlot.Primary;
            var rump = b.Ellipsoid(o.Group, o.Pelvis, new Vec3(-o.HipR * 0.12, o.HipR * 0.16, 0), new Vec3(o.HipR * 1.15, o.HipR, o.HipR * o.Wz * 0.93), MaterialSlot.Primary, PatternDomain.Body, 0, o.HipR * 2.2 / dom);
            rump.DomainLength = dom; rump.Tag = "rump";
            var chestP = b.Ellipsoid(o.Group, o.Chest, new Vec3(o.ChestR * 0.1, o.ChestR * 0.04, 0), new Vec3(o.ChestR * 1.18, o.ChestR * 1.04, o.ChestR * o.Wz * 0.9), chestMat, PatternDomain.Body, (o.L - o.ChestR * 0.2) / dom, 1);
            chestP.DomainLength = dom; chestP.Tag = "chest";
            o.MidR = (o.HipR + o.ChestR) * 0.5 * DMath.Lerp(0.85, 1.05, mass);
            o.Drop = o.MidR * DMath.Lerp(0.05, 0.25, mass);
            var barrel = b.Ellipsoid(o.Group, o.Spine, new Vec3(0, -o.Drop, 0), new Vec3(o.L * 0.43, o.MidR, o.MidR * o.Wz * 0.94), MaterialSlot.Primary, PatternDomain.Body, 0.15, 0.85);
            barrel.DomainLength = dom; barrel.Tag = "barrel";
            // high density musculature: shoulder blades, withers, hamstrings, ribcage and loin
            double hd = Limbs.HdDensity;
            for (int side = -1; side <= 1; side += 2)
            {
                var sc = b.Ellipsoid(o.Group, o.Chest, new Vec3(o.ChestR * 0.05, o.ChestR * 0.42, side * o.ChestR * o.Wz * 0.6), new Vec3(o.ChestR * 0.62, o.ChestR * 0.46, o.ChestR * 0.3),
                    chestMat, PatternDomain.Body, 0.72, 0.9, Quat.AxisAngle(Vec3.UnitZ, 0.45), side);
                sc.MinDensity = hd; sc.DomainLength = dom; sc.Tag = "scapula";
                var hs = b.Ellipsoid(o.Group, o.Pelvis, new Vec3(-o.HipR * 0.32, -o.HipR * 0.08, side * o.HipR * o.Wz * 0.6), new Vec3(o.HipR * 0.62, o.HipR * 0.78, o.HipR * 0.36),
                    MaterialSlot.Primary, PatternDomain.Body, 0.02, 0.2, Quat.AxisAngle(Vec3.UnitZ, -0.25), side);
                hs.MinDensity = hd; hs.DomainLength = dom; hs.Tag = "hamstring";
            }
            if (form != "hydra")
            {
                barrel.MaxDensity = hd;
                var rib = b.Ellipsoid(o.Group, o.Spine, new Vec3(o.L * 0.12, -o.Drop * 1.1 - o.MidR * 0.06, 0), new Vec3(o.L * 0.33, o.MidR * 1.04, o.MidR * o.Wz * 0.95), MaterialSlot.Primary, PatternDomain.Body, 0.4, 0.85);
                rib.MinDensity = hd; rib.DomainLength = dom; rib.Tag = "ribcage";
                var loin = b.Ellipsoid(o.Group, o.Spine, new Vec3(-o.L * 0.22, o.MidR * 0.14, 0), new Vec3(o.L * 0.3, o.MidR * 0.8, o.MidR * o.Wz * 0.86), MaterialSlot.Primary, PatternDomain.Body, 0.15, 0.45);
                loin.MinDensity = hd; loin.DomainLength = dom; loin.Tag = "loin";
            }

            // ---- neck
            double neckLen = baseL * DMath.Lerp(0.15, 0.6, g.Get("neck.length")) * (form == "hydra" ? 1.35 : (form == "unicorn" || form == "kirin" ? 1.15 : 1.0));
            double carriage = (form == "unicorn" || form == "kirin" ? 55 : (form == "hydra" ? 50 : 32)) * DMath.Deg2Rad;
            o.NeckDir = new Vec3(DMath.Cos(carriage), DMath.Sin(carriage), 0);
            o.N0 = o.C + new Vec3(o.ChestR * 0.5, o.ChestR * 0.45, 0);
            o.NeckLen = neckLen;
            o.Nr0 = Math.Max(1.4, o.ChestR * (form == "unicorn" || form == "kirin" ? 0.62 : 0.68));
            o.Nr1 = Math.Max(1.1, o.Nr0 * 0.68);
            o.Neck0 = b.BoneAlong("neck.0", o.Chest, o.N0, o.NeckDir, Vec3.UnitY, BoneRole.Neck, 0, neckLen * 0.5);
            o.Neck1 = b.BoneAlong("neck.1", o.Neck0, o.N0 + o.NeckDir * (neckLen * 0.5), o.NeckDir, Vec3.UnitY, BoneRole.Neck, 0, neckLen * 0.5);
            if (form != "centaur")
            {
                var nk0 = b.Segment(o.Group, o.Neck0, o.Nr0, o.Neck1, (o.Nr0 + o.Nr1) * 0.5, form == "griffin" ? MaterialSlot.White : MaterialSlot.Primary, PatternDomain.Neck, 0, 0.5);
                nk0.DomainLength = neckLen + 2;
            }
            return o;
        }

        private static HeadSpec HeadFor(string form, Body o, double skullL, EyeStyle eye, CreatureGenome g)
        {
            double eyeSize = DMath.Clamp(DMath.Lerp(1.6, 4.0, g.Get("head.eyeSize")) * Math.Sqrt(skullL / 9.0), 1.4, 5.5);
            var hs = new HeadSpec
            {
                SkullLength = skullL, SkullHeight = skullL * 0.75, SkullWidth = skullL * 0.68, SkullOffset = 0.28,
                EyeSize = eyeSize, EyeStyle = eye, EyeForward = 0.45, EyeHeight = 0.25,
                Teeth = TeethKind.Small, TeethSize = DMath.Clamp(skullL / 8.0, 0.8, 1.6), Group = o.Group, DomainLength = skullL * 1.8, JawOpenMax = 0.6,
            };
            double pitch = -18 * DMath.Deg2Rad;
            switch (form)
            {
                case "griffin":
                    // eagle head: round feathered skull, hooked beak (accent), fierce brow
                    hs.SkullHeight = skullL * 0.85; hs.SkullWidth = skullL * 0.72;
                    hs.SnoutLength = skullL * 0.55; hs.SnoutRadius = skullL * 0.22; hs.SnoutTaper = 0.45; hs.SnoutDrop = 0.35;
                    hs.SnoutMaterial = MaterialSlot.Accent; hs.SnoutPattern = false; hs.NoseSize = 0; hs.Nostrils = false;
                    hs.JawLength = 0.85; hs.JawRadius = skullL * 0.12; hs.JawMaterial = MaterialSlot.Accent;
                    hs.Teeth = TeethKind.Beak; hs.Brow = 0.8; hs.EyeForward = 0.55; hs.Material = MaterialSlot.White; hs.Structure = false;
                    pitch = -8 * DMath.Deg2Rad;
                    break;
                case "unicorn":
                case "kirin":
                case "centaur":
                    // long equine head with a broad muzzle
                    hs.SkullLength = skullL * 0.9; hs.SkullHeight = skullL * 0.62; hs.SkullWidth = skullL * 0.55;
                    hs.SnoutLength = skullL * 0.95; hs.SnoutRadius = skullL * 0.24; hs.SnoutTaper = 0.85; hs.SnoutDrop = 0.12; hs.SnoutWidth = 1.1;
                    hs.JawLength = 0.95; hs.JawRadius = skullL * 0.17; hs.Teeth = TeethKind.None; hs.EyeForward = 0.2; hs.NoseSize = 1.0; hs.DarkNose = false;
                    pitch = -50 * DMath.Deg2Rad;
                    if (form == "kirin") { hs.Teeth = TeethKind.Fangs; hs.Brow = 0.6; hs.SnoutWidth = 1.2; hs.Cheeks = 0.6; }
                    break;
                case "hydra":
                    // serpentine dragon head
                    hs.SkullHeight = skullL * 0.58; hs.SkullWidth = skullL * 0.62;
                    hs.SnoutLength = skullL * 0.85; hs.SnoutRadius = skullL * 0.2; hs.SnoutTaper = 0.7; hs.SnoutDrop = 0.05;
                    hs.JawLength = 1.0; hs.JawRadius = skullL * 0.13; hs.Teeth = TeethKind.Fangs; hs.Brow = 0.7; hs.EyeForward = 0.35; hs.JawOpenMax = 0.85;
                    pitch = -10 * DMath.Deg2Rad;
                    break;
                case "cerberus":
                    // hound head
                    hs.SnoutLength = skullL * 0.75; hs.SnoutRadius = skullL * 0.25; hs.SnoutTaper = 0.75; hs.SnoutDrop = 0.06;
                    hs.JawLength = 0.95; hs.JawRadius = skullL * 0.16; hs.Teeth = TeethKind.Fangs; hs.Brow = 0.65; hs.JawOpenMax = 0.8;
                    break;
                default:
                    // manticore: broad lion face
                    hs.SkullHeight = skullL * 0.88; hs.SkullWidth = skullL * 0.85;
                    hs.SnoutLength = skullL * 0.42; hs.SnoutRadius = skullL * 0.3; hs.SnoutTaper = 0.85; hs.SnoutWidth = 1.25; hs.SnoutHeight = -0.2;
                    hs.JawLength = 0.9; hs.JawRadius = skullL * 0.18; hs.Teeth = TeethKind.Sabre; hs.Brow = 0.6; hs.Cheeks = 0.7; hs.EyeForward = 0.7; hs.JawOpenMax = 0.8;
                    break;
            }
            hs.Forward = new Vec3(DMath.Cos(pitch), DMath.Sin(pitch), 0);
            return hs;
        }

        private static void HeadExtras(AnatomyBuilder b, CreatureGenome g, string form, HeadResult head, double skullL, Rng rng)
        {
            var sc = head.SkullCenterLocal;
            double hornSize = g.Get("head.hornSize");
            switch (form)
            {
                case "griffin":
                    // feathered ear tufts
                    Ears.Build(b, new EarSpec { Kind = EarKind.Tufted, HeadBone = head.Head, Base = sc + new Vec3(-skullL * 0.2, skullL * 0.32, skullL * 0.2), Size = skullL * 0.45, Width = 0.8, Material = MaterialSlot.White });
                    break;
                case "unicorn":
                    Ears.Build(b, new EarSpec { Kind = EarKind.Pointed, HeadBone = head.Head, Base = sc + new Vec3(-skullL * 0.15, skullL * 0.28, skullL * 0.18), Size = skullL * 0.42 });
                    Horns.Build(b, new HornSpec
                    {
                        Kind = HornKind.Unicorn, HeadBone = head.Head, Front = sc + new Vec3(skullL * 0.3, skullL * 0.3, 0),
                        Size = skullL * DMath.Lerp(0.6, 1.1, hornSize), Thickness = 0.42, Material = MaterialSlot.Accent,
                    });
                    SpiralRidges(b, head.Head, sc + new Vec3(skullL * 0.3, skullL * 0.3, 0), skullL * DMath.Lerp(0.6, 1.1, hornSize));
                    break;
                case "kirin":
                    Ears.Build(b, new EarSpec { Kind = EarKind.Fin, HeadBone = head.Head, Base = sc + new Vec3(-skullL * 0.2, skullL * 0.15, skullL * 0.25), Size = skullL * 0.5 });
                    Horns.Build(b, new HornSpec { Kind = HornKind.Antlers, HeadBone = head.Head, Base = sc + new Vec3(-skullL * 0.05, skullL * 0.3, skullL * 0.15), Size = skullL * DMath.Lerp(0.6, 1.2, hornSize), Material = MaterialSlot.Accent });
                    // whiskers trailing from the muzzle
                    Whiskers(b, head, skullL);
                    break;
                case "hydra":
                    Horns.Build(b, new HornSpec { Kind = HornKind.Backswept, HeadBone = head.Head, Base = sc + new Vec3(-skullL * 0.1, skullL * 0.22, skullL * 0.18), Size = skullL * DMath.Lerp(0.4, 0.9, hornSize), Material = MaterialSlot.Accent });
                    break;
                case "cerberus":
                    Ears.Build(b, new EarSpec { Kind = EarKind.Pointed, HeadBone = head.Head, Base = sc + new Vec3(-skullL * 0.12, skullL * 0.35, skullL * 0.25), Size = skullL * 0.55 });
                    break;
                default:
                    Ears.Build(b, new EarSpec { Kind = EarKind.Round, HeadBone = head.Head, Base = sc + new Vec3(-skullL * 0.15, skullL * 0.35, skullL * 0.3), Size = skullL * 0.4 });
                    if (hornSize > 0.4)
                        Horns.Build(b, new HornSpec { Kind = HornKind.Curved, HeadBone = head.Head, Base = sc + new Vec3(0, skullL * 0.38, skullL * 0.2), Size = skullL * DMath.Lerp(0.4, 0.9, hornSize), Material = MaterialSlot.Accent });
                    break;
            }
            _ = rng;
        }

        /// <summary>Spiral groove on a unicorn horn (high density): small dark rings stepping up the horn.</summary>
        private static void SpiralRidges(AnatomyBuilder b, int head, Vec3 front, double size)
        {
            Vec3 d = new Vec3(0.8, 0.75, 0).Normalized();
            int g = b.Group("horn.spiral", 0.2, 0, GroupFlags.NoContour, 0.5);
            for (int i = 1; i < 6; i++)
            {
                double t = i / 6.0;
                double r = DMath.Lerp(0.32 * size * 0.42, 0.3, t) * 1.04;
                var ring = b.Ellipsoid(g, head, front + d * (size * t), new Vec3(0.35, r, r), MaterialSlot.Accent, PatternDomain.None, 0, 1,
                    AnatomyBuilder.QuatFromBasis(Mat3.LookAlong(d, Vec3.UnitY)) * Quat.AxisAngle(Vec3.UnitZ, 0.35));
                ring.Flags |= PrimFlags.NoPattern | PrimFlags.Hard;
                ring.ShadeBias = -1;
                ring.MinDensity = Limbs.HdDensity;
                ring.Tag = "spiral";
            }
        }

        private static void Whiskers(AnatomyBuilder b, HeadResult head, double skullL)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                Vec3 a = head.SnoutTipLocal + new Vec3(-skullL * 0.1, -skullL * 0.05, side * skullL * 0.12);
                var ch = Chains.Build(b, new ChainSpec
                {
                    Name = "whisker" + (side < 0 ? "L" : "R"), ParentBone = head.Head, Base = b.RestWorld(head.Head).Point(a),
                    Direction = b.RestWorld(head.Head).Dir(new Vec3(0.2, -0.5, side)).NormalizedOr(Vec3.UnitZ), Length = skullL * 0.9, Segments = 3,
                    RadiusBase = 0.45, RadiusTip = 0.3, Curl = -0.8, Material = MaterialSlot.Hair, Domain = PatternDomain.Appendage,
                    Role = BoneRole.Antenna, Stiffness = 0.3, Droop = 0.6, Side = side, Flags = PrimFlags.Thin | PrimFlags.NoPattern,
                });
                b.Rig.Appendages.Add(ch);
            }
        }

        /// <summary>A further head on its own long neck. The neck is an appendage chain: the heads weave independently.</summary>
        private static void ExtraHead(AnatomyBuilder b, CreatureGenome g, string form, Body o, double skullL, EyeStyle eye, int i, int count)
        {
            bool hydra = form == "hydra";
            double side = (i % 2 == 0 ? 1 : -1) * (1 + i / 2);
            // fan the heads out sideways (and the hydra's in height too) so each one reads in every view
            double spreadDeg = hydra ? 38 : 58;
            double yaw = side * spreadDeg * DMath.Deg2Rad;
            double len = o.NeckLen * (hydra ? DMath.Lerp(0.95, 1.25, (i % 3) / 2.0) : 1.0);
            Vec3 baseP = o.C + new Vec3(o.ChestR * 0.25, o.ChestR * 0.55, Math.Sign(side) * o.ChestR * o.Wz * (hydra ? 0.5 : 0.62));
            double el = (hydra ? 40 + 14 * ((i / 2) % 2) : 34) * DMath.Deg2Rad;
            // the neck leans out on the same side as its root, so necks never cross
            Vec3 dir = new Vec3(DMath.Cos(el) * DMath.Cos(yaw), DMath.Sin(el), DMath.Cos(el) * DMath.Sin(yaw)).Normalized();
            int segs = hydra ? 3 : 2;
            var bones = new List<int>();
            int par = o.Chest;
            Vec3 p = baseP;
            Vec3 d = dir;
            for (int k = 0; k < segs; k++)
            {
                bones.Add(b.BoneAlong($"neck{i}.{k}", par, p, d, Vec3.UnitY, BoneRole.Tentacle, Math.Sign((int)side), len / segs));
                par = bones[bones.Count - 1];
                p = p + d * (len / segs);
                if (hydra) d = (d + new Vec3(0.25, -0.12, 0)).Normalized();
            }
            var hs = HeadFor(form, o, skullL * (hydra ? 0.92 : 0.95), eye, g);
            hs.Name = $"head{i}";
            hs.ParentBone = par;
            hs.Position = p;
            hs.Forward = (d + new Vec3(0.35, -0.25, 0)).Normalized();
            hs.Group = b.Group($"neckhead{i}", DMath.Clamp(o.Nr0 * 0.4, 1.2, 3.0), Math.Sign((int)side));
            var head = Heads.Build(b, hs);
            bones.Add(head.Head);
            double r0 = o.Nr0 * (hydra ? 0.8 : 0.85), r1 = o.Nr1 * 0.9;
            for (int k = 0; k < segs; k++)
            {
                int nb = bones[k + 1];
                var seg = b.Segment(hs.Group, bones[k], DMath.Lerp(r0, r1, k / (double)segs), nb, DMath.Lerp(r0, r1, (k + 1) / (double)segs), MaterialSlot.Primary, PatternDomain.Neck, k / (double)segs, (k + 1) / (double)segs);
                seg.DomainLength = len + 2;
            }
            if (!hydra)
                Ears.Build(b, new EarSpec { Kind = EarKind.Pointed, HeadBone = head.Head, Base = head.SkullCenterLocal + new Vec3(-skullL * 0.12, skullL * 0.35, skullL * 0.25), Size = skullL * 0.5, Name = $"ear{i}" });
            else
                Horns.Build(b, new HornSpec { Kind = HornKind.Backswept, HeadBone = head.Head, Base = head.SkullCenterLocal + new Vec3(-skullL * 0.1, skullL * 0.22, skullL * 0.18), Size = skullL * DMath.Lerp(0.35, 0.8, g.Get("head.hornSize")), Material = MaterialSlot.Accent, Name = $"horn{i}" });
            var lens = new double[bones.Count - 1];
            for (int k = 0; k + 1 < bones.Count; k++) lens[k] = (b.RestWorld(bones[k + 1]).Origin - b.RestWorld(bones[k]).Origin).Length;
            b.Rig.Appendages.Add(new ChainRig
            {
                Name = $"neck{i}", Bones = bones.ToArray(), Lengths = lens, TotalLength = len, Side = Math.Sign((int)side),
                Radii = new double[bones.Count], Stiffness = hydra ? 0.55 : 0.85, Droop = hydra ? 0.25 : 0.1,
                RestPitch = new double[bones.Count - 1], RestYaw = new double[bones.Count - 1],
            });
            _ = count;
        }

        /// <summary>Flowing mane: locks along the crest of the neck that swing as spring chains, plus a forelock; lions get a ruff.</summary>
        private static void BuildMane(AnatomyBuilder b, Body o, string form, double mane, double skullL, HeadResult head, Rng rng)
        {
            bool ruff = form == "manticore";
            if (form == "cerberus") return;
            if (ruff)
            {
                // lion ruff: shaggy hair masses around the neck and chest (hair slot: fur silhouette at high density)
                int g = b.Group("ruff", 2.0, 0, GroupFlags.CreaseShade, 0.1);
                for (int i = 0; i < 7; i++)
                {
                    double t = i / 6.0;
                    double a = DMath.Lerp(-1.3, 1.3, t);
                    Vec3 p = o.N0 + o.NeckDir * (o.NeckLen * 0.55) + new Vec3(-o.Nr0 * 0.3, DMath.Cos(a) * o.Nr0 * 0.6, DMath.Sin(a) * o.Nr0 * 1.0);
                    double r = o.Nr0 * DMath.Lerp(0.9, 1.4, mane);
                    var e = b.Ellipsoid(g, o.Neck1, b.RestWorld(o.Neck1).InversePoint(p), new Vec3(r * 0.9, r, r * 0.8), MaterialSlot.Hair, PatternDomain.Neck);
                    e.Flags |= PrimFlags.NoPattern | PrimFlags.ShadowCaster;
                    e.DomainLength = skullL * 3;
                    e.Tag = "ruff";
                }
                var chestRuff = b.Ellipsoid(g, o.Chest, new Vec3(o.ChestR * 0.85, o.ChestR * 0.1, 0), new Vec3(o.ChestR * 0.6, o.ChestR * 0.85 * DMath.Lerp(0.8, 1.1, mane), o.ChestR * 0.7), MaterialSlot.Hair, PatternDomain.Body);
                chestRuff.Flags |= PrimFlags.NoPattern;
                chestRuff.DomainLength = skullL * 3;
                return;
            }
            // flowing locks along the crest, falling to one side
            int locks = form == "centaur" ? 0 : 5;
            Vec3 up = new Vec3(-o.NeckDir.Y, o.NeckDir.X, 0);
            for (int i = 0; i < locks; i++)
            {
                double t = (i + 0.3) / locks;
                int bone = t < 0.5 ? o.Neck0 : o.Neck1;
                Vec3 at = o.N0 + o.NeckDir * (o.NeckLen * t * 0.95) + up * (DMath.Lerp(o.Nr0, o.Nr1, t) * 0.85);
                double len = skullL * DMath.Lerp(0.5, 1.2, mane) * DMath.Lerp(1.0, 0.75, t);
                var ch = Chains.Build(b, new ChainSpec
                {
                    Name = $"mane{i}", ParentBone = bone, Base = at, Direction = (new Vec3(-0.6, -0.2, 0.8) + up * 0.3).NormalizedOr(Vec3.UnitZ),
                    Length = len, Segments = 3, RadiusBase = Math.Max(1.0, o.Nr1 * 0.55), RadiusTip = 0.45, Curl = -0.5, Sweep = -0.3,
                    Material = MaterialSlot.Hair, Domain = PatternDomain.Appendage, Role = BoneRole.Antenna, Stiffness = 0.3, Droop = 0.55, Side = 1,
                    Flags = PrimFlags.NoPattern, Thin = true,
                });
                b.Rig.Appendages.Add(ch);
            }
            // forelock between the ears
            var fl = Chains.Build(b, new ChainSpec
            {
                Name = "forelock", ParentBone = head.Head, Base = b.RestWorld(head.Head).Point(head.SkullCenterLocal + new Vec3(0, skullL * 0.3, 0)),
                Direction = b.RestWorld(head.Head).Dir(new Vec3(0.8, 0.2, 0.2)).NormalizedOr(Vec3.UnitX), Length = skullL * 0.55 * (0.5 + mane), Segments = 2,
                RadiusBase = Math.Max(0.8, skullL * 0.1), RadiusTip = 0.4, Curl = -0.8, Material = MaterialSlot.Hair, Domain = PatternDomain.Appendage,
                Role = BoneRole.Antenna, Stiffness = 0.35, Droop = 0.5, Flags = PrimFlags.NoPattern,
            });
            b.Rig.Appendages.Add(fl);
            _ = rng;
        }

        private static void BuildTail(AnatomyBuilder b, CreatureGenome g, string form, Body o, double tailLen)
        {
            var tc = form switch { "manticore" => 70, "hydra" => -20, "unicorn" or "kirin" or "centaur" => -35, _ => -10 } * DMath.Deg2Rad;
            Vec3 dir = new Vec3(-DMath.Cos(tc), DMath.Sin(tc), 0);
            bool hairTail = form == "unicorn" || form == "kirin" || form == "centaur";
            double tr0 = Math.Max(1.0, o.HipR * (form == "hydra" ? 0.55 : (hairTail ? 0.32 : 0.22)));
            var spec = new ChainSpec
            {
                Name = "tail", ParentBone = o.Pelvis, Base = o.P + new Vec3(-o.HipR * 0.95, o.HipR * 0.42, 0), Direction = dir, Length = tailLen,
                Segments = DMath.Clamp((int)Math.Round(tailLen / 3.2), 3, 10), RadiusBase = tr0, RadiusTip = Math.Max(0.45, tr0 * 0.3),
                Curl = form == "manticore" ? 2.4 : 0.3, Tip = TailTip.Plain, TipSize = DMath.Clamp(tr0 * 1.1, 0.9, 2.6), Group = o.Group, Stiffness = 0.5,
                Droop = hairTail ? 0.7 : 0.4,
            };
            switch (form)
            {
                case "manticore":
                    // segmented scorpion tail arching over the back with a stinger
                    spec.Tip = TailTip.Stinger; spec.TipMaterial = MaterialSlot.Accent; spec.Stiffness = 0.75; spec.Material = MaterialSlot.Marking; spec.Group = -1;
                    break;
                case "griffin":
                case "cerberus":
                    spec.Tip = TailTip.Tuft; spec.TipMaterial = MaterialSlot.Hair;
                    break;
                case "hydra":
                    spec.RadiusTip = 0.7; spec.TaperPower = 0.8;
                    break;
                default:
                    // horse tail: a short dock and long flowing hair
                    spec.Length = tailLen * 0.25; spec.Segments = 2; spec.Tip = TailTip.Plain;
                    break;
            }
            b.Rig.Tail = Chains.Build(b, spec);
            if (hairTail)
            {
                var dock = b.Rig.Tail.Bones[b.Rig.Tail.Bones.Length - 1];
                var hair = Chains.Build(b, new ChainSpec
                {
                    Name = "tailhair", ParentBone = dock, Base = b.RestWorld(dock).Origin, Direction = new Vec3(-0.45, -1, 0).NormalizedOr(Vec3.UnitY), Length = tailLen * 0.85,
                    Segments = 4, RadiusBase = tr0 * 1.3, RadiusTip = tr0 * 0.6, Bulge = 0.5, Material = MaterialSlot.Hair, Domain = PatternDomain.Tail,
                    Role = BoneRole.Antenna, Stiffness = 0.25, Droop = 0.8, Flags = PrimFlags.NoPattern,
                });
                b.Rig.Appendages.Add(hair);
            }
        }

        private static void BuildLegs(AnatomyBuilder b, CreatureGenome g, string form, Body o)
        {
            bool hooves = form == "unicorn" || form == "kirin" || form == "centaur";
            double claws = g.Get("claws");
            for (int pair = 0; pair < 2; pair++)
            {
                bool front = pair == 0;
                for (int side = -1; side <= 1; side += 2)
                {
                    double bodyR = front ? o.ChestR : o.HipR;
                    Vec3 hipJ = front ? o.C + new Vec3(o.ChestR * 0.12, -o.ChestR * 0.22, side * o.ChestR * o.Wz * 0.5) : o.P + new Vec3(o.HipR * 0.05, -o.HipR * 0.12, side * o.HipR * o.Wz * 0.55);
                    Vec3 contact = new Vec3(hipJ.X + (front ? o.L * 0.03 : -o.L * 0.02), 0, side * bodyR * o.Wz * (front ? 0.6 : 0.66));
                    var style = hooves ? LegStyle.Unguligrade : (form == "hydra" ? LegStyle.Sprawling : LegStyle.Digitigrade);
                    // griffin front legs: scaly eagle legs with talons
                    bool talon = form == "griffin" && front;
                    double factor = hooves ? (front ? 1.2 : 1.32) : (front ? 1.16 : 1.34);
                    double fu = hooves ? (front ? 0.38 : 0.33) : (front ? 0.42 : 0.36);
                    double fl = hooves ? (front ? 0.36 : 0.35) : (front ? 0.4 : 0.38);
                    double fd = hooves ? (front ? 0.26 : 0.32) : (front ? 0.18 : 0.26);
                    double meta = hooves ? (front ? 6 : 14) : (front ? 12 : 24);
                    if (form == "hydra") { factor = 1.3; fu = 0.42; fl = 0.38; fd = 0.2; meta = 0; }
                    double total = hipJ.Y * factor;
                    double rTop = Math.Max(1.3, bodyR * DMath.Lerp(0.38, 0.55, g.Get("mass")));
                    double pawR = Math.Max(1.0, rTop * (hooves ? 0.55 : 0.62));
                    var spec = new LegSpec
                    {
                        Name = (front ? "front" : "hind") + (side < 0 ? "L" : "R"), Side = side, Pair = pair, Front = front,
                        AnchorBone = front ? o.Chest : o.Pelvis, Hip = hipJ, Contact = contact, Style = style,
                        Length = total, Upper = fu, Lower = fl, Distal = fd,
                        RadiusTop = rTop, RadiusKnee = Math.Max(0.9, rTop * (talon ? 0.5 : 0.62)), RadiusAnkle = Math.Max(0.7, rTop * (talon ? 0.36 : 0.44)), RadiusFoot = Math.Max(0.7, rTop * 0.48),
                        Haunch = front ? 0.85 : 1.1, PawLength = pawR * (talon ? 2.6 : 2.0), PawRadius = pawR,
                        Material = talon ? MaterialSlot.Accent : MaterialSlot.Primary, FootMaterial = talon ? MaterialSlot.Accent : MaterialSlot.Primary,
                        Claws = hooves || claws < 0.2 ? 0 : (talon ? 4 : 3), ClawSize = DMath.Lerp(0.6, 1.8, claws) * Math.Sqrt(o.BaseL / 26.0) * (talon ? 1.4 : 1.0),
                        LiftHeight = Math.Max(1.5, total * 0.14), MetaAngle = meta, BlendRadius = DMath.Clamp(rTop * 0.45, 0.8, 2.2), DomainLength = total,
                        Cloven = form == "kirin", Muscle = hooves ? 1.0 : 0.8, Feathering = form == "kirin" || (form == "unicorn" && side > 0 && pair == 1) ? 0.6 : (form == "unicorn" ? 0.5 : 0),
                        Toes = talon ? 4 : 0,
                    };
                    var leg = Limbs.Build(b, spec);
                    leg.Phase = (front, side < 0) switch { (false, true) => 0.0, (true, true) => 0.25, (false, false) => 0.5, _ => 0.75 };
                    b.Rig.Legs.Add(leg);
                }
            }
        }

        /// <summary>Centaur: a humanoid torso rising from the horse chest, with arms and a human head.</summary>
        private static HeadResult BuildCentaurTorso(AnatomyBuilder b, CreatureGenome g, Body o, double skullL, EyeStyle eye)
        {
            double tl = o.BaseL * 0.55;
            double tr = o.ChestR * 0.62;
            Vec3 waist = o.C + new Vec3(o.ChestR * 0.55, o.ChestR * 0.55, 0);
            Vec3 up = new Vec3(0.12, 1, 0).Normalized();
            int t0 = b.BoneAlong("torso.0", o.Chest, waist, up, Vec3.UnitX * -1, BoneRole.Neck, 0, tl * 0.5);
            int t1 = b.BoneAlong("torso.1", t0, waist + up * (tl * 0.5), up, Vec3.UnitX * -1, BoneRole.Neck, 0, tl * 0.5);
            Vec3 sh = waist + up * tl;
            int tg = b.Group("torso", DMath.Clamp(tr * 0.35, 1.2, 3), 0, GroupFlags.None);
            var abd = b.Segment(tg, t0, tr * 0.95, t1, tr * 1.05, MaterialSlot.Secondary, PatternDomain.Body, 0.8, 0.9);
            abd.DomainLength = tl * 2;
            var ribs = b.Ellipsoid(tg, t1, new Vec3(tl * 0.3, 0, 0), new Vec3(tl * 0.35, tr * 1.05, tr * 1.25), MaterialSlot.Secondary, PatternDomain.Body, 0.9, 1.0);
            ribs.DomainLength = tl * 2;
            // high density: pectorals and abdominal plates
            for (int s = -1; s <= 1; s += 2)
            {
                var pec = b.Ellipsoid(tg, t1, new Vec3(tl * 0.42, tr * 0.55, s * tr * 0.48), new Vec3(tl * 0.16, tr * 0.42, tr * 0.5), MaterialSlot.Secondary, PatternDomain.Body, 0.95, 1.0, null, s);
                pec.MinDensity = Limbs.HdDensity; pec.Tag = "pectoral";
            }
            for (int k = 0; k < 3; k++)
            {
                var ab = b.Ellipsoid(tg, t0, new Vec3(tl * (0.12 + 0.15 * k), tr * 0.62, 0), new Vec3(tl * 0.06, tr * 0.32, tr * 0.6), MaterialSlot.Secondary, PatternDomain.Body, 0.85, 0.9);
                ab.MinDensity = Limbs.HdDensity; ab.Tag = "abs";
            }
            // head on a short neck
            int neck = b.BoneAlong("neck", t1, sh, up, Vec3.UnitX * -1, BoneRole.Neck, 0, skullL * 0.4);
            var hs = HeadFor("centaur", o, skullL, eye, g);
            hs.SkullLength = skullL * 0.62; hs.SkullHeight = skullL * 0.66; hs.SkullWidth = skullL * 0.58; hs.SkullOffset = 0.15;
            hs.SnoutLength = skullL * 0.12; hs.SnoutRadius = skullL * 0.15; hs.Nose = NoseKind.Long; hs.NoseSize = 0.7; hs.JawLength = 0.6; hs.JawRadius = skullL * 0.12;
            hs.EyeForward = 0.85; hs.EyeHeight = 0.2; hs.Material = MaterialSlot.Secondary; hs.JawMaterial = MaterialSlot.Secondary; hs.SnoutMaterial = MaterialSlot.Secondary;
            hs.ParentBone = neck; hs.Position = sh + up * (skullL * 0.45); hs.Forward = new Vec3(1, -0.1, 0).Normalized(); hs.Group = tg; hs.Structure = false;
            var head = Heads.Build(b, hs);
            var nk = b.Segment(tg, neck, tr * 0.45, head.Head, tr * 0.4, MaterialSlot.Secondary, PatternDomain.Neck);
            nk.DomainLength = tl;
            Ears.Build(b, new EarSpec { Kind = EarKind.Elf, HeadBone = head.Head, Base = head.SkullCenterLocal + new Vec3(-skullL * 0.05, 0, skullL * 0.27), Size = skullL * 0.35, Material = MaterialSlot.Secondary });
            // hair: a cap and a long ponytail
            int hg = b.Group("hair", 1.0, 0, GroupFlags.None, 0.2);
            var cap = b.Ellipsoid(hg, head.Head, head.SkullCenterLocal + new Vec3(-skullL * 0.06, skullL * 0.1, 0), head.SkullRadii * 1.08, MaterialSlot.Hair, PatternDomain.Head);
            cap.Flags |= PrimFlags.NoPattern;
            var tail = Chains.Build(b, new ChainSpec
            {
                Name = "ponytail", ParentBone = head.Head, Base = b.RestWorld(head.Head).Point(head.SkullCenterLocal + new Vec3(-head.SkullRadii.X * 0.9, head.SkullRadii.Y * 0.2, 0)),
                Direction = new Vec3(-0.4, -1, 0).NormalizedOr(Vec3.UnitY), Length = skullL * DMath.Lerp(0.6, 1.4, g.Get("mane")), Segments = 3, RadiusBase = skullL * 0.16, RadiusTip = skullL * 0.08,
                Material = MaterialSlot.Hair, Domain = PatternDomain.Appendage, Role = BoneRole.Antenna, Stiffness = 0.3, Droop = 0.7, Flags = PrimFlags.NoPattern,
            });
            b.Rig.Appendages.Add(tail);
            // arms with hands
            for (int s = -1; s <= 1; s += 2)
            {
                Vec3 shoulder = sh - up * (tr * 0.4) + new Vec3(0, 0, s * tr * 1.2);
                double armLen = tl * 1.25;
                Vec3 wrist = shoulder + new Vec3(armLen * 0.25, -armLen * 0.6, s * armLen * 0.12);
                var arm = Limbs.Build(b, new LegSpec
                {
                    Name = "arm" + (s < 0 ? "L" : "R"), Side = s, Pair = 0, Front = true, IsArm = true, Hand = true, Style = LegStyle.Plantigrade,
                    AnchorBone = t1, Hip = shoulder, Contact = wrist, Length = armLen, Upper = 0.45, Lower = 0.4, Distal = 0.15,
                    RadiusTop = tr * 0.38, RadiusKnee = tr * 0.3, RadiusAnkle = tr * 0.24, RadiusFoot = tr * 0.24, Haunch = 0.8,
                    PawLength = armLen * 0.15, PawRadius = tr * 0.25, Material = MaterialSlot.Secondary, FootMaterial = MaterialSlot.Secondary,
                    BlendRadius = 1.0, DomainLength = armLen, Pole = new Vec3(-0.6, 0.1, 0.6), Muscle = 0.9,
                });
                b.Rig.Arms.Add(arm);
            }
            b.Rig.Neck = new ChainRig { Name = "torso", Bones = new[] { t0, t1, neck, head.Head }, Lengths = new[] { tl * 0.5, tl * 0.5, skullL * 0.45 }, TotalLength = tl + skullL * 0.45 };
            b.Rig.Tuning["action.armSide"] = 1;
            return head;
        }
    }
}
