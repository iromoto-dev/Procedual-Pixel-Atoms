// Procedural Pixel Creations for Unity - a living creation on a SpriteRenderer.
//
// Add the component to a GameObject (a SpriteRenderer is added automatically), pick a family, a seed and
// optionally a biome, or assign a preset, and press Play. The actor builds its model (on a worker thread
// where available), simulates the motor with a fixed 60 Hz step that does not depend on the frame rate,
// rasterizes the pose at the animation frame rate into a point-filtered texture and moves its transform
// over the ground. In edit mode it shows the creation in its rest pose.
//
// Units: the core works in ground pixels. 'pixelsPerUnit' converts them to Unity units; the sprite is
// rendered at 'pixelDensity' output pixels per ground pixel and drawn at pixelsPerUnit * density, so the
// world size never depends on the density (2 = HD).
//
// Biomes: the biome adapts newly sampled creations (desert cacti, snow coats ...) and dresses the sprite
// with its overlay (snow caps, sand, moss, mud, wet glints, colour grade). Presets keep their genes; only
// the overlay applies to them.

#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using PixelCreatures.Core;
using PixelCreatures.Core.Anatomy;
using PixelCreatures.Core.Genetics;
using PixelCreatures.Core.Motion;
using PixelCreatures.Core.Palettes;
using PixelCreatures.Core.Rendering;
using UnityEngine;
using UnityEngine.Events;

namespace PixelCreatures.Unity
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    [AddComponentMenu("Procedural Pixel Creations/Pixel Creature Actor")]
    public sealed class PixelCreatureActor : MonoBehaviour
    {
        [Serializable] public sealed class StringEvent : UnityEvent<string> { }

        /// <summary>Largest sprite edge in pixels (the rasterizer's frame limit).</summary>
        public const int MaxSpriteSize = 1024;

        [Header("Creation")]
        [Tooltip("Optional ppc.creature preset (JSON). When set it is used instead of family and seed.")]
        [SerializeField] private TextAsset? preset;
        [Tooltip("Family id (quadruped, biped, mythical, monster, flora, prop, ...).")]
        [SerializeField] private string familyId = "quadruped";
        [Tooltip("Seed text: a number or any text.")]
        [SerializeField] private string seed = "1";
        [Tooltip("Biome id (temperate, desert, snow, jungle, swamp, volcanic): adapts sampled creations and dresses the sprite.")]
        [SerializeField] private string biome = Biomes.TemperateId;
        [Tooltip("Strength of the biome look (snow, sand, moss, mud, grade). 0 = off.")]
        [SerializeField, Range(0f, 2f)] private float biomeIntensity = 1f;

        [Header("Pixels")]
        [Tooltip("Output pixels per ground pixel. 2 = HD with high density anatomy, 1 = classic. 0 = PixelCreatureRuntime.DefaultDensity.")]
        [SerializeField, Range(0f, 4f)] private float pixelDensity;
        [Tooltip("Ground pixels per Unity unit. 0 = PixelCreatureRuntime.DefaultPixelsPerUnit.")]
        [SerializeField, Min(0f)] private float pixelsPerUnit;
        [Tooltip("Rendered poses per second (pixel-art animation rate). 0 = every frame.")]
        [SerializeField, Range(0, 60)] private int animationFps = 15;
        [SerializeField] private bool drawShadow = true;
        [SerializeField, Range(0, 2)] private int quality = 2;

        [Header("Behaviour")]
        [SerializeField] private FacingMode facing = FacingMode.Eight;
        [Tooltip("Facing direction at start: 0 = east, 2 = north, 4 = west, 6 = south, 7 = south east.")]
        [SerializeField, Range(0, 7)] private int startDirection = 7;
        [Tooltip("Move this transform with the creature's ground position.")]
        [SerializeField] private bool moveTransform = true;
        [Tooltip("Set SpriteRenderer.sortingOrder from the ground depth every frame, so creations further south " +
                 "draw in front. Works with every render pipeline; alternatively use a custom transparency sort axis (0, 1, 0).")]
        [SerializeField] private bool sortByGroundDepth;
        [Tooltip("Added to the depth based sorting order.")]
        [SerializeField] private int sortingOrderOffset;
        [Tooltip("Skip rasterization while no camera sees the sprite (simulation continues).")]
        [SerializeField] private bool cullWhenInvisible = true;
        [Tooltip("Build models on a worker thread (falls back to the main thread on WebGL).")]
        [SerializeField] private bool buildAsync = true;
        [SerializeField, Range(0f, 4f)] private float timeScale = 1f;
        [SerializeField] private bool paused;

        [Header("Events")]
        public UnityEvent onModelChanged = new UnityEvent();
        public StringEvent onBuildFailed = new StringEvent();
        public StringEvent onMotionStateChanged = new StringEvent();

        // ------------------------------------------------------------------ public state

        public CreatureModel? Model { get; private set; }
        public CreatureMotor? Motor { get; private set; }
        public CreatureGenome? Genome => Model?.Genome;

        /// <summary>
        /// Ground position (x east, y south, in ground pixels). Setting it places the creation there instantly
        /// (like Teleport); use MoveVelocity to walk.
        /// </summary>
        public Vector2 GroundPosition
        {
            get => _ground;
            set => Teleport(value);
        }
        /// <summary>Desired ground velocity in ground pixels per second (x east, y south).</summary>
        public Vector2 MoveVelocity { get; set; }
        /// <summary>Optional veto for every simulation step (from, to). A blocked step keeps the creation in place.</summary>
        public Func<Vector2, Vector2, bool>? CanMove { get; set; }
        /// <summary>Optional terrain height (x, y in ground pixels) for planted feet.</summary>
        public Func<double, double, double>? GroundHeightAt { get; set; }
        public double WaterLevel { get; set; } = double.NaN;
        /// <summary>Overlay used instead of the biome's (code driven looks). Null = the biome's overlay.</summary>
        public BiomeOverlay? OverlayOverride { get => _overlayOverride; set { _overlayOverride = value; RequestRender(); } }
        public Texture2D? Texture => _texture;
        public Sprite? Sprite => _sprite;
        public RenderStats LastStats { get; private set; }
        public long RenderCount { get; private set; }
        public string LastError { get; private set; } = string.Empty;
        public bool BuildPending => _buildTask != null;
        public MotionStateKind State => Motor?.State ?? MotionStateKind.Idle;

        public string FamilyId { get => familyId; set => familyId = value ?? string.Empty; }
        public string Seed { get => seed; set => seed = value ?? string.Empty; }
        public TextAsset? Preset { get => preset; set => preset = value; }
        /// <summary>Biome id. Changing it affects the next build (sampling) and the look immediately.</summary>
        public string Biome { get => biome; set { biome = string.IsNullOrEmpty(value) ? Biomes.TemperateId : value; RequestRender(); } }
        public float BiomeIntensity { get => biomeIntensity; set { biomeIntensity = Mathf.Clamp(value, 0f, 2f); RequestRender(); } }
        public int AnimationFps { get => animationFps; set => animationFps = Mathf.Clamp(value, 0, 60); }
        public bool Paused { get => paused; set => paused = value; }
        public float TimeScale { get => timeScale; set => timeScale = Mathf.Max(0f, value); }
        public bool MoveTransform { get => moveTransform; set => moveTransform = value; }
        public bool CullWhenInvisible { get => cullWhenInvisible; set => cullWhenInvisible = value; }
        public bool SortByGroundDepth { get => sortByGroundDepth; set => sortByGroundDepth = value; }
        public int SortingOrderOffset { get => sortingOrderOffset; set => sortingOrderOffset = value; }
        public bool DrawShadow { get => drawShadow; set { drawShadow = value; RequestRender(); } }

        public float PixelDensity
        {
            get => pixelDensity;
            set { pixelDensity = Mathf.Clamp(value, 0f, 4f); UpdateCanvas(); }
        }

        public float PixelsPerUnit
        {
            get => pixelsPerUnit;
            set { pixelsPerUnit = Mathf.Max(0f, value); _spriteDirty = true; RequestRender(); }
        }

        /// <summary>Requested density (at least 1).</summary>
        public double RequestedDensity => Math.Max(1.0, pixelDensity > 0 ? pixelDensity : PixelCreatureRuntime.DefaultDensity);

        /// <summary>
        /// Density actually rendered: the requested density, reduced for very large creations so the sprite
        /// stays within MaxSpriteSize (with room for poses that reach beyond the rest canvas).
        /// </summary>
        public double EffectiveDensity
        {
            get
            {
                double d = RequestedDensity;
                var m = Model;
                if (m == null) return d;
                int edge = Math.Max(m.Anatomy.Canvas.Width, m.Anatomy.Canvas.Height);
                double limit = MaxSpriteSize * 0.85 / Math.Max(1, edge);
                return Math.Max(1.0, Math.Min(d, limit));
            }
        }

        public float EffectivePixelsPerUnit => pixelsPerUnit > 0 ? pixelsPerUnit : Mathf.Max(1f, PixelCreatureRuntime.DefaultPixelsPerUnit);

        /// <summary>The overlay the sprite is rendered with (the biome look for this creation, or the override).</summary>
        public BiomeOverlay EffectiveOverlay =>
            _overlayOverride ?? PixelCreatureRuntime.GetBiome(biome).OverlayFor(Model, biomeIntensity);

        // ------------------------------------------------------------------ internals

        private SpriteRenderer? _spriteRenderer;
        private Texture2D? _texture;
        private Sprite? _sprite;
        private bool _spriteDirty = true;
        private PixelFrame? _frame;
        private CanvasSpec? _canvas;
        private byte[] _rgba = Array.Empty<byte>();
        private byte[] _flipped = Array.Empty<byte>();
        private readonly byte[] _lut = new byte[256 * 4];
        private CreaturePose? _renderPose;
        private CreaturePalette? _renderPalette;
        private ViewSpec _view;
        private double _renderFlash;
        private readonly FixedClock _clock = new FixedClock();
        private double _sinceRender = 1e9;
        private bool _dirty = true, _phased, _rastered, _rasterOk;
        private int _pendingSteps;
        private int _growL, _growR, _growT, _growB;
        private MotionStateKind _lastState = MotionStateKind.Idle;
        private double? _facingRequest;
        private bool _restRequest;
        private Vector2 _ground;
        private bool _groundSet;
        private BiomeOverlay? _overlayOverride;
        private Task<CreatureModel>? _buildTask;
        private CancellationTokenSource? _buildCancel;
        private CreatureGenome? _requestedGenome;
        private CreatureGenome? _pendingGenome;
        /// <summary>
        /// CreationKey() of the inspector settings the shown (or building) creation corresponds to. Inspector
        /// changes rebuild only when the key changes; creations set from code are never replaced by a
        /// view-only change (density, shadow, biome look ...).
        /// </summary>
        private string _appliedKey = string.Empty;
        private string _rasterError = string.Empty;
#if UNITY_EDITOR
        private bool _settingsChanged;
#endif

        /// <summary>Play mode for this object (false in edit mode and in prefab mode, which also run [ExecuteAlways]).</summary>
        private bool Live => Application.IsPlaying(gameObject);

        // ------------------------------------------------------------------ lifecycle

        private void OnEnable()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
            if (EditorBuildInProgress()) return;
#if UNITY_EDITOR
            _settingsChanged = false;
#endif
            if (_buildTask != null) return; // requested while disabled: Update applies it
            if (_pendingGenome != null)
            {
                // a build was cancelled when the actor was disabled: start it again (models are cached)
                var g = _pendingGenome;
                _pendingGenome = null;
                SetGenome(g, async: Live && buildAsync);
            }
            else if (Model == null || CreationKey() != _appliedKey) BuildFromSettings(async: Live && buildAsync);
            else
            {
                UpdateCanvas();
                if (!Live) RenderImmediate();
            }
        }

        private void Start()
        {
            if (!Live) return;
            if (!_groundSet) _ground = PixelCreatureRuntime.WorldToGround(transform.position, EffectivePixelsPerUnit);
            if (Motor != null) Motor.Teleport(new Vec3(_ground.x, 0, _ground.y), Motor.Yaw);
        }

        private void OnDisable()
        {
            PixelCreatureRenderQueue.Remove(this);
            // a build in flight (or finished but not applied yet) is started again when the actor is enabled
            if (_buildTask != null && _requestedGenome != null) _pendingGenome = _requestedGenome;
            CancelBuild();
            // edit mode (script reloads, entering play mode, disabling): free the preview texture and sprite;
            // OnDestroy is not called on a domain reload, so this is the only place they can be released
            if (!Live) ReleaseGraphics();
        }

        private void OnDestroy()
        {
            CancelBuild();
            ReleaseGraphics();
        }

        private void OnValidate()
        {
            _spriteDirty = true;
#if UNITY_EDITOR
            _settingsChanged = true;
            // rebuild after the inspector change has been applied (objects cannot be created inside OnValidate)
            UnityEditor.EditorApplication.delayCall -= ApplyValidatedSettings;
            UnityEditor.EditorApplication.delayCall += ApplyValidatedSettings;
#endif
        }

#if UNITY_EDITOR
        private void ApplyValidatedSettings()
        {
            // disabled actors pick up changed settings in OnEnable
            if (this == null || !isActiveAndEnabled || !_settingsChanged) return;
            _settingsChanged = false;
            if ((Model != null || _buildTask != null) && _appliedKey == CreationKey())
            {
                // only view settings changed (density, shadow, biome look ...)
                UpdateCanvas();
                if (!Live) RenderImmediate();
            }
            else BuildFromSettings(async: Live && buildAsync);
        }
#endif

        private static bool EditorBuildInProgress()
        {
#if UNITY_EDITOR
            return UnityEditor.BuildPipeline.isBuildingPlayer;
#else
            return false;
#endif
        }

        // ------------------------------------------------------------------ genome & model

        private string CreationKey() =>
            preset != null ? "preset:" + preset.GetInstanceID() + ":" + preset.text.Length + ":" + preset.text.GetHashCode() : familyId + "|" + seed + "|" + biome;

        /// <summary>Rebuilds from the serialized preset or family + seed (+ biome).</summary>
        public void BuildFromSettings(bool async)
        {
            CreatureGenome genome;
            string key = CreationKey();
            try
            {
                if (preset != null)
                {
                    var res = PixelCreatureRuntime.LoadPreset(preset.text);
                    if (!res.Ok || res.Genome == null) { Fail($"Preset '{preset.name}': {res.Describe()}"); return; }
                    genome = res.Genome;
                }
                else
                {
                    if (!PixelCreatureRuntime.Registry.TryGetFamily(familyId, out _)) { Fail($"Unknown family '{familyId}'."); return; }
                    genome = PixelCreatureRuntime.Sample(familyId, PixelCreatureRuntime.SeedFromText(seed), biome);
                }
            }
            catch (Exception e)
            {
                Fail(e.Message);
                return;
            }
            SetGenome(genome, async);
            _appliedKey = key;
        }

        /// <summary>Replaces the creation. Async builds keep showing the old one until the new model is ready.</summary>
        public void SetGenome(CreatureGenome genome, bool async = true)
        {
            CancelBuild();
            _requestedGenome = genome;
            _pendingGenome = null;
            _appliedKey = CreationKey();
            if (!async || !PixelCreatureRuntime.ThreadsSupported || !Live)
            {
                try { ApplyModel(PixelCreatureRuntime.Build(genome)); }
                catch (Exception e) { Fail(e.Message); }
                return;
            }
            _buildCancel = new CancellationTokenSource();
            var token = _buildCancel.Token;
            _buildTask = Task.Run(() => PixelCreatureRuntime.Factory.Build(genome, token), token);
        }

        /// <summary>Loads a ppc.creature preset from JSON text.</summary>
        public bool LoadPreset(string json, bool async = false)
        {
            var res = PixelCreatureRuntime.LoadPreset(json);
            if (!res.Ok || res.Genome == null) { Fail(res.Describe()); return false; }
            SetGenome(res.Genome, async);
            return true;
        }

        /// <summary>Samples a new creation of the current family (and biome) from a seed.</summary>
        public void Reroll(string newSeed, bool async = true)
        {
            preset = null;
            seed = newSeed;
            BuildFromSettings(async);
        }

        /// <summary>Uses an already built model (keeps position, facing and rest state).</summary>
        public void ApplyModel(CreatureModel model)
        {
            var old = Motor;
            // a snapshot taken earlier in this frame belongs to the previous model
            PixelCreatureRenderQueue.Remove(this);
            _renderPalette = null;
            Model = model;
            familyId = model.Genome.FamilyId;
            _appliedKey = CreationKey();
            _spriteDirty = true;
            Motor = new CreatureMotor(model, PixelCreatureRuntime.Registry, new MotorOptions { Facing = facing, InternalDrive = true });
            double yaw = old?.Yaw ?? PixelCamera.DirectionYaw(startDirection);
            Motor.Teleport(new Vec3(_ground.x, 0, _ground.y), yaw);
            // only an explicit FaceYaw/FaceDirection request pins the facing; otherwise the creature keeps
            // the direction it last moved in
            Motor.FacingOverride = _facingRequest;
            if ((old != null && old.Resting) || _restRequest) Motor.SnapRest(true);
            Motor.Resting = _restRequest;
            _renderPose = new CreaturePose(model.Anatomy.Skeleton);
            UpdateCanvas();
            _clock.Reset();
            LastError = string.Empty;
            if (!Live) RenderImmediate();
            onModelChanged.Invoke();
        }

        private void UpdateCanvas()
        {
            var m = Model;
            if (m == null) return;
            var c = m.Anatomy.Canvas.Scaled(EffectiveDensity);
            if (_canvas == null || c.Width != _canvas.Width || c.Height != _canvas.Height || c.OriginX != _canvas.OriginX || c.OriginY != _canvas.OriginY)
            {
                _canvas = c;
                _spriteDirty = true;
            }
            RequestRender();
        }

        private void CancelBuild()
        {
            if (_buildCancel != null)
            {
                _buildCancel.Cancel();
                _buildCancel.Dispose();
                _buildCancel = null;
            }
            _buildTask = null;
        }

        private void PollBuild()
        {
            var task = _buildTask;
            if (task == null || !task.IsCompleted) return;
            _buildTask = null;
            _buildCancel?.Dispose();
            _buildCancel = null;
            if (task.IsCanceled) return;
            if (task.IsFaulted)
            {
                var e = task.Exception?.GetBaseException();
                Fail(e?.Message ?? "Build failed.");
                return;
            }
            ApplyModel(task.Result);
        }

        /// <summary>Rendering errors repeat every frame: report each one once.</summary>
        internal void ReportRenderError(string message)
        {
            if (message != LastError) Fail(message);
        }

        internal void Fail(string message)
        {
            LastError = message;
            Debug.LogWarning($"[PixelCreatureActor] {name}: {message}", this);
            onBuildFailed.Invoke(message);
        }

        // ------------------------------------------------------------------ commands

        public void FaceYaw(double yaw)
        {
            _facingRequest = yaw;
            if (Motor != null) Motor.FacingOverride = yaw;
        }

        /// <summary>
        /// 0 = east, 1 = north east, 2 = north ... 6 = south, 7 = south east. The creation turns there while it
        /// stands; the request ends when MoveVelocity steers it again.
        /// </summary>
        public void FaceDirection(int dir8) => FaceYaw(PixelCamera.DirectionYaw(dir8));

        public bool Resting
        {
            get => _restRequest;
            set { _restRequest = value; if (Motor != null) Motor.Resting = value; }
        }

        public void TriggerAction() => Motor?.TriggerAction();

        /// <summary>Hit from a ground direction (x east, y south) pointing from the creation to the attacker.</summary>
        public void TriggerHit(Vector2 fromDirection, float strength = 1f) =>
            Motor?.TriggerHit(new Vec3(fromDirection.x, 0, fromDirection.y), strength);

        /// <summary>Moves instantly (resets feet and spring chains).</summary>
        public void Teleport(Vector2 groundPosition)
        {
            _ground = groundPosition;
            _groundSet = true;
            Motor?.Teleport(new Vec3(groundPosition.x, 0, groundPosition.y), Motor.Yaw);
            SyncTransform();
            _dirty = true;
        }

        /// <summary>Advances a paused actor by animation frames.</summary>
        public void StepFrames(int frames)
        {
            int fps = animationFps > 0 ? animationFps : 60;
            _pendingSteps += Math.Max(1, frames) * Math.Max(1, (int)Math.Round(60.0 / fps));
        }

        public void RequestRender() => _dirty = true;

        // ------------------------------------------------------------------ frame loop

        private void Update()
        {
            PollBuild();
            if (Motor == null) return;
            if (!Live)
            {
                if (_dirty) RenderImmediate();
                // prefab merges and undo can reset the renderer's sprite: keep showing the preview
                else if (_sprite != null && _spriteRenderer != null && _spriteRenderer.sprite != _sprite) _spriteRenderer.sprite = _sprite;
                return;
            }
            Motor.Options.Facing = facing;
            Motor.Context.GroundHeightAt = GroundHeightAt;
            if (_facingRequest.HasValue && MoveVelocity.sqrMagnitude > 1e-4f)
            {
                // steering takes over from a FaceYaw request
                _facingRequest = null;
                Motor.FacingOverride = null;
            }
            int steps = paused ? 0 : _clock.Advance(Time.deltaTime * Math.Max(0f, timeScale));
            steps += _pendingSteps;
            _pendingSteps = 0;
            for (int i = 0; i < steps; i++)
            {
                var before = Motor.Position;
                Motor.DesiredVelocity = new Vec3(MoveVelocity.x, 0, MoveVelocity.y);
                Motor.Step(_clock.StepSize);
                if (CanMove != null && !CanMove(new Vector2((float)before.X, (float)before.Z), new Vector2((float)Motor.Position.X, (float)Motor.Position.Z)))
                {
                    // stay in place without resetting the gait (Teleport would snap the legs every step)
                    Motor.Context.Position = before;
                    Motor.Context.Velocity = Vec3.Zero;
                }
                _sinceRender += _clock.StepSize;
            }
            _ground = new Vector2((float)Motor.Position.X, (float)Motor.Position.Z);
            SyncTransform();
            if (sortByGroundDepth && _spriteRenderer != null)
                _spriteRenderer.sortingOrder = Mathf.Clamp(Mathf.RoundToInt(_ground.y) + sortingOrderOffset, short.MinValue, short.MaxValue);

            var state = Motor.State;
            if (state != _lastState)
            {
                _lastState = state;
                onMotionStateChanged.Invoke(state.ToString());
            }

            double interval = animationFps > 0 ? 1.0 / animationFps : 0;
            bool tick = steps > 0 && _sinceRender + 1e-9 >= interval;
            bool visible = !cullWhenInvisible || _spriteRenderer == null || _spriteRenderer.isVisible || !_rastered;
            if ((_dirty || tick) && visible)
            {
                if (!_phased && interval > 0)
                {
                    // spread the rasterization of a crowd over the frames of one animation interval
                    _phased = true;
                    _sinceRender = -interval * StableHash.Hash01((ulong)GetInstanceID(), 7, 11);
                }
                else _sinceRender = 0;
                _dirty = false;
                SnapshotView();
                PixelCreatureRenderQueue.Enqueue(this);
            }
        }

        private void SyncTransform()
        {
            if (!moveTransform || !Live) return;
            var p = PixelCreatureRuntime.GroundToWorld(_ground, EffectivePixelsPerUnit);
            var t = transform;
            t.position = new Vector3(p.x, p.y, t.position.z);
        }

        /// <summary>Main thread: copies everything the worker thread needs (pose, view, palette).</summary>
        private void SnapshotView()
        {
            if (Motor == null || _renderPose == null || Model == null) return;
            _renderPose.CopyFrom(Motor.Pose);
            _view = ViewSpec.Default(Motor.Yaw, EffectiveDensity);
            _view.DrawShadow = drawShadow;
            _view.Quality = quality;
            _view.WaterLevel = WaterLevel;
            _view.Overlay = EffectiveOverlay;
            _renderPalette = Model.PaletteFor(_view.Overlay);
            _renderFlash = Motor.HitFlash;
        }

        /// <summary>Simulates nothing, renders the current pose right now on the main thread (edit mode, tools, tests).</summary>
        public void RenderImmediate()
        {
            if (Motor == null || EditorBuildInProgress()) return;
            // edit mode: a disabled actor would create preview objects nothing releases (no OnDisable follows)
            if (!Live && !isActiveAndEnabled) { _dirty = true; return; }
            _dirty = false;
            SnapshotView();
            if (!PrepareRaster()) return;
            Rasterize(PixelCreatureRenderQueue.RendererForThisThread);
            Upload();
        }

        // ------------------------------------------------------------------ rendering (called by the render queue)

        /// <summary>Main thread: makes sure the frame buffers match the canvas.</summary>
        internal bool PrepareRaster()
        {
            if (Model == null || _renderPose == null || _canvas == null || _renderPalette == null) return false;
            var c = _canvas;
            if (c.Width > MaxSpriteSize || c.Height > MaxSpriteSize)
            {
                ReportRenderError($"The sprite would be {c.Width}x{c.Height} px; the limit is {MaxSpriteSize} px.");
                return false;
            }
            if (_frame == null || _frame.Width != c.Width || _frame.Height != c.Height)
            {
                _frame = new PixelFrame(c.Width, c.Height);
                _rgba = new byte[c.Width * c.Height * 4];
                _flipped = new byte[c.Width * c.Height * 4];
            }
            _frame.OriginX = c.OriginX;
            _frame.OriginY = c.OriginY;
            _rasterOk = false;
            return true;
        }

        /// <summary>Any thread: rasterizes the snapshot pose into the RGBA buffer (rows bottom-up for Unity). No Unity calls.</summary>
        internal void Rasterize(CreatureRenderer renderer)
        {
            var model = Model;
            var frame = _frame;
            var pose = _renderPose;
            var palette = _renderPalette;
            if (model == null || frame == null || pose == null || palette == null) return;
            try
            {
                var stats = renderer.Render(model.Anatomy, palette, model.Surface, pose, _view, frame);
                LastStats = stats;
                if (stats.Overflow)
                {
                    _growL = Math.Max(_growL, stats.OverflowLeft);
                    _growR = Math.Max(_growR, stats.OverflowRight);
                    _growT = Math.Max(_growT, stats.OverflowTop);
                    _growB = Math.Max(_growB, stats.OverflowBottom);
                }
                palette.BuildLut(_lut, _renderFlash);
                frame.ToRgba(_lut, _rgba, palette.WaterTint);
                int row = frame.Width * 4;
                for (int y = 0; y < frame.Height; y++)
                    Buffer.BlockCopy(_rgba, y * row, _flipped, (frame.Height - 1 - y) * row, row);
                _rasterOk = true;
            }
            catch (Exception e)
            {
                // reported on the main thread in Upload (Unity's logging is not called from workers)
                _rasterError = e.Message;
            }
        }

        /// <summary>Main thread: uploads the texture, grows the canvas if a pose did not fit.</summary>
        internal void Upload()
        {
            if (!string.IsNullOrEmpty(_rasterError))
            {
                string e = _rasterError;
                _rasterError = string.Empty;
                ReportRenderError(e);
                return;
            }
            if (_frame == null || _canvas == null || !_rasterOk) return;
            if (_growL + _growR + _growT + _growB > 0)
            {
                var c = _canvas;
                int l = _growL + 2, r = _growR + 2, t = _growT + 2, b = _growB + 2;
                _growL = _growR = _growT = _growB = 0;
                int w2 = Math.Min(MaxSpriteSize, c.Width + l + r), h2 = Math.Min(MaxSpriteSize, c.Height + t + b);
                if (w2 != c.Width || h2 != c.Height)
                {
                    // grown canvas (never beyond the frame limit); the origin moves with the added left/top margin
                    int ox = Math.Min(w2 - 1, c.OriginX + Math.Min(l, w2 - c.Width));
                    int oy = Math.Min(h2 - 1, c.OriginY + Math.Min(t, h2 - c.Height));
                    _canvas = new CanvasSpec(w2, h2, ox, oy);
                    _spriteDirty = true;
                    RequestRender();
                    if (!Live) RenderImmediate();
                    return;
                }
            }
            int w = _frame.Width, h = _frame.Height;
            if (_texture == null || _texture.width != w || _texture.height != h)
            {
                ReleaseObject(_texture);
                _texture = new Texture2D(w, h, TextureFormat.RGBA32, false)
                {
                    name = "PixelCreature",
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.DontSave,
                };
                _spriteDirty = true;
            }
            _texture.SetPixelData(_flipped, 0);
            _texture.Apply(false, false);
            if (_spriteDirty || _sprite == null)
            {
                ReleaseObject(_sprite);
                var pivot = new Vector2(_canvas.OriginX / (float)w, (h - _canvas.OriginY) / (float)h);
                _sprite = Sprite.Create(_texture, new Rect(0, 0, w, h), pivot, EffectivePixelsPerUnit * (float)EffectiveDensity, 0, SpriteMeshType.FullRect);
                _sprite.name = Model != null ? Model.Genome.Name : "PixelCreature";
                _sprite.hideFlags = HideFlags.DontSave;
                _spriteDirty = false;
            }
            if (_spriteRenderer == null) _spriteRenderer = GetComponent<SpriteRenderer>();
            if (_spriteRenderer != null && _spriteRenderer.sprite != _sprite) _spriteRenderer.sprite = _sprite;
            _rastered = true;
            RenderCount++;
        }

        /// <summary>
        /// Removes the edit mode preview sprite from the SpriteRenderer and frees it. Called before player builds
        /// process a scene (the preview is never saved and must not reach a build).
        /// </summary>
        public void ReleasePreview() => ReleaseGraphics();

        private void ReleaseGraphics()
        {
            if (_spriteRenderer == null) _spriteRenderer = GetComponent<SpriteRenderer>();
            if (_spriteRenderer != null && _sprite != null && _spriteRenderer.sprite == _sprite) _spriteRenderer.sprite = null;
            ReleaseObject(_sprite);
            ReleaseObject(_texture);
            _sprite = null;
            _texture = null;
            _spriteDirty = true;
            _rastered = false;
        }

        private static void ReleaseObject(UnityEngine.Object? o)
        {
            if (o == null) return;
            if (Application.isPlaying) Destroy(o);
            else DestroyImmediate(o);
        }

        // ------------------------------------------------------------------ gizmos

        private void OnDrawGizmosSelected()
        {
            if (Motor == null || Model == null) return;
            float ppu = EffectivePixelsPerUnit;
            Vector3 origin = transform.position;
            var pose = Motor.Pose.Skeleton;
            var sk = Model.Anatomy.Skeleton;
            double yaw = Motor.Yaw;
            Vector3 P(Vec3 p)
            {
                var o = PixelCreatureRuntime.CreatureToWorldOffset(p, yaw, ppu);
                return origin + new Vector3(o.x, o.y, 0);
            }
            Gizmos.color = new Color(0.3f, 0.9f, 1f, 0.9f);
            for (int i = 0; i < sk.Count; i++)
            {
                int parent = sk[i].Parent;
                if (parent >= 0) Gizmos.DrawLine(P(pose.World[parent].Origin), P(pose.World[i].Origin));
            }
            foreach (var c in Motor.Pose.Contacts)
            {
                Gizmos.color = c.Planted ? new Color(0.2f, 1f, 0.3f) : new Color(1f, 0.6f, 0.1f);
                Gizmos.DrawWireSphere(P(c.Position), 1.5f / ppu);
            }
        }
    }
}
