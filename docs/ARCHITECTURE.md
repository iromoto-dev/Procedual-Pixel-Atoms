# Architecture

## Pipeline

```
Seed ─┐
      ├─► Genome (genes + 4 stream seeds + generator version)
Preset┘        │
               ├─ Anatomy stream ─► Body plan (family) ─► Anatomy: skeleton, primitives, groups,
               │                                                   features, MotionRig, metrics, canvas
               ├─ Color stream ───► Palette rule ─► CreaturePalette (OKLCH ramps, 10–16 colors)
               ├─ Pattern stream ─► SurfaceSpec (material, micro-texture, body-anchored patterns)
               └─ Motion stream ──► MotionTraits (energy, weight, stiffness, tempo …)
                                         │
Inputs (velocity, facing,                │
action/hit/rest) ─► CreatureMotor ───────┴─► PoseParams ─► RigDriver + locomotion module ─► Pose
                    (fixed time step)                                      │
                                                                         CreatureRenderer
                                                                           │ (CPU, logical pixels)
                                                                           ▼
                                                   PixelFrame (palette indices) ─► texture / PNG / sprite sheet
```

Every stage is a pure function of its inputs. A model (anatomy, palette, surface, motion traits) is
built once per genome and is immutable from then on; only the motor has state.

## Layers and folders

| Folder | Role | Dependencies |
|---|---|---|
| `addons/procedural_creatures/Core/` | Framework core: math, genetics, body plans, shape blocks, anatomy, motion, rasterizer, palettes, serialization, export | .NET 8 only (no Godot types) |
| `addons/procedural_creatures/Runtime/` | Godot layer: `CreatureActor` (Node2D), `CreatureRuntime` (registry/factory/preset I/O), `CreatureRenderQueue` | Core + Godot |
| `addons/procedural_creatures_plantfolk/` | Example extension (its own family, shape block, palette rule, locomotion module) | public Core API only |
| `workshop/` | Workshop application and test area | Runtime + Core |
| `examples/minimal_integration/` | minimal game example | Runtime + Core only |
| `tests/Core/` | engine-independent tests (run in the harness and in Godot) | Core |
| `tests/*.cs` | Godot self-test, benchmark/soak test, batch tools | everything |
| `tools/core_harness/` | console program, compiles **only** Core + Plantfolk Core + core tests | .NET 8 |

The fact that the harness builds without any Godot reference and produces the same images as the
game is ongoing proof that generation, animation and rasterization are engine-independent.

## Extensions

`CreatureRegistry` registers creature families, reusable shape blocks, palette/material rules and
locomotion modules. Classes marked `[CreatureExtension]` implement `ICreatureExtension` and expose
a parameterless constructor. The shared registry discovers them in loaded assemblies; the console
harness explicitly calls `DiscoverExtensions` for its assembly.

Families declare a `CreationCategory` (creature, mythical, monster, flora, prop) that user interfaces use
for grouping, and may implement `ISurfaceCustomizer` to give material slots their own surface styles
(the flora family gives its canopy the foliage style while the trunk keeps bark; props use wood, masonry
or crystal per part). Customized surfaces are cached per family.

The `addons/procedural_creatures_plantfolk/` add-on is a complete example using the public core API.
Use stable family and gene IDs when extending an existing project, and update generator versions
or migrations when changes would affect saved genomes. The Godot integration example is
[`MinimalIntegration.cs`](../examples/minimal_integration/MinimalIntegration.cs).

## Determinism

- **Math:** `DMath` implements sine, cosine, tangent, arctangent, exponential, logarithm
  and power following fdlibm, using only basic IEEE 754 operations. `System.Math` transcendentals differ
  in the last bit between Windows/Linux/macOS and are forbidden in Core for generated data.
- **Randomness:** `Rng` = PCG32 (XSH-RR), unbiased integers following Lemire, 53-bit doubles.
  Derived seeds: SplitMix64 finalizer over (parent seed XOR FNV-1a-64(label)). No `System.Random`,
  no `GetHashCode`, no time-based seeds. Position-dependent quantities (patterns, spots) use
  `StableHash.Hash01(seed, x, y, z)`.
- **Separate streams:** A genome carries four seeds (anatomy, color, pattern, motion). Color changes
  can therefore never alter the anatomy, and rerolling the patterns leaves body and colors untouched
  (test `genome.streams_are_independent`).
- **Quantized genes:** Values are rounded to 6 decimal places on save and reconstructed
  identically on load (`genome.quantized_json_roundtrip`).
- **Simulation:** The motor runs at a fixed time step (1/60 s). `FixedClock` accumulates real time and
  emits whole steps; the pose depends only on the step count and the inputs
  (`motion.framerate_independent`: 60 and 144 fps as well as irregular frame times produce the same pose).
- **Rasterization:** single-threaded per frame, without floating-point reductions in varying order.
  Parallelism exists only between independent creatures.
- **Golden values:** `render.deterministic_and_golden` compares, per family, the genome hash, the rest-pose
  frame and the frame after 1.5 s of walking against values generated on Linux x64.

## Genome and genes

`GeneSchema` (built per family with `GeneSchemaBuilder`) describes every gene: id, display name, group,
stream, kind (float/int/choice/bool), range, default, step size, mutation strength, inheritance mode
and linkage group. Color, pattern and motion genes are the same for all families (`SharedGenes`);
families only steer how they are sampled via `FamilyStyleHints` (hues, schemes, materials, patterns).

Genetic operators (`GenomeOps`) are pure functions with an explicit seed:

- **Mutation:** Gaussian steps per gene, scaled by the mutation strength; choice genes switch rarely;
  locked genes/groups remain unchanged. Several children of one parent form a recognizable
  family (same streams, small deviations).
- **Crossover:** Linkage groups (e.g. "Head", "Ears & horns") are inherited together from A, from B or mixed,
  so that no anatomically contradictory combinations arise; the origin of each group is recorded in
  `lineage.provenance`. Partners from a different family are rejected.
- **Reroll with locks:** replaces only unlocked genes with a fresh sample.

## Anatomy

A body plan (`ICreatureFamily.BuildAnatomy`) uses the `AnatomyBuilder` to assemble the following from the genes:

- **Skeleton:** bones with a role (pelvis, chest, neck, head, jaw, leg, wing, fin, tail …)
  and a rest pose.
- **Primitives:** ellipsoids, rounded cones and two-sided plates (triangles) attached to bones, each with
  a material slot, pattern domain and flags (shadow caster, thin, emissive, translucent …).
- **Groups:** primitives within a group blend smoothly (soft union with fillets); between groups
  the depth buffer decides – this keeps near and far legs, wings and tail legibly separate.
- **Features:** eyes, nostrils and dots are stamped as designed pixel clusters.
- **MotionRig:** a semantic description for motion: legs (with pair, side, foot bone,
  pole direction), arms, wings, fins, grippers, tongue, tail/appendage chains, body chain,
  uprightness, flight altitude, action and rest style, fine-tuning (`Tuning`).
- **Metrics and canvas:** body length, leg length, speeds; the canvas is computed from the
  horizontal extent in all facing directions plus a motion margin.

**Solids** (`Core/Shapes/Solids.cs`) build hard-edged boxes, prisms, frustums, pyramids and half-cylinder
vaults from triangle plates for props. Each face carries explicit surface coordinates
(`PrimFlags.UvMapped`), so wood planks, masonry courses and metal bands line up across the triangles of a
face, and faces are wound outwards so the plate shading treats them as the front.

Shape blocks (`IShapeBlock`) encapsulate reusable parts: legs (IK in the rest pose), tails
and chains, heads with snout/jaw/eyes and nose shape (animal nose, bulbous, hooked or flat
nose), ears, horns, wings (membrane, feathers, insect – with a designed folded pose), fins,
dorsal crests, pincers. Via the registry, they are available to extensions as well.

**Gear** (`Core/Shapes/Gear.cs`) is fitted to the body rather than placed freely: clothing
consists of enlarged copies of the torso ellipsoids (`Gear.Cover`) or of rings whose dimensions are
computed from the torso cross-section at belt height (`Gear.Section`, `Gear.Belt`). Each part is a
separate group with a slight depth priority, so that it sits cleanly on the body in every pose without
jumping in front of limbs. Loincloth flaps, skirt panels and long beards are two-link spring chains;
headgear and beards orient themselves on the skull (`HeadResult.SkullCenterLocal/SkullRadii`,
jaw bone). Material slots: `Cloth` (cloth/leather), `Metal` (steel, iron, bronze, gold), `Hair`,
plus `White` for markings; each slot has its own color ramp and material style.

## Rendering

`CreatureRenderer` rasterizes on the CPU into logical pixels (typically 64–128 px creature size, a separate
canvas per creature):

1. Bone transforms are combined with the facing direction and an oblique orthographic camera (30° elevation);
   heights are foreshortened by cos, ground depth by sin.
2. Primitives are prepared analytically (bounding rectangles, distance fields).
3. Groups are rasterized: soft union with a bounded distance field (fillets at joins), smoothed
   depth and normals; Z-buffer between groups.
4. Stylization: body-anchored patterns (stripes, spots, rosettes, saddle, dorsal stripe, mottling,
   bands, blotches; plus countershading, markings on tips and muzzle, socks,
   face masks, piebald patches, wing patterns on insect wings),
   quantized lighting per material (form-light remapping: roughly a third of the body faces
   away from the light), hue shifting in the ramps, darkening of far sides, micro-textures (fur,
   scales, plates, feathers, bark, hair), **contact shadows** (per pixel, a short ray toward the
   light through the depth buffer; if it hits another part just above, the shade drops by one level),
   cleanup of stray pixels, inner and selective outer outlines.
5. Features (eyes react to expression and facing direction, blinking, mood).
6. Ground shadow as a metaball field of the supporting parts (smaller and softer as a whole when airborne),
   waterline with tint.

The result is a `PixelFrame` of palette indices; the palette (including hit flash) is only applied
when converting to RGBA. After warm-up, the renderer does not allocate.

### Pixel density and HD detail

`ViewSpec.Density` sets the output pixels per world unit (2 = HD, the runtime default; 1 = classic).
The renderer folds the density into the creature-to-view transform, so geometry, blend radii, depth
thresholds, contact-shadow rays, features and pattern coordinates scale together, while outlines and
plate thickness stay one pixel. Motion, metrics and `CanvasSpec` stay in world units; frames are sized
with `CanvasSpec.Scaled(density)`. Micro textures grow slower than the density (more, finer strands,
scales and leaves), and fur, feather, bark and foliage silhouettes are broken up by body-anchored tufts.

Level of detail by density: a primitive or feature with `MinDensity` is only drawn at that density or
above, one with `MaxDensity` only below it, so shape blocks can add HD-only anatomy or swap a classic
shape for an HD one. The shared blocks add an HD layer this way — limb muscles, joint knobs, toes and
claws, hooves, fingers, facial structure, flat ears, chitin joints and bristles — and families add torso
musculature, scutes and ribs. Detail primitives never enlarge the canvas, and classic density output is
bit-identical to the output without them (the golden values pin both).

## Animation

`CreatureMotor` simulates one instance with a fixed time step. Each step:

1. **Drive:** desired velocity → limited acceleration; alternatively an externally prescribed position.
2. **Facing:** 4/8 directions or free, with hysteresis, and turning through the front view on
   U-turns.
3. **State weights:** moving, running, rest, action, hit as soft weights (no hard cuts).
4. **Behavior → `PoseParams`:** idle (breathing, blinking, gestures, head movement), locomotion module,
   action (anticipation → strike → hold → recovery, per style), hit (recoil, flash), rest
   (lying down, curling up, folding up, sinking …). They all act on the same parameters, so
   transitions are parameter blends, not pixel blends.
5. **`RigDriver`:** turns parameters into bones (spine, neck, head, jaw, ears, wings with
   flap/fold/twist, fins, grippers, tongue, tentacles), volume-preserving squash & stretch.
   Wings fold with shoulder, elbow and wrist angles; feathered and membrane wings additionally carry
   a correction (`WingRig.ArmFix/ForeFix/HandFix`) that blends in toward the end of the fold and leads into the
   designed final pose (bird wings flat against the body, dragon wings with a raised wrist).
6. **Spring chains** (`SpringChain`) for tails, ears, antennae, tentacles: world-space positions, a target per
   joint, length preservation, ground contact → follow-through and overshoot.
7. **Limbs:** the module solves IK (two segments plus foot, pole vector, foot roll) and reports
   ground contacts.

Locomotion modules (swappable via the registry):

| Module | Bodies | Key points |
|---|---|---|
| `legged` | 2–8 legs, arms | gaits by leg count (walk, trot, gallop, tetrapod/tripod gait), planned footfalls, stance phase planted on the ground (test: slipping < 0.05 px), arms swing in antiphase |
| `flight` | birds, bats, dragons, insects | takeoff with run-up and crouch, skewed wingbeat (fast downstroke), gliding, banking into turns, landing with a flare, hovering insects |
| `swim` | fish, sharks, rays, eels | body wave or wing flapping (rays), fin oscillators, depth bob |
| `serpentine` | snakes, worms | the head lays down a trail and every joint sits on it (no sideways slipping), lateral undulation or peristalsis, rearing up, turning in place by crawling in an arc; when standing, the trail glides into a per-creature rest shape (S or curve, integrated over the heading angle so that arc lengths stay exact); cobras and sandworms rear up into a column |
| `hop` | slimes | crouch → jump → landing with squash & stretch, wobbling |
| `float` | jellyfish, wisps, floating eyes | pulsing, trailing tentacles, hovering |
| `rootwalk` (extension) | Plantfolk | waddling root gait with plant sway |
| `rooted` | trees, flora, props | stationary: gusty trunk lean and travelling sway waves on branch, frond, curtain, banner, lantern and flame chains (`wind.trunk`, `wind.branch`, `wind.hz` tuning); loops exactly in export mode |

Rigs flagged `Stationary` never move on their own; they still turn to a facing direction, act and react
to hits (they pivot about their base instead of sliding). Their action is `Shake` (a rustle that lashes
the chains and opens jaws such as chest lids) and their rest is `Dormant` (calmer wind, closed lids).

## Godot runtime

- **Pixel density:** `CreatureActor.PixelDensity` (default `CreatureRuntime.DefaultDensity`, 2) sets the
  sprite resolution. With `ScaleToWorld` (default) the sprite is drawn at 1/density, so the actor keeps its
  world size and moves in world units; pixel stages in the workshop disable it and show one texel per
  stage pixel instead, halving their automatic zoom. The 100-creature overview grid and gallery
  thumbnails use classic density for throughput.
- **`CreatureActor`** (Node2D, scene `Runtime/CreatureActor.tscn`): loads a preset or family + seed,
  builds asynchronously, simulates with a fixed time step, renders at the animation rate (`AnimationFps`,
  default 15 poses/s – typical for pixel art) and uploads the texture. Controlled via
  `MoveVelocity`/`GroundPosition`, `FaceDirection`, `TriggerAction`, `TriggerHit`, `Resting`,
  `TimeScale`, `Paused`, `StepFrames`; signals `ModelChanged`, `BuildFailed`, `MotionStateChanged`.
- **Asynchronous build:** `CreatureBuildRequester` builds on the thread pool with a `CancellationToken` and a
  ticket; only the most recently requested result is applied, stale ones are discarded
  (test `runtime.async_build_discards_stale_results`).
- **Render queue:** actors register in `_Process`; shortly before drawing
  (`RenderingServer.FramePreDraw`), all pending frames are rasterized in parallel with thread-local renderers,
  after which the textures are uploaded on the main thread. A per-actor offset
  render phase prevents all creatures from rendering in the same frame.
- **Textures:** same size → `ImageTexture.Update`; new size → new texture, and the old one is
  freed immediately.
- **Culling (opt-in):** With `CullOffscreen`, an actor checks every frame whether its canvas lies within the visible
  area of its viewport (plus `CullMargin`). Outside of it, simulation and movement continue while
  rasterization, upload and drawing pause; on re-entry it is rasterized again immediately. Costs
  therefore scale with the *visible* creatures rather than with all of them (test `workshop.test_area_100_creatures_culling`).
- **Test area** (`workshop/TestArea`): `EnvironmentArt.GenerateMap` generates the map deterministically from
  size and seed – ponds with coves and sandy shores, paths, meadows, props at an even density, minimap –
  using managed buffers only, in parallel row by row and cancelable, on a worker thread; images and textures
  are created afterwards on the main thread. The water shimmer is stored as four pre-uploaded frames per
  pond. New creatures are spawned with a time budget of 4 ms per frame.
- **Test area scenery** (`workshop/Environment/Scenery.cs`): with the default procedural style, every prop
  spot shows a `flora` or `prop` creation. The generator samples two or three genomes per form from the
  map seed (rejection-sampling seeds until the family picks that archetype) and builds them in parallel on
  its worker thread; spots pick a form by habitat (willows by the water, conifers and crystals on the
  hills, signposts and lanterns along paths, mushrooms in dark patches) plus a facing and a wind phase.
  The solid base of each creation – a per-form share of `Metrics.HorizontalRadius` (trunks for trees,
  nearly the whole reach for rocks and crates), foreshortened to an ellipse – is cleared from
  `TestMap.Clearance`, so `CanOccupy`/`CanTravel` route creatures around it. The test area creates one
  `CreatureActor` per spot (nearest to the camera first, everything in view at once, the rest within the
  spawn budget) with `CullOffscreen`, 6 poses/s and the creatures' pixel density, placed with the same
  projection as creatures so it y-sorts against them and the cliff faces. Scenery outside the view plus
  a margin is put to sleep (`ProcessMode.Disabled`) once it has culled itself, so hundreds of trees on the
  large maps cost nothing until the camera comes near; creations without wind-driven parts (no trunk lean,
  no Tentacle chains: rocks, crates, gravestones) are paused and only rasterized when they come into view.
  The classic style (`P`, `--scenery=classic`) keeps the same layout with shared static textures and small
  square footprints.

### Thread rules

1. Godot objects (nodes, resources, textures) are only touched on the main thread.
2. Worker threads only receive immutable models and their own buffers (`PixelFrame`, thread-local
   `CreatureRenderer`).
3. Results return to the main thread via `Callable.CallDeferred` and are only
   applied there if the ticket and instance are still valid.
4. `CreatureRegistry` takes a lock for registrations and is thread-safe for reads afterwards.

### Caches and memory

- `CreatureFactory` keeps LRU caches for models, anatomies, palettes, surfaces and
  motion traits (default 192 entries, twice as many for palettes/surfaces). Keys are
  content hashes of the relevant genome parts plus the generator version – a color change does not
  rebuild the anatomy.
- Gallery thumbnails are capped at 400.
- Leak and soak tests check nodes, orphan nodes, engine objects and managed memory
  (reports are written to the ignored `reports/` directory).

## Export

`SpritesheetExporter` simulates each clip with a fresh motor in export mode and uses the same
renderer as the live view. Looping clips (idle, walk, run, sleep) have an exact
period (gait frequency snapped to fps/N, breathing and blinking quantized to the loop). All
frames share size and origin (ground point); the root motion is stored in the metadata so that a
game can move the sprite without foot sliding.

Presets use the versioned `ppc.creature` JSON format and retain the family, generator version, genes,
four random-stream seeds and lineage. `GenomeSerializer` validates input and applies registered
migrations. Sprite-sheet metadata uses `ppc.spritesheet` and records page sizes, frame rectangles,
pivots, timing, facing directions and root motion. `SpritesheetOptions.Density` exports HD frames;
the metadata records `pixelDensity`, and root motion is given in sprite pixels. Both formats have version 1; the current
generator version is 2. The concrete serializers live in `Core/Serialization/` and `Core/Export/`.

## Terrain and habitats

`TerrainRelief.cs` augments the deterministic map with elevation, water depth, a translucent water
surface and cliff-face strips. The terrain texture, props and actors share the projection
`screenY = groundScreenY - elevation`. Cliff strips participate in entity depth sorting. Paths and
a broad access ramp grade between terraces. The minimap is generated from the resulting terrain.

`TestMap.Clearance` is a conservative distance field around shores, cliff edges and prop bases.
`CanOccupy` checks the creature footprint; `CanTravel` sweeps the segment in at most two-pixel steps
and rejects steep land transitions. `CreatureActor.CanMove` validates every fixed simulation step,
including residual velocity after input stops. Fliers bypass obstacle checks only after takeoff.

`SurfaceHeight` supplies the root's screen elevation. `GroundHeightAt` supplies physical world height
to the locomotion context; planted leg targets sample the height difference at their world position,
limited by limb reach. Both callbacks are optional, preserving the flat-ground integration API and
existing deterministic render goldens.

Aquatic actors are rendered below the translucent water surface and its glints, with full underwater
tint, a depth offset and no land shadow. `HabitatEffects` draws local currents and rising bubbles.
Swimmer body radius determines shore clearance. Local steering softens turns and adds separation;
it is not a global pathfinder or rigid-body crowd simulation.

Population generation uses a distinct deterministic seed for each arrival, with a four-millisecond
spawn budget per frame. Reroll replaces the population while preserving its requested size.

## Fixed workshop framing

Workshop, offspring, breeding and inspector stages use `PixelStage.FixedFraming`. Camera centering
and automatic integer zoom use the immutable model animation canvas rather than the current pose's
bounding box. Their actors disable `SyncNodePosition`: the motor still advances to drive gait and
foot contacts, while the displayed root stays in place. Manual zoom remains available. Test-area actors retain their movement and camera behavior.

## Windows launcher

`Start.cmd` is a thin PowerShell wrapper. `Start.ps1` coordinates the shared build/import/launch
helpers in `scripts/common.ps1`; `scripts/bootstrap.ps1` resolves or installs compatible tools.
`scripts/toolchain.json` pins official archive URLs, versions and SHA-512 hashes. Downloads are
verified before staged extraction into `.tools`. Existing installations are checked for the correct
Godot .NET version and an x64 .NET SDK with the net8.0 runtime. The selected SDK is exposed to Godot
through process-local PATH and DOTNET_ROOT variables. A project-local NuGet feed uses the engine's
bundled packages. Failed builds/imports stop startup and leave the next launch able to retry.
