// Procedural Pixel Creations for Unity - sprite sheet export and asset baking (editor only).
//
// Export writes the portable sprite sheet (PNG pages + ppc.spritesheet JSON with frames, pivots, timing and
// root motion). Bake turns the same frames into native Unity assets:
//   - PNG sheet pages imported point filtered, uncompressed, without resizing (max texture size fits the page)
//   - a PixelCreatureSpriteSet asset with one Sprite sub-asset per frame (pivot on the ground point)
//   - an AnimationClip per clip and direction that swaps SpriteRenderer.sprite
//   - an AnimatorController with one state per clip
// Baking again updates every asset in place: GUIDs (and the references of prefabs, animators and scenes)
// survive, outputs that are no longer produced are removed. Pages are limited to MaxPageSize (4096 by
// default, safe on every platform); long clips wrap onto further rows.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        /// <summary>Frames per second; must divide 60 (6, 10, 12, 15, 20, 30).</summary>
        public int Fps = 12;
        public List<int> Directions = new List<int> { 0, 1, 2, 3, 4, 5, 6, 7 };
        public List<ClipKind> Clips = new List<ClipKind> { ClipKind.Idle, ClipKind.Walk, ClipKind.Run, ClipKind.Action, ClipKind.Hit, ClipKind.RestEnter, ClipKind.Sleep, ClipKind.RestExit };
        public bool DrawShadow = true;
        public bool CreateAnimatorController = true;
        /// <summary>Largest sheet page edge in pixels.</summary>
        public int MaxPageSize = 4096;
        /// <summary>Environment look of the frames (see Biome.OverlayFor). Default none.</summary>
        public BiomeOverlay Overlay;
        /// <summary>Biome id recorded in the metadata and the sprite set.</summary>
        public string Biome = string.Empty;
    }

    public static class PixelCreatureBaker
    {
        public static SpritesheetResult Render(CreatureModel model, BakeOptions o, string baseName)
        {
            var opt = new SpritesheetOptions
            {
                Fps = o.Fps, DrawShadow = o.DrawShadow, Density = Math.Max(1f, o.Density), MaxPageSize = Math.Max(256, o.MaxPageSize),
                CompactFrames = true, Overlay = o.Overlay, Biome = o.Biome ?? string.Empty,
            };
            opt.Clips.Clear();
            opt.Clips.AddRange(o.Clips);
            opt.Directions.Clear();
            opt.Directions.AddRange(o.Directions);
            return SpritesheetExporter.Export(model, PixelCreatureRuntime.Registry, opt, baseName);
        }

        /// <summary>Writes PNG page(s) and the JSON metadata into a project folder ("Assets/..."). Returns the JSON path.</summary>
        public static string ExportSpriteSheet(CreatureModel model, string folder, BakeOptions o)
        {
            folder = folder.Replace('\\', '/').TrimEnd('/');
            string baseName = SafeName(model.Genome.Name);
            EnsureFolder(folder);
            var result = Render(model, o, baseName);
            var pages = new List<(string path, int w, int h)>();
            for (int i = 0; i < result.Pages.Count; i++)
            {
                var (rgba, w, h) = result.Pages[i];
                string file = result.Pages.Count == 1 ? $"{baseName}.png" : $"{baseName}_{i}.png";
                string path = $"{folder}/{file}";
                File.WriteAllBytes(path, PngEncoder.Encode(rgba, w, h));
                pages.Add((path, w, h));
            }
            string json = $"{folder}/{baseName}.json";
            File.WriteAllText(json, result.MetadataJson);
            AssetDatabase.Refresh();
            foreach (var (p, w, h) in pages) ConfigurePixelTexture(p, o.PixelsPerUnit * Math.Max(1f, o.Density), w, h, sprite: true);
            return json;
        }

        /// <summary>
        /// Point filtering, no compression, no mipmaps and no resizing (non power of two sizes kept, maximum size
        /// large enough for the page) for an imported pixel-art PNG.
        /// </summary>
        public static void ConfigurePixelTexture(string assetPath, float spritePixelsPerUnit, int width, int height, bool sprite)
        {
            if (!(AssetImporter.GetAtPath(assetPath) is TextureImporter ti)) return;
            ti.textureType = sprite ? TextureImporterType.Sprite : TextureImporterType.Default;
            if (sprite)
            {
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.spritePixelsPerUnit = spritePixelsPerUnit;
            }
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.filterMode = FilterMode.Point;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.mipmapEnabled = false;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.alphaIsTransparency = true;
            ti.isReadable = false;
            int edge = Math.Max(width, height);
            int max = 32;
            while (max < edge && max < 16384) max *= 2;
            ti.maxTextureSize = max;
            // platform overrides (for example from a default preset) must not shrink or compress the sheet either
            foreach (var platform in new[] { "Standalone", "Web", "WebGL", "iPhone", "Android", "tvOS", "Windows Store Apps", "PS4", "PS5", "XboxOne", "GameCoreXboxOne", "GameCoreScarlett", "Switch", "Server" })
            {
                var ps = ti.GetPlatformTextureSettings(platform);
                if (ps == null || !ps.overridden) continue;
                ps.maxTextureSize = Math.Max(ps.maxTextureSize, max);
                ps.format = TextureImporterFormat.RGBA32;
                ps.textureCompression = TextureImporterCompression.Uncompressed;
                ti.SetPlatformTextureSettings(ps);
            }
            if (edge > SystemInfo.maxTextureSize)
                Debug.LogWarning($"[Procedural Pixel Creations] {assetPath} is {width}x{height} px; this GPU supports {SystemInfo.maxTextureSize} px. Use a smaller page size, fewer clips or a lower density.");
            ti.SaveAndReimport();
        }

        /// <summary>
        /// Bakes sheet textures, a sprite set, animation clips and (optionally) an animator controller into
        /// 'folder/name/'. Existing assets of an earlier bake are updated in place. Returns the asset folder.
        /// </summary>
        public static string Bake(CreatureModel model, string folder, BakeOptions o)
        {
            folder = folder.Replace('\\', '/').TrimEnd('/');
            string baseName = SafeName(model.Genome.Name);
            string dir = $"{folder}/{baseName}";
            EnsureFolder(dir);
            // the metadata names the pages exactly as they are written below ({name}_sheet[_i].png)
            var result = Render(model, o, baseName + "_sheet");
            float density = Math.Max(1f, o.Density);
            float ppu = o.PixelsPerUnit * density;

            // 1. sheet pages as PNG files (rewriting a file keeps its GUID)
            var pages = new List<string>();
            for (int i = 0; i < result.Pages.Count; i++)
            {
                var (rgba, w, h) = result.Pages[i];
                string path = result.Pages.Count == 1 ? $"{dir}/{baseName}_sheet.png" : $"{dir}/{baseName}_sheet_{i}.png";
                File.WriteAllBytes(path, PngEncoder.Encode(rgba, w, h));
                pages.Add(path);
            }
            AssetDatabase.Refresh();
            for (int i = 0; i < pages.Count; i++) ConfigurePixelTexture(pages[i], ppu, result.Pages[i].width, result.Pages[i].height, sprite: false);
            var textures = pages.Select(p => AssetDatabase.LoadAssetAtPath<Texture2D>(p)).ToList();
            if (textures.Any(t => t == null)) throw new InvalidOperationException("The baked sheet pages could not be imported.");

            // 2. sprite set: one sprite sub-asset per frame, updated in place when it exists
            string setPath = $"{dir}/{baseName}.asset";
            var set = AssetDatabase.LoadAssetAtPath<PixelCreatureSpriteSet>(setPath);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<PixelCreatureSpriteSet>();
                AssetDatabase.CreateAsset(set, setPath);
            }
            var existing = new Dictionary<string, Sprite>(StringComparer.Ordinal);
            foreach (var s in AssetDatabase.LoadAllAssetsAtPath(setPath).OfType<Sprite>())
                if (!existing.ContainsKey(s.name)) existing[s.name] = s;
            var used = new HashSet<string>(StringComparer.Ordinal);
            set.creationName = model.Genome.Name;
            set.family = model.Genome.FamilyId;
            set.biome = o.Biome ?? string.Empty;
            set.pixelsPerUnit = o.PixelsPerUnit;
            set.pixelDensity = density;
            set.clips.Clear();
            var pivot = new Vector2(result.OriginX / (float)result.FrameWidth, (result.FrameHeight - result.OriginY) / (float)result.FrameHeight);
            foreach (var clip in result.Clips)
            {
                string state = SpritesheetExporter.ClipName(clip.Kind);
                string dirName = PixelCamera.DirectionNames[((clip.Direction % 8) + 8) % 8];
                var frames = new Sprite[clip.Frames.Count];
                for (int f = 0; f < clip.Frames.Count; f++)
                {
                    var fr = clip.Frames[f];
                    var page = textures[fr.Page]!;
                    // frames are laid out top-down; Unity textures start at the bottom row
                    var rect = new Rect(fr.X, page.height - fr.Y - result.FrameHeight, result.FrameWidth, result.FrameHeight);
                    string name = $"{state}_{dirName}_{f:00}";
                    var sprite = Sprite.Create(page, rect, pivot, ppu, 0, SpriteMeshType.FullRect);
                    sprite.name = name;
                    if (existing.TryGetValue(name, out var old) && !used.Contains(name))
                    {
                        // keep the sub-asset (and every reference to it), replace its content
                        EditorUtility.CopySerialized(sprite, old);
                        UnityEngine.Object.DestroyImmediate(sprite);
                        sprite = old;
                    }
                    else AssetDatabase.AddObjectToAsset(sprite, set);
                    used.Add(name);
                    frames[f] = sprite;
                }
                set.clips.Add(new PixelCreatureSpriteSet.Clip
                {
                    state = state, direction = clip.Direction, loop = clip.Loop, fps = clip.Fps, frames = frames, groundSpeed = (float)(clip.RootSpeed / density),
                });
            }
            foreach (var kv in existing)
            {
                if (used.Contains(kv.Key)) continue;
                AssetDatabase.RemoveObjectFromAsset(kv.Value);
                UnityEngine.Object.DestroyImmediate(kv.Value, true);
            }
            EditorUtility.SetDirty(set);

            // 3. animation clips (updated in place)
            var clipAssets = new List<(string state, AnimationClip clip)>();
            var clipPaths = new HashSet<string>(StringComparer.Ordinal);
            var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
            foreach (var c in set.clips)
            {
                if (c.frames.Length == 0) continue;
                string dirName = PixelCamera.DirectionNames[((c.direction % 8) + 8) % 8];
                string stateName = $"{c.state}_{dirName}";
                string path = $"{dir}/{baseName}_{stateName}.anim";
                var ac = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                bool isNew = ac == null;
                if (ac == null) ac = new AnimationClip();
                else ac.ClearCurves();
                ac.name = $"{baseName}_{stateName}";
                ac.frameRate = c.fps;
                var keys = new ObjectReferenceKeyframe[c.frames.Length + 1];
                for (int f = 0; f < c.frames.Length; f++) keys[f] = new ObjectReferenceKeyframe { time = f / c.fps, value = c.frames[f] };
                // hold the last frame for its full duration
                keys[c.frames.Length] = new ObjectReferenceKeyframe { time = c.frames.Length / c.fps, value = c.frames[c.frames.Length - 1] };
                AnimationUtility.SetObjectReferenceCurve(ac, binding, keys);
                var settings = AnimationUtility.GetAnimationClipSettings(ac);
                settings.loopTime = c.loop;
                AnimationUtility.SetAnimationClipSettings(ac, settings);
                if (isNew) AssetDatabase.CreateAsset(ac, path);
                else EditorUtility.SetDirty(ac);
                clipAssets.Add((stateName, ac));
                clipPaths.Add(path);
            }

            // 4. animator controller (updated in place)
            string ctrlPath = $"{dir}/{baseName}.controller";
            if (o.CreateAnimatorController && clipAssets.Count > 0)
            {
                var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ctrlPath) ?? AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
                var sm = ctrl.layers[0].stateMachine;
                foreach (var st in sm.states.ToArray()) sm.RemoveState(st.state);
                AnimatorState? idle = null, first = null;
                foreach (var (stateName, ac) in clipAssets)
                {
                    var state = sm.AddState(stateName);
                    state.motion = ac;
                    first ??= state;
                    if (stateName == "idle_SE" || (idle == null && stateName.StartsWith("idle_", StringComparison.Ordinal))) idle = state;
                }
                sm.defaultState = idle ?? first;
                EditorUtility.SetDirty(ctrl);
            }

            // 5. remove outputs of earlier bakes that this bake no longer produces
            foreach (var guid in AssetDatabase.FindAssets(string.Empty, new[] { dir }))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetDirectoryName(p)?.Replace('\\', '/') != dir) continue;
                string file = Path.GetFileName(p);
                bool stale =
                    (file.EndsWith(".anim", StringComparison.Ordinal) && file.StartsWith(baseName + "_", StringComparison.Ordinal) && !clipPaths.Contains(p)) ||
                    (file.StartsWith(baseName + "_sheet", StringComparison.Ordinal) && !pages.Contains(p)) ||
                    (file == $"{baseName}.controller" && !(o.CreateAnimatorController && clipAssets.Count > 0));
                if (stale) AssetDatabase.DeleteAsset(p);
            }

            // 6. the portable metadata (frame rectangles, pivots, root motion) next to the assets
            File.WriteAllText($"{dir}/{baseName}.json", result.MetadataJson);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset($"{dir}/{baseName}.json");
            return dir;
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
            if (!folder.StartsWith("Assets", StringComparison.Ordinal))
                throw new ArgumentException($"'{folder}' is not inside the project's Assets folder.");
            if (AssetDatabase.IsValidFolder(folder)) return;
            int slash = folder.LastIndexOf('/');
            if (slash <= 0) return;
            string parent = folder.Substring(0, slash);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folder.Substring(slash + 1));
        }
    }
}
