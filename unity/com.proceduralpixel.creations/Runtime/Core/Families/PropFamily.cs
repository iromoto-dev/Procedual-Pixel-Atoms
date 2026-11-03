// Procedural Pixel Creatures - family "prop": procedural objects and scenery.
//
// Props are assembled from hard edged solids (boxes, prisms, vaults) with mapped surfaces (wood planks,
// masonry courses, faceted stone, crystal), plus soft primitives where organic shapes read better
// (boulders, pots, flames, cloth). They are stationary and use the "rooted" motion module: flames
// flicker and banners and lanterns swing in the wind (spring chains), the "action" shakes the object
// and opens lids (the chest lid is the rig's jaw), hits make it wobble about its base.

#nullable enable

using System;
using System.Collections.Generic;
using PixelCreatures.Core.Anatomy;
using PixelCreatures.Core.Genetics;
using PixelCreatures.Core.Motion;
using PixelCreatures.Core.Palettes;
using PixelCreatures.Core.Shapes;

namespace PixelCreatures.Core.Families
{
    public sealed class PropFamily : CreatureFamilyBase, ISurfaceCustomizer
    {
        public override string Id => "prop";
        public override string DisplayName => "Props & Scenery";
        public override string Description => "Procedural objects: crates, barrels, treasure chests, rocks, crystal clusters, lamp posts, braziers, campfires, gravestones, pillars, signposts, pots and banners. Flames flicker, banners and lanterns sway, lids open.";
        public override int Order => 310;
        public override CreationCategory Category => CreationCategory.Prop;

        private static readonly ArchetypeInfo[] ArchetypeList =
        {
            new ArchetypeInfo("crate", "Crate", 1.0),
            new ArchetypeInfo("barrel", "Barrel", 1.0),
            new ArchetypeInfo("chest", "Treasure chest", 1.0),
            new ArchetypeInfo("rock", "Rock", 1.1),
            new ArchetypeInfo("crystal", "Crystal cluster", 0.9),
            new ArchetypeInfo("lantern", "Lamp post", 0.8),
            new ArchetypeInfo("brazier", "Brazier", 0.7),
            new ArchetypeInfo("campfire", "Campfire", 0.8),
            new ArchetypeInfo("gravestone", "Gravestone", 0.9),
            new ArchetypeInfo("pillar", "Pillar", 0.8),
            new ArchetypeInfo("signpost", "Signpost", 0.7),
            new ArchetypeInfo("pot", "Pot", 0.9),
            new ArchetypeInfo("banner", "Banner", 0.7),
        };

        public override IReadOnlyList<ArchetypeInfo> Archetypes => ArchetypeList;

        public static readonly (string, string)[] Forms =
        {
            ("crate", "Crate"), ("barrel", "Barrel"), ("chest", "Treasure chest"), ("rock", "Rock"), ("crystal", "Crystal cluster"), ("lantern", "Lamp post"),
            ("brazier", "Brazier"), ("campfire", "Campfire"), ("gravestone", "Gravestone"), ("pillar", "Pillar"), ("signpost", "Signpost"), ("pot", "Pot"), ("banner", "Banner"),
        };
        public static readonly (string, string)[] Metals = { ("steel", "Steel"), ("iron", "Iron"), ("bronze", "Bronze"), ("gold", "Gold") };
        public static readonly (string, string)[] PropMaterials =
        {
            ("wood", "Wood"), ("stone", "Stone"), ("masonry", "Masonry"), ("crystal", "Crystal"), ("metal", "Metal"), ("glazed", "Glazed"), ("hide", "Plain"), ("bark", "Bark"),
        };
        public static readonly (string, string)[] PropPatterns = { ("none", "None"), ("mottled", "Moss & lichen"), ("blotches", "Stains"), ("bands", "Painted bands"), ("stripes", "Stripes"), ("spots", "Spots") };

        protected override GeneSchema CreateSchema()
        {
            var b = new GeneSchemaBuilder(Id);
            b.Group("shape", "Shape", GeneStream.Anatomy)
             .Choice("form", "Object", Forms, "crate", "Kind of object used as a template when rerolling.", 0.04)
             .Float("size", "Size", 0, 1, 0.5, "", 0.08)
             .Float("height", "Height", 0, 1, 0.5, "", 0.1)
             .Float("width", "Width", 0, 1, 0.5, "", 0.1)
             .Float("count", "Count", 0, 1, 0.4, "Stacks, pieces, logs, crystals or signs.", 0.12)
             .Float("wear", "Wear", 0, 1, 0.2, "Damage: missing planks, broken tops, cracks and rubble.", 0.12)
             .Float("ornament", "Ornament", 0, 1, 0.5, "Metal fittings, bands, carvings and emblems.", 0.12)
             .Float("light", "Light", 0, 1, 0.5, "Size of flames and glow.", 0.12);
            b.Group("propcolor", "Fittings & cloth", GeneStream.Color)
             .Hue("color.clothHue", "Cloth hue", 15, "Banners, cushions, logs and paint.")
             .Float("color.clothSaturation", "Cloth saturation", 0, 1, 0.6, "", 0.12)
             .Float("color.clothValue", "Cloth brightness", 0, 1, 0.45, "", 0.12)
             .Choice("color.metal", "Metal", Metals, "iron", "Bands, hinges, locks, lanterns and bowls.", 0.1);
            SharedGenes.AddColorGenes(b);
            b.Group("pattern", "Surface", GeneStream.Pattern)
             .Choice("surface.material", "Surface material", PropMaterials, "wood", "Wood, stone, masonry, crystal, metal or glazed clay.", 0.08)
             .Float("surface.texture", "Surface texture", 0, 1, 0.6, "Strength of grain, joints and chisel marks.", 0.12)
             .Choice("pattern.type", "Weathering", PropPatterns, "none", "Moss, stains or painted decoration.", 0.12)
             .Float("pattern.size", "Weathering size", 0, 1, 0.5, "", 0.12)
             .Float("pattern.density", "Weathering amount", 0, 1, 0.4, "", 0.12)
             .Bool("pattern.dark", "Dark weathering", true, "", 0.08);
            return b.Build();
        }

        private sealed class Prior
        {
            public double Size = 0.5, Height = 0.5, Width = 0.5, Count = 0.4, Wear = 0.2, Ornament = 0.5, Light = 0.5;
            public string Material = "wood";
        }

        private static Prior PriorFor(string a) => a switch
        {
            "barrel" => new Prior { Size = 0.45, Count = 0.2 },
            "chest" => new Prior { Size = 0.4, Ornament = 0.7, Count = 0.6 },
            "rock" => new Prior { Size = 0.5, Material = "stone", Count = 0.5, Wear = 0.4 },
            "crystal" => new Prior { Size = 0.45, Material = "crystal", Count = 0.55, Light = 0.6 },
            "lantern" => new Prior { Size = 0.55, Height = 0.6, Ornament = 0.5, Light = 0.5 },
            "brazier" => new Prior { Size = 0.4, Material = "metal", Light = 0.6 },
            "campfire" => new Prior { Size = 0.4, Material = "stone", Count = 0.5, Light = 0.6 },
            "gravestone" => new Prior { Size = 0.4, Material = "stone", Wear = 0.4, Ornament = 0.4 },
            "pillar" => new Prior { Size = 0.6, Height = 0.6, Material = "masonry", Wear = 0.35, Ornament = 0.5 },
            "signpost" => new Prior { Size = 0.45, Height = 0.55, Count = 0.4 },
            "pot" => new Prior { Size = 0.35, Material = "glazed", Ornament = 0.5 },
            "banner" => new Prior { Size = 0.55, Height = 0.6, Ornament = 0.6 },
            _ => new Prior { Size = 0.45, Count = 0.3, Ornament = 0.4 },
        };

        public override void SampleAnatomy(double[] v, Rng rng, string arch)
        {
            var p = PriorFor(arch);
            SetChoice(v, "form", arch);
            Around(v, rng, "size", p.Size, 0.13);
            Around(v, rng, "height", p.Height, 0.14);
            Around(v, rng, "width", p.Width, 0.14);
            Around(v, rng, "count", p.Count, 0.2);
            Set(v, "wear", rng.Chance(0.55) ? rng.Range(0.0, 0.25) : rng.Range(0.25, 0.9));
            Around(v, rng, "ornament", p.Ornament, 0.2);
            Around(v, rng, "light", p.Light, 0.15);
        }

        public override FamilyStyleHints StyleHints(string arch)
        {
            var h = new FamilyStyleHints
            {
                Materials = new Dictionary<string, double> { ["hide"] = 1 },
                Patterns = new Dictionary<string, double> { ["none"] = 1 },
                Accents = new Dictionary<string, double> { ["bone"] = 1, ["dark"] = 1 },
                // wood: browns from pale pine to dark oak
                Coats = new[] { Coats.Brown(2.5), Coats.Tan(1.5), Coats.Chocolate(1), Coats.Golden(0.5) },
                Schemes = new Dictionary<string, double> { ["natural"] = 3, ["analogous"] = 1 },
                CountershadeMin = 0, CountershadeMax = 0,
                SocksChance = 0, MaskChance = 0, PiebaldChance = 0, GlowChance = 0,
            };
            switch (arch)
            {
                case "rock":
                case "gravestone":
                case "pillar":
                case "campfire":
                    h.Coats = new[] { Coats.Grey(2), Coats.Slate(1), Coats.Sand(1), Coats.Cream(0.5) };
                    break;
                case "crystal":
                    h.Coats = new[] { Coats.Vivid(3), Coats.Blue(1), Coats.Violet(1), Coats.Teal(1), Coats.Pink(0.6) };
                    h.Accents = new Dictionary<string, double> { ["dark"] = 1 };
                    h.GlowChance = 0.7;
                    break;
                case "pot":
                    h.Coats = new[] { Coats.Rust(2), Coats.Tan(1), Coats.Sand(1), Coats.Blue(0.7), Coats.Teal(0.5), Coats.Cream(0.6) };
                    break;
                case "brazier":
                    h.Coats = new[] { Coats.Slate(1), Coats.Grey(1) };
                    break;
                case "chest":
                    h.Accents = new Dictionary<string, double> { ["gold"] = 1 };
                    break;
            }
            return h;
        }

        public override void AdjustSample(double[] values, Rng rng, string arch)
        {
            var p = PriorFor(arch);
            SetChoice(values, "surface.material", arch switch
            {
                "rock" => PickWeighted(rng, ("stone", 3.0), ("masonry", 0.3)),
                "gravestone" => PickWeighted(rng, ("stone", 2.0), ("masonry", 1.0)),
                "pillar" => PickWeighted(rng, ("masonry", 2.0), ("stone", 1.0)),
                "pot" => PickWeighted(rng, ("glazed", 2.0), ("hide", 1.0)),
                _ => p.Material,
            });
            Set(values, "surface.texture", rng.Range(0.45, 0.9));
            bool stony = arch is "rock" or "gravestone" or "pillar" or "campfire";
            SetChoice(values, "pattern.type", stony ? PickWeighted(rng, ("mottled", 2.0), ("none", 1.0), ("blotches", 0.6))
                : arch == "pot" ? PickWeighted(rng, ("bands", 2.0), ("none", 1.0), ("spots", 0.5))
                : PickWeighted(rng, ("none", 3.0), ("blotches", 1.0)));
            Set(values, "pattern.size", rng.Range(0.3, 0.8));
            Set(values, "pattern.density", rng.Range(0.25, 0.7));
            Set(values, "pattern.dark", arch == "pot" ? (rng.Chance(0.6) ? 1 : 0) : 1);
            // metal fittings
            SetChoice(values, "color.metal", arch switch
            {
                "chest" => PickWeighted(rng, ("gold", 2.0), ("iron", 1.5), ("bronze", 1.0)),
                "brazier" => PickWeighted(rng, ("iron", 2.0), ("bronze", 1.5)),
                "lantern" => PickWeighted(rng, ("iron", 2.0), ("bronze", 1.0), ("steel", 0.6)),
                "banner" => PickWeighted(rng, ("gold", 2.0), ("steel", 1.0)),
                _ => PickWeighted(rng, ("iron", 2.0), ("steel", 1.0), ("bronze", 0.6)),
            });
            // cloth: heraldic colours for banners, wood brown for campfire logs
            if (arch == "campfire")
            {
                Set(values, "color.clothHue", 55 + rng.Gaussian(0, 6, 2));
                Set(values, "color.clothSaturation", rng.Range(0.3, 0.5));
                Set(values, "color.clothValue", rng.Range(0.2, 0.35));
            }
            else
            {
                double hue = PickWeighted(rng, ("25", 2.0), ("265", 1.5), ("145", 1.0), ("305", 0.8), ("85", 0.8), ("200", 0.6)) switch
                { var s => double.Parse(s, System.Globalization.CultureInfo.InvariantCulture) };
                Set(values, "color.clothHue", hue + rng.Gaussian(0, 6, 2));
                Set(values, "color.clothSaturation", rng.Range(0.55, 0.9));
                Set(values, "color.clothValue", rng.Range(0.35, 0.6));
            }
            // flames: warm glow; crystals glow in their own colour
            bool fire = arch is "lantern" or "brazier" or "campfire";
            if (fire)
            {
                Set(values, "color.glow", rng.Range(0.7, 1.0));
                Set(values, "color.eyeHue", rng.Chance(0.88) ? rng.Range(55.0, 78.0) : rng.Range(190.0, 260.0));
            }
            else if (arch == "crystal")
            {
                Set(values, "color.eyeHue", values[Schema.IndexOf("color.hue")]);
                if (values[Schema.IndexOf("color.glow")] < 0.3) Set(values, "color.glow", rng.Range(0.4, 0.9));
            }
            else Set(values, "color.glow", 0);
        }

        public void CustomizeSurface(CreatureGenome genome, SurfaceSpec surface)
        {
            string form = genome.GetChoice("form");
            surface.OverrideSlotStyle(MaterialSlot.Accent, form == "crystal" ? MaterialStyles.Stone : MaterialStyles.Glossy);
            surface.OverrideSlotStyle(MaterialSlot.Cloth, form == "campfire" ? MaterialStyles.Bark : MaterialStyles.Matte);
            surface.OverrideSlotStyle(MaterialSlot.Secondary, form == "crystal" ? MaterialStyles.Crystal : surface.StyleOf(MaterialSlot.Primary));
        }

        // ------------------------------------------------------------------ anatomy

        private sealed class Ctx
        {
            public AnatomyBuilder B = null!;
            public int Root, Base, Top;
            public Rng Rng = null!;
            public double Wear, Ornament, Light, Count;
        }

        public override CreatureAnatomy BuildAnatomy(CreatureGenome g, CreatureRegistry registry)
        {
            var b = new AnatomyBuilder(Id, g.AnatomySeed);
            string form = g.GetChoice("form");
            b.Trait(form);
            var c = new Ctx { B = b, Rng = b.RngFor("prop"), Wear = g.Get("wear"), Ornament = g.Get("ornament"), Light = g.Get("light"), Count = g.Get("count") };
            double size = g.Get("size");
            c.Root = b.Bone("root", -1, Vec3.Zero, BoneRole.Root);
            c.Base = b.Bone("base", c.Root, Vec3.Zero, BoneRole.Pelvis);
            b.Rig.Root = c.Root;
            b.Rig.Pelvis = c.Base;
            b.Rig.Upright = true;
            b.Rig.Stationary = true;
            b.Rig.Locomotion = LocomotionKind.Legged;
            b.Rig.LocomotionModule = RootedLocomotion.ModuleId;
            b.Rig.Action = ActionStyle.Shake;
            b.Rig.Rest = RestStyle.Dormant;
            b.Rig.Tuning["wind.trunk"] = 0;
            b.Rig.Tuning["wind.branch"] = 0.2;
            double top = form switch
            {
                "barrel" => Barrel(c, g, size, Vec3.Zero, 1.0),
                "chest" => Chest(c, g, size),
                "rock" => Rock(c, g, size),
                "crystal" => Crystal(c, g, size),
                "lantern" => Lantern(c, g, size),
                "brazier" => Brazier(c, g, size),
                "campfire" => Campfire(c, g, size),
                "gravestone" => Gravestone(c, g, size),
                "pillar" => Pillar(c, g, size),
                "signpost" => Signpost(c, g, size),
                "pot" => Pot(c, g, size),
                "banner" => Banner(c, g, size),
                _ => Crates(c, g, size),
            };
            if (b.Rig.Chest < 0)
            {
                c.Top = b.Bone("top", c.Base, new Vec3(0, Math.Max(1, top), 0), BoneRole.Chest);
                b.Rig.Chest = c.Top;
            }
            b.Rig.Spine = new ChainRig { Name = "body", Bones = new[] { c.Base, b.Rig.Chest }, Lengths = new[] { Math.Max(1, top) }, TotalLength = Math.Max(1, top) };
            b.Rig.Tuning["rest.bodyHeight"] = 1e6;
            b.Metrics.LegLength = Math.Max(3, top * 0.3);
            b.Metrics.BackHeight = top;
            b.Metrics.BodyLength = Math.Max(8, top);
            b.Metrics.Mass = DMath.Pow(Math.Max(6, top) / 26.0, 3);
            b.Metrics.WalkSpeed = 1;
            b.Metrics.RunSpeed = 2;
            b.Metrics.PatternScale = DMath.Clamp(Math.Sqrt(Math.Max(6, top) / 26.0), 0.7, 1.3);
            b.AnimationMargin = 4;
            b.ExtraHeadroom = 3;
            return b.Build(g.AnatomyKey);
        }

        private static SolidStyle Style(MaterialSlot slot, string tag, bool pattern = true, bool faceted = false, bool vertical = false) =>
            new SolidStyle { Material = slot, Tag = tag, Pattern = pattern, Faceted = faceted, VerticalGrain = vertical };

        // ---- crates

        private double Crates(Ctx c, CreatureGenome g, double size)
        {
            var b = c.B;
            double s = DMath.Lerp(6, 14, size);
            int count = 1 + (int)Math.Round(c.Count * 2);
            double topY = 0;
            var stacks = new List<(Vec3 at, double s)>();
            stacks.Add((Vec3.Zero, s));
            if (count >= 2) stacks.Add((new Vec3(c.Rng.Range(-0.3, 0.3) * s, s * 2, c.Rng.Range(-0.2, 0.2) * s), s * c.Rng.Range(0.75, 0.9)));
            if (count >= 3) stacks.Add((new Vec3(-s * 0.4, 0, s * 2.15), s * c.Rng.Range(0.7, 0.85)));
            int i = 0;
            foreach (var (at, hs) in stacks)
            {
                int g0 = b.Group($"crate{i}", 0, 0, GroupFlags.None, 0.01 * i);
                double h = hs * DMath.Lerp(0.85, 1.15, g.Get("height"));
                double w = hs * DMath.Lerp(0.85, 1.2, g.Get("width"));
                Vec3 ctr = at + new Vec3(0, h, 0);
                var q = Quat.RotY(c.Rng.Range(-0.25, 0.25));
                Solids.Box(b, g0, c.Base, ctr, new Vec3(w, h, hs), Style(MaterialSlot.Primary, "crate"), q);
                // frame boards along the edges, and a diagonal brace on the faces
                var frame = Style(MaterialSlot.Secondary, "frame");
                double t = Math.Max(0.6, hs * 0.14);
                var m = Mat3.FromQuat(q);
                for (int sy = -1; sy <= 1; sy += 2)
                    for (int sz = -1; sz <= 1; sz += 2)
                        Solids.Box(b, g0, c.Base, ctr + m.Mul(new Vec3(0, sy * (h - t * 0.5), sz * (hs + 0.15))), new Vec3(w + 0.2, t * 0.5, t * 0.35), frame, q);
                for (int sx = -1; sx <= 1; sx += 2)
                    for (int sz = -1; sz <= 1; sz += 2)
                        Solids.Box(b, g0, c.Base, ctr + m.Mul(new Vec3(sx * (w - t * 0.5), 0, sz * (hs + 0.15))), new Vec3(t * 0.5, h, t * 0.35), frame, q);
                if (c.Ornament > 0.45)
                {
                    // metal corner brackets
                    var metal = Style(MaterialSlot.Metal, "bracket", false);
                    for (int sx = -1; sx <= 1; sx += 2)
                        for (int sy = -1; sy <= 1; sy += 2)
                            for (int sz = -1; sz <= 1; sz += 2)
                                Solids.Box(b, g0, c.Base, ctr + m.Mul(new Vec3(sx * (w - t * 0.4), sy * (h - t * 0.4), sz * (hs - t * 0.4))), new Vec3(t * 0.55, t * 0.55, t * 0.55), metal, q);
                }
                topY = Math.Max(topY, ctr.Y + h);
                i++;
            }
            return topY;
        }

        // ---- barrels

        private double Barrel(Ctx c, CreatureGenome g, double size, Vec3 at, double scale)
        {
            var b = c.B;
            double r = DMath.Lerp(4.5, 9, size) * DMath.Lerp(0.85, 1.2, g.Get("width")) * scale;
            double h = r * DMath.Lerp(2.2, 3.0, g.Get("height"));
            int gr = b.Group("barrel", 0, 0, GroupFlags.None, 0);
            var wood = Style(MaterialSlot.Primary, "staves", true, false, true);
            int sides = 12;
            double bulge = 1.14;
            // three frustums give the bulged belly of the staves
            Solids.Prism(b, gr, c.Base, at, at + new Vec3(0, h * 0.3, 0), sides, r, r * bulge, wood, 0, Vec3.UnitX, true, false);
            Solids.Prism(b, gr, c.Base, at + new Vec3(0, h * 0.3, 0), at + new Vec3(0, h * 0.7, 0), sides, r * bulge, r * bulge, wood, 0, Vec3.UnitX, false, false);
            Solids.Prism(b, gr, c.Base, at + new Vec3(0, h * 0.7, 0), at + new Vec3(0, h, 0), sides, r * bulge, r, wood, 0, Vec3.UnitX, false, true);
            // the lid sits slightly inside the rim
            Solids.Prism(b, gr, c.Base, at + new Vec3(0, h - 0.5, 0), at + new Vec3(0, h + 0.15, 0), sides, r * 0.86, r * 0.86, Style(MaterialSlot.Secondary, "lid", false), 0, Vec3.UnitX);
            // metal hoops
            var hoop = Style(MaterialSlot.Metal, "hoop", false);
            foreach (double t in new[] { 0.1, 0.3, 0.7, 0.9 })
            {
                double rr = t < 0.3 || t > 0.7 ? DMath.Lerp(r, r * bulge, t < 0.5 ? t / 0.3 : (1 - t) / 0.3) : r * bulge;
                double hb = Math.Max(0.7, h * 0.04);
                Solids.Prism(b, gr, c.Base, at + new Vec3(0, h * t - hb, 0), at + new Vec3(0, h * t + hb, 0), sides, rr + 0.35, rr + 0.35, hoop, 0, Vec3.UnitX);
            }
            if (c.Ornament > 0.6)
            {
                // a bung on the belly
                var bung = b.Ellipsoid(gr, c.Base, at + new Vec3(r * bulge, h * 0.5, 0), new Vec3(0.7, 0.9, 0.9), MaterialSlot.Marking);
                bung.Flags |= PrimFlags.NoPattern | PrimFlags.Hard;
            }
            return at.Y + h + 0.2;
        }

        // ---- treasure chest (the lid is the jaw: it opens on actions, hits and when shaken)

        private double Chest(Ctx c, CreatureGenome g, double size)
        {
            var b = c.B;
            double w = DMath.Lerp(7, 14, size) * DMath.Lerp(0.85, 1.2, g.Get("width"));
            double d = w * 0.62, h = w * DMath.Lerp(0.42, 0.6, g.Get("height"));
            int body = b.Group("chest", 0, 0, GroupFlags.None, 0);
            var wood = Style(MaterialSlot.Primary, "chest");
            Solids.Box(b, body, c.Base, new Vec3(0, h * 0.5, 0), new Vec3(d * 0.5, h * 0.5, w * 0.5), wood);
            var metal = Style(MaterialSlot.Metal, "band", false);
            double t = Math.Max(0.6, w * 0.05);
            // vertical bands and the lock plate
            int bands = c.Ornament > 0.3 ? 2 : 0;
            for (int k = 0; k < bands; k++)
            {
                double z = (k == 0 ? -1 : 1) * w * 0.3;
                Solids.Box(b, body, c.Base, new Vec3(0, h * 0.5, z), new Vec3(d * 0.5 + 0.25, h * 0.5 + 0.1, t * 0.5), metal);
            }
            // the gold inside is revealed when the lid opens
            int loot = b.Group("loot", 0.8, 0, GroupFlags.None, -0.2);
            for (int k = 0; k < 5; k++)
            {
                var coin = b.Ellipsoid(loot, c.Base, new Vec3(c.Rng.Range(-0.3, 0.3) * d, h * 0.86, c.Rng.Range(-0.38, 0.38) * w), new Vec3(d * 0.22, d * 0.12, d * 0.22), MaterialSlot.Accent);
                coin.Flags |= PrimFlags.NoPattern | PrimFlags.NoShadow;
                coin.Tag = "gold";
            }
            // lid on a hinge at the back top edge; bone +X towards the front, +Y down (the jaw opens upwards)
            Vec3 hinge = new Vec3(-d * 0.5, h, 0);
            int lid = b.BoneAlong("lid", c.Base, hinge, Vec3.UnitX, Vec3.UnitY * -1, BoneRole.Jaw, 0, d);
            var lx = b.RestWorld(lid);
            int lidG = b.Group("lid", 0, 0, GroupFlags.None, 0.15);
            double lidH = h * 0.18;
            Solids.Box(b, lidG, lid, lx.InversePoint(new Vec3(0, h + lidH * 0.5, 0)), new Vec3(d * 0.5 + 0.2, lidH * 0.5, w * 0.5 + 0.2), wood);
            Solids.Vault(b, lidG, lid, lx.InversePoint(new Vec3(0, h + lidH, 0)), lx.InverseDir(Vec3.UnitZ), lx.InverseDir(Vec3.UnitY), w * 0.5 + 0.2, d * 0.5 + 0.2, 6, wood, 0.75);
            for (int k = 0; k < bands; k++)
            {
                double z = (k == 0 ? -1 : 1) * w * 0.3;
                Solids.Vault(b, lidG, lid, lx.InversePoint(new Vec3(0, h + lidH, z)), lx.InverseDir(Vec3.UnitZ), lx.InverseDir(Vec3.UnitY), t * 0.5, d * 0.5 + 0.45, 6, metal, 0.78);
            }
            // lock
            Solids.Box(b, lidG, lid, lx.InversePoint(new Vec3(d * 0.5 + 0.3, h, 0)), new Vec3(0.4, Math.Max(1, h * 0.15), Math.Max(1, w * 0.08)), Style(MaterialSlot.Metal, "lock", false));
            b.Rig.Jaw = lid;
            b.Rig.JawOpenMax = 1.35;
            b.Rig.Head = -1;
            return h + lidH + d * 0.4;
        }

        // ---- rocks and boulders

        private double Rock(Ctx c, CreatureGenome g, double size)
        {
            var b = c.B;
            double R = DMath.Lerp(5, 16, size);
            int n = 1 + (int)Math.Round(c.Count * 3);
            int gr = b.Group("rock", Math.Max(0.6, R * 0.12), 0, GroupFlags.CreaseShade, 0);
            double topY = 0;
            for (int i = 0; i < n; i++)
            {
                double r = i == 0 ? R : R * c.Rng.Range(0.35, 0.6);
                double a = c.Rng.Range(0.0, DMath.TwoPi);
                Vec3 at = i == 0 ? Vec3.Zero : new Vec3(DMath.Cos(a), 0, DMath.Sin(a)) * (R * c.Rng.Range(0.7, 1.1));
                double hy = r * DMath.Lerp(0.55, 0.95, g.Get("height")) * c.Rng.Range(0.85, 1.1);
                var q = Quat.Euler(c.Rng.Range(0.0, DMath.TwoPi), c.Rng.Range(-0.2, 0.2), c.Rng.Range(-0.2, 0.2));
                var e = b.Ellipsoid(gr, c.Base, at + new Vec3(0, hy * 0.6, 0), new Vec3(r * DMath.Lerp(0.85, 1.25, g.Get("width")), hy, r * 0.9), MaterialSlot.Primary, PatternDomain.Body, 0, 1, q);
                e.Flags |= PrimFlags.Faceted | PrimFlags.ShadowCaster;
                e.FacetSeed = (uint)(c.Rng.NextUInt() | 1);
                e.DomainLength = R * 3;
                e.Tag = "boulder";
                topY = Math.Max(topY, hy * 1.6);
                if (i == 0 && c.Wear > 0.5)
                {
                    // a split: a dark crack wedge across the main boulder
                    var crack = b.Ellipsoid(gr, c.Base, at + new Vec3(r * 0.6, hy * 0.9, 0), new Vec3(0.5, hy * 0.6, r * 0.12), MaterialSlot.Marking);
                    crack.Flags |= PrimFlags.NoPattern | PrimFlags.Hard;
                    crack.ShadeBias = -2;
                }
            }
            return topY;
        }

        // ---- crystal clusters

        private double Crystal(Ctx c, CreatureGenome g, double size)
        {
            var b = c.B;
            double H = DMath.Lerp(8, 26, size) * DMath.Lerp(0.8, 1.25, g.Get("height"));
            int n = 3 + (int)Math.Round(c.Count * 5);
            // base rock
            int rg = b.Group("rockbase", 1.0, 0, GroupFlags.CreaseShade, 0);
            var rock = b.Ellipsoid(rg, c.Base, new Vec3(0, H * 0.06, 0), new Vec3(H * 0.38, H * 0.14, H * 0.32), MaterialSlot.Accent, PatternDomain.Body);
            rock.Flags |= PrimFlags.Faceted | PrimFlags.NoPattern | PrimFlags.ShadowCaster;
            rock.FacetSeed = 7;
            double topY = 0;
            for (int i = 0; i < n; i++)
            {
                double t = i / (double)n;
                double h = H * (i == 0 ? 1.0 : c.Rng.Range(0.35, 0.8));
                double r = h * DMath.Lerp(0.14, 0.22, g.Get("width"));
                double a = i * 2.39996 + c.Rng.Range(-0.3, 0.3);
                double tilt = i == 0 ? c.Rng.Range(0.0, 0.15) : c.Rng.Range(0.25, 0.75);
                Vec3 dir = new Vec3(DMath.Sin(tilt) * DMath.Cos(a), DMath.Cos(tilt), DMath.Sin(tilt) * DMath.Sin(a));
                Vec3 at = new Vec3(DMath.Cos(a), 0, DMath.Sin(a)) * (i == 0 ? 0 : H * 0.18 * c.Rng.Range(0.5, 1.2));
                int gg = b.Group($"crystal{i}", 0, 0, GroupFlags.None, 0.02 * i);
                var st = new SolidStyle { Material = MaterialSlot.Secondary, Tag = "crystal", Faceted = false, Pattern = false };
                Vec3 shoulder = at + dir * (h * 0.78);
                var ring = Solids.Prism(b, gg, c.Base, at - dir * (r * 0.6), shoulder, 6, r, r * 0.92, st, a, null, false, false);
                Solids.Prism(b, gg, c.Base, shoulder, at + dir * h, 6, r * 0.92, 0, st, a, null, false, false);
                // glowing core
                if (g.Get("color.glow") > 0.2)
                {
                    var core = b.Cone(gg, c.Base, at + dir * (h * 0.2), r * 0.35, c.Base, at + dir * (h * 0.7), r * 0.25, MaterialSlot.Glow);
                    core.Flags |= PrimFlags.Emissive | PrimFlags.NoPattern | PrimFlags.NoShadow;
                    core.MinDensity = Limbs.HdDensity;
                    core.Tag = "core";
                    core.Group = gg;
                }
                topY = Math.Max(topY, (at + dir * h).Y);
                _ = ring; _ = t;
            }
            return topY;
        }

        // ---- lamp post with a swinging lantern

        private double Lantern(Ctx c, CreatureGenome g, double size)
        {
            var b = c.B;
            double H = DMath.Lerp(18, 40, size) * DMath.Lerp(0.8, 1.25, g.Get("height"));
            double pr = Math.Max(0.8, H * 0.035);
            int post = b.Group("post", 0.6, 0, GroupFlags.None, 0);
            int top = b.Bone("top", c.Base, new Vec3(0, H, 0), BoneRole.Chest);
            b.Rig.Chest = top;
            var wood = Style(MaterialSlot.Primary, "post", true, false, true);
            Solids.Prism(b, post, c.Base, Vec3.Zero, new Vec3(0, H, 0), 6, pr, pr * 0.85, wood, 0, Vec3.UnitX);
            Solids.Prism(b, post, c.Base, Vec3.Zero, new Vec3(0, H * 0.08, 0), 6, pr * 1.7, pr * 1.4, wood, 0, Vec3.UnitX);
            // the arm: a beam and a bracket
            double arm = H * 0.22;
            Solids.Box(b, post, c.Base, new Vec3(arm * 0.45, H * 0.96, 0), new Vec3(arm * 0.55, pr * 0.6, pr * 0.6), wood);
            var brace = b.Cone(post, c.Base, new Vec3(0, H * 0.8, 0), pr * 0.4, c.Base, new Vec3(arm * 0.6, H * 0.95, 0), pr * 0.35, MaterialSlot.Metal);
            brace.Flags |= PrimFlags.NoPattern;
            // the lantern hangs on a two link chain (simulated: it swings in the wind and when hit)
            Vec3 hook = new Vec3(arm * 0.9, H * 0.94, 0);
            double ls = Math.Max(2.5, H * DMath.Lerp(0.09, 0.14, c.Light));
            int l0 = b.BoneAlong("lantern.0", top, hook, Vec3.UnitY * -1, Vec3.UnitX, BoneRole.Tentacle, 0, ls * 0.5);
            int l1 = b.BoneAlong("lantern.1", l0, hook - new Vec3(0, ls * 0.5, 0), Vec3.UnitY * -1, Vec3.UnitX, BoneRole.Tentacle, 0, ls * 1.5);
            int l2 = b.BoneAt("lantern.2", l1, hook - new Vec3(0, ls * 2.0, 0), BoneRole.Tentacle, 0);
            b.Rig.Appendages.Add(new ChainRig { Name = "lantern", Bones = new[] { l0, l1, l2 }, Lengths = new[] { ls * 0.5, ls * 1.5 }, TotalLength = ls * 2, Radii = new[] { 0.5, ls * 0.5, ls * 0.5 }, Stiffness = 0.25, Droop = 0.9 });
            int lg = b.Group("lantern", 0, 0, GroupFlags.None, 0.2);
            var ring = b.Segment(lg, l0, 0.35, l1, 0.35, MaterialSlot.Metal);
            ring.Flags |= PrimFlags.Thin | PrimFlags.NoPattern;
            var lx = b.RestWorld(l1);
            Vec3 lc = hook - new Vec3(0, ls * 1.3, 0);
            var metal = Style(MaterialSlot.Metal, "lantern", false);
            // roof, floor and four corner posts around a glowing glass core
            Solids.Prism(b, lg, l1, lx.InversePoint(lc + new Vec3(0, ls * 0.45, 0)), lx.InversePoint(lc + new Vec3(0, ls * 0.8, 0)), 4, ls * 0.48, 0, metal, DMath.Pi / 4, lx.InverseDir(Vec3.UnitX));
            Solids.Prism(b, lg, l1, lx.InversePoint(lc - new Vec3(0, ls * 0.5, 0)), lx.InversePoint(lc - new Vec3(0, ls * 0.38, 0)), 4, ls * 0.42, ls * 0.42, metal, DMath.Pi / 4, lx.InverseDir(Vec3.UnitX));
            var glass = b.Ellipsoid(lg, l1, lx.InversePoint(lc), new Vec3(ls * 0.3, ls * 0.38, ls * 0.3), MaterialSlot.Glow);
            glass.Flags |= PrimFlags.Emissive | PrimFlags.NoPattern | PrimFlags.NoShadow;
            glass.Tag = "light";
            for (int k = 0; k < 4; k++)
            {
                double a = DMath.Pi / 4 + k * DMath.HalfPi;
                Vec3 o = new Vec3(DMath.Cos(a), 0, DMath.Sin(a)) * (ls * 0.36);
                var bar = b.Cone(lg, l1, lx.InversePoint(lc + o - new Vec3(0, ls * 0.42, 0)), 0.38, l1, lx.InversePoint(lc + o + new Vec3(0, ls * 0.46, 0)), 0.38, MaterialSlot.Metal);
                bar.Flags |= PrimFlags.NoPattern | PrimFlags.Hard;
            }
            b.Rig.Tuning["wind.branch"] = 0.12;
            b.Rig.Tuning["wind.hz"] = 0.5;
            return H + pr;
        }

        // ---- fire: flames are chains of glowing cones that flicker (fast wind) and flare when shaken

        private void Flames(Ctx c, int bone, Vec3 at, double height, double width, int count)
        {
            var b = c.B;
            int fg = b.Group("flames", Math.Max(0.5, width * 0.12), 0, GroupFlags.NoContour | GroupFlags.NoFarShade, 0.3);
            for (int i = 0; i < count; i++)
            {
                double a = i * 2.39996;
                double rr = i == 0 ? 0 : width * 0.45 * c.Rng.Range(0.4, 1.0);
                Vec3 p0 = at + new Vec3(DMath.Cos(a) * rr, 0, DMath.Sin(a) * rr);
                double h = height * (i == 0 ? 1.0 : c.Rng.Range(0.5, 0.8));
                int segs = 3;
                var bones = new int[segs + 1];
                int par = bone;
                for (int k = 0; k <= segs; k++)
                {
                    Vec3 p = p0 + new Vec3(0, h * k / segs, 0);
                    bones[k] = b.BoneAlong($"flame{i}.{k}", par, p, Vec3.UnitY, Vec3.UnitX, BoneRole.Tentacle, 0, h / segs);
                    par = bones[k];
                }
                double w0 = width * (i == 0 ? 0.38 : 0.24);
                for (int k = 0; k < segs; k++)
                {
                    double t0 = k / (double)segs, t1 = (k + 1) / (double)segs;
                    var outer = b.Segment(fg, bones[k], Math.Max(0.4, w0 * (1 - t0 * 0.85)), bones[k + 1], Math.Max(0.3, w0 * (1 - t1 * 0.85)), MaterialSlot.Glow);
                    outer.Flags |= PrimFlags.Emissive | PrimFlags.NoPattern | PrimFlags.NoShadow;
                    outer.ShadeBias = -1;
                    outer.Tag = "flame";
                    if (k < 2)
                    {
                        var core = b.Segment(fg, bones[k], Math.Max(0.35, w0 * 0.5 * (1 - t0)), bones[k + 1], Math.Max(0.3, w0 * 0.5 * (1 - t1)), MaterialSlot.Glow);
                        core.Flags |= PrimFlags.Emissive | PrimFlags.NoPattern | PrimFlags.NoShadow | PrimFlags.Hard;
                        core.ShadeBias = 1;
                        core.Tag = "flamecore";
                    }
                }
                b.Rig.Appendages.Add(new ChainRig
                {
                    Name = $"flame{i}", Bones = bones, Lengths = new[] { h / segs, h / segs, h / segs }, TotalLength = h,
                    Radii = new[] { w0, w0 * 0.7, w0 * 0.4, 0.3 }, Stiffness = 0.15, Droop = 0, RestPitch = new double[segs], RestYaw = new double[segs],
                });
            }
            b.Rig.Tuning["wind.branch"] = 0.75;
            b.Rig.Tuning["wind.hz"] = 2.2;
        }

        private double Brazier(Ctx c, CreatureGenome g, double size)
        {
            var b = c.B;
            double H = DMath.Lerp(8, 18, size) * DMath.Lerp(0.8, 1.3, g.Get("height"));
            double R = H * DMath.Lerp(0.35, 0.5, g.Get("width"));
            int gr = b.Group("brazier", 0, 0, GroupFlags.None, 0);
            var metal = Style(MaterialSlot.Metal, "bowl", false);
            // tripod legs
            for (int k = 0; k < 3; k++)
            {
                double a = k * DMath.TwoPi / 3 + 0.5;
                var leg = b.Cone(gr, c.Base, new Vec3(DMath.Cos(a) * R * 0.95, 0, DMath.Sin(a) * R * 0.95), Math.Max(0.5, R * 0.08), c.Base, new Vec3(DMath.Cos(a) * R * 0.4, H * 0.7, DMath.Sin(a) * R * 0.4), Math.Max(0.5, R * 0.1), MaterialSlot.Metal);
                leg.Flags |= PrimFlags.NoPattern | PrimFlags.ShadowCaster;
            }
            Solids.Prism(b, gr, c.Base, new Vec3(0, H * 0.62, 0), new Vec3(0, H, 0), 8, R * 0.55, R, metal, 0.2, Vec3.UnitX);
            Solids.Prism(b, gr, c.Base, new Vec3(0, H - 0.6, 0), new Vec3(0, H + 0.4, 0), 8, R * 1.08, R * 1.08, Style(MaterialSlot.Metal, "rim", false), 0.2, Vec3.UnitX);
            // embers
            int eg = b.Group("embers", 0.6, 0, GroupFlags.None, 0.1);
            var coals = b.Ellipsoid(eg, c.Base, new Vec3(0, H + 0.2, 0), new Vec3(R * 0.85, R * 0.2, R * 0.85), MaterialSlot.Glow);
            coals.Flags |= PrimFlags.Emissive | PrimFlags.NoPattern | PrimFlags.NoShadow;
            coals.ShadeBias = -1;
            Flames(c, c.Base, new Vec3(0, H + 0.3, 0), R * DMath.Lerp(1.2, 2.4, c.Light), R * 1.1, 3 + (int)Math.Round(c.Light * 2));
            return H + R * 2.4;
        }

        private double Campfire(Ctx c, CreatureGenome g, double size)
        {
            var b = c.B;
            double R = DMath.Lerp(5, 12, size) * DMath.Lerp(0.85, 1.2, g.Get("width"));
            int sg = b.Group("stones", 0.5, 0, GroupFlags.CreaseShade, 0);
            int stones = 7 + (int)Math.Round(c.Count * 4);
            for (int i = 0; i < stones; i++)
            {
                double a = i * DMath.TwoPi / stones + c.Rng.Range(-0.1, 0.1);
                double r = R * c.Rng.Range(0.16, 0.22);
                var st = b.Ellipsoid(sg, c.Base, new Vec3(DMath.Cos(a) * R, r * 0.55, DMath.Sin(a) * R), new Vec3(r, r * 0.7, r * 0.9), MaterialSlot.Primary, PatternDomain.Body, 0, 1, Quat.RotY(-a));
                st.Flags |= PrimFlags.Faceted | PrimFlags.ShadowCaster;
                st.FacetSeed = (uint)(i * 977 + 13);
                st.DomainLength = R * 4;
            }
            // crossed logs (bark coloured from the cloth genes)
            int lg = b.Group("logs", 0.4, 0, GroupFlags.None, 0.05);
            int logs = 3 + (int)Math.Round(c.Count * 2);
            for (int i = 0; i < logs; i++)
            {
                double a = i * DMath.TwoPi / logs + 0.3;
                Vec3 d = new Vec3(DMath.Cos(a), 0, DMath.Sin(a));
                var log = b.Cone(lg, c.Base, d * (R * 0.75) + new Vec3(0, R * 0.1, 0), R * 0.14, c.Base, d * (R * 0.05) + new Vec3(0, R * 0.55, 0), R * 0.11, MaterialSlot.Cloth, PatternDomain.Body);
                log.DomainLength = R * 2;
                log.Flags |= PrimFlags.ShadowCaster;
                log.Tag = "log";
            }
            var coals = b.Ellipsoid(lg, c.Base, new Vec3(0, R * 0.12, 0), new Vec3(R * 0.5, R * 0.14, R * 0.5), MaterialSlot.Glow);
            coals.Flags |= PrimFlags.Emissive | PrimFlags.NoPattern | PrimFlags.NoShadow;
            coals.ShadeBias = -1;
            Flames(c, c.Base, new Vec3(0, R * 0.25, 0), R * DMath.Lerp(1.0, 2.2, c.Light), R * 0.75, 3 + (int)Math.Round(c.Light * 3));
            return R * 2.6;
        }

        // ---- gravestones

        private double Gravestone(Ctx c, CreatureGenome g, double size)
        {
            var b = c.B;
            double H = DMath.Lerp(8, 18, size) * DMath.Lerp(0.8, 1.3, g.Get("height"));
            double W = H * DMath.Lerp(0.45, 0.7, g.Get("width"));
            double T = Math.Max(1.5, W * 0.22);
            int gr = b.Group("stone", 0, 0, GroupFlags.None, 0);
            var stone = Style(MaterialSlot.Primary, "stone", true, false, true);
            double lean = (c.Wear - 0.3) * 0.25;
            var q = Quat.RotZ(lean) * Quat.RotY(c.Rng.Range(-0.2, 0.2));
            var m = Mat3.FromQuat(q);
            // plinth
            Solids.Box(b, gr, c.Base, new Vec3(0, T * 0.35, 0), new Vec3(T * 1.1, T * 0.35, W * 0.62), Style(MaterialSlot.Secondary, "plinth", true));
            int shape = (int)Math.Floor(c.Count * 2.99);
            if (shape == 1)
            {
                // cross
                Solids.Box(b, gr, c.Base, m.Mul(new Vec3(0, H * 0.55, 0)) + new Vec3(0, T * 0.5, 0), new Vec3(T * 0.5, H * 0.5, W * 0.13), stone, q);
                Solids.Box(b, gr, c.Base, m.Mul(new Vec3(0, H * 0.72, 0)) + new Vec3(0, T * 0.5, 0), new Vec3(T * 0.5, W * 0.13, W * 0.42), stone, q);
            }
            else if (shape == 2)
            {
                // obelisk
                Solids.Prism(b, gr, c.Base, new Vec3(0, T * 0.6, 0), m.Mul(new Vec3(0, H * 0.85, 0)), 4, W * 0.32, W * 0.22, stone, DMath.Pi / 4, Vec3.UnitX);
                Solids.Prism(b, gr, c.Base, m.Mul(new Vec3(0, H * 0.85, 0)), m.Mul(new Vec3(0, H * 1.05, 0)), 4, W * 0.22, 0, stone, DMath.Pi / 4, Vec3.UnitX);
            }
            else
            {
                // rounded headstone: slab plus an arched top facing the viewer's side (+X)
                double slabH = H - W * 0.5;
                Solids.Box(b, gr, c.Base, m.Mul(new Vec3(0, slabH * 0.5, 0)) + new Vec3(0, T * 0.5, 0), new Vec3(T * 0.5, slabH * 0.5, W * 0.5), stone, q);
                Solids.Vault(b, gr, c.Base, m.Mul(new Vec3(0, slabH, 0)) + new Vec3(0, T * 0.5, 0), m.Mul(Vec3.UnitX), m.Mul(Vec3.UnitY), T * 0.5, W * 0.5, 8, stone);
                // engraving: dark grooves on the front face (high density)
                for (int k = 0; k < 3; k++)
                {
                    double y = slabH * DMath.Lerp(0.75, 0.4, k / 2.0);
                    double ww = W * (k == 0 ? 0.32 : 0.24);
                    var groove = b.Ellipsoid(gr, c.Base, m.Mul(new Vec3(T * 0.5, y, 0)) + new Vec3(0, T * 0.5, 0), new Vec3(0.35, 0.4, ww), MaterialSlot.Marking, PatternDomain.None, 0, 1, q);
                    groove.Flags |= PrimFlags.NoPattern | PrimFlags.Hard | PrimFlags.NoShadow;
                    groove.ShadeBias = -1;
                    groove.MinDensity = Limbs.HdDensity;
                    groove.Tag = "engraving";
                }
            }
            if (c.Wear > 0.55)
            {
                // a broken chunk lying at the foot
                var chunk = b.Ellipsoid(gr, c.Base, new Vec3(T * 1.6, T * 0.35, W * 0.3), new Vec3(T * 0.6, T * 0.4, T * 0.5), MaterialSlot.Primary, PatternDomain.Body);
                chunk.Flags |= PrimFlags.Faceted;
            }
            return H + T;
        }

        // ---- pillars and ruins

        private double Pillar(Ctx c, CreatureGenome g, double size)
        {
            var b = c.B;
            double H = DMath.Lerp(18, 48, size) * DMath.Lerp(0.8, 1.25, g.Get("height"));
            double R = H * DMath.Lerp(0.09, 0.14, g.Get("width"));
            int gr = b.Group("pillar", 0, 0, GroupFlags.None, 0);
            var stone = Style(MaterialSlot.Primary, "pillar", true, false, false);
            var trim = Style(MaterialSlot.Secondary, "trim", true);
            Solids.Box(b, gr, c.Base, new Vec3(0, R * 0.35, 0), new Vec3(R * 1.45, R * 0.35, R * 1.45), trim);
            Solids.Prism(b, gr, c.Base, new Vec3(0, R * 0.7, 0), new Vec3(0, R * 1.0, 0), 12, R * 1.25, R * 1.1, trim, 0, Vec3.UnitX);
            bool broken = c.Wear > 0.45;
            double shaftTop = broken ? H * DMath.Lerp(0.85, 0.45, c.Wear) : H - R * 1.0;
            int sides = c.Ornament > 0.5 ? 12 : 8;
            Solids.Prism(b, gr, c.Base, new Vec3(0, R, 0), new Vec3(0, shaftTop, 0), sides, R, R * 0.92, stone, 0, Vec3.UnitX);
            if (!broken)
            {
                Solids.Prism(b, gr, c.Base, new Vec3(0, shaftTop, 0), new Vec3(0, shaftTop + R * 0.4, 0), 12, R * 0.95, R * 1.25, trim, 0, Vec3.UnitX);
                Solids.Box(b, gr, c.Base, new Vec3(0, shaftTop + R * 0.65, 0), new Vec3(R * 1.4, R * 0.25, R * 1.4), trim);
            }
            else
            {
                // jagged break: a tilted wedge on top and rubble at the foot
                Solids.Prism(b, gr, c.Base, new Vec3(0, shaftTop, 0), new Vec3(R * 0.3, shaftTop + R * 0.9, R * 0.2), sides, R * 0.92, R * 0.15, stone, 0.3, Vec3.UnitX, false, false);
                int rg = b.Group("rubble", 0.4, 0, GroupFlags.CreaseShade, 0);
                for (int i = 0; i < 4; i++)
                {
                    double a = c.Rng.Range(0.0, DMath.TwoPi);
                    double r = R * c.Rng.Range(0.35, 0.7);
                    var rb = b.Ellipsoid(rg, c.Base, new Vec3(DMath.Cos(a), 0, DMath.Sin(a)) * (R * c.Rng.Range(1.8, 2.8)) + new Vec3(0, r * 0.5, 0), new Vec3(r, r * 0.7, r * 0.85), MaterialSlot.Primary, PatternDomain.Body);
                    rb.Flags |= PrimFlags.Faceted | PrimFlags.ShadowCaster;
                    rb.FacetSeed = (uint)(i * 31 + 5);
                    rb.DomainLength = H;
                }
                b.Trait("broken");
            }
            return broken ? shaftTop + R : shaftTop + R * 0.9;
        }

        // ---- signposts

        private double Signpost(Ctx c, CreatureGenome g, double size)
        {
            var b = c.B;
            double H = DMath.Lerp(14, 30, size) * DMath.Lerp(0.8, 1.25, g.Get("height"));
            double pr = Math.Max(0.8, H * 0.045);
            int gr = b.Group("signpost", 0, 0, GroupFlags.None, 0);
            var wood = Style(MaterialSlot.Primary, "post", true, false, true);
            Solids.Box(b, gr, c.Base, new Vec3(0, H * 0.5, 0), new Vec3(pr, H * 0.5, pr), wood, Quat.RotY(0.3));
            Solids.Prism(b, gr, c.Base, new Vec3(0, H, 0), new Vec3(0, H + pr * 1.6, 0), 4, pr * 1.42, 0, wood, DMath.Pi / 4 + 0.3, Vec3.UnitX);
            int signs = 1 + (int)Math.Round(c.Count * 2);
            var board = Style(MaterialSlot.Secondary, "sign", true);
            for (int i = 0; i < signs; i++)
            {
                double y = H * (0.88 - i * 0.2);
                double yaw = c.Rng.Range(-1.2, 1.2) + i * 1.9;
                double len = H * c.Rng.Range(0.35, 0.5);
                double bh = Math.Max(2.0, H * 0.11);
                var q = Quat.RotY(yaw);
                var m = Mat3.FromQuat(q);
                Vec3 ctr = new Vec3(0, y, 0) + m.Mul(new Vec3(len * 0.42, 0, 0));
                Solids.Box(b, gr, c.Base, ctr, new Vec3(len * 0.5, bh * 0.5, 0.45), board, q);
                // arrow tip
                Vec3 tip = new Vec3(0, y, 0) + m.Mul(new Vec3(len * 0.92 + bh * 0.5, 0, 0));
                Solids.Prism(b, gr, c.Base, ctr + m.Mul(new Vec3(len * 0.5, 0, -0.45)), ctr + m.Mul(new Vec3(len * 0.5, 0, 0.45)), 3, bh * 0.62, bh * 0.62, board, 0, m.Mul(Vec3.UnitX));
                _ = tip;
                if (c.Ornament > 0.4)
                {
                    // nails
                    var nail = b.Ellipsoid(gr, c.Base, new Vec3(0, y, 0) + m.Mul(new Vec3(pr * 0.5, 0, 0.6)), new Vec3(0.45, 0.45, 0.45), MaterialSlot.Metal);
                    nail.Flags |= PrimFlags.Hard | PrimFlags.NoPattern;
                    nail.MinDensity = Limbs.HdDensity;
                }
            }
            return H + pr * 1.6;
        }

        // ---- pots and urns (lathe profiles)

        private double Pot(Ctx c, CreatureGenome g, double size)
        {
            var b = c.B;
            double H = DMath.Lerp(6, 16, size) * DMath.Lerp(0.8, 1.3, g.Get("height"));
            double R = H * DMath.Lerp(0.32, 0.5, g.Get("width"));
            int gr = b.Group("pot", Math.Max(0.6, R * 0.25), 0, GroupFlags.None, 0);
            PrimitiveDef E(Vec3 at, Vec3 r, double u0, double u1)
            {
                var e = b.Ellipsoid(gr, c.Base, at, r, MaterialSlot.Primary, PatternDomain.Body, u0, u1);
                e.DomainLength = H * 1.5;
                e.Flags |= PrimFlags.ShadowCaster;
                return e;
            }
            int shape = (int)Math.Floor(c.Count * 2.99);
            if (shape == 0)
            {
                // round belly jar
                E(new Vec3(0, H * 0.42, 0), new Vec3(R, H * 0.42, R), 0, 0.7);
                var neck = b.Cone(gr, c.Base, new Vec3(0, H * 0.75, 0), R * 0.5, c.Base, new Vec3(0, H, 0), R * 0.55, MaterialSlot.Primary, PatternDomain.Body, 0.7, 1);
                neck.DomainLength = H * 1.5;
            }
            else if (shape == 1)
            {
                // amphora: tall egg with a narrow neck and handles
                E(new Vec3(0, H * 0.45, 0), new Vec3(R * 0.85, H * 0.45, R * 0.85), 0, 0.75);
                var neck = b.Cone(gr, c.Base, new Vec3(0, H * 0.82, 0), R * 0.32, c.Base, new Vec3(0, H * 1.08, 0), R * 0.4, MaterialSlot.Primary, PatternDomain.Body, 0.75, 1);
                neck.DomainLength = H * 1.5;
                for (int s = -1; s <= 1; s += 2)
                {
                    var h1 = b.Cone(gr, c.Base, new Vec3(0, H * 1.0, s * R * 0.35), 0.55, c.Base, new Vec3(0, H * 0.95, s * R * 0.75), 0.55, MaterialSlot.Primary);
                    var h2 = b.Cone(gr, c.Base, new Vec3(0, H * 0.95, s * R * 0.75), 0.55, c.Base, new Vec3(0, H * 0.72, s * R * 0.72), 0.55, MaterialSlot.Primary);
                    h1.Flags |= PrimFlags.Hard; h2.Flags |= PrimFlags.Hard;
                }
            }
            else
            {
                // wide bowl / planter
                E(new Vec3(0, H * 0.35, 0), new Vec3(R * 1.15, H * 0.35, R * 1.15), 0, 0.8);
                var rim = b.Cone(gr, c.Base, new Vec3(0, H * 0.55, 0), R * 1.05, c.Base, new Vec3(0, H * 0.7, 0), R * 1.12, MaterialSlot.Primary, PatternDomain.Body, 0.8, 1);
                rim.DomainLength = H * 1.5;
            }
            // dark opening at the mouth
            double top = shape == 1 ? H * 1.08 : (shape == 2 ? H * 0.7 : H);
            double mr = shape == 1 ? R * 0.3 : (shape == 2 ? R * 1.0 : R * 0.45);
            int mg = b.Group("mouth", 0.3, 0, GroupFlags.NoOutline, 0.4);
            var mouth = b.Ellipsoid(mg, c.Base, new Vec3(0, top + 0.1, 0), new Vec3(mr, 0.4, mr), MaterialSlot.Marking);
            mouth.Flags |= PrimFlags.NoPattern | PrimFlags.Hard | PrimFlags.NoShadow;
            mouth.ShadeBias = -2;
            return top + 0.5;
        }

        // ---- banners

        private double Banner(Ctx c, CreatureGenome g, double size)
        {
            var b = c.B;
            double H = DMath.Lerp(18, 42, size) * DMath.Lerp(0.85, 1.2, g.Get("height"));
            double pr = Math.Max(0.7, H * 0.025);
            int top = b.Bone("top", c.Base, new Vec3(0, H, 0), BoneRole.Chest);
            b.Rig.Chest = top;
            int pole = b.Group("pole", 0.5, 0, GroupFlags.None, 0);
            var pp = b.Segment(pole, c.Base, pr, top, pr * 0.85, MaterialSlot.Primary, PatternDomain.Body);
            pp.DomainLength = H;
            pp.Flags |= PrimFlags.ShadowCaster;
            var finial = b.Ellipsoid(pole, top, new Vec3(0, pr * 1.6, 0), new Vec3(pr * 1.3, pr * 1.6, pr * 1.3), MaterialSlot.Metal);
            finial.Flags |= PrimFlags.NoPattern | PrimFlags.Hard;
            // crossbar and a cloth hanging from it: a centre chain with the cloth spanned sideways
            double W = H * DMath.Lerp(0.28, 0.42, g.Get("width"));
            double L = H * DMath.Lerp(0.4, 0.6, g.Get("height"));
            Vec3 bar = new Vec3(0, H * 0.94, 0);
            var cross = b.Cone(pole, c.Base, bar + new Vec3(0, 0, -W * 0.6), pr * 0.7, c.Base, bar + new Vec3(0, 0, W * 0.6), pr * 0.7, MaterialSlot.Primary, PatternDomain.Body);
            cross.DomainLength = H;
            int segs = 4;
            var bones = new int[segs + 1];
            int par = top;
            for (int i = 0; i <= segs; i++)
            {
                bones[i] = b.BoneAlong($"cloth.{i}", par, bar - new Vec3(0, L * i / segs, 0), Vec3.UnitY * -1, Vec3.UnitX, BoneRole.Tentacle, 0, L / segs);
                par = bones[i];
            }
            int cg = b.Group("cloth", 0, 0, GroupFlags.NoFarShade, 0.2);
            for (int i = 0; i < segs; i++)
            {
                // the bone frame has +X down along the cloth, the world Z axis (cloth width) maps to +-Z or -+Z
                var x0 = b.RestWorld(bones[i]); var x1 = b.RestWorld(bones[i + 1]);
                Vec3 l0 = x0.InverseDir(Vec3.UnitZ) * (W * 0.5), l1 = x1.InverseDir(Vec3.UnitZ) * (W * 0.5);
                bool last = i == segs - 1;
                // swallow tail: the last row ends in two points
                Vec3 tipL = last ? l1 * 1.0 + new Vec3(L / segs * 0.6, 0, 0) : l1, tipR = last ? -l1 + new Vec3(L / segs * 0.6, 0, 0) : -l1;
                Vec3 mid = last ? new Vec3(-L / segs * 0.2, 0, 0) : Vec3.Zero;
                var t1 = b.Triangle(cg, bones[i], l0, bones[i], -l0, bones[i + 1], last ? mid : Vec3.Zero, MaterialSlot.Cloth, PatternDomain.None);
                var t2 = b.Triangle(cg, bones[i], l0, bones[i + 1], last ? mid : Vec3.Zero, bones[i + 1], tipL, MaterialSlot.Cloth, PatternDomain.None);
                var t3 = b.Triangle(cg, bones[i], -l0, bones[i + 1], tipR, bones[i + 1], last ? mid : Vec3.Zero, MaterialSlot.Cloth, PatternDomain.None);
                foreach (var t in new[] { t1, t2, t3 }) { t.Flags |= PrimFlags.TwoSided | PrimFlags.NoPattern | PrimFlags.NoShadow; t.Tag = "cloth"; }
            }
            if (c.Ornament > 0.3)
            {
                // emblem: a metal diamond in the middle of the cloth
                int mid = bones[segs / 2];
                var xm = b.RestWorld(mid);
                Vec3 nz = xm.InverseDir(Vec3.UnitZ) * (W * 0.18), nx = new Vec3(L / segs * 0.45, 0, 0);
                Vec3 off = xm.InverseDir(Vec3.UnitX) * 0.35;
                var e1 = b.Triangle(cg, mid, off - nx, mid, off + nz, mid, off + nx, MaterialSlot.Metal, PatternDomain.None);
                var e2 = b.Triangle(cg, mid, off - nx, mid, off + nx, mid, off - nz, MaterialSlot.Metal, PatternDomain.None);
                e1.Flags |= PrimFlags.TwoSided | PrimFlags.NoPattern | PrimFlags.NoShadow; e2.Flags |= PrimFlags.TwoSided | PrimFlags.NoPattern | PrimFlags.NoShadow;
            }
            b.Rig.Appendages.Add(new ChainRig
            {
                Name = "cloth", Bones = bones, Lengths = new double[] { L / segs, L / segs, L / segs, L / segs }, TotalLength = L,
                Radii = new[] { 0.5, 0.5, 0.5, 0.5, 0.5 }, Stiffness = 0.3, Droop = 0.85, RestPitch = new double[segs], RestYaw = new double[segs],
            });
            b.Rig.Tuning["wind.branch"] = 0.45;
            b.Rig.Tuning["wind.hz"] = 0.8;
            b.Rig.Tuning["wind.trunk"] = 0.006;
            return H + pr * 3;
        }
    }
}
