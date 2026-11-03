// Procedural Pixel Creations for Unity - Creation Studio (Window > Procedural Pixel Creations > Creation Studio).
//
// Pick a category, family and archetype, roll seeds, mutate, edit every gene, watch the animated preview
// in any direction and clip, then save a preset, export a sprite sheet, bake sprites + animation clips or
// drop a live PixelCreatureActor into the scene.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PixelCreatures.Core;
using PixelCreatures.Core.Export;
using PixelCreatures.Core.Genetics;
using PixelCreatures.Core.Motion;
using PixelCreatures.Core.Rendering;
using UnityEditor;
using UnityEngine;

namespace PixelCreatures.Unity.Editor
{
    public sealed class CreationStudioWindow : EditorWindow
    {
        private static readonly string[] ClipLabels = { "Idle", "Walk", "Run", "Action", "Hit", "Rest" };
        private static readonly string[] DensityLabels = { "Classic (1x)", "HD (2x)", "HD (3x)" };
        private static readonly int[] FpsOptions = { 6, 10, 12, 15, 20, 30 };
        private static readonly string[] DirectionModes = { "8 directions", "4 directions", "Preview direction" };
        // 3x3 grid of facing buttons: NW N NE / W - E / SW S SE
        private static readonly int[] DirGrid = { 3, 2, 1, 4, -1, 0, 5, 6, 7 };
        private static readonly string[] DirArrows = { "↖", "↑", "↗", "←", "", "→", "↙", "↓", "↘" };

        [SerializeField] private string _familyId = "quadruped";
        [SerializeField] private string _archetype = string.Empty;
        [SerializeField] private string _seed = "1";
        [SerializeField] private string _presetJson = string.Empty;
        [SerializeField] private int _clip;
        [SerializeField] private int _dir = 7;
        [SerializeField] private int _density = 1;
        [SerializeField] private int _zoom;
        [SerializeField] private bool _playing = true;
        [SerializeField] private bool _showAdvanced;
        [SerializeField] private float _mutation = 0.35f;
        [SerializeField] private string _folder = "Assets/PixelCreations";
        [SerializeField] private float _pixelsPerUnit = 32f;
        [SerializeField] private int _fpsIndex = 2;
        [SerializeField] private int _directionMode;
        [SerializeField] private bool _shadow = true;
        [SerializeField] private bool[] _clipToggles = { true, true, true, true, true, true, true, true };

        private CreatureGenome? _genome;
        private CreatureModel? _model;
        private CreatureMotor? _motor;
        private Texture2D? _preview;
        private PixelFrame? _frame;
        private byte[] _rgba = Array.Empty<byte>(), _flipped = Array.Empty<byte>();
        private readonly byte[] _lut = new byte[1024];
        private readonly CreatureRenderer _renderer = new CreatureRenderer();
        private readonly FixedClock _clock = new FixedClock();
        private double _lastTime, _sinceRender, _sinceCommand;
        private Vector2 _geneScroll;
        private readonly Dictionary<string, bool> _foldouts = new Dictionary<string, bool>();
        private string _status = string.Empty;
        private static readonly System.Random SeedRandom = new System.Random();

        [MenuItem("Window/Procedural Pixel Creations/Creation Studio")]
        public static void Open()
        {
            var w = GetWindow<CreationStudioWindow>();
            w.titleContent = new GUIContent("Creation Studio");
            w.minSize = new Vector2(760, 520);
            w.Show();
        }

        public static void Open(CreatureGenome genome)
        {
            Open();
            var w = GetWindow<CreationStudioWindow>();
            w.SetGenome(genome);
        }

        private void OnEnable()
        {
            EditorApplication.update += Tick;
            _lastTime = EditorApplication.timeSinceStartup;
            if (_genome == null)
            {
                if (!string.IsNullOrEmpty(_presetJson))
                {
                    var res = PixelCreatureRuntime.LoadPreset(_presetJson);
                    if (res.Ok && res.Genome != null) { SetGenome(res.Genome); return; }
                }
                NewCreation(keepSeed: true);
            }
        }

        private void OnDisable()
        {
            EditorApplication.update -= Tick;
            if (_preview != null) DestroyImmediate(_preview);
            _preview = null;
        }

        // ------------------------------------------------------------------ genome

        private void NewCreation(bool keepSeed)
        {
            if (!PixelCreatureRuntime.Registry.TryGetFamily(_familyId, out _)) _familyId = PixelCreatureRuntime.Families[0].Id;
            if (!keepSeed) _seed = SeedRandom.Next(1, int.MaxValue).ToString();
            ulong seed = PixelCreatureRuntime.SeedFromText(_seed);
            var g = string.IsNullOrEmpty(_archetype)
                ? PixelCreatureRuntime.Sample(_familyId, seed)
                : PixelCreatureRuntime.SampleArchetype(_familyId, _archetype, seed);
            SetGenome(g);
        }

        public void SetGenome(CreatureGenome genome)
        {
            try
            {
                _model = PixelCreatureRuntime.Build(genome);
            }
            catch (Exception e)
            {
                _status = e.Message;
                return;
            }
            _genome = genome;
            _familyId = genome.FamilyId;
            _presetJson = PixelCreatureRuntime.SavePreset(genome);
            _motor = new CreatureMotor(_model, PixelCreatureRuntime.Registry, new MotorOptions { Facing = FacingMode.Eight });
            double yaw = PixelCamera.DirectionYaw(_dir);
            _motor.Teleport(Vec3.Zero, yaw);
            _motor.FacingOverride = yaw;
            _clock.Reset();
            _status = _model.Warnings.Count > 0 ? string.Join(" ", _model.Warnings) : string.Empty;
            RenderPreview();
            Repaint();
        }

        private float Density => _density + 1;

        // ------------------------------------------------------------------ animation

        private void Tick()
        {
            if (_motor == null || _model == null) return;
            double now = EditorApplication.timeSinceStartup;
            double dt = Math.Min(0.25, Math.Max(0, now - _lastTime));
            _lastTime = now;
            if (!_playing) return;
            int steps = _clock.Advance(dt);
            for (int i = 0; i < steps; i++)
            {
                ApplyClipInputs(_clock.StepSize);
                _motor.Step(_clock.StepSize);
            }
            _sinceRender += dt;
            if (steps > 0 && _sinceRender >= 1.0 / 15.0)
            {
                _sinceRender = 0;
                RenderPreview();
                Repaint();
            }
        }

        private void ApplyClipInputs(double dt)
        {
            var m = _motor!;
            double yaw = PixelCamera.DirectionYaw(_dir);
            m.FacingOverride = yaw;
            var fwd = new Vec3(DMath.Cos(yaw), 0, -DMath.Sin(yaw));
            m.DesiredVelocity = _clip == 1 ? fwd * _model!.Motion.WalkSpeed : (_clip == 2 ? fwd * _model!.Motion.RunSpeed : Vec3.Zero);
            m.Resting = _clip == 5;
            _sinceCommand += dt;
            if (_clip == 3 && !m.ActionActive && _sinceCommand > 0.6) { m.TriggerAction(); _sinceCommand = 0; }
            if (_clip == 4 && _sinceCommand > 1.4) { m.TriggerHit(new Vec3(-fwd.X, 0, -fwd.Z)); _sinceCommand = 0; }
        }

        private void RenderPreview()
        {
            if (_model == null || _motor == null) return;
            var c = _model.Anatomy.Canvas.Scaled(Density);
            if (_frame == null || _frame.Width != c.Width || _frame.Height != c.Height)
            {
                _frame = new PixelFrame(c.Width, c.Height);
                _rgba = new byte[c.Width * c.Height * 4];
                _flipped = new byte[_rgba.Length];
            }
            _frame.OriginX = c.OriginX;
            _frame.OriginY = c.OriginY;
            var view = ViewSpec.Default(_motor.Yaw, Density);
            view.DrawShadow = _shadow;
            _renderer.Render(_model.Anatomy, _model.Palette, _model.Surface, _motor.Pose, view, _frame);
            _model.Palette.BuildLut(_lut, _motor.HitFlash);
            _frame.ToRgba(_lut, _rgba, _model.Palette.WaterTint);
            int row = c.Width * 4;
            for (int y = 0; y < c.Height; y++) Buffer.BlockCopy(_rgba, y * row, _flipped, (c.Height - 1 - y) * row, row);
            if (_preview == null || _preview.width != c.Width || _preview.height != c.Height)
            {
                if (_preview != null) DestroyImmediate(_preview);
                _preview = new Texture2D(c.Width, c.Height, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave,
                };
            }
            _preview.SetPixelData(_flipped, 0);
            _preview.Apply(false, false);
        }

        // ------------------------------------------------------------------ GUI

        private void OnGUI()
        {
            DrawHeader();
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true)))
                {
                    DrawPreview();
                    DrawPreviewControls();
                }
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(Mathf.Max(300, position.width * 0.38f))))
                {
                    DrawGenes();
                }
            }
            DrawOutput();
            if (!string.IsNullOrEmpty(_status)) EditorGUILayout.HelpBox(_status, MessageType.Info);
        }

        private void DrawHeader()
        {
            var families = PixelCreatureRuntime.Families;
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                int fi = Math.Max(0, families.ToList().FindIndex(f => f.Id == _familyId));
                int nf = EditorGUILayout.Popup(fi, families.Select(PixelCreatureRuntime.FamilyLabel).ToArray(), EditorStyles.toolbarPopup, GUILayout.Width(260));
                if (nf != fi) { _familyId = families[nf].Id; _archetype = string.Empty; NewCreation(keepSeed: true); GUIUtility.ExitGUI(); }
                var fam = families[Math.Max(0, families.ToList().FindIndex(f => f.Id == _familyId))];
                var archLabels = new[] { "Any archetype" }.Concat(fam.Archetypes.Select(a => a.Label)).ToArray();
                int ai = string.IsNullOrEmpty(_archetype) ? 0 : 1 + fam.Archetypes.ToList().FindIndex(a => a.Id == _archetype);
                int na = EditorGUILayout.Popup(Math.Max(0, ai), archLabels, EditorStyles.toolbarPopup, GUILayout.Width(170));
                if (na != ai) { _archetype = na == 0 ? string.Empty : fam.Archetypes[na - 1].Id; NewCreation(keepSeed: true); GUIUtility.ExitGUI(); }
                GUILayout.Label("Seed", GUILayout.Width(32));
                string s = EditorGUILayout.DelayedTextField(_seed, EditorStyles.toolbarTextField, GUILayout.Width(110));
                if (s != _seed) { _seed = s; NewCreation(keepSeed: true); GUIUtility.ExitGUI(); }
                if (GUILayout.Button("New", EditorStyles.toolbarButton, GUILayout.Width(44))) { NewCreation(keepSeed: false); GUIUtility.ExitGUI(); }
                GUILayout.FlexibleSpace();
                GUILayout.Label("Mutation", GUILayout.Width(56));
                _mutation = GUILayout.HorizontalSlider(_mutation, 0.05f, 1f, GUILayout.Width(80));
                if (GUILayout.Button("Mutate", EditorStyles.toolbarButton, GUILayout.Width(56)) && _genome != null)
                {
                    var child = GenomeOps.Mutate(_genome, new MutationSettings { Strength = _mutation }, (ulong)SeedRandom.Next(1, int.MaxValue));
                    SetGenome(child);
                    GUIUtility.ExitGUI();
                }
            }
            if (_genome != null && _model != null)
            {
                string arch = _genome.Schema.Contains("form") ? _genome.GetChoice("form") : "-";
                string archLabel = _model.Family.Archetypes.FirstOrDefault(a => a.Id == arch)?.Label ?? arch;
                EditorGUILayout.LabelField($"{_genome.Name}  ·  {archLabel}  ·  {PixelCreatureRuntime.FamilyLabel(_model.Family)}", EditorStyles.boldLabel);
            }
        }

        private void DrawPreview()
        {
            var rect = GUILayoutUtility.GetRect(320, 10000, 240, 10000, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            EditorGUI.DrawRect(rect, new Color(0.24f, 0.3f, 0.26f));
            if (_preview == null) return;
            int zoom = _zoom > 0 ? _zoom : Math.Max(1, Math.Min((int)(rect.width / _preview.width), (int)(rect.height / _preview.height)));
            float w = _preview.width * zoom, h = _preview.height * zoom;
            var r = new Rect(Mathf.Round(rect.x + (rect.width - w) * 0.5f), Mathf.Round(rect.y + (rect.height - h) * 0.5f), w, h);
            GUI.DrawTexture(r, _preview, ScaleMode.StretchToFill, true);
        }

        private void DrawPreviewControls()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                int clip = GUILayout.Toolbar(_clip, ClipLabels);
                if (clip != _clip) { _clip = clip; _sinceCommand = 10; if (_motor != null) _motor.Resting = clip == 5; }
                _playing = GUILayout.Toggle(_playing, _playing ? "Pause" : "Play", "Button", GUILayout.Width(54));
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(96)))
                {
                    for (int row = 0; row < 3; row++)
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            for (int col = 0; col < 3; col++)
                            {
                                int k = row * 3 + col, d = DirGrid[k];
                                if (d < 0) { GUILayout.Space(30); continue; }
                                bool on = GUILayout.Toggle(_dir == d, DirArrows[k], "Button", GUILayout.Width(28), GUILayout.Height(20));
                                if (on && _dir != d) { _dir = d; if (_motor != null) _motor.FacingOverride = PixelCamera.DirectionYaw(d); }
                            }
                        }
                    }
                }
                using (new EditorGUILayout.VerticalScope())
                {
                    int dens = EditorGUILayout.Popup("Pixel density", _density, DensityLabels);
                    if (dens != _density) { _density = dens; RenderPreview(); }
                    _zoom = EditorGUILayout.IntSlider("Zoom (0 = fit)", _zoom, 0, 12);
                    bool shadow = EditorGUILayout.Toggle("Ground shadow", _shadow);
                    if (shadow != _shadow) { _shadow = shadow; RenderPreview(); }
                    if (_model != null)
                    {
                        var c = _model.Anatomy.Canvas.Scaled(Density);
                        EditorGUILayout.LabelField($"Sprite {c.Width}x{c.Height} px · {_model.Anatomy.Primitives.Count} primitives · {_model.Anatomy.Skeleton.Count} bones", EditorStyles.miniLabel);
                    }
                }
            }
        }

        private void DrawGenes()
        {
            if (_genome == null) return;
            var schema = _genome.Schema;
            _showAdvanced = EditorGUILayout.ToggleLeft("Show advanced genes", _showAdvanced);
            _geneScroll = EditorGUILayout.BeginScrollView(_geneScroll);
            CreatureGenome? changed = null;
            foreach (var group in schema.Groups)
            {
                if (!_foldouts.TryGetValue(group.Id, out bool open)) open = group.Stream == GeneStream.Anatomy;
                open = EditorGUILayout.Foldout(open, $"{group.Label}  ({group.Stream})", true);
                _foldouts[group.Id] = open;
                if (!open) continue;
                EditorGUI.indentLevel++;
                foreach (var def in schema.Genes)
                {
                    if (def.Group != group.Id || (def.Advanced && !_showAdvanced)) continue;
                    double v = _genome.Get(def.Id);
                    var label = new GUIContent(def.Label, def.Description);
                    double nv = v;
                    switch (def.Kind)
                    {
                        case GeneKind.Choice:
                            nv = EditorGUILayout.Popup(label, (int)Math.Round(v), def.Options.Select(o => new GUIContent(o.Label)).ToArray());
                            break;
                        case GeneKind.Bool:
                            nv = EditorGUILayout.Toggle(label, v >= 0.5) ? 1 : 0;
                            break;
                        case GeneKind.Int:
                            nv = EditorGUILayout.IntSlider(label, (int)Math.Round(v), (int)def.Min, (int)def.Max);
                            break;
                        default:
                            nv = EditorGUILayout.Slider(label, (float)v, (float)def.Min, (float)def.Max);
                            break;
                    }
                    if (Math.Abs(nv - v) > 1e-6) changed = _genome.With(def.Id, nv);
                }
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndScrollView();
            if (changed != null) SetGenome(changed);
        }

        private void DrawOutput()
        {
            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                _folder = EditorGUILayout.TextField("Output folder", _folder);
                if (GUILayout.Button("...", GUILayout.Width(28)))
                {
                    string abs = EditorUtility.OpenFolderPanel("Output folder", Application.dataPath, string.Empty);
                    if (!string.IsNullOrEmpty(abs))
                    {
                        string data = Application.dataPath.Replace('\\', '/');
                        abs = abs.Replace('\\', '/');
                        if (abs.StartsWith(data, StringComparison.Ordinal)) _folder = "Assets" + abs.Substring(data.Length);
                        else _status = "Choose a folder inside Assets.";
                    }
                }
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                _pixelsPerUnit = Mathf.Max(1, EditorGUILayout.FloatField("Pixels per unit", _pixelsPerUnit));
                _fpsIndex = EditorGUILayout.Popup("Frames per second", _fpsIndex, FpsOptions.Select(f => f.ToString()).ToArray());
                _directionMode = EditorGUILayout.Popup(_directionMode, DirectionModes, GUILayout.Width(130));
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                var kinds = (ClipKind[])Enum.GetValues(typeof(ClipKind));
                if (_clipToggles.Length != kinds.Length) _clipToggles = Enumerable.Repeat(true, kinds.Length).ToArray();
                for (int i = 0; i < kinds.Length; i++)
                    _clipToggles[i] = GUILayout.Toggle(_clipToggles[i], SpritesheetExporter.ClipName(kinds[i]), "Button");
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(_model == null))
                {
                    if (GUILayout.Button("Save Preset")) SavePreset();
                    if (GUILayout.Button("Export Sprite Sheet (PNG + JSON)")) Run(() => PixelCreatureBaker.ExportSpriteSheet(_model!, _folder, Options()), "Exported");
                    if (GUILayout.Button("Bake Sprites & Animations")) Run(() => PixelCreatureBaker.Bake(_model!, _folder, Options()), "Baked into");
                    if (GUILayout.Button("Create Actor in Scene")) CreateActor();
                }
            }
        }

        private BakeOptions Options()
        {
            var kinds = (ClipKind[])Enum.GetValues(typeof(ClipKind));
            var o = new BakeOptions { Density = Density, PixelsPerUnit = _pixelsPerUnit, Fps = FpsOptions[Mathf.Clamp(_fpsIndex, 0, FpsOptions.Length - 1)], DrawShadow = _shadow };
            o.Clips.Clear();
            for (int i = 0; i < kinds.Length; i++) if (i >= _clipToggles.Length || _clipToggles[i]) o.Clips.Add(kinds[i]);
            if (o.Clips.Count == 0) o.Clips.Add(ClipKind.Idle);
            o.Directions.Clear();
            if (_directionMode == 0) o.Directions.AddRange(new[] { 0, 1, 2, 3, 4, 5, 6, 7 });
            else if (_directionMode == 1) o.Directions.AddRange(new[] { 0, 2, 4, 6 });
            else o.Directions.Add(_dir);
            return o;
        }

        private void Run(Func<string> action, string verb)
        {
            try
            {
                EditorUtility.DisplayProgressBar("Procedural Pixel Creations", "Rendering frames...", 0.5f);
                string path = action();
                _status = $"{verb} {path}";
                var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                if (asset != null) EditorGUIUtility.PingObject(asset);
            }
            catch (Exception e)
            {
                _status = e.Message;
                Debug.LogException(e);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private string SavePresetAsset()
        {
            PixelCreatureBaker.EnsureFolder($"{_folder}/Presets");
            string path = AssetDatabase.GenerateUniqueAssetPath($"{_folder}/Presets/{PixelCreatureBaker.SafeName(_genome!.Name)}.json");
            File.WriteAllText(path, PixelCreatureRuntime.SavePreset(_genome));
            AssetDatabase.ImportAsset(path);
            return path;
        }

        private void SavePreset()
        {
            if (_genome == null) return;
            string path = SavePresetAsset();
            _status = $"Saved {path}";
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<TextAsset>(path));
        }

        private void CreateActor()
        {
            if (_genome == null) return;
            var preset = AssetDatabase.LoadAssetAtPath<TextAsset>(SavePresetAsset());
            var go = new GameObject(_genome.Name, typeof(SpriteRenderer));
            Undo.RegisterCreatedObjectUndo(go, "Create pixel creation");
            var actor = go.AddComponent<PixelCreatureActor>();
            actor.Preset = preset;
            actor.PixelDensity = Density;
            actor.PixelsPerUnit = _pixelsPerUnit;
            actor.BuildFromSettings(async: false);
            if (SceneView.lastActiveSceneView != null) go.transform.position = new Vector3(SceneView.lastActiveSceneView.pivot.x, SceneView.lastActiveSceneView.pivot.y, 0);
            Selection.activeGameObject = go;
            _status = $"Created {go.name} (preset {AssetDatabase.GetAssetPath(preset)}).";
        }
    }
}
