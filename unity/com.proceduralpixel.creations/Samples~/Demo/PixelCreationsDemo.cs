// Procedural Pixel Creations for Unity - demo.
//
// Add this component to an empty GameObject in an empty scene and press Play. It sets up an
// orthographic camera, spawns a gallery of creations of every category (creatures, mythical
// creatures, monsters, trees & flora, props) in a biome, lets the animals wander around and gives you
// one creature to steer:
//   WASD / arrow keys / left stick   move (hold Shift or the south button to run)
//   Space / east button              action (attack, howl, ...)
//   R / north button                 rest / wake up
//   N                                new random creature for the player
//   1 .. 6                           biome: temperate, desert, snow, jungle, swamp, volcanic (rebuilds the gallery)

#nullable enable

using System.Collections.Generic;
using PixelCreatures.Core;
using PixelCreatures.Unity;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace PixelCreatures.Samples
{
    [AddComponentMenu("Procedural Pixel Creations/Samples/Pixel Creations Demo")]
    public sealed class PixelCreationsDemo : MonoBehaviour
    {
        [Tooltip("Family ids shown in the gallery. Empty = every registered family.")]
        public string[] families = new string[0];
        [Range(1, 12)] public int perFamily = 5;
        [Tooltip("Gallery spacing in ground pixels.")]
        public float spacing = 76f;
        [Tooltip("2 = HD sprites with high density anatomy, 1 = classic.")]
        [Range(1f, 4f)] public float pixelDensity = 2f;
        public float pixelsPerUnit = 32f;
        public string playerFamily = "quadruped";
        public string playerSeed = "hero";
        [Tooltip("temperate, desert, snow, jungle, swamp or volcanic.")]
        public string biome = "temperate";
        public bool wander = true;

        private readonly List<PixelCreatureActor> _crowd = new List<PixelCreatureActor>();
        private readonly List<float> _nextWander = new List<float>();
        private PixelCreatureActor? _player;
        private Camera? _camera;
        private int _rerolls;

        private void Start()
        {
            PixelCreatureRuntime.DefaultDensity = pixelDensity;
            PixelCreatureRuntime.DefaultPixelsPerUnit = pixelsPerUnit;
            SetupCamera();
            BuildScene();
        }

        private void BuildScene()
        {
            foreach (var a in _crowd) if (a != null) Destroy(a.gameObject);
            _crowd.Clear();
            _nextWander.Clear();
            if (_player != null) Destroy(_player.gameObject);
            var b = PixelCreatureRuntime.GetBiome(biome);
            if (_camera != null) _camera.backgroundColor = PixelCreatureRuntime.ToColor(b.Ground);

            var ids = new List<string>();
            if (families.Length > 0) ids.AddRange(families);
            else foreach (var f in PixelCreatureRuntime.Families) ids.Add(f.Id);

            // gallery: one row per family, north of the player
            for (int row = 0; row < ids.Count; row++)
            {
                if (!PixelCreatureRuntime.Registry.TryGetFamily(ids[row], out _)) { Debug.LogWarning($"Unknown family '{ids[row]}'"); continue; }
                for (int i = 0; i < perFamily; i++)
                {
                    var ground = new Vector2((i - (perFamily - 1) * 0.5f) * spacing, -(row + 1) * spacing);
                    var actor = Spawn(ids[row], $"{ids[row]}-{i + 1}", ground);
                    _crowd.Add(actor);
                    _nextWander.Add(Random.Range(1f, 4f));
                }
            }
            _player = Spawn(playerFamily, playerSeed, new Vector2(0, spacing * 0.8f));
            _player.name = "Player";
            _player.CullWhenInvisible = false;
        }

        private PixelCreatureActor Spawn(string familyId, string seed, Vector2 ground)
        {
            var go = new GameObject($"{familyId} {seed}", typeof(SpriteRenderer));
            go.SetActive(false); // configure before OnEnable builds the creation
            go.transform.SetParent(transform, false);
            var actor = go.AddComponent<PixelCreatureActor>();
            actor.FamilyId = familyId;
            actor.Seed = seed;
            actor.Biome = biome;
            actor.SortByGroundDepth = true;
            var w = PixelCreatureRuntime.GroundToWorld(ground, pixelsPerUnit);
            go.transform.position = new Vector3(w.x, w.y, 0);
            go.SetActive(true);
            return actor;
        }

        private void SetupCamera()
        {
            _camera = Camera.main;
            if (_camera == null)
            {
                var go = new GameObject("Main Camera", typeof(Camera));
                go.tag = "MainCamera";
                _camera = go.GetComponent<Camera>();
            }
            _camera.orthographic = true;
            // about 360 ground pixels high: chunky pixels on every screen
            _camera.orthographicSize = 180f / pixelsPerUnit;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = PixelCreatureRuntime.ToColor(PixelCreatureRuntime.GetBiome(biome).Ground);
            _camera.transform.position = new Vector3(0, 0, -10);
        }

        private void Update()
        {
            if (_player == null) return;
            ReadInput(out var move, out bool run, out bool action, out bool rest, out bool reroll, out int biomeKey);
            var biomes = PixelCreatureRuntime.Biomes;
            if (biomeKey >= 0 && biomeKey < biomes.Count && biomes[biomeKey].Id != biome)
            {
                biome = biomes[biomeKey].Id;
                BuildScene();
                return;
            }
            float speed = 0;
            if (_player.Model != null) speed = (float)(run ? _player.Model.Motion.RunSpeed : _player.Model.Motion.WalkSpeed);
            _player.MoveVelocity = move.sqrMagnitude > 0.01f ? move.normalized * speed : Vector2.zero;
            if (action) _player.TriggerAction();
            if (rest) _player.Resting = !_player.Resting;
            if (reroll) _player.Reroll($"{playerSeed}-{++_rerolls}");

            if (wander)
            {
                for (int i = 0; i < _crowd.Count; i++)
                {
                    _nextWander[i] -= Time.deltaTime;
                    if (_nextWander[i] > 0) continue;
                    _nextWander[i] = Random.Range(2f, 6f);
                    var a = _crowd[i];
                    if (a.Model == null || a.Model.Anatomy.Rig.Stationary) continue;
                    // wander a little or stand still; stay near the gallery slot
                    bool walk = Random.value < 0.55f;
                    a.MoveVelocity = walk ? Random.insideUnitCircle.normalized * (float)a.Model.Motion.WalkSpeed * 0.8f : Vector2.zero;
                    if (!walk && Random.value < 0.15f) a.TriggerAction();
                }
            }
        }

        private void LateUpdate()
        {
            if (_player == null || _camera == null) return;
            var p = _player.transform.position;
            var c = _camera.transform.position;
            _camera.transform.position = Vector3.Lerp(c, new Vector3(p.x, p.y, c.z), 1f - Mathf.Exp(-6f * Time.deltaTime));
        }

        private static void ReadInput(out Vector2 move, out bool run, out bool action, out bool rest, out bool reroll, out int biomeKey)
        {
            move = Vector2.zero;
            run = action = rest = reroll = false;
            biomeKey = -1;
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            if (k != null)
            {
                if (k.aKey.isPressed || k.leftArrowKey.isPressed) move.x -= 1;
                if (k.dKey.isPressed || k.rightArrowKey.isPressed) move.x += 1;
                if (k.wKey.isPressed || k.upArrowKey.isPressed) move.y -= 1;
                if (k.sKey.isPressed || k.downArrowKey.isPressed) move.y += 1;
                run |= k.leftShiftKey.isPressed || k.rightShiftKey.isPressed;
                action |= k.spaceKey.wasPressedThisFrame;
                rest |= k.rKey.wasPressedThisFrame;
                reroll |= k.nKey.wasPressedThisFrame;
                var digits = new[] { k.digit1Key, k.digit2Key, k.digit3Key, k.digit4Key, k.digit5Key, k.digit6Key };
                for (int i = 0; i < digits.Length; i++) if (digits[i].wasPressedThisFrame) biomeKey = i;
            }
            var g = Gamepad.current;
            if (g != null)
            {
                var s = g.leftStick.ReadValue();
                if (s.sqrMagnitude > 0.04f) move += new Vector2(s.x, -s.y);
                run |= g.buttonSouth.isPressed;
                action |= g.buttonEast.wasPressedThisFrame;
                rest |= g.buttonNorth.wasPressedThisFrame;
            }
#else
            // ground y points south: "up" on screen is negative ground y
            move = new Vector2(Input.GetAxisRaw("Horizontal"), -Input.GetAxisRaw("Vertical"));
            run = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            action = Input.GetKeyDown(KeyCode.Space);
            rest = Input.GetKeyDown(KeyCode.R);
            reroll = Input.GetKeyDown(KeyCode.N);
            for (int i = 0; i < 6; i++) if (Input.GetKeyDown(KeyCode.Alpha1 + i)) biomeKey = i;
#endif
        }
    }
}
