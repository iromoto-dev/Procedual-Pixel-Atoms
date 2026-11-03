// Procedural Pixel Creations for Unity - shared registry, factory, presets and coordinate conversion.
//
// The engine independent core (PixelCreatures.Core) generates, animates and rasterizes creations; this
// layer connects it to Unity. Ground coordinates of the core are pixels on the ground plane (x east,
// y/z south); Unity world units are 'pixelsPerUnit' pixels. The fixed 30 degree camera of the pixel art
// foreshortens the ground depth: a step south moves the sprite down by sin(30 deg) pixels.

#nullable enable

using System;
using System.Collections.Generic;
using PixelCreatures.Core;
using PixelCreatures.Core.Genetics;
using PixelCreatures.Core.Palettes;
using PixelCreatures.Core.Rendering;
using PixelCreatures.Core.Serialization;
using UnityEngine;

namespace PixelCreatures.Unity
{
    public static class PixelCreatureRuntime
    {
        private static readonly object Lock = new object();
        private static CreatureFactory? _factory;
        private static GenomeSerializer? _serializer;

        /// <summary>Process wide registry: built-in families plus every discovered [CreatureExtension].</summary>
        public static CreatureRegistry Registry => CreatureRegistry.Shared;

        /// <summary>Caching model factory (thread safe).</summary>
        public static CreatureFactory Factory
        {
            get { lock (Lock) return _factory ??= new CreatureFactory(Registry); }
        }

        public static GenomeSerializer Serializer
        {
            get { lock (Lock) return _serializer ??= new GenomeSerializer(Registry); }
        }

        /// <summary>
        /// Pixel density used by actors that do not set their own: 2 = HD sprites with high density anatomy,
        /// 1 = classic. Output pixels per ground pixel.
        /// </summary>
        public static float DefaultDensity { get; set; } = (float)CreatureFramework.HdDensity;

        /// <summary>Unity world units per ground pixel are 1 / PixelsPerUnit (actors can override it).</summary>
        public static float DefaultPixelsPerUnit { get; set; } = 32f;

        /// <summary>Worker threads (async builds, parallel rasterization) are unavailable on WebGL.</summary>
        public static bool ThreadsSupported =>
            Application.platform != RuntimePlatform.WebGLPlayer && SystemInfo.processorCount > 1;

        // "Enter Play Mode" without a domain reload keeps statics: start every session with the defaults
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            DefaultDensity = (float)CreatureFramework.HdDensity;
            DefaultPixelsPerUnit = 32f;
        }

        public static CreatureGenome Sample(string familyId, ulong seed) => GenomeFactory.Sample(Registry.GetFamily(familyId), seed);

        /// <summary>A genome sampled in a biome (null, empty or "temperate" = the family's own distribution).</summary>
        public static CreatureGenome Sample(string familyId, ulong seed, string? biomeId) =>
            GenomeFactory.Sample(Registry.GetFamily(familyId), seed, GetBiome(biomeId));

        /// <summary>Registered biomes (temperate, desert, snow, jungle, swamp, volcanic and extension biomes).</summary>
        public static IReadOnlyList<Biome> Biomes => Registry.Biomes;

        /// <summary>The biome with this id; unknown ids give the neutral temperate biome.</summary>
        public static Biome GetBiome(string? id) => Registry.GetBiome(id);

        /// <summary>Unity colour of a core colour (biome ground colours for cameras and tilemaps).</summary>
        public static Color32 ToColor(Rgba c) => new Color32(c.R, c.G, c.B, c.A);

        /// <summary>A genome of a given archetype (searches seeds deterministically from 'seed').</summary>
        public static CreatureGenome SampleArchetype(string familyId, string archetypeId, ulong seed, string? biomeId = null)
        {
            var family = Registry.GetFamily(familyId);
            var biome = GetBiome(biomeId);
            for (ulong k = 0; k < 4096; k++)
            {
                var g = GenomeFactory.Sample(family, seed + k * 7919UL, biome);
                if (!g.Schema.Contains("form") || g.GetChoice("form") == archetypeId) return g;
            }
            return GenomeFactory.Sample(family, seed, biome);
        }

        public static ulong SeedFromText(string text) => StableHash.SeedFromText(text ?? string.Empty);

        public static CreatureModel Build(CreatureGenome genome) => Factory.Build(genome);

        /// <summary>Loads a ppc.creature preset (JSON text). Check result.Ok and result.Describe().</summary>
        public static GenomeLoadResult LoadPreset(string json) => Serializer.Deserialize(json);

        public static string SavePreset(CreatureGenome genome, PresetMeta? meta = null) => Serializer.Serialize(genome, meta);

        public static string CategoryLabel(CreationCategory c)
        {
            switch (c)
            {
                case CreationCategory.Mythical: return "Mythical";
                case CreationCategory.Monster: return "Monsters";
                case CreationCategory.Flora: return "Flora";
                case CreationCategory.Prop: return "Props";
                default: return "Creatures";
            }
        }

        /// <summary>"Flora: Trees & Flora" style label for pickers.</summary>
        public static string FamilyLabel(ICreatureFamily f) => $"{CategoryLabel(CreatureFamilies.CategoryOf(f))}: {f.DisplayName}";

        public static IReadOnlyList<ICreatureFamily> Families => Registry.Families;

        // ------------------------------------------------------------------ coordinates

        private static readonly float SinE = (float)DMath.Sin(PixelCamera.DefaultElevation);
        private static readonly float CosE = (float)DMath.Cos(PixelCamera.DefaultElevation);

        /// <summary>Ground position (x east, y south, in ground pixels) to Unity world units on the XY plane.</summary>
        public static Vector2 GroundToWorld(Vector2 ground, float pixelsPerUnit, float altitude = 0f) =>
            new Vector2(ground.x / pixelsPerUnit, -(ground.y * SinE - altitude * CosE) / pixelsPerUnit);

        /// <summary>Unity world position (XY plane) to the ground position under it.</summary>
        public static Vector2 WorldToGround(Vector2 world, float pixelsPerUnit) =>
            new Vector2(world.x * pixelsPerUnit, -world.y * pixelsPerUnit / SinE);

        /// <summary>Creature space point (pixels, y up) to the sprite plane relative to the ground point, in world units.</summary>
        public static Vector2 CreatureToWorldOffset(Vec3 p, double yaw, float pixelsPerUnit)
        {
            var s = PixelCamera.CreatureToScreen(p, yaw, PixelCamera.DefaultElevation, Vec3.Zero);
            return new Vector2((float)s.X / pixelsPerUnit, -(float)s.Y / pixelsPerUnit);
        }

        /// <summary>Unit ground direction of one of the eight facing directions (0 = east, 2 = north, 6 = south).</summary>
        public static Vector2 DirectionVector(int dir8)
        {
            double yaw = PixelCamera.DirectionYaw(dir8);
            return new Vector2((float)DMath.Cos(yaw), -(float)DMath.Sin(yaw));
        }
    }
}
