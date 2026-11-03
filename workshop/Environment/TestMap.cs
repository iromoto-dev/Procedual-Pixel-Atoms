// Procedural Pixel Creature Workshop - scalable map of the test area: ponds with sandy shores, winding
// paths, grass with light meadows and darker patches, props (trees, bushes, rocks) and a minimap image.
// Props are either procedural scenery (flora and prop creations, see Scenery.cs) or static textures.
// Deterministic for (size, seed, style, resolution). Generation only touches managed buffers and core
// models - no engine calls - so it can run on a worker thread; the test area turns the buffers into
// images, textures and actors.
//
// Two grids: the logical layers (terrain, elevation, water depth, clearance) have one cell per map unit
// and drive navigation. The images (ground, water surface, cliff faces, glints) have Resolution texels
// per map unit: at 2 (HD, matching HD sprites) the same continuous shapes - shorelines, paths, colour
// noise, relief - are evaluated per texel, and small details (grass blades, flowers, glints, rock strata)
// are drawn on the texel grid, so they get finer instead of just bigger. Texel (r*x, r*y) samples exactly
// the point of logical cell (x, y), so both grids agree, and resolution 1 is the classic map.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using PixelCreatures.Core;

namespace PixelCreatures.Workshop
{
    /// <summary>Map size presets of the test area (screen pixels; the ground is already foreshortened).</summary>
    public readonly struct MapSize
    {
        public readonly string Name;
        public readonly int Width, Height;

        public MapSize(string name, int width, int height)
        {
            Name = name;
            Width = width;
            Height = height;
        }

        public override string ToString() => $"{Name} ({Width}×{Height})";

        public static readonly MapSize[] All =
        {
            new MapSize("Small", 640, 400),
            new MapSize("Medium", 960, 600),
            new MapSize("Large", 1280, 800),
            new MapSize("Very large", 1920, 1200),
            new MapSize("Huge", 2560, 1600),
        };
    }

    public enum PropKind : byte
    {
        Tree,
        Bush,
        Rock,
    }

    public struct PropSpot
    {
        public int X, Y;
        public PropKind Kind;
        /// <summary>Static texture variant (classic style, and the fallback for spots without scenery).</summary>
        public int Variant;
        /// <summary>Index into <see cref="TestMap.Scenery"/> (procedural style), -1 = static texture.</summary>
        public int Scenery;
        /// <summary>Facing direction (0..7, 6 = towards the viewer) of the procedural scenery.</summary>
        public int Facing;
        /// <summary>Wind phase of the procedural scenery (seconds): spots that share a variant sway out of step.</summary>
        public float Phase;
    }

    public sealed class PondSpec
    {
        public double Cx, Cy, Rx, Ry;
        /// <summary>Seed of the pond's outline (lobes and bays), so ponds of one map differ in shape.</summary>
        public ulong ShapeSeed;
        /// <summary>Area that holds the pond with its shore and glints (screen pixels).</summary>
        public Rect2I Bounds;
        /// <summary>Four frames of water glints (sparkle runs in ground texels), shown in a loop.</summary>
        public GlintRun[][] GlintFrames = Array.Empty<GlintRun[]>();
    }

    /// <summary>A horizontal sparkle on the water: <see cref="Length"/> texels from (X, Y) in ground texels.</summary>
    public readonly struct GlintRun
    {
        public readonly short X, Y;
        public readonly byte Length;
        /// <summary>Just appeared (foam white); the following frame it fades to light water.</summary>
        public readonly bool Bright;

        public GlintRun(int x, int y, int length, bool bright)
        {
            X = (short)x;
            Y = (short)y;
            Length = (byte)length;
            Bright = bright;
        }
    }

    public sealed class TestMap
    {
        public const byte Grass = 0, Path = 1, Sand = 2, Water = 3;

        public int Width, Height;
        public ulong Seed;
        /// <summary>Texels per map unit of the images (ground, water surface, cliff faces, glints): 1 = classic, 2 = HD.</summary>
        public int Resolution = 1;
        public int TexWidth => Width * Resolution;
        public int TexHeight => Height * Resolution;
        /// <summary>Terrain per screen pixel: <see cref="Grass"/>, <see cref="Path"/>, <see cref="Sand"/>, <see cref="Water"/>.</summary>
        public byte[] Terrain = Array.Empty<byte>();
        /// <summary>Ground colours per texel (TexWidth × TexHeight, already projected by the relief).</summary>
        public byte[] GroundRgba = Array.Empty<byte>();
        public float[] Elevation = Array.Empty<float>();
        public float[] WaterDepth = Array.Empty<float>();
        public byte[] Clearance = Array.Empty<byte>();
        /// <summary>Translucent water tint per texel (TexWidth × TexHeight), drawn above swimmers.</summary>
        public byte[] WaterSurfaceRgba = Array.Empty<byte>();
        public readonly List<ReliefFace> Cliffs = new List<ReliefFace>();
        public readonly List<PondSpec> Ponds = new List<PondSpec>();
        public readonly List<PropSpot> Props = new List<PropSpot>();
        /// <summary>How the props are shown; procedural maps carry the built flora and prop creations in <see cref="Scenery"/>.</summary>
        public SceneryStyle Style;
        public readonly List<SceneryVariant> Scenery = new List<SceneryVariant>();
        public int MiniWidth, MiniHeight;
        public byte[] MiniRgba = Array.Empty<byte>();
        public double GenerateMs;

        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;
        public bool IsWater(int x, int y) => InBounds(x, y) && Terrain[y * Width + x] == Water;

        /// <summary>
        /// Drops the large image buffers (ground, water surface, cliff faces) once they are on the GPU; the logical
        /// layers, glints and minimap stay. An HD map of the largest size holds more than 100 MB of them.
        /// </summary>
        public void ReleaseImages()
        {
            GroundRgba = Array.Empty<byte>();
            WaterSurfaceRgba = Array.Empty<byte>();
            foreach (var face in Cliffs) face.Rgba = Array.Empty<byte>();
        }

        public float HeightAt(Vector2 p)
        {
            int x = Math.Clamp((int)p.X, 0, Width - 2), y = Math.Clamp((int)p.Y, 0, Height - 2);
            float fx = Math.Clamp(p.X - x, 0, 1), fy = Math.Clamp(p.Y - y, 0, 1);
            int i = y * Width + x;
            return Mathf.Lerp(Mathf.Lerp(Elevation[i], Elevation[i + 1], fx), Mathf.Lerp(Elevation[i + Width], Elevation[i + Width + 1], fx), fy);
        }

        public Vector2 Project(Vector2 p) => p - new Vector2(0, HeightAt(p));

        /// <summary>The flora or prop creation of a spot, null for a static texture.</summary>
        public SceneryVariant? SceneryOf(in PropSpot p) => p.Scenery >= 0 && p.Scenery < Scenery.Count ? Scenery[p.Scenery] : null;

        /// <summary>
        /// Blocked area of a prop in map pixels (half extents around the spot). Procedural scenery blocks its solid
        /// footprint, a circle on the ground plane that the camera foreshortens to an ellipse; static props a square.
        /// </summary>
        public Vector2 FootprintExtents(in PropSpot p)
        {
            var v = SceneryOf(p);
            if (v == null)
            {
                int r = p.Kind == PropKind.Tree ? 3 : p.Kind == PropKind.Rock ? 4 : 2;
                return new Vector2(r, r);
            }
            float rx = (float)v.Footprint;
            return new Vector2(rx, rx * (float)DMath.Sin(PixelCamera.DefaultElevation));
        }

        /// <summary>Checks the whole footprint, including banks, cliffs and solid prop bases.</summary>
        public bool CanOccupy(Vector2 p, bool aquatic, bool flying, float radius = 5)
        {
            if (p.X < radius + 2 || p.Y < radius + 2 || p.X >= Width - radius - 2 || p.Y >= Height - radius - 2) return false;
            if (flying) return true;
            int i = (int)p.Y * Width + (int)p.X;
            if (IsWater((int)p.X, (int)p.Y) != aquatic || Clearance[i] < radius) return false;
            return true;
        }

        public bool CanTravel(Vector2 from, Vector2 to, bool aquatic, bool flying, float radius = 5)
        {
            int steps = Math.Max(1, (int)Math.Ceiling(from.DistanceTo(to) / 2));
            var previous = from;
            for (int k = 1; k <= steps; k++)
            {
                var p = from.Lerp(to, k / (float)steps);
                if (!CanOccupy(p, aquatic, flying, radius)) return false;
                if (!aquatic && !flying && Math.Abs(HeightAt(p) - HeightAt(previous)) > 1.6f) return false;
                previous = p;
            }
            return true;
        }

        /// <summary>True when the area around (x, y) is grass only (no water, sand, path or map edge).</summary>
        public bool IsGrassArea(int x, int y, int margin)
        {
            for (int dy = -margin / 2; dy <= margin / 2; dy += 2)
                for (int dx = -margin; dx <= margin; dx += 3)
                {
                    int xx = x + dx, yy = y + dy;
                    if (!InBounds(xx, yy) || Terrain[yy * Width + xx] != Grass) return false;
                }
            return true;
        }

        /// <summary>Share of the map covered by one terrain kind (0..1).</summary>
        public double Fraction(byte kind)
        {
            long n = 0;
            foreach (var t in Terrain) if (t == kind) n++;
            return Terrain.Length == 0 ? 0 : n / (double)Terrain.Length;
        }
    }

    public static partial class EnvironmentArt
    {
        public const int TreeVariants = 10, BushVariants = 8, RockVariants = 8;
        public const int MinimapMaxWidth = 240, MinimapMaxHeight = 150;
        /// <summary>Highest image resolution (texels per map unit); the images of the largest map grow with its square.</summary>
        public const int MaxResolution = 2;
        /// <summary>Screen pixels² per prop: 46 props on the small 640×400 map, same density on larger maps.</summary>
        private const double PropArea = 5600;

        /// <summary>
        /// Generates a test area map of width x height screen pixels. Runs the per-pixel work in parallel
        /// (row by row, results identical to a sequential run) and can be cancelled. The procedural style also
        /// samples and builds the flora and prop creations of the props (see <see cref="AssignScenery"/>); spot
        /// positions and kinds are the same in both styles. The resolution (texels per map unit, 1..<see cref="MaxResolution"/>)
        /// only changes the images: terrain, elevation, water depth, clearance and props are the same at every resolution.
        /// </summary>
        public static TestMap GenerateMap(int width, int height, ulong seed, CancellationToken cancel = default, SceneryStyle style = SceneryStyle.Procedural, int resolution = 1)
        {
            var sw = Stopwatch.StartNew();
            int w = Math.Max(64, width), h = Math.Max(64, height), r = Math.Clamp(resolution, 1, MaxResolution);
            var map = new TestMap { Width = w, Height = h, Seed = seed, Style = style, Resolution = r, Terrain = new byte[w * h] };
            int tw = map.TexWidth, th = map.TexHeight;
            map.GroundRgba = new byte[tw * th * 4];
            // terrain per texel; at resolution 1 that is the logical terrain itself
            var tex = r == 1 ? map.Terrain : new byte[tw * th];
            PlacePonds(map, new Rng(seed ^ 0x504F4E44UL));
            var rowPaths = HorizontalPaths(w, h, seed, r);
            var columnPaths = VerticalPaths(w, h, seed, r);
            var options = new ParallelOptions { CancellationToken = cancel };
            Parallel.For(0, th, options, y => GroundRow(map, tex, rowPaths, columnPaths, y));
            cancel.ThrowIfCancellationRequested();
            Tufts(map, tex);
            Parallel.For(1, th - 1, options, y => ShoreRow(map, tex, y));
            BuildRelief(map, tex, cancel);
            foreach (var pond in map.Ponds) pond.GlintFrames = Glints(map, tex, pond);
            cancel.ThrowIfCancellationRequested();
            PlaceProps(map, new Rng(seed ^ 0x50524F50UL));
            if (style == SceneryStyle.Procedural) AssignScenery(map, cancel);
            BuildClearance(map);
            BuildMinimap(map);
            map.GenerateMs = sw.Elapsed.TotalMilliseconds;
            return map;
        }

        // ------------------------------------------------------------------ layout

        private static void PlacePonds(TestMap map, Rng rng)
        {
            int w = map.Width, h = map.Height;
            int count = (int)Math.Round(w * (double)h / 300000.0);
            if (count <= 1)
            {
                // the small map keeps its familiar single pond in the lower right
                map.Ponds.Add(new PondSpec { Cx = w * 0.69, Cy = h * 0.61, Rx = w * 0.235, Ry = h * 0.285 });
            }
            else
            {
                for (int attempts = 0; map.Ponds.Count < count && attempts < count * 80; attempts++)
                {
                    // from three ponds on, the first one is a lake
                    double s = map.Ponds.Count == 0 ? (count >= 3 ? rng.Range(1.6, 1.9) : 1.55) : rng.Range(1.2, 1.4);
                    double rx = 108 * s, ry = 80 * s;
                    if (2 * rx + 60 >= w || 2 * ry + 70 >= h) continue;
                    double cx = rng.Range(rx + 30, w - rx - 30), cy = rng.Range(ry + 44, h - ry - 26);
                    bool free = true;
                    foreach (var p in map.Ponds)
                    {
                        // keep shores apart (ellipse metric with padding for wobble, sand and a walkway)
                        double dx = (cx - p.Cx) / (rx + p.Rx + 56), dy = (cy - p.Cy) / (ry + p.Ry + 44);
                        if (dx * dx + dy * dy < 1) { free = false; break; }
                    }
                    if (free) map.Ponds.Add(new PondSpec { Cx = cx, Cy = cy, Rx = rx, Ry = ry });
                }
            }
            for (int i = 0; i < map.Ponds.Count; i++)
            {
                var p = map.Ponds[i];
                p.ShapeSeed = map.Seed + 31 + (ulong)i * 977;
                // lobes reach ~1.27 radii, shore jitter and sand add up to 13.5 px: 1.6 radii cover both
                int x0 = Math.Max(0, (int)(p.Cx - p.Rx * 1.6)), x1 = Math.Min(w, (int)Math.Ceiling(p.Cx + p.Rx * 1.6));
                int y0 = Math.Max(0, (int)(p.Cy - p.Ry * 1.6)), y1 = Math.Min(h, (int)Math.Ceiling(p.Cy + p.Ry * 1.6));
                p.Bounds = new Rect2I(x0, y0, x1 - x0, y1 - y0);
            }
        }

        /// <summary>Centre lines (screen y per texel column) of the paths crossing the map from west to east.</summary>
        private static double[][] HorizontalPaths(int w, int h, ulong seed, int r)
        {
            int n = Math.Max(1, (int)Math.Round(h / 400.0));
            double band = h / (double)n;
            var paths = new double[n][];
            for (int k = 0; k < n; k++)
            {
                // a single path sits at 0.42 h with the original curve of the small map
                double baseY = band * k + band * 0.42, amp = Math.Min(band * 0.14, 64), phase = 0.6 + k * 1.7;
                ulong s = seed + 3 + (ulong)k * 101;
                var c = new double[w * r];
                for (int tx = 0; tx < c.Length; tx++)
                {
                    double x = tx / (double)r;
                    c[tx] = baseY + DMath.Sin(x * 0.018 + phase) * amp + PatternNoise.Perlin2(s, x * 0.03, 0.5) * 8;
                }
                paths[k] = c;
            }
            return paths;
        }

        /// <summary>Centre lines (screen x per texel row) of the paths crossing from north to south (wider maps only).</summary>
        private static double[][] VerticalPaths(int w, int h, ulong seed, int r)
        {
            int n = w / 900;
            double band = n > 0 ? w / (double)n : w;
            var paths = new double[n][];
            for (int k = 0; k < n; k++)
            {
                double baseX = band * k + band * 0.5, phase = 1.3 + k * 2.1;
                ulong s = seed + 7 + (ulong)k * 101;
                var c = new double[h * r];
                for (int ty = 0; ty < c.Length; ty++)
                {
                    double y = ty / (double)r;
                    c[ty] = baseX + DMath.Sin(y * 0.02 + phase) * 48 + PatternNoise.Perlin2(s, y * 0.03, 0.5) * 10;
                }
                paths[k] = c;
            }
            return paths;
        }

        /// <summary>Large-scale grass variation in [-1, 1]: positive = light meadow, negative = darker patch.</summary>
        private static double Meadow(ulong seed, double x, double y) => PatternNoise.Perlin2(seed + 60, x / 150.0, y / 95.0);

        private static double MeadowLift(double m) => Math.Clamp((m - 0.2) * 2.5, 0, 1);

        private static double MeadowShade(double m) => Math.Clamp((-m - 0.3) * 2.5, 0, 1);

        /// <summary>Terrain and base colour of one texel row (map point of texel (tx, ty) = (tx, ty) / Resolution).</summary>
        private static void GroundRow(TestMap map, byte[] tex, double[][] rowPaths, double[][] columnPaths, int ty)
        {
            int w = map.Width, tw = map.TexWidth, res = map.Resolution;
            double y = ty / (double)res;
            bool logicalRow = ty % res == 0;
            ulong seed = map.Seed;
            var ponds = map.Ponds;
            for (int tx = 0; tx < tw; tx++)
            {
                double x = tx / (double)res;
                // ponds: ellipses with lobes and bays relative to their size, a little shore jitter in
                // pixels and a sandy rim of about 10 px (thinner at the top and bottom, as seen from above)
                byte t = TestMap.Grass;
                double jitter = double.NaN;
                foreach (var p in ponds)
                {
                    var b = p.Bounds;
                    if (x < b.Position.X || y < b.Position.Y || x >= b.End.X || y >= b.End.Y) continue;
                    if (double.IsNaN(jitter)) jitter = PatternNoise.Perlin2(seed, x * 0.06, y * 0.09) * 3.5;
                    double dx = (x - p.Cx) / p.Rx, dy = (y - p.Cy) / p.Ry;
                    double v = Math.Sqrt(dx * dx + dy * dy) + PatternNoise.Perlin2(p.ShapeSeed, dx * 1.7, dy * 1.7) * 0.3 + jitter / p.Rx;
                    if (v < 1.0) { t = TestMap.Water; break; }
                    if (v < 1.0 + 10.0 / p.Rx) t = TestMap.Sand;
                }
                if (t == TestMap.Grass && OnPath(seed, rowPaths, columnPaths, tx, ty, x, y)) t = TestMap.Path;
                tex[ty * tw + tx] = t;
                if (logicalRow && tx % res == 0) map.Terrain[ty / res * w + tx / res] = t;

                double n = PatternNoise.Fbm2(seed + 9, x / 14.0, y / 10.0, 2) * 0.5 + 0.5;
                Color c;
                switch (t)
                {
                    case TestMap.Water: c = n < 0.45 ? Water0 : Water1; break;
                    case TestMap.Sand: c = n < 0.4 ? Sand0 : (n > 0.72 ? Sand2 : Sand1); break;
                    case TestMap.Path: c = n < 0.4 ? Dirt0 : (n > 0.7 ? Dirt2 : Dirt1); break;
                    default:
                        double m = Meadow(seed, x, y), lift = MeadowLift(m), shade = MeadowShade(m);
                        double lo = 0.38 - lift * 0.14 + shade * 0.2, hi = 0.63 - lift * 0.2 + shade * 0.15;
                        c = n < lo ? Grass0 : (n > hi ? (lift > 0.6 && n > hi + 0.24 ? Grass3 : Grass2) : Grass1);
                        break;
                }
                int i = (ty * tw + tx) * 4;
                map.GroundRgba[i] = (byte)c.R8;
                map.GroundRgba[i + 1] = (byte)c.G8;
                map.GroundRgba[i + 2] = (byte)c.B8;
                map.GroundRgba[i + 3] = 255;
            }
        }

        /// <summary>Path test at map point (x, y); the centre lines are sampled per texel column (tx) and row (ty).</summary>
        private static bool OnPath(ulong seed, double[][] rowPaths, double[][] columnPaths, int tx, int ty, double x, double y)
        {
            foreach (var c in rowPaths)
            {
                double d = Math.Abs(y - c[tx]);
                if (d < 10 && d - 7 - PatternNoise.Perlin2(seed + 4, x * 0.1, y * 0.1) * 2.5 < 0) return true;
            }
            foreach (var c in columnPaths)
            {
                double d = Math.Abs(x - c[ty]);
                if (d < 16 && d - 12 - PatternNoise.Perlin2(seed + 5, x * 0.1, y * 0.1) * 3 < 0) return true;
            }
            return false;
        }

        /// <summary>Grass tufts and flowers (more flowers on meadows). Sequential: neighbouring cells may touch the same texel.</summary>
        private static void Tufts(TestMap map, byte[] tex)
        {
            if (map.Resolution > 1)
            {
                HdTufts(map, tex);
                return;
            }
            const int cell = 5;
            ulong seed = map.Seed;
            for (int cy = 0; cy < map.Height / cell; cy++)
            {
                for (int cx = 0; cx < map.Width / cell; cx++)
                {
                    double r = Hash(seed + 11, cx, cy);
                    int x = cx * cell + (int)(Hash(seed + 12, cx, cy) * (cell - 2)) + 1;
                    int y = cy * cell + (int)(Hash(seed + 13, cx, cy) * (cell - 2)) + 1;
                    double flowerRate = 0.012 + MeadowLift(Meadow(seed, x, y)) * 0.07;
                    if (r < 0.34)
                    {
                        // small tuft: dark base, light tip (lit from the upper left)
                        GrassPixel(map, tex, x, y + 1, Grass0);
                        GrassPixel(map, tex, x - 1, y + 1, Grass0);
                        GrassPixel(map, tex, x, y, Grass2);
                        if (r < 0.14) GrassPixel(map, tex, x - 1, y, Grass3);
                    }
                    else if (r < 0.34 + flowerRate)
                    {
                        var fc = Flowers[(int)(Hash(seed + 14, cx, cy) * Flowers.Length) % Flowers.Length];
                        GrassPixel(map, tex, x, y, fc);
                        GrassPixel(map, tex, x, y + 1, Grass0);
                        if (r < 0.34 + flowerRate * 0.4) GrassPixel(map, tex, x + 1, y, fc.Darkened(0.2f));
                    }
                }
            }
        }

        /// <summary>
        /// HD grass: more and finer tufts on the texel grid - two to four single-texel blades of one to three texels,
        /// dark at the root and lit at the tip from the upper left - and small blossoms on stems, more on meadows.
        /// </summary>
        private static void HdTufts(TestMap map, byte[] tex)
        {
            int res = map.Resolution, cell = 3 * res + 1;
            ulong seed = map.Seed;
            for (int cy = 0; cy < map.TexHeight / cell; cy++)
            {
                for (int cx = 0; cx < map.TexWidth / cell; cx++)
                {
                    double r = Hash(seed + 11, cx, cy);
                    int x = cx * cell + (int)(Hash(seed + 12, cx, cy) * (cell - 3)) + 1;
                    int y = cy * cell + (int)(Hash(seed + 13, cx, cy) * (cell - 4)) + 3;
                    double lift = MeadowLift(Meadow(seed, x / (double)res, y / (double)res));
                    double flowerRate = 0.008 + lift * 0.05;
                    if (r < 0.3)
                    {
                        int blades = 2 + (int)(Hash(seed + 15, cx, cy) * 3);
                        for (int k = 0; k < blades; k++)
                        {
                            int bx = x + k - blades / 2;
                            // the middle blades grow tallest; the leftmost catches the light
                            int height = 1 + (int)(Hash(seed + 16, cx * 4 + k, cy) * (k == 0 || k == blades - 1 ? 2 : 3));
                            GrassPixel(map, tex, bx, y + 1, Grass0);
                            for (int j = 0; j < height; j++)
                            {
                                bool tip = j == height - 1;
                                GrassPixel(map, tex, bx, y - j, tip ? (k == 0 && r < 0.15 ? Grass3 : Grass2) : Grass1);
                            }
                        }
                    }
                    else if (r < 0.3 + flowerRate)
                    {
                        var fc = Flowers[(int)(Hash(seed + 14, cx, cy) * Flowers.Length) % Flowers.Length];
                        // a blossom of two to three texels, lit from the upper left, on a dark stem
                        GrassPixel(map, tex, x, y + 2, Grass0);
                        GrassPixel(map, tex, x, y + 1, Grass1);
                        GrassPixel(map, tex, x, y, fc);
                        GrassPixel(map, tex, x + 1, y, fc.Darkened(0.2f));
                        if (r < 0.3 + flowerRate * 0.5) GrassPixel(map, tex, x, y - 1, fc.Lightened(0.15f));
                    }
                }
            }
        }

        private static void GrassPixel(TestMap map, byte[] tex, int x, int y, Color c)
        {
            int tw = map.TexWidth;
            if (x < 0 || y < 0 || x >= tw || y >= map.TexHeight || tex[y * tw + x] != TestMap.Grass) return;
            int i = (y * tw + x) * 4;
            map.GroundRgba[i] = (byte)c.R8;
            map.GroundRgba[i + 1] = (byte)c.G8;
            map.GroundRgba[i + 2] = (byte)c.B8;
        }

        /// <summary>Foam on water next to land, a dark edge below the bank (depth cue). Reads terrain only (per texel).</summary>
        private static void ShoreRow(TestMap map, byte[] t, int y)
        {
            int w = map.TexWidth;
            for (int x = 1; x < w - 1; x++)
            {
                int i = y * w + x;
                if (t[i] != TestMap.Water) continue;
                if (t[i - 1] == TestMap.Water && t[i + 1] == TestMap.Water && t[i - w] == TestMap.Water && t[i + w] == TestMap.Water) continue;
                var c = t[i - w] != TestMap.Water ? Water0 : Foam;
                map.GroundRgba[i * 4] = (byte)c.R8;
                map.GroundRgba[i * 4 + 1] = (byte)c.G8;
                map.GroundRgba[i * 4 + 2] = (byte)c.B8;
            }
        }

        /// <summary>
        /// Four frames of animated glints for one pond: cells on the texel grid, so frames line up and HD glints are
        /// finer and more numerous. A glint appears bright for one frame and fades to light water in the next.
        /// </summary>
        private static GlintRun[][] Glints(TestMap map, byte[] tex, PondSpec pond)
        {
            int res = map.Resolution, cell = res == 1 ? 9 : 6 * res - 1;
            int tw = map.TexWidth, th = map.TexHeight;
            int x0 = pond.Bounds.Position.X * res, y0 = pond.Bounds.Position.Y * res, x1 = pond.Bounds.End.X * res, y1 = pond.Bounds.End.Y * res;
            bool Water(int x, int y) => x >= 0 && y >= 0 && x < tw && y < th && tex[y * tw + x] == TestMap.Water;
            ulong seed = map.Seed;
            var frames = new GlintRun[4][];
            var runs = new List<GlintRun>();
            for (int k = 0; k < 4; k++)
            {
                runs.Clear();
                for (int cy = y0 / cell; cy <= y1 / cell; cy++)
                {
                    for (int cx = x0 / cell; cx <= x1 / cell; cx++)
                    {
                        double r = Hash(seed + 40, cx, cy);
                        if (r > 0.16) continue;
                        int ph = (int)(Hash(seed + 41, cx, cy) * 4);
                        int age = (k - ph + 4) % 4;
                        if (age > 1) continue;
                        int x = cx * cell + (int)(Hash(seed + 42, cx, cy) * 5), y = cy * cell + (int)(Hash(seed + 43, cx, cy) * 6);
                        if (!Water(x, y) || !Water(x + 3, y) || !Water(x, y - 2) || !Water(x, y + 2)) continue;
                        // HD glints are finer than classic ones (two to three map units) but long enough to catch the eye
                        int len = res == 1 ? (age == 0 ? 2 : 3) : (age == 0 ? 3 : 4);
                        // clipped to the pond's area: where pond areas overlap, the neighbour shows its own glints
                        int start = Math.Max(x + age, x0), end = Math.Min(x + age + len, x1);
                        if (y < y0 || y >= y1 || end <= start) continue;
                        runs.Add(new GlintRun(start, y, end - start, age == 0));
                    }
                }
                frames[k] = runs.ToArray();
            }
            return frames;
        }

        /// <summary>Trees, bushes and rocks on open grass at a constant density, never in water or on paths.</summary>
        private static void PlaceProps(TestMap map, Rng rng)
        {
            int w = map.Width, h = map.Height;
            int target = (int)Math.Round(w * (double)h / PropArea);
            const int grid = 12;
            int gw = w / grid + 1, gh = h / grid + 1;
            var occupied = new bool[gw * gh];
            for (int attempts = 0; map.Props.Count < target && attempts < target * 25; attempts++)
            {
                int x = rng.Range(8, w - 9), y = rng.Range(20, h - 5);
                if (!map.IsGrassArea(x, y, 10)) continue;
                int gx = x / grid, gy = y / grid;
                bool free = true;
                for (int oy = Math.Max(0, gy - 1); oy <= Math.Min(gh - 1, gy + 1) && free; oy++)
                    for (int ox = Math.Max(0, gx - 1); ox <= Math.Min(gw - 1, gx + 1); ox++)
                        if (occupied[oy * gw + ox]) { free = false; break; }
                if (!free) continue;
                occupied[gy * gw + gx] = true;
                double r = rng.NextDouble();
                var kind = r < 0.45 ? PropKind.Tree : (r < 0.75 ? PropKind.Bush : PropKind.Rock);
                int variants = kind == PropKind.Tree ? TreeVariants : (kind == PropKind.Bush ? BushVariants : RockVariants);
                map.Props.Add(new PropSpot { X = x, Y = y, Kind = kind, Variant = rng.NextInt(variants), Scenery = -1, Facing = 6 });
            }
        }

        /// <summary>Downscaled overview (box filter on a 3×3 sample grid per minimap pixel), trees as dark dots.</summary>
        private static void BuildMinimap(TestMap map)
        {
            double scale = Math.Min(MinimapMaxWidth / (double)map.Width, MinimapMaxHeight / (double)map.Height);
            int mw = Math.Max(1, (int)Math.Round(map.Width * scale)), mh = Math.Max(1, (int)Math.Round(map.Height * scale));
            var buf = new byte[mw * mh * 4];
            for (int my = 0; my < mh; my++)
            {
                for (int mx = 0; mx < mw; mx++)
                {
                    int r = 0, g = 0, b = 0;
                    for (int sy = 0; sy < 3; sy++)
                        for (int sx = 0; sx < 3; sx++)
                        {
                            int x = Math.Min(map.TexWidth - 1, (int)((mx + (sx + 0.5) / 3.0) / scale * map.Resolution));
                            int y = Math.Min(map.TexHeight - 1, (int)((my + (sy + 0.5) / 3.0) / scale * map.Resolution));
                            int i = (y * map.TexWidth + x) * 4;
                            r += map.GroundRgba[i];
                            g += map.GroundRgba[i + 1];
                            b += map.GroundRgba[i + 2];
                        }
                    int o = (my * mw + mx) * 4;
                    buf[o] = (byte)(r / 9);
                    buf[o + 1] = (byte)(g / 9);
                    buf[o + 2] = (byte)(b / 9);
                    buf[o + 3] = 255;
                }
            }
            var treeDot = Color.Color8(36, 70, 48);
            foreach (var p in map.Props)
            {
                if (p.Kind != PropKind.Tree) continue;
                PutClip(buf, mw, mh, (int)(p.X * scale), (int)(p.Y * scale) - 1, treeDot);
            }
            map.MiniWidth = mw;
            map.MiniHeight = mh;
            map.MiniRgba = buf;
        }
    }
}
