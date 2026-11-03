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
                    var c = actor.Model!.Anatomy.Canvas.Scaled(density);
                    Assert.AreEqual(c.Width, actor.Texture!.width, $"{family.Id}: texture width");
                    var px = actor.Texture.GetPixels32();
                    Assert.Greater(px.Count(p => p.a > 0), 20, $"{family.Id}: rendered texture is empty");
                    Assert.AreEqual(FilterMode.Point, actor.Texture.filterMode);
                    Assert.AreEqual(actor.EffectivePixelsPerUnit * density, actor.Sprite!.pixelsPerUnit, 1e-3f, $"{family.Id}: world size must not depend on the density");
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
        public void Bake_CreatesSpritesClipsAndController()
        {
            const string root = "Assets/__PixelCreationsTest";
            try
            {
                var model = PixelCreatureRuntime.Build(PixelCreatureRuntime.Sample("prop", 3));
                var o = new BakeOptions { Fps = 10, Density = 2 };
                o.Clips.Clear(); o.Clips.Add(ClipKind.Idle); o.Clips.Add(ClipKind.Action);
                o.Directions.Clear(); o.Directions.Add(7);
                string dir = PixelCreatureBaker.Bake(model, root, o);
                var sprites = AssetDatabase.FindAssets("t:Sprite", new[] { dir });
                var clips = AssetDatabase.FindAssets("t:AnimationClip", new[] { dir });
                Assert.Greater(sprites.Length, 0, "no sprites baked");
                Assert.AreEqual(2, clips.Length, "one clip per exported clip and direction");
                Assert.IsTrue(File.Exists($"{dir}/{PixelCreatureBaker.SafeName(model.Genome.Name)}.json"), "metadata missing");
                Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>($"{dir}/{PixelCreatureBaker.SafeName(model.Genome.Name)}.controller"));
            }
            finally
            {
                AssetDatabase.DeleteAsset(root);
            }
        }
    }
}
