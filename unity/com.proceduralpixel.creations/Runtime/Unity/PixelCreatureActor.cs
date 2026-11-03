// Procedural Pixel Creations for Unity - a living creation on a SpriteRenderer.
//
// Add the component to a GameObject (a SpriteRenderer is added automatically), pick a family and a seed
// or assign a preset, and press Play. The actor builds its model (on a worker thread where available),
// simulates the motor with a fixed 60 Hz step that does not depend on the frame rate, rasterizes the
// pose at the animation frame rate into a point-filtered texture and moves its transform over the
// ground. In edit mode it shows the creation in its rest pose.
//
// Units: the core works in ground pixels. 'pixelsPerUnit' converts them to Unity units; the sprite is
// rendered at 'pixelDensity' output pixels per ground pixel and drawn at pixelsPerUnit * density, so the
// world size never depends on the density (2 = HD).

#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using PixelCreatures.Core;
using PixelCreatures.Core.Anatomy;
using PixelCreatures.Core.Genetics;
using PixelCreatures.Core.Motion;
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

        [Header("Creation")]
        [Tooltip("Optional ppc.creature preset (JSON). When set it is used instead of family and seed.")]
        [SerializeField] private TextAsset? preset;
        [Tooltip("Family id (quadruped, biped, mythical, monster, flora, prop, ...).")]
        [SerializeField] private string familyId = "quadruped";
        [Tooltip("Seed text: a number or any text.")]
        [SerializeField] private string seed = "1";

        [Header("Pixels")]
        [Tooltip("Output pixels per ground pixel. 2 = HD with high density anatomy, 1 = classic. 0 = PixelCreatureRuntime.DefaultDensity.")]
        [SerializeField, Min(0f)] private float pixelDensity;
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
        /// <summary>Ground position in ground pixels (x east, y south).</summary>
        public Vector2 GroundPosition { get; set; }
        /// <summary>Desired ground velocity in ground pixels per second (x east, y south).</summary>
        public Vector2 MoveVelocity { get; set; }
        /// <summary>Optional collision callback (from, to ground positions): false blocks the step.</summary>
        public Func<Vector2, Vector2, bool>? CanMove { get; set; }
        /// <summary>Optional terrain height (ground x, ground y) for foot placement.</summary>
        public Func<double, double, double>? GroundHeightAt { get; set; }
        /// <summary>Water surface height for swimmers (NaN = none): parts below are tinted.</summary>
        public double WaterLevel { get; set; } = double.NaN;
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
            set { pixelDensity = Mathf.Max(0f, value); if (Model != null) { _canvas = Model.Anatomy.Canvas.Scaled(EffectiveDensity); RequestRender(); } }
        }

        public float PixelsPerUnit
        {
            get => pixelsPerUnit;
            set { pixelsPerUnit = Mathf.Max(0f, value); _spriteDirty = true; RequestRender(); }
        }

        public double EffectiveDensity => Math.Max(1.0, pixelDensity > 0 ? pixelDensity : PixelCreatureRuntime.DefaultDensity);
        public float EffectivePixelsPerUnit => pixelsPerUnit > 0 ? pixelsPerUnit : Mathf.Max(1f, PixelCreatureRuntime.DefaultPixelsPerUnit);

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
        private ViewSpec _view;
        private readonly FixedClock _clock = new FixedClock();
        private double _sinceRender = 1e9;
        private bool _dirty = true, _phased, _rastered;
        private int _pendingSteps;
        private int _growL, _growR, _growT, _growB;
        private MotionStateKind _lastState = MotionStateKind.Idle;
        private double? _facingRequest;
        private bool _restRequest;
        private Task<CreatureModel>? _buildTask;
        private CancellationTokenSource? _buildCancel;
        private int _buildTicket;
#if UNITY_EDITOR
        private bool _settingsChanged;
#endif

        // ------------------------------------------------------------------ lifecycle

        private void OnEnable()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
            if (Model == null) BuildFromSettings(async: Application.isPlaying && buildAsync);
            else RequestRender();
        }

        private void Start()
        {
            if (!Application.isPlaying) return;
            GroundPosition = PixelCreatureRuntime.WorldToGround(transform.position, EffectivePixelsPerUnit);
            if (Motor != null) Motor.Teleport(new Vec3(GroundPosition.x, 0, GroundPosition.y), Motor.Yaw);
        }

        private void OnDisable()
        {
            PixelCreatureRenderQueue.Remove(this);
            CancelBuild();
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
            if (this == null || !isActiveAndEnabled || !_settingsChanged) return;
            _settingsChanged = false;
            if (Application.isPlaying) RequestRender();
            else BuildFromSettings(async: false);
        }
#endif

        // ------------------------------------------------------------------ genome & model

        /// <summary>Rebuilds from the serialized preset or family + seed.</summary>
        public void BuildFromSettings(bool async)
        {
            CreatureGenome genome;
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
                    genome = PixelCreatureRuntime.Sample(familyId, PixelCreatureRuntime.SeedFromText(seed));
                }
            }
            catch (Exception e)
            {
                Fail(e.Message);
                return;
            }
            SetGenome(genome, async);
        }

        /// <summary>Replaces the creation. Async builds keep showing the old one until the new model is ready.</summary>
        public void SetGenome(CreatureGenome genome, bool async = true)
        {
            CancelBuild();
            if (!async || !PixelCreatureRuntime.ThreadsSupported || !Application.isPlaying)
            {
                try { ApplyModel(PixelCreatureRuntime.Build(genome)); }
                catch (Exception e) { Fail(e.Message); }
                return;
            }
            _buildCancel = new CancellationTokenSource();
            var token = _buildCancel.Token;
            _buildTicket++;
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

        /// <summary>Samples a new creation of the current family from a seed.</summary>
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
            Model = model;
            familyId = model.Genome.FamilyId;
            Motor = new CreatureMotor(model, PixelCreatureRuntime.Registry, new MotorOptions { Facing = facing, InternalDrive = true });
            double yaw = old?.Yaw ?? PixelCamera.DirectionYaw(startDirection);
            Motor.Teleport(new Vec3(GroundPosition.x, 0, GroundPosition.y), yaw);
            // only an explicit FaceYaw/FaceDirection request pins the facing; otherwise the creature keeps
            // the direction it last moved in
            Motor.FacingOverride = _facingRequest;
            if ((old != null && old.Resting) || _restRequest) Motor.SnapRest(true);
            Motor.Resting = _restRequest;
            _renderPose = new CreaturePose(model.Anatomy.Skeleton);
            _canvas = model.Anatomy.Canvas.Scaled(EffectiveDensity);
            _clock.Reset();
            LastError = string.Empty;
            RequestRender();
            if (!Application.isPlaying) RenderImmediate();
            onModelChanged.Invoke();
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

        private void Fail(string message)
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
            GroundPosition = groundPosition;
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
            if (!Application.isPlaying)
            {
                if (_dirty) RenderImmediate();
                return;
            }
            Motor.Options.Facing = facing;
            if (_facingRequest.HasValue && MoveVelocity.sqrMagnitude > 1e-4f)
            {
                // steering takes over from a FaceYaw request
                _facingRequest = null;
                Motor.FacingOverride = null;
            }
            Motor.Context.GroundHeightAt = GroundHeightAt;
            int steps = paused ? 0 : _clock.Advance(Time.deltaTime * Math.Max(0f, timeScale));
            steps += _pendingSteps;
            _pendingSteps = 0;
            for (int i = 0; i < steps; i++)
            {
                var before = Motor.Position;
                Motor.DesiredVelocity = new Vec3(MoveVelocity.x, 0, MoveVelocity.y);
                Motor.Step(_clock.StepSize);
                if (CanMove != null && !CanMove(new Vector2((float)before.X, (float)before.Z), new Vector2((float)Motor.Position.X, (float)Motor.Position.Z)))
                    Motor.Teleport(before, Motor.Yaw);
                _sinceRender += _clock.StepSize;
            }
            GroundPosition = new Vector2((float)Motor.Position.X, (float)Motor.Position.Z);
            SyncTransform();
            if (sortByGroundDepth && _spriteRenderer != null)
                _spriteRenderer.sortingOrder = Mathf.Clamp(Mathf.RoundToInt(GroundPosition.y) + sortingOrderOffset, short.MinValue, short.MaxValue);

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
            if (!moveTransform || !Application.isPlaying) return;
            var p = PixelCreatureRuntime.GroundToWorld(GroundPosition, EffectivePixelsPerUnit);
            var t = transform;
            t.position = new Vector3(p.x, p.y, t.position.z);
        }

        private void SnapshotView()
        {
            if (Motor == null || _renderPose == null) return;
            _renderPose.CopyFrom(Motor.Pose);
            _view = ViewSpec.Default(Motor.Yaw, EffectiveDensity);
            _view.DrawShadow = drawShadow;
            _view.Quality = quality;
            _view.WaterLevel = WaterLevel;
        }

        /// <summary>Simulates nothing, renders the current pose right now on the main thread (edit mode, tools, tests).</summary>
        public void RenderImmediate()
        {
            if (Motor == null) return;
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
            if (Model == null || _renderPose == null || _canvas == null) return false;
            var c = _canvas;
            if (_frame == null || _frame.Width != c.Width || _frame.Height != c.Height)
            {
                _frame = new PixelFrame(c.Width, c.Height);
                _rgba = new byte[c.Width * c.Height * 4];
                _flipped = new byte[c.Width * c.Height * 4];
            }
            _frame.OriginX = c.OriginX;
            _frame.OriginY = c.OriginY;
            return true;
        }

        /// <summary>Any thread: rasterizes the snapshot pose into the RGBA buffer (rows bottom-up for Unity). No Unity calls.</summary>
        internal void Rasterize(CreatureRenderer renderer)
        {
            var model = Model;
            var frame = _frame;
            var pose = _renderPose;
            if (model == null || frame == null || pose == null) return;
            try
            {
                var stats = renderer.Render(model.Anatomy, model.Palette, model.Surface, pose, _view, frame);
                LastStats = stats;
                if (stats.Overflow)
                {
                    _growL = Math.Max(_growL, stats.OverflowLeft);
                    _growR = Math.Max(_growR, stats.OverflowRight);
                    _growT = Math.Max(_growT, stats.OverflowTop);
                    _growB = Math.Max(_growB, stats.OverflowBottom);
                }
                model.Palette.BuildLut(_lut, Motor?.HitFlash ?? 0);
                frame.ToRgba(_lut, _rgba, model.Palette.WaterTint);
                int row = frame.Width * 4;
                for (int y = 0; y < frame.Height; y++)
                    Buffer.BlockCopy(_rgba, y * row, _flipped, (frame.Height - 1 - y) * row, row);
                _rastered = true;
            }
            catch (Exception e)
            {
                LastError = e.Message;
            }
        }

        /// <summary>Main thread: uploads the texture, grows the canvas if a pose did not fit.</summary>
        internal void Upload()
        {
            if (_frame == null || _canvas == null) return;
            if (_growL + _growR + _growT + _growB > 0)
            {
                var c = _canvas;
                int l = _growL + 2, r = _growR + 2, t = _growT + 2, b = _growB + 2;
                _growL = _growR = _growT = _growB = 0;
                _canvas = new CanvasSpec(Math.Min(2048, c.Width + l + r), Math.Min(2048, c.Height + t + b), c.OriginX + l, c.OriginY + t);
                RequestRender();
                if (!Application.isPlaying) RenderImmediate();
                return;
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
            RenderCount++;
        }

        private void ReleaseGraphics()
        {
            if (_spriteRenderer != null && _spriteRenderer.sprite == _sprite) _spriteRenderer.sprite = null;
            ReleaseObject(_sprite);
            ReleaseObject(_texture);
            _sprite = null;
            _texture = null;
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
