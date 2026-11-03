// Procedural Pixel Creations for Unity - sprite sheet export and asset baking (editor only).
//
// Export writes the portable sprite sheet (PNG pages + ppc.spritesheet JSON with frames, pivots,
// timing and root motion). Bake turns the same frames into native Unity assets: a point filtered
// Texture2D asset per page with one Sprite sub-asset per frame (pivot on the ground point), an
// AnimationClip per clip and facing direction that swaps SpriteRenderer.sprite, and an
// AnimatorController with one state per clip. Only stable AssetDatabase APIs are used, so no sprite
// importer settings or extra packages are needed.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using PixelCreatures.Core;
using PixelCreatures.Core.Export;
using PixelCreatures.Core.Rendering;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace PixelCreatures.Unity.Editor
{
    public sealed class BakeOptions
    {
        /// <summary>Output pixels per ground pixel (2 = HD).</summary>
        public float Density = (float)CreatureFramework.HdDensity;
        /// <summary>Ground pixels per Unity unit (sprites use PixelsPerUnit * Density).</summary>
        public float PixelsPerUnit = 32f;
        public int Fps = 12;
        public List<int> Directions = new List<int> { 0, 1, 2, 3, 4, 5, 6, 7 };
        public List<ClipKind> Clips = new List<ClipKind> { ClipKind.Idle, ClipKind.Walk, ClipKind.Run, ClipKind.Action, ClipKind.Hit, ClipKind.RestEnter, ClipKind.Sleep, ClipKind.RestExit };
        public bool DrawShadow = true;
        public bool CreateAnimatorController = true;
    }

    public static class PixelCreatureBaker
    {
        public static SpritesheetResult Render(CreatureModel model, BakeOptions o, string baseName)
        {
            var opt = new SpritesheetOptions { Fps = o.Fps, DrawShadow = o.DrawShadow, Density = Math.Max(1f, o.Density) };
            opt.Clips.Clear();
            opt.Clips.AddRange(o.Clips);
            opt.Directions.Clear();
            opt.Directions.AddRange(o.Directions);
            return SpritesheetExporter.Export(model, PixelCreatureRuntime.Registry, opt, baseName);
        }

        /// <summary>Writes PNG page(s) and the JSON metadata into a project folder ("Assets/..."). Returns the JSON path.</summary>
        public static string ExportSpriteSheet(CreatureModel model, string folder, BakeOptions o)
        {
            string baseName = SafeName(model.Genome.Name);
            var result = Render(model, o, baseName);
            EnsureFolder(folder);
            var pngPaths = new List<string>();
            for (int i = 0; i < result.Pages.Count; i++)
            {
                var (rgba, w, h) = result.Pages[i];
                string file = result.Pages.Count == 1 ? $"{baseName}.png" : $"{baseName}_{i}.png";
                string path = $"{folder}/{file}";
                File.WriteAllBytes(path, PngEncoder.Encode(rgba, w, h));
                pngPaths.Add(path);
            }
            string json = $"{folder}/{baseName}.json";
            File.WriteAllText(json, result.MetadataJson);
            AssetDatabase.Refresh();
            foreach (var p in pngPaths) ConfigurePixelTexture(p, o.PixelsPerUnit * Math.Max(1f, o.Density));
            return json;
        }

        /// <summary>Point filtering, no compression and no mipmaps for an imported pixel-art PNG.</summary>
        public static void ConfigurePixelTexture(string assetPath, float spritePixelsPerUnit)
        {
            if (!(AssetImporter.GetAtPath(assetPath) is TextureImporter ti)) return;
            ti.textureType = TextureImporterType.Sprite;
            ti.filterMode = FilterMode.Point;
            ti.mipmapEnabled = false;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.alphaIsTransparency = true;
            ti.spritePixelsPerUnit = spritePixelsPerUnit;
            ti.SaveAndReimport();
        }

        /// <summary>
        /// Bakes texture + sprite assets, animation clips and (optionally) an animator controller into
        /// 'folder/name/'. Returns the asset folder.
        /// </summary>
        public static string Bake(CreatureModel model, string folder, BakeOptions o)
        {
            string baseName = SafeName(model.Genome.Name);
            string dir = $"{folder}/{baseName}";
            EnsureFolder(dir);
            var result = Render(model, o, baseName);
            float ppu = o.PixelsPerUnit * Math.Max(1f, o.Density);

            // one texture asset per page; sprites are sub-assets of their page
            var pages = new List<Texture2D>();
            for (int i = 0; i < result.Pages.Count; i++)
            {
                var (rgba, w, h) = result.Pages[i];
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
                {
                    name = result.Pages.Count == 1 ? baseName : $"{baseName}_{i}",
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                };
                tex.SetPixelData(FlipRows(rgba, w, h), 0);
                tex.Apply(false, false);
                string path = $"{dir}/{tex.name}_sheet.asset";
                AssetDatabase.DeleteAsset(path);
                AssetDatabase.CreateAsset(tex, path);
                pages.Add(tex);
            }

            var clips = new List<AnimationClip>();
            var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
            foreach (var clip in result.Clips)
            {
                string dirName = PixelCamera.DirectionNames[((clip.Direction % 8) + 8) % 8];
                var keys = new List<ObjectReferenceKeyframe>();
                float t = 0;
                for (int f = 0; f < clip.Frames.Count; f++)
                {
                    var fr = clip.Frames[f];
                    var page = pages[fr.Page];
                    // frames are laid out top-down; Unity textures start at the bottom row
                    var rect = new Rect(fr.X, page.height - fr.Y - result.FrameHeight, result.FrameWidth, result.FrameHeight);
                    var pivot = new Vector2(result.OriginX / (float)result.FrameWidth, (result.FrameHeight - result.OriginY) / (float)result.FrameHeight);
                    var sprite = Sprite.Create(page, rect, pivot, ppu, 0, SpriteMeshType.FullRect);
                    sprite.name = $"{clip.Name}_{dirName}_{f:00}";
                    AssetDatabase.AddObjectToAsset(sprite, page);
                    keys.Add(new ObjectReferenceKeyframe { time = t, value = sprite });
                    t += fr.DurationMs / 1000f;
                }
                if (keys.Count == 0) continue;
                // hold the last frame for its full duration
                keys.Add(new ObjectReferenceKeyframe { time = t, value = keys[keys.Count - 1].value });
                var ac = new AnimationClip { name = $"{baseName}_{clip.Name}_{dirName}", frameRate = clip.Fps };
                AnimationUtility.SetObjectReferenceCurve(ac, binding, keys.ToArray());
                var settings = AnimationUtility.GetAnimationClipSettings(ac);
                settings.loopTime = clip.Loop;
                AnimationUtility.SetAnimationClipSettings(ac, settings);
                string clipPath = $"{dir}/{ac.name}.anim";
                AssetDatabase.DeleteAsset(clipPath);
                AssetDatabase.CreateAsset(ac, clipPath);
                clips.Add(ac);
            }

            if (o.CreateAnimatorController && clips.Count > 0)
            {
                string ctrlPath = $"{dir}/{baseName}.controller";
                AssetDatabase.DeleteAsset(ctrlPath);
                var ctrl = AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
                var sm = ctrl.layers[0].stateMachine;
                AnimatorState? idle = null;
                foreach (var ac in clips)
                {
                    var state = sm.AddState(ac.name.Substring(baseName.Length + 1));
                    state.motion = ac;
                    if (idle == null && ac.name.Contains("_idle_")) idle = state;
                }
                if (idle != null) sm.defaultState = idle;
            }

            // the portable metadata (frame rectangles, pivots, root motion) next to the assets
            File.WriteAllText($"{dir}/{baseName}.json", result.MetadataJson);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return dir;
        }

        private static byte[] FlipRows(byte[] rgba, int w, int h)
        {
            var o = new byte[rgba.Length];
            int row = w * 4;
            for (int y = 0; y < h; y++) Buffer.BlockCopy(rgba, y * row, o, (h - 1 - y) * row, row);
            return o;
        }

        public static string SafeName(string name)
        {
            var chars = (string.IsNullOrEmpty(name) ? "Creation" : name).ToCharArray();
            for (int i = 0; i < chars.Length; i++)
                if (!char.IsLetterOrDigit(chars[i]) && chars[i] != '_' && chars[i] != '-') chars[i] = '_';
            return new string(chars);
        }

        /// <summary>Makes sure a project folder ("Assets/A/B") exists.</summary>
        public static void EnsureFolder(string folder)
        {
            folder = folder.Replace('\\', '/').TrimEnd('/');
            if (AssetDatabase.IsValidFolder(folder)) return;
            int slash = folder.LastIndexOf('/');
            if (slash <= 0) return;
            string parent = folder.Substring(0, slash);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folder.Substring(slash + 1));
        }
    }
}
