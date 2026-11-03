# Procedural Pixel Creations for Unity

Procedural pixel-art **creatures, mythical creatures, monsters, trees & flora and props**, generated,
animated and rasterized in code at HD pixel density. Every creation comes from a seed (or a
`ppc.creature` preset), has a skeleton, walks, runs, rests, attacks and reacts to hits, and is
drawn into a point-filtered sprite in eight directions.

The package contains the same engine-independent core as the Godot addon in this repository, so a
seed gives the same creation in both engines.

## Install

Unity 2021.3 LTS or newer (Unity 6 included), any render pipeline (Built-in, URP, HDRP), the
Mono and IL2CPP scripting backends.

**Package Manager > + > Add package from git URL...**

```
https://github.com/iromoto-dev/Procedual-Pixel-Atoms.git?path=/unity/com.proceduralpixel.creations
```

or add it to `Packages/manifest.json`:

```json
"com.proceduralpixel.creations": "https://github.com/iromoto-dev/Procedual-Pixel-Atoms.git?path=/unity/com.proceduralpixel.creations"
```

Append `#<branch or tag>` to the URL to pin a version. No other packages are required.

## Quick start

- **GameObject > Procedural Pixel Creations > Pixel Creature Actor** adds a live creation to the
  scene. Pick the family and seed in the inspector (Reroll gives a new one); it renders in edit
  mode, too.
- **Window > Procedural Pixel Creations > Creation Studio** previews creations of every family
  animated in all eight directions, edits genes, saves presets, exports sprite sheets and bakes
  native assets.
- **Samples** (Package Manager > Procedural Pixel Creations > Samples): *Demo scene setup* (add
  `PixelCreationsDemo` to an empty GameObject and press Play: a gallery of every category and a
  creature you steer with WASD / arrows / a gamepad) and *Example presets* (92 presets).

## Families

| Category | Family id | Archetypes |
|---|---|---|
| Creatures | `quadruped`, `biped`, `winged`, `aquatic`, `serpent`, `reptile`, `arthropod`, `amorphous`, `plantfolk` | mammals, humanoids, birds, bats, insects, fish, snakes, lizards, spiders, slimes, plant people, ... |
| Mythical | `mythical` | griffin, unicorn & pegasus, hydra, cerberus, kirin, manticore, centaur (feathered or membrane wings) |
| Monsters | `monster` | mimic, cyclops, minotaur, skeleton, wraith, gargoyle, eldritch horror (club, axe or sword) |
| Flora | `flora` | broadleaf tree, conifer, birch, willow, palm, dead tree, blossom tree, bush, giant mushroom, cactus, flowers |
| Props | `prop` | crate, barrel, treasure chest, rock, crystal cluster, lamp post, brazier, campfire, gravestone, pillar, signpost, pot, banner |

Trees, plants and props are stationary: they sway in the wind, rustle when hit and open, light up
or close on *Action* / *Rest*.

## Live actors

```csharp
using PixelCreatures.Unity;

var go = new GameObject("Dragon", typeof(SpriteRenderer));
var actor = go.AddComponent<PixelCreatureActor>();
actor.FamilyId = "mythical";
actor.Seed = "ember";            // any text or number
actor.PixelDensity = 2;          // 2 = HD (default), 1 = classic pixel size
actor.BuildFromSettings(async: true);

actor.MoveVelocity = new Vector2(40, 0); // ground pixels per second: x east, y south
actor.TriggerAction();
actor.TriggerHit(Vector2.left);
actor.Resting = true;
actor.FaceDirection(6);                  // 0 east, 2 north, 4 west, 6 south
```

- **Coordinates.** The camera looks down at 30 degrees. Ground positions are pixels (x east, y
  south); `PixelCreatureRuntime.GroundToWorld` / `WorldToGround` convert to Unity units
  (`PixelsPerUnit`, default 32 ground pixels per unit). With *Move Transform* on, the actor drives
  its transform.
- **Pixel density.** Density 2 doubles the sprite resolution *and* switches on the high density
  anatomy (claws, scales, feathers, bark, rivets, ...). The sprite's pixels per unit are
  `PixelsPerUnit * density`, so the world size does not change with the density.
- **Sorting.** Turn on *Sort By Ground Depth* (sets `sortingOrder` from the ground depth, works
  with every pipeline), or use a custom transparency sort axis (0, 1, 0) (Project Settings >
  Graphics, or the 2D Renderer asset in URP).
- **Performance.** Models build on worker threads; all actors rasterize together once per frame
  in parallel (`LateUpdate`), at their *Animation Fps* (15 by default), skipped while invisible,
  and spread over the frames of an animation interval. On WebGL everything runs on the main
  thread.
- **Collisions and terrain.** `CanMove(from, to)` can veto a step, `GroundHeightAt(x, y)` lifts
  the feet onto terrain, `WaterLevel` submerges swimmers.
- **Events.** `onModelChanged`, `onBuildFailed(message)`, `onMotionStateChanged(state)`.

## Presets

```csharp
string json = PixelCreatureRuntime.SavePreset(actor.Genome);
actor.LoadPreset(json);                       // or assign a TextAsset to the Preset field
var result = PixelCreatureRuntime.LoadPreset(json);
if (!result.Ok) Debug.LogWarning(result.Describe());
```

Presets are versioned JSON (`ppc.creature`) and interchangeable with the presets of the Godot addon.

## Sprite sheets and baking

The Creation Studio (or `PixelCreatures.Unity.Editor.PixelCreatureBaker` from editor scripts)
offers two outputs:

- **Export Sprite Sheet**: PNG page(s) plus `ppc.spritesheet` JSON metadata (frame rectangles,
  pivots, timing, root motion), imported as point-filtered, uncompressed sprites.
- **Bake**: a texture asset per page with one `Sprite` sub-asset per frame (pivot on the ground
  point), an `AnimationClip` per clip and direction that swaps `SpriteRenderer.sprite`, and an
  `AnimatorController` with one state per clip. Baked creations need no runtime code at all.

## Platforms and IL2CPP

- `Runtime/link.xml` and a build-time linker processor keep the creation families (they are
  found by reflection) when managed code stripping is on.
- Families of your own: implement `ICreatureFamily` (or derive from `CreatureFamilyBase`) in a
  class marked `[CreatureExtension]`; assemblies that reference `PixelCreatures.Core` are scanned
  automatically and preserved in IL2CPP builds.
- The core uses only .NET Standard 2.1 and C# 9; no unsafe code, no native plugins.

## Determinism

The core uses its own math library and random number generator instead of `System.Math` and
`UnityEngine.Random`, so a seed produces the same creation on every platform. Bit-exact output is
verified on .NET and Mono (the test suite checks golden hashes of the generated anatomy, poses
and rendered pixels). IL2CPP translates the code to C++; a C++ compiler that fuses floating point
operations could change a rare pixel, which is invisible in practice but means golden hashes are
only guaranteed on Mono and .NET.

## Tests

Add the package to `testables` in `Packages/manifest.json`:

```json
"testables": ["com.proceduralpixel.creations"]
```

Window > General > Test Runner > EditMode runs the full core suite (including the golden values)
inside Unity and tests of the actor, presets, coordinates and baking.

## Layout

```
Runtime/Core        engine-independent core (mirrored from addons/procedural_creatures/Core)
Runtime/Plantfolk   example extension family (mirrored from addons/procedural_creatures_plantfolk)
Runtime/Unity       PixelCreatureActor, PixelCreatureRuntime, render queue
Editor              Creation Studio, inspector, baker, IL2CPP linker processor
Tests/Editor        edit mode tests (core suite mirrored from tests/Core)
Samples~            demo script and example presets
```

`Runtime/Core`, `Runtime/Plantfolk`, `Tests/Editor/Core`, `Samples~/Presets` and all `.meta`
files are generated from the repository with `tools/core_harness` (`unity-sync`); edit the
originals, then run `scripts/unity-check.sh --sync`.
