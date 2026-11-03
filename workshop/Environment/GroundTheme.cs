// Procedural Pixel Creature Workshop - colour themes of the procedural landscape per biome.
//
// EnvironmentArt draws ground tiles, paths, shores, water, rocks, trees and bushes from the current theme.
// Set EnvironmentArt.Theme (main thread) before generating a map; tile caches are keyed by theme id.

using System;
using Godot;
using PixelCreatures.Core;

namespace PixelCreatures.Workshop
{
    public sealed class GroundTheme
    {
        public string Id = Biomes.TemperateId;
        public Color Grass0, Grass1, Grass2, Grass3;
        public Color Dirt0, Dirt1, Dirt2;
        public Color Sand0, Sand1, Sand2;
        public Color Water0, Water1, Water2, Foam;
        public Color Stone0, Stone1, Stone2, Stone3;
        public Color Leaf0, Leaf1, Leaf2, Leaf3, LeafOutline;
        public Color Bark0, Bark1, Bark2;
        /// <summary>Cliff faces of terraces: seam line, dark, light, top edge.</summary>
        public Color CliffLine, Cliff0, Cliff1, CliffTop;
        public Color[] Flowers = Array.Empty<Color>();
        /// <summary>0..1: how often grass tufts and flowers grow on the ground.</summary>
        public double Vegetation = 1;

        private static Color C(int r, int g, int b) => Color.Color8((byte)r, (byte)g, (byte)b);

        public static readonly GroundTheme Temperate = new GroundTheme
        {
            Id = Biomes.TemperateId,
            Grass0 = C(58, 100, 58), Grass1 = C(80, 132, 66), Grass2 = C(101, 154, 74), Grass3 = C(132, 180, 90),
            Dirt0 = C(104, 78, 60), Dirt1 = C(132, 102, 74), Dirt2 = C(158, 126, 90),
            Sand0 = C(178, 154, 106), Sand1 = C(206, 184, 130), Sand2 = C(226, 208, 156),
            Water0 = C(40, 76, 122), Water1 = C(52, 100, 150), Water2 = C(78, 138, 180), Foam = C(196, 228, 236),
            Stone0 = C(78, 80, 92), Stone1 = C(108, 110, 120), Stone2 = C(142, 142, 148), Stone3 = C(178, 176, 176),
            Leaf0 = C(36, 70, 48), Leaf1 = C(52, 98, 56), Leaf2 = C(74, 128, 62), Leaf3 = C(112, 162, 76), LeafOutline = C(26, 48, 40),
            Bark0 = C(62, 44, 38), Bark1 = C(96, 68, 50), Bark2 = C(124, 92, 64),
            CliffLine = C(67, 71, 59), Cliff0 = C(86, 84, 67), Cliff1 = C(116, 102, 76), CliffTop = C(146, 139, 92),
            Flowers = new[] { C(238, 206, 88), C(238, 234, 222), C(222, 128, 158), C(126, 152, 224) },
        };

        public static readonly GroundTheme Desert = new GroundTheme
        {
            Id = "desert",
            Grass0 = C(184, 146, 92), Grass1 = C(204, 168, 112), Grass2 = C(220, 188, 130), Grass3 = C(236, 210, 154),
            Dirt0 = C(150, 108, 70), Dirt1 = C(174, 132, 88), Dirt2 = C(194, 154, 104),
            Sand0 = C(206, 186, 138), Sand1 = C(226, 208, 160), Sand2 = C(240, 226, 186),
            Water0 = C(30, 98, 116), Water1 = C(44, 128, 142), Water2 = C(80, 168, 172), Foam = C(212, 236, 228),
            Stone0 = C(130, 90, 64), Stone1 = C(164, 120, 84), Stone2 = C(196, 154, 108), Stone3 = C(222, 188, 140),
            Leaf0 = C(70, 84, 46), Leaf1 = C(94, 108, 56), Leaf2 = C(122, 134, 70), Leaf3 = C(156, 162, 90), LeafOutline = C(50, 58, 36),
            Bark0 = C(92, 66, 46), Bark1 = C(128, 96, 66), Bark2 = C(160, 126, 88),
            CliffLine = C(112, 78, 52), Cliff0 = C(148, 106, 70), Cliff1 = C(180, 136, 90), CliffTop = C(214, 180, 130),
            Flowers = new[] { C(236, 96, 120), C(244, 200, 92) },
            Vegetation = 0.25,
        };

        public static readonly GroundTheme Snow = new GroundTheme
        {
            Id = "snow",
            Grass0 = C(196, 210, 230), Grass1 = C(216, 226, 242), Grass2 = C(232, 238, 250), Grass3 = C(248, 250, 255),
            Dirt0 = C(140, 150, 172), Dirt1 = C(164, 174, 194), Dirt2 = C(186, 196, 214),
            Sand0 = C(132, 138, 150), Sand1 = C(158, 164, 174), Sand2 = C(184, 190, 198),
            Water0 = C(52, 90, 134), Water1 = C(72, 120, 164), Water2 = C(118, 168, 206), Foam = C(232, 244, 252),
            Stone0 = C(86, 94, 116), Stone1 = C(118, 126, 146), Stone2 = C(154, 162, 180), Stone3 = C(200, 208, 222),
            Leaf0 = C(26, 58, 50), Leaf1 = C(38, 80, 62), Leaf2 = C(206, 220, 238), Leaf3 = C(246, 250, 255), LeafOutline = C(20, 40, 40),
            Bark0 = C(56, 42, 40), Bark1 = C(86, 64, 54), Bark2 = C(112, 88, 72),
            CliffLine = C(86, 94, 116), Cliff0 = C(108, 116, 138), Cliff1 = C(138, 146, 166), CliffTop = C(232, 240, 250),
            Flowers = new[] { C(255, 255, 255), C(206, 224, 252) },
            Vegetation = 0.35,
        };

        public static readonly GroundTheme Jungle = new GroundTheme
        {
            Id = "jungle",
            Grass0 = C(34, 74, 40), Grass1 = C(46, 98, 48), Grass2 = C(62, 122, 56), Grass3 = C(90, 152, 66),
            Dirt0 = C(80, 56, 40), Dirt1 = C(102, 74, 52), Dirt2 = C(126, 96, 66),
            Sand0 = C(150, 128, 86), Sand1 = C(176, 154, 108), Sand2 = C(198, 178, 128),
            Water0 = C(28, 84, 82), Water1 = C(40, 110, 102), Water2 = C(70, 150, 132), Foam = C(180, 226, 210),
            Stone0 = C(62, 82, 68), Stone1 = C(88, 108, 90), Stone2 = C(118, 136, 114), Stone3 = C(150, 166, 140),
            Leaf0 = C(20, 62, 36), Leaf1 = C(30, 92, 44), Leaf2 = C(48, 126, 52), Leaf3 = C(86, 166, 64), LeafOutline = C(14, 40, 28),
            Bark0 = C(58, 40, 30), Bark1 = C(86, 60, 42), Bark2 = C(112, 82, 56),
            CliffLine = C(38, 56, 36), Cliff0 = C(56, 76, 46), Cliff1 = C(80, 98, 58), CliffTop = C(108, 140, 70),
            Flowers = new[] { C(238, 80, 100), C(250, 180, 60), C(200, 90, 220), C(255, 230, 120) },
            Vegetation = 1.6,
        };

        public static readonly GroundTheme Swamp = new GroundTheme
        {
            Id = "swamp",
            Grass0 = C(58, 70, 42), Grass1 = C(74, 88, 52), Grass2 = C(92, 106, 62), Grass3 = C(114, 126, 74),
            Dirt0 = C(62, 48, 36), Dirt1 = C(80, 64, 46), Dirt2 = C(98, 80, 58),
            Sand0 = C(108, 102, 72), Sand1 = C(128, 122, 88), Sand2 = C(148, 142, 104),
            Water0 = C(42, 64, 50), Water1 = C(54, 82, 62), Water2 = C(80, 110, 82), Foam = C(150, 176, 140),
            Stone0 = C(68, 72, 68), Stone1 = C(90, 94, 88), Stone2 = C(116, 118, 110), Stone3 = C(142, 144, 132),
            Leaf0 = C(36, 52, 34), Leaf1 = C(50, 70, 42), Leaf2 = C(70, 92, 52), Leaf3 = C(98, 118, 64), LeafOutline = C(26, 36, 28),
            Bark0 = C(48, 40, 36), Bark1 = C(70, 60, 50), Bark2 = C(92, 80, 64),
            CliffLine = C(50, 54, 40), Cliff0 = C(66, 70, 50), Cliff1 = C(88, 90, 64), CliffTop = C(110, 116, 76),
            Flowers = new[] { C(210, 220, 120), C(170, 140, 200) },
            Vegetation = 1.2,
        };

        public static readonly GroundTheme Volcanic = new GroundTheme
        {
            Id = "volcanic",
            Grass0 = C(52, 46, 48), Grass1 = C(66, 58, 60), Grass2 = C(82, 72, 72), Grass3 = C(104, 92, 90),
            Dirt0 = C(38, 32, 34), Dirt1 = C(52, 44, 46), Dirt2 = C(68, 58, 58),
            Sand0 = C(96, 72, 62), Sand1 = C(120, 92, 76), Sand2 = C(142, 112, 92),
            Water0 = C(44, 80, 92), Water1 = C(58, 106, 118), Water2 = C(96, 150, 160), Foam = C(220, 232, 232),
            Stone0 = C(40, 36, 42), Stone1 = C(64, 58, 64), Stone2 = C(92, 84, 88), Stone3 = C(124, 114, 114),
            Leaf0 = C(44, 40, 38), Leaf1 = C(62, 56, 52), Leaf2 = C(84, 76, 70), Leaf3 = C(110, 100, 92), LeafOutline = C(28, 24, 24),
            Bark0 = C(30, 24, 24), Bark1 = C(48, 38, 36), Bark2 = C(66, 54, 50),
            CliffLine = C(28, 24, 26), Cliff0 = C(44, 38, 40), Cliff1 = C(64, 56, 56), CliffTop = C(98, 88, 86),
            Flowers = new[] { C(255, 120, 40), C(255, 190, 80) },
            Vegetation = 0.3,
        };

        /// <summary>The theme of a biome; biomes of extensions get a theme derived from their ground colours.</summary>
        public static GroundTheme For(Biome biome)
        {
            switch (biome.Id)
            {
                case "desert": return Desert;
                case "snow": return Snow;
                case "jungle": return Jungle;
                case "swamp": return Swamp;
                case "volcanic": return Volcanic;
                case Biomes.TemperateId: return Temperate;
            }
            Color G(PixelCreatures.Core.Palettes.Rgba c, float k) => new Color(c.R / 255f * k, c.G / 255f * k, c.B / 255f * k);
            var t = (GroundTheme)Temperate.MemberwiseClone();
            t.Id = biome.Id;
            t.Grass0 = G(biome.GroundShade, 0.9f); t.Grass1 = G(biome.GroundShade, 1f); t.Grass2 = G(biome.Ground, 1f); t.Grass3 = G(biome.GroundLight, 1f);
            t.Water0 = G(biome.Water, 0.8f); t.Water1 = G(biome.Water, 1f); t.Water2 = G(biome.Water, 1.25f);
            return t;
        }
    }
}
