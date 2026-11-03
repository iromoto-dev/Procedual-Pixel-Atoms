// Procedural Pixel Creations for Unity - edit mode tests (Window > General > Test Runner > EditMode).
//
// To run them in a project, list the package under "testables" in Packages/manifest.json:
//   "testables": ["com.proceduralpixel.creations"]
// CoreSuite runs the engine independent core tests inside Unity's runtime (including the golden values
// that prove bit exact generation); the other tests exercise the Unity layer.

#nullable enable

using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PixelCreatures.Core;
using PixelCreatures.Core.Export;
using PixelCreatures.Core.Genetics;
using PixelCreatures.Unity;
using PixelCreatures.Unity.Editor;
using UnityEditor;
using UnityEngine;

namespace PixelCreatures.Tests.UnityIntegration
{
    public sealed class PixelCreaturesUnityTests
    {
        [Test]
        public void CoreSuite_PassesWithGoldenValues()
        {
            var registry = CreatureRegistry.CreateDefault();
            registry.DiscoverExtensions(typeof(PixelCreatures.Plantfolk.PlantfolkExtension).Assembly);
            var log = new System.Text.StringBuilder();
            var runner = new PixelCreatures.Tests.TestRunner { Log = s => log.AppendLine(s) };
            PixelCreatures.Tests.CoreTests.RunAll(runner, registry, 5);
            Assert.AreEqual(0, runner.Failed, runner.Summary() + "\n" + log);
        }

        [Test]
        public void Registry_ContainsEveryCategory()
        {
            var cats = PixelCreatureRuntime.Families.Select(CreatureFamilies.CategoryOf).Distinct().ToList();
            foreach (CreationCategory c in Enum.GetValues(typeof(CreationCategory)))
                Assert.Contains(c, cats, $"no family of category {c}");
            Assert.IsTrue(PixelCreatureRuntime.Registry.TryGetFamily("plantfolk", out _), "the plantfolk extension was not discovered");
        }

        [Test]
        public void Actor_RendersEveryFamilyInEditMode([Values(1f, 2f)] float density)
        {
            foreach (var family in PixelCreatureRuntime.Families)
            {
                var go = new GameObject("test actor", typeof(SpriteRenderer));
                try
                {
                    var actor = go.AddComponent<PixelCreatureActor>();
                    actor.PixelDensity = density;
                    actor.SetGenome(PixelCreatureRuntime.Sample(family.Id, 42), async: false);
                    actor.RenderImmediate();
                    Assert.IsNotNull(actor.Texture, $"{family.Id}: no texture ({actor.LastError})");
                    Assert.IsNotNull(go.GetComponent<SpriteRenderer>().sprite, $"{family.Id}: sprite not assigned");
                    var c = actor.Model!.Anatomy.Canvas.Scaled(actor.EffectiveDensity);
                    // the canvas only ever grows (poses reaching beyond the rest canvas)
                    Assert.GreaterOrEqual(actor.Texture!.width, c.Width, $"{family.Id}: texture width");
                    var px = actor.Texture.GetPixels32();
                    Assert.Greater(px.Count(p => p.a > 0), 20, $"{family.Id}: rendered texture is empty");
                    Assert.AreEqual(FilterMode.Point, actor.Texture.filterMode);
                    Assert.AreEqual(actor.EffectivePixelsPerUnit * (float)actor.EffectiveDensity, actor.Sprite!.pixelsPerUnit, 1e-3f, $"{family.Id}: world size must not depend on the density");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(go);
                }
            }
        }

        [Test]
        public void Presets_RoundTrip()
        {
            var g = PixelCreatureRuntime.Sample("mythical", 7);
            var res = PixelCreatureRuntime.LoadPreset(PixelCreatureRuntime.SavePreset(g));
            Assert.IsTrue(res.Ok, res.Describe());
            Assert.AreEqual(g.ContentHash, res.Genome!.ContentHash);
        }

        [Test]
        public void Coordinates_RoundTrip()
        {
            var ground = new Vector2(123.5f, -42.25f);
            var world = PixelCreatureRuntime.GroundToWorld(ground, 32f);
            var back = PixelCreatureRuntime.WorldToGround(world, 32f);
            Assert.AreEqual(ground.x, back.x, 1e-3f);
            Assert.AreEqual(ground.y, back.y, 1e-3f);
            // south (positive ground y) is down on screen, east is right
            Assert.Less(PixelCreatureRuntime.GroundToWorld(new Vector2(0, 10), 32f).y, 0f);
            Assert.Greater(PixelCreatureRuntime.GroundToWorld(new Vector2(10, 0), 32f).x, 0f);
        }

        [Test]
        public void Actor_RendersBiomeOverlay()
        {
            var go = new GameObject("biome actor", typeof(SpriteRenderer));
            try
            {
                var actor = go.AddComponent<PixelCreatureActor>();
                actor.FamilyId = "quadruped";
                actor.Seed = "2024";
                actor.Biome = "snow";
                actor.BuildFromSettings(async: false);
                Assert.IsTrue(actor.EffectiveOverlay.HasCover, "snow covers a quadruped");
                var snowy = actor.Texture!.GetPixels32();
                actor.BiomeIntensity = 0f;
                actor.RenderImmediate();
                var plain = actor.Texture!.GetPixels32();
                Assert.AreEqual(snowy.Length, plain.Length);
                Assert.IsTrue(snowy.Where((c, i) => !c.Equals(plain[i])).Any(), "the biome look changes the sprite");
                // the biome adapts sampling: a desert flora sample is a desert plant more often than not
                int desertPlants = 0;
                for (ulong s = 1; s <= 24; s++)
                {
                    var g = PixelCreatureRuntime.Sample("flora", s, "desert");
                    string form = g.GetChoice("form");
                    if (form == "cactus" || form == "palm" || form == "dead") desertPlants++;
                }
                Assert.Greater(desertPlants, 12, "desert sampling favours cacti, palms and dead trees");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Actor_ClampsDensityToTheSpriteLimit()
        {
            var go = new GameObject("dense actor", typeof(SpriteRenderer));
            try
            {
                var actor = go.AddComponent<PixelCreatureActor>();
                actor.PixelDensity = 4f;
                foreach (var family in new[] { "reptile", "monster", "mythical" })
                {
                    for (ulong s = 1; s <= 6; s++)
                    {
                        actor.SetGenome(PixelCreatureRuntime.Sample(family, s), async: false);
                        actor.RenderImmediate();
                        Assert.IsTrue(string.IsNullOrEmpty(actor.LastError), $"{family}/{s}: {actor.LastError}");
                        Assert.IsNotNull(actor.Texture, $"{family}/{s}: no texture");
                        Assert.LessOrEqual(Math.Max(actor.Texture!.width, actor.Texture.height), PixelCreatureActor.MaxSpriteSize, $"{family}/{s}: sprite too large");
                    }
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Bake_CreatesSpritesClipsAndController_AndRebakesInPlace()
        {
            const string root = "Assets/__PixelCreationsTest";
            try
            {
                var model = PixelCreatureRuntime.Build(PixelCreatureRuntime.Sample("prop", 3));
                var o = new BakeOptions { Fps = 10, Density = 2, Overlay = PixelCreatureRuntime.GetBiome("snow").OverlayFor(null), Biome = "snow" };
                o.Clips.Clear(); o.Clips.Add(ClipKind.Idle); o.Clips.Add(ClipKind.Action);
                o.Directions.Clear(); o.Directions.Add(7);
                string dir = PixelCreatureBaker.Bake(model, root, o);
                string name = PixelCreatureBaker.SafeName(model.Genome.Name);
                string setPath = $"{dir}/{name}.asset", ctrlPath = $"{dir}/{name}.controller";
                var set = AssetDatabase.LoadAssetAtPath<PixelCreatureSpriteSet>(setPath);
                Assert.IsNotNull(set, "sprite set missing");
                var sprites = AssetDatabase.LoadAllAssetsAtPath(setPath).OfType<Sprite>().ToList();
                Assert.Greater(sprites.Count, 0, "no sprites baked");
                Assert.AreEqual(2, set.clips.Count, "one clip per exported clip and direction");
                Assert.IsNotNull(set.Find("idle", 6), "nearest direction lookup");
                var clips = AssetDatabase.FindAssets("t:AnimationClip", new[] { dir });
                Assert.AreEqual(2, clips.Length, "one animation clip per clip and direction");
                Assert.IsTrue(File.Exists($"{dir}/{name}.json"), "metadata missing");
                Assert.IsTrue(File.ReadAllText($"{dir}/{name}.json").Contains("\"biome\""), "biome recorded");
                Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(ctrlPath));
                var sheet = AssetDatabase.LoadAssetAtPath<Texture2D>($"{dir}/{name}_sheet.png");
                Assert.IsNotNull(sheet, "sheet page missing");
                Assert.AreEqual(FilterMode.Point, sheet.filterMode);

                // bake again: assets keep their GUIDs and sprite objects (references in scenes and prefabs survive)
                string ctrlGuid = AssetDatabase.AssetPathToGUID(ctrlPath), setGuid = AssetDatabase.AssetPathToGUID(setPath);
                var firstSprite = sprites.First(s => s.name == "idle_SE_00");
                int spriteId = firstSprite.GetInstanceID();
                PixelCreatureBaker.Bake(model, root, o);
                Assert.AreEqual(ctrlGuid, AssetDatabase.AssetPathToGUID(ctrlPath), "controller GUID kept");
                Assert.AreEqual(setGuid, AssetDatabase.AssetPathToGUID(setPath), "sprite set GUID kept");
                var again = AssetDatabase.LoadAllAssetsAtPath(setPath).OfType<Sprite>().First(s => s.name == "idle_SE_00");
                Assert.AreEqual(spriteId, again.GetInstanceID(), "sprite sub-asset kept");

                // fewer clips: stale clips are removed
                o.Clips.Remove(ClipKind.Action);
                PixelCreatureBaker.Bake(model, root, o);
                Assert.AreEqual(1, AssetDatabase.FindAssets("t:AnimationClip", new[] { dir }).Length, "stale clip removed");
            }
            finally
            {
                AssetDatabase.DeleteAsset(root);
            }
        }
    }
}
