// Procedural Pixel Creatures - built-in biomes: temperate (neutral), desert, snow, jungle, swamp, volcanic.

#nullable enable

using System.Collections.Generic;
using PixelCreatures.Core.Genetics;
using PixelCreatures.Core.Palettes;

namespace PixelCreatures.Core
{
    public static class Biomes
    {
        public const string TemperateId = "temperate";

        private static readonly BiomeScope Animals = BiomeScope.Animals;
        private static readonly BiomeScope Flora = BiomeScope.Flora;
        private static readonly BiomeScope Props = BiomeScope.Props;

        /// <summary>The neutral default: meadows and forests. Changes nothing.</summary>
        public static readonly Biome Temperate = new Biome(TemperateId, "Temperate",
            "Meadows and forests: every creation as the families define it.")
        {
            Order = 0,
        };

        public static readonly Biome Desert = CreateDesert();
        public static readonly Biome Snow = CreateSnow();
        public static readonly Biome Jungle = CreateJungle();
        public static readonly Biome Swamp = CreateSwamp();
        public static readonly Biome Volcanic = CreateVolcanic();

        public static IReadOnlyList<Biome> All { get; } = new[] { Temperate, Desert, Snow, Jungle, Swamp, Volcanic };

        private static Biome CreateDesert()
        {
            var b = new Biome("desert", "Desert",
                "Dunes and badlands: cacti and palms, sandworms and scorpions, sand-coloured coats with big ears and long legs, dusty feet.")
            {
                Order = 10, AnimalCover = 0.8,
                Ground = new Rgba(214, 182, 122), GroundShade = new Rgba(188, 152, 96), GroundLight = new Rgba(232, 208, 152),
                Water = new Rgba(60, 140, 160),
                Overlay = new BiomeOverlay
                {
                    Cover = OverlayMaterial.Sand, CoverAmount = 0.16, CoverPatchiness = 0.9,
                    Grime = OverlayMaterial.Dust, GrimeAmount = 0.5,
                    Grade = new Rgba(255, 196, 140), GradeAmount = 0.07,
                },
            };
            b.Archetypes("flora", ("cactus", 7), ("palm", 3), ("dead", 2), ("bush", 0.6), ("flowers", 0.4), ("oak", 0.12), ("pine", 0.05),
                ("birch", 0.05), ("willow", 0.04), ("blossom", 0.2), ("mushroom", 0.1));
            b.Archetypes("prop", ("rock", 2), ("pot", 2), ("pillar", 2), ("brazier", 1.3), ("campfire", 1.2), ("banner", 1.2), ("crystal", 0.6), ("lantern", 0.6), ("barrel", 0.7));
            b.Archetypes("serpent", ("sandworm", 5), ("cobra", 2.5), ("viper", 2), ("constrictor", 0.4), ("worm", 0.3));
            b.Archetypes("arthropod", ("scorpion", 4), ("ant", 2), ("beetle", 1.5), ("crab", 0.2), ("mantis", 0.7));
            b.Archetypes("reptile", ("lizard", 3), ("tortoise", 2), ("crocodile", 0.15), ("salamander", 0.2));
            b.Archetypes("quadruped", ("canine", 1.5), ("feline", 1.5), ("equine", 1.3), ("rodent", 1.5), ("ursine", 0.15), ("cervine", 0.6), ("boar", 0.5));
            b.Archetypes("winged", ("raptor", 2), ("bat", 1.2), ("songbird", 0.6), ("owl", 0.6), ("dragonfly", 0.2));
            b.Archetypes("amorphous", ("wisp", 1.5), ("slime", 0.6), ("jelly", 0.1), ("octopus", 0.1));
            b.Archetypes("biped", ("kobold", 2), ("goblin", 1.5), ("golem", 1.5), ("orc", 1.2), ("troll", 0.5), ("yeti", 0.02), ("dwarf", 0.6));
            b.Archetypes("mythical", ("manticore", 3), ("griffin", 1.5), ("unicorn", 0.5), ("kirin", 0.5), ("hydra", 0.6));
            b.Archetypes("monster", ("skeleton", 2), ("minotaur", 1.2), ("mimic", 1.2), ("horror", 0.6));
            b.Archetypes("plantfolk", ("cactus", 6), ("mandrake", 0.8), ("bloom", 0.6), ("sapling", 0.5), ("flytrap", 0.5), ("mushroom", 0.3));
            b.Coat(Animals, "*", 0.5);
            b.Coats(Animals, ("sand", 4), ("tan", 3), ("golden", 2.5), ("ginger", 2), ("cream", 1.5), ("rust", 1.5), ("red", 1.2),
                ("green", 0.3), ("olive", 0.6), ("teal", 0.3), ("blue", 0.3), ("violet", 0.4), ("black", 0.4), ("slate", 0.4), ("grey", 0.6),
                ("silver", 0.6), ("albino", 0.5), ("vivid", 0.5));
            b.AddCoat(Animals, Genetics.Coats.Sand(1), 0.35);
            b.AddCoat(Animals, Genetics.Coats.Tan(1), 0.2);
            b.Coats(Props, ("sand", 4), ("tan", 2), ("grey", 0.6), ("slate", 0.5));
            b.AddCoat(Props, Genetics.Coats.Sand(1), 0.4);
            b.Coats(Flora, ("tan", 2), ("sand", 2), ("grey", 1.5), ("chocolate", 0.6));
            b.Patterns(Animals, ("none", 1.3), ("bands", 1.2), ("stripes", 0.7));
            b.Gene(Animals, "head.earSize", 0.8, 0.35);
            b.Gene(Animals, "mass", 0.32, 0.25);
            b.Gene(Animals, "body.mass", 0.32, 0.25);
            b.Gene(Animals, "legs.length", 0.66, 0.2);
            b.Gene(Animals, "color.value", 0.66, 0.25);
            b.Gene(Animals, "color.saturation", 0.45, 0.15);
            b.Gene(Flora, "color.leafSaturation", 0.34, 0.45);
            b.Gene(Flora, "color.leafValue", 0.46, 0.25);
            b.Gene(Flora, "color.leafHue", 112, 0.4);
            b.Gene(Flora, "canopy.density", 0.35, 0.3);
            b.Gene(Props, "wear", 0.7, 0.35);
            return b;
        }

        private static Biome CreateSnow()
        {
            var b = new Biome("snow", "Snow",
                "Tundra and snowy forests: conifers under snow, yetis and polar coats, bulky bodies with small ears, snow on every back.")
            {
                Order = 20, AnimalCover = 1.0,
                Ground = new Rgba(232, 238, 246), GroundShade = new Rgba(196, 210, 230), GroundLight = new Rgba(248, 250, 255),
                Water = new Rgba(70, 120, 160),
                Overlay = new BiomeOverlay
                {
                    Cover = OverlayMaterial.Snow, CoverAmount = 0.42, CoverPatchiness = 0.5,
                    Grime = OverlayMaterial.Snow, GrimeAmount = 0.14,
                    Grade = new Rgba(196, 218, 255), GradeAmount = 0.09,
                },
            };
            b.Archetypes("flora", ("pine", 5), ("birch", 2.5), ("dead", 1.5), ("bush", 0.8), ("oak", 0.3), ("willow", 0.3), ("blossom", 0.15),
                ("mushroom", 0.4), ("flowers", 0.15), ("palm", 0), ("cactus", 0));
            b.Archetypes("prop", ("crystal", 2), ("rock", 1.5), ("lantern", 1.4), ("campfire", 1.4), ("brazier", 1.2), ("signpost", 1.2), ("pot", 0.5), ("pillar", 0.8));
            b.Archetypes("quadruped", ("ursine", 3), ("canine", 2), ("cervine", 2), ("bovine", 1.5), ("feline", 1), ("boar", 0.6), ("equine", 0.6));
            b.Archetypes("biped", ("yeti", 6), ("dwarf", 2), ("troll", 1.5), ("goblin", 0.6), ("kobold", 0.5), ("imp", 0.3), ("orc", 0.8));
            b.Archetypes("winged", ("owl", 3), ("raptor", 1.5), ("songbird", 0.8), ("bat", 0.3), ("moth", 0.3), ("dragonfly", 0.05));
            b.Archetypes("reptile", ("dragon", 2), ("drake", 1.5), ("lizard", 0.2), ("tortoise", 0.2), ("crocodile", 0.05), ("salamander", 0.2));
            b.Archetypes("serpent", ("worm", 1), ("snake", 0.3), ("viper", 0.3), ("cobra", 0.2), ("constrictor", 0.2), ("sandworm", 0.1));
            b.Archetypes("arthropod", ("crab", 0.8), ("beetle", 0.6), ("ant", 0.3), ("scorpion", 0.1), ("mantis", 0.1));
            b.Archetypes("aquatic", ("shark", 1.5), ("ray", 1.2), ("reef", 0.5), ("puffer", 0.5));
            b.Archetypes("amorphous", ("wisp", 2), ("jelly", 0.5), ("octopus", 0.4), ("ooze", 0.6));
            b.Archetypes("mythical", ("unicorn", 1.5), ("griffin", 1.2), ("manticore", 0.4), ("hydra", 0.6));
            b.Archetypes("monster", ("wraith", 2), ("skeleton", 1.5), ("gargoyle", 1.2));
            b.Archetypes("plantfolk", ("sapling", 2), ("mushroom", 1), ("cactus", 0.1), ("flytrap", 0.2), ("bloom", 0.4));
            b.Coat(Animals, "*", 0.5);
            b.Coats(Animals, ("albino", 4), ("silver", 3), ("cream", 2.5), ("grey", 2.5), ("slate", 1.8), ("blue", 0.8), ("teal", 0.6),
                ("brown", 0.7), ("black", 0.6), ("tan", 0.6), ("red", 0.4), ("ginger", 0.4), ("golden", 0.4), ("sand", 0.3), ("chocolate", 0.5),
                ("green", 0.15), ("olive", 0.2), ("yellow", 0.2), ("vivid", 0.3), ("crimson", 0.3), ("rust", 0.3), ("pink", 0.4));
            b.AddCoat(Animals, Genetics.Coats.Albino(1), 0.25);
            b.AddCoat(Animals, Genetics.Coats.Silver(1), 0.2);
            b.Coats(BiomeScope.Archetype("prop", "crystal"), ("blue", 3), ("teal", 3), ("vivid", 0.3), ("violet", 0.8), ("pink", 0.3));
            b.AddCoat(BiomeScope.Archetype("prop", "crystal"), Genetics.Coats.Silver(1), 0.3);
            b.Patterns(Animals, ("none", 2), ("mottled", 1.2), ("spots", 0.6), ("stripes", 0.6), ("rosettes", 0.7));
            b.Gene(Animals, "mass", 0.74, 0.3);
            b.Gene(Animals, "body.mass", 0.74, 0.3);
            b.Gene(Animals, "head.earSize", 0.25, 0.35);
            b.Gene(Animals, "legs.length", 0.4, 0.2);
            b.Gene(Animals, "tail.length", 0.38, 0.2);
            b.Gene(Animals, "color.saturation", 0.2, 0.3);
            b.Gene(Animals, "color.value", 0.8, 0.3);
            b.Gene(Animals, "head.beard", 0.7, 0.3);
            b.Gene(Flora, "snow", 0.9, 0.85);
            b.Gene(Flora, "color.leafValue", 0.32, 0.4);
            b.Gene(Flora, "color.leafSaturation", 0.4, 0.3);
            b.Gene(Flora, "color.leafHue", 158, 0.35);
            b.Gene(Flora, "fruit", 0, 0.6);
            return b;
        }

        private static Biome CreateJungle()
        {
            var b = new Biome("jungle", "Jungle",
                "Rain forest: dense palms, blossoms and giant mushrooms, vivid patterned coats, overgrown ruins, mossy backs and wet glints.")
            {
                Order = 30, AnimalCover = 0.3,
                Ground = new Rgba(66, 112, 52), GroundShade = new Rgba(46, 84, 40), GroundLight = new Rgba(92, 140, 62),
                Water = new Rgba(48, 116, 108),
                Overlay = new BiomeOverlay
                {
                    Cover = OverlayMaterial.Moss, CoverAmount = 0.22, CoverPatchiness = 0.85,
                    Grime = OverlayMaterial.Mud, GrimeAmount = 0.32,
                    Wetness = 0.6,
                    Grade = new Rgba(150, 226, 150), GradeAmount = 0.05,
                },
            };
            b.Archetypes("flora", ("palm", 3), ("oak", 2), ("willow", 1.5), ("blossom", 1.6), ("mushroom", 2), ("flowers", 2.5), ("bush", 2),
                ("pine", 0.15), ("birch", 0.15), ("dead", 0.3), ("cactus", 0.03));
            b.Archetypes("prop", ("pillar", 2), ("gravestone", 1.5), ("pot", 1.5), ("chest", 1.5), ("crystal", 1), ("lantern", 0.6), ("signpost", 0.7), ("barrel", 0.8));
            b.Archetypes("quadruped", ("feline", 3), ("boar", 1.5), ("rodent", 1.5), ("beast", 1.5), ("cervine", 0.8), ("canine", 0.6), ("ursine", 0.6), ("equine", 0.3));
            b.Archetypes("biped", ("goblin", 2), ("imp", 1), ("troll", 1), ("orc", 1), ("dwarf", 0.3), ("yeti", 0.02));
            b.Archetypes("winged", ("songbird", 2.5), ("dragonfly", 2), ("moth", 2), ("bat", 1.5), ("owl", 0.5));
            b.Archetypes("reptile", ("lizard", 2), ("crocodile", 2), ("salamander", 2), ("tortoise", 1), ("drake", 1), ("dragon", 0.7));
            b.Archetypes("serpent", ("constrictor", 4), ("viper", 2), ("snake", 2), ("cobra", 1), ("worm", 0.6), ("sandworm", 0.05));
            b.Archetypes("arthropod", ("mantis", 3), ("beetle", 2.5), ("spider", 2), ("ant", 2), ("scorpion", 0.5), ("crab", 0.6));
            b.Archetypes("aquatic", ("reef", 1.5), ("eel", 1.5), ("puffer", 1), ("shark", 0.5));
            b.Archetypes("amorphous", ("slime", 1.5), ("ooze", 1), ("jelly", 0.4));
            b.Archetypes("mythical", ("kirin", 2), ("manticore", 1.5), ("hydra", 1.5), ("griffin", 0.8), ("unicorn", 0.8), ("cerberus", 0.6));
            b.Archetypes("monster", ("horror", 1.5), ("cyclops", 1.2), ("mimic", 1.2), ("skeleton", 0.8), ("wraith", 0.6));
            b.Archetypes("plantfolk", ("flytrap", 3), ("bloom", 2.5), ("mandrake", 1.5), ("mushroom", 1.5), ("cactus", 0.05));
            b.Coat(Animals, "*", 0.9);
            b.Coats(Animals, ("green", 3), ("vivid", 3), ("teal", 2), ("yellow", 2), ("golden", 2), ("black", 1.5), ("crimson", 1.5), ("blue", 1.5),
                ("violet", 1.5), ("olive", 1.5), ("red", 1.2), ("ginger", 1.2), ("grey", 0.5), ("albino", 0.2), ("silver", 0.3), ("cream", 0.5), ("sand", 0.4));
            b.AddCoat(Animals, Genetics.Coats.Vivid(1), 0.2);
            b.AddCoat(Animals, Genetics.Coats.Green(1), 0.15);
            b.Patterns(Animals, ("spots", 2), ("rosettes", 3), ("stripes", 2), ("blotches", 1.5), ("mottled", 1.2), ("none", 0.6));
            b.Glow(Animals, 1.5);
            b.Glow(Flora, 2);
            b.Gene(Animals, "color.saturation", 0.75, 0.35);
            b.Gene(Animals, "tail.length", 0.7, 0.2);
            b.Gene(Flora, "color.leafSaturation", 0.75, 0.4);
            b.Gene(Flora, "color.leafValue", 0.46, 0.2);
            b.Gene(Flora, "color.leafHue", 138, 0.5);
            b.Gene(Flora, "canopy.density", 0.82, 0.4);
            b.Gene(Flora, "canopy.size", 0.7, 0.3);
            b.Gene(Flora, "canopy.droop", 0.55, 0.25);
            b.Gene(Flora, "fruit", 0.6, 0.35);
            b.Gene(Flora, "snow", 0, 1);
            b.Gene(Props, "wear", 0.78, 0.45);
            return b;
        }

        private static Biome CreateSwamp()
        {
            var b = new Biome("swamp", "Swamp",
                "Bogs and marshes: willows, dead trees and mushrooms, crocodiles, hydras and trolls, murky olive coats, muddy legs and algae.")
            {
                Order = 40, AnimalCover = 0.4,
                Ground = new Rgba(80, 94, 58), GroundShade = new Rgba(60, 72, 46), GroundLight = new Rgba(102, 116, 70),
                Water = new Rgba(62, 88, 66),
                Overlay = new BiomeOverlay
                {
                    Cover = OverlayMaterial.Algae, CoverAmount = 0.18, CoverPatchiness = 0.9,
                    Grime = OverlayMaterial.Mud, GrimeAmount = 0.62,
                    Wetness = 0.8,
                    Grade = new Rgba(136, 168, 110), GradeAmount = 0.08,
                },
            };
            b.Archetypes("flora", ("willow", 4), ("dead", 3), ("mushroom", 3), ("bush", 1.5), ("flowers", 0.8), ("oak", 0.5), ("pine", 0.4), ("birch", 0.4),
                ("palm", 0.2), ("blossom", 0.3), ("cactus", 0));
            b.Archetypes("prop", ("gravestone", 2), ("lantern", 2), ("barrel", 1.2), ("signpost", 1.2), ("crystal", 0.6), ("pillar", 0.8), ("brazier", 0.6), ("banner", 0.5));
            b.Archetypes("reptile", ("crocodile", 4), ("salamander", 3), ("tortoise", 2), ("drake", 0.6), ("dragon", 0.4));
            b.Archetypes("amorphous", ("slime", 2.5), ("ooze", 2.5), ("wisp", 2), ("octopus", 0.6), ("jelly", 0.3));
            b.Archetypes("serpent", ("snake", 2), ("constrictor", 2), ("worm", 2), ("cobra", 0.4), ("sandworm", 0));
            b.Archetypes("arthropod", ("spider", 2), ("beetle", 1.5), ("crab", 1.5), ("scorpion", 0.1));
            b.Archetypes("winged", ("dragonfly", 3), ("moth", 2), ("bat", 2), ("raptor", 0.4), ("songbird", 0.6));
            b.Archetypes("quadruped", ("boar", 2), ("rodent", 2), ("beast", 1.5), ("equine", 0.3), ("feline", 0.6));
            b.Archetypes("biped", ("troll", 3), ("goblin", 2), ("ogre", 1.5), ("dwarf", 0.3), ("yeti", 0));
            b.Archetypes("aquatic", ("eel", 2.5), ("angler", 1.5), ("reef", 0.4), ("shark", 0.3));
            b.Archetypes("mythical", ("hydra", 4), ("griffin", 0.4), ("unicorn", 0.4), ("centaur", 0.5));
            b.Archetypes("monster", ("horror", 2), ("wraith", 2), ("skeleton", 1.5), ("gargoyle", 0.6));
            b.Archetypes("plantfolk", ("mandrake", 3), ("mushroom", 2.5), ("flytrap", 2), ("bloom", 0.6), ("cactus", 0));
            b.Coat(Animals, "*", 0.6);
            b.Coats(Animals, ("olive", 4), ("green", 2), ("chocolate", 2), ("brown", 2), ("slate", 1.5), ("grey", 1.5), ("teal", 1.2), ("black", 1),
                ("rust", 0.8), ("yellow", 0.6), ("violet", 0.5), ("vivid", 0.5), ("cream", 0.4), ("golden", 0.4), ("sand", 0.3), ("albino", 0.2), ("pink", 0.3));
            b.AddCoat(Animals, Genetics.Coats.Olive(1), 0.3);
            b.Coats(Flora, ("grey", 2), ("chocolate", 1.5), ("olive", 2), ("black", 1));
            b.Patterns(Animals, ("mottled", 3), ("blotches", 2), ("spots", 1), ("stripes", 0.6));
            b.Glow(BiomeScope.All, 1.6);
            b.Gene(Animals, "color.saturation", 0.35, 0.3);
            b.Gene(Animals, "color.value", 0.38, 0.3);
            b.Gene(Flora, "color.leafSaturation", 0.35, 0.4);
            b.Gene(Flora, "color.leafValue", 0.38, 0.3);
            b.Gene(Flora, "color.leafHue", 108, 0.45);
            b.Gene(Flora, "canopy.droop", 0.8, 0.4);
            b.Gene(Flora, "trunk.gnarl", 0.7, 0.35);
            b.Gene(Flora, "snow", 0, 1);
            b.Gene(Props, "wear", 0.85, 0.5);
            return b;
        }

        private static Biome CreateVolcanic()
        {
            var b = new Biome("volcanic", "Volcanic",
                "Ash fields and lava: charred dead trees, glowing crystals and braziers, dragons, imps and hellhounds, dark coats with embers, ash on every surface.")
            {
                Order = 50, AnimalCover = 0.85,
                Ground = new Rgba(70, 60, 62), GroundShade = new Rgba(48, 40, 44), GroundLight = new Rgba(96, 84, 82),
                Water = new Rgba(58, 106, 118),
                Overlay = new BiomeOverlay
                {
                    Cover = OverlayMaterial.Ash, CoverAmount = 0.3, CoverPatchiness = 0.7,
                    Grime = OverlayMaterial.Soot, GrimeAmount = 0.45,
                    Grade = new Rgba(255, 146, 104), GradeAmount = 0.08,
                },
            };
            b.Archetypes("flora", ("dead", 6), ("mushroom", 1.5), ("cactus", 1), ("bush", 0.6), ("pine", 0.3), ("palm", 0.2), ("flowers", 0.2),
                ("oak", 0.1), ("birch", 0.1), ("willow", 0.05), ("blossom", 0.1));
            b.Archetypes("prop", ("crystal", 3), ("rock", 2.5), ("brazier", 2), ("pillar", 1.5), ("gravestone", 1.5), ("campfire", 1.2),
                ("crate", 0.4), ("barrel", 0.5), ("lantern", 0.6), ("signpost", 0.6));
            b.Archetypes("reptile", ("dragon", 4), ("drake", 3), ("salamander", 2.5), ("tortoise", 0.6), ("crocodile", 0.2));
            b.Archetypes("monster", ("gargoyle", 2.5), ("horror", 1.5), ("skeleton", 1.5), ("wraith", 1.2), ("minotaur", 1.2), ("cyclops", 1.2), ("mimic", 0.8));
            b.Archetypes("mythical", ("cerberus", 3), ("manticore", 2), ("hydra", 1.5), ("griffin", 0.6), ("unicorn", 0.4), ("centaur", 0.4));
            b.Archetypes("biped", ("imp", 4), ("golem", 3), ("orc", 1.5), ("kobold", 1.5), ("dwarf", 1.2), ("troll", 0.8), ("goblin", 0.8), ("yeti", 0));
            b.Archetypes("amorphous", ("wisp", 2.5), ("ooze", 1.5), ("beholder", 1.5), ("jelly", 0.1), ("octopus", 0.1));
            b.Archetypes("serpent", ("worm", 2), ("sandworm", 1.5), ("viper", 1.5), ("constrictor", 0.4));
            b.Archetypes("arthropod", ("scorpion", 2.5), ("beetle", 2), ("ant", 1.5), ("spider", 1.5), ("mantis", 0.5), ("crab", 0.6));
            b.Archetypes("winged", ("bat", 3), ("raptor", 1.2), ("songbird", 0.3), ("owl", 0.4), ("dragonfly", 0.3));
            b.Archetypes("quadruped", ("beast", 3), ("canine", 1.5), ("boar", 1.2), ("cervine", 0.3), ("rodent", 0.6));
            b.Archetypes("plantfolk", ("mandrake", 1.5), ("cactus", 1.5), ("mushroom", 1.5), ("sapling", 0.4), ("bloom", 0.3));
            b.Coat(Animals, "*", 0.45);
            b.Coats(Animals, ("black", 4), ("slate", 3), ("crimson", 3), ("rust", 2.5), ("red", 2), ("grey", 1.5), ("chocolate", 1), ("ginger", 1),
                ("vivid", 0.8), ("yellow", 0.6), ("golden", 0.6), ("silver", 0.6), ("sand", 0.4), ("cream", 0.3), ("blue", 0.3), ("green", 0.2),
                ("teal", 0.2), ("albino", 0.1), ("pink", 0.2));
            b.AddCoat(Animals, Genetics.Coats.Black(1), 0.25);
            b.AddCoat(Animals, Genetics.Coats.Crimson(1), 0.2);
            b.Coats(Props, ("black", 3), ("slate", 3), ("grey", 1.5), ("crimson", 2), ("rust", 1.5), ("brown", 0.5), ("tan", 0.4));
            b.AddCoat(Props, Genetics.Coats.Slate(1), 0.3);
            b.Coats(BiomeScope.Archetype("prop", "crystal"), ("crimson", 4), ("vivid", 2), ("blue", 0.3), ("teal", 0.3));
            b.AddCoat(BiomeScope.Archetype("prop", "crystal"), Genetics.Coats.Crimson(1), 0.6);
            b.Coats(Flora, ("black", 3), ("slate", 2), ("grey", 1), ("brown", 0.4));
            b.AddCoat(Flora, Genetics.Coats.Black(1), 0.4);
            b.Patterns(Animals, ("dorsal", 1.5), ("bands", 1.2), ("mottled", 1.5));
            b.Glow(BiomeScope.All, 3);
            b.Gene(Animals, "color.value", 0.3, 0.3);
            b.Gene(Animals, "head.hornSize", 0.7, 0.25);
            b.Gene(Animals, "horns", 0.7, 0.25);
            // charred, ash-dusted foliage: dark, dry and almost grey
            b.Gene(Flora, "color.leafValue", 0.26, 0.6);
            b.Gene(Flora, "color.leafSaturation", 0.14, 0.75);
            b.Gene(Flora, "color.leafHue", 70, 0.5);
            b.Gene(Flora, "canopy.density", 0.3, 0.4);
            b.Gene(Flora, "snow", 0, 1);
            b.Gene(Props, "light", 0.9, 0.5);
            b.Gene(Props, "wear", 0.7, 0.3);
            return b;
        }
    }
}
