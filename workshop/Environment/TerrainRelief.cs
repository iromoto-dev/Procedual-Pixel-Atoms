using System;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using PixelCreatures.Core;

namespace PixelCreatures.Workshop
{
    /// <summary>
    /// A strip of cliff face drawn as its own sprite, so it takes part in the depth ordering of trees and creatures.
    /// X, Y (bottom edge, the sort key), Width and Height are in ground texels (map units × Resolution).
    /// </summary>
    public sealed class ReliefFace
    {
        public int X, Y, Width, Height;
        public byte[] Rgba = Array.Empty<byte>();
    }

    public static partial class EnvironmentArt
    {
        // Terrain coordinates describe the ground plane. The texture, props and actors all use the
        // same projection y - elevation. Paths cut gently graded ramps through the terraced hills.
        // Elevation and water depth are logical layers (one cell per map unit); the water colours, the
        // projected ground and the cliff faces are images on the texel grid (tex = terrain per texel).
        private static void BuildRelief(TestMap map, byte[] tex, CancellationToken cancel)
        {
            int w = map.Width, h = map.Height, res = map.Resolution, tw = map.TexWidth, th = map.TexHeight;
            map.Elevation = new float[w * h];
            map.WaterDepth = new float[w * h];
            map.WaterSurfaceRgba = new byte[tw * th * 4];
            var pathDistance = new byte[w * h];
            for (int i = 0; i < pathDistance.Length; i++) pathDistance[i] = map.Terrain[i] == TestMap.Path ? (byte)0 : (byte)100;
            for (int y = 1; y < h; y++)
                for (int x = 1; x < w; x++)
                { int i = y * w + x; pathDistance[i] = (byte)Math.Min(pathDistance[i], 1 + Math.Min(pathDistance[i - 1], pathDistance[i - w])); }
            for (int y = h - 2; y >= 0; y--)
                for (int x = w - 2; x >= 0; x--)
                { int i = y * w + x; pathDistance[i] = (byte)Math.Min(pathDistance[i], 1 + Math.Min(pathDistance[i + 1], pathDistance[i + w])); }
            Parallel.For(0, h, new ParallelOptions { CancellationToken = cancel }, y =>
            {
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    double shore = 1e6;
                    foreach (var p in map.Ponds)
                    {
                        double dx = (x - p.Cx) / p.Rx, dy = (y - p.Cy) / p.Ry;
                        double v = Math.Sqrt(dx * dx + dy * dy) + PatternNoise.Perlin2(p.ShapeSeed, dx * 1.7, dy * 1.7) * 0.3;
                        shore = Math.Min(shore, (v - 1) * Math.Min(p.Rx, p.Ry));
                    }
                    if (map.Terrain[i] == TestMap.Water)
                    {
                        map.WaterDepth[i] = (float)Math.Clamp(-shore / 42, 0, 1);
                        continue;
                    }
                    double n = PatternNoise.Perlin2(map.Seed + 702, x / 210.0, y / 155.0);
                    // A broad mesa guarantees a readable elevation change even on the smallest map.
                    double hx = (x - w * 0.27) / Math.Min(190, w * 0.25), hy = (y - h * 0.29) / Math.Min(130, h * 0.25);
                    double mound = Math.Max(0, 1 - Math.Sqrt(hx * hx + hy * hy));
                    double raw = Math.Max(mound * 45, Math.Max(0, n + 0.08) * 65);
                    double terrace = Math.Floor(raw / 12) * 12;
                    // A north/south access ramp at every hill band, wide enough for large creatures.
                    double rampCenter = w * 0.27 + Math.Sin(y * 0.012) * 12;
                    double ramp = Math.Clamp((Math.Abs(x - rampCenter) - 20) / 22, 0, 1);
                    ramp = Math.Min(ramp, Math.Clamp(pathDistance[i] / 24.0, 0, 1));
                    ramp = ramp * ramp * (3 - 2 * ramp);
                    double elevation = raw * (1 - ramp) + terrace * ramp;
                    double bank = Math.Clamp((shore - 16) / 40, 0, 1);
                    bank = bank * bank * (3 - 2 * bank);
                    double edge = Math.Clamp(Math.Min(Math.Min(x, w - x - 1), Math.Min(y, h - y - 1)) / 42.0, 0, 1);
                    map.Elevation[i] = (float)(elevation * bank * edge);
                }
            });
            // Water: depth tint, grain and caustic lines per texel (depth interpolated between map cells).
            var shallow = Color.Color8(75, 151, 145);
            var deep = Color.Color8(21, 61, 89);
            Parallel.For(0, th, new ParallelOptions { CancellationToken = cancel }, ty =>
            {
                double y = ty / (double)res;
                for (int tx = 0; tx < tw; tx++)
                {
                    int ti = ty * tw + tx;
                    if (tex[ti] != TestMap.Water) continue;
                    double x = tx / (double)res;
                    float depth = Sample(map.WaterDepth, w, h, x, y);
                    double grain = Hash(map.Seed + 601, tx / 2, ty / 2);
                    Color c = shallow.Lerp(deep, depth * 0.9f);
                    c = c.Lightened((float)(grain * 0.055));
                    // Sand bars and quiet caustic lines remain visible through the water.
                    double caustic = Math.Sin(x * 0.17 + Math.Sin(y * 0.13) * 2.4) + Math.Cos(y * 0.26 + x * 0.04);
                    if (caustic > 1.82) c = c.Lightened(0.14f * (1 - depth * 0.6f));
                    PutClip(map.GroundRgba, tw, th, tx, ty, c);
                    int j = ti * 4;
                    map.WaterSurfaceRgba[j] = 39;
                    map.WaterSurfaceRgba[j + 1] = 125;
                    map.WaterSurfaceRgba[j + 2] = 151;
                    map.WaterSurfaceRgba[j + 3] = (byte)(30 + depth * 22);
                }
            });
            // Sunlit crowns, darker slopes, mottled paths and stone fragments. No blurry resampling: every texel
            // moves up by its elevation (in texels); rows a cliff pulls apart are filled with rock strata.
            float E(double x, double y) => ImageElevation(map.Elevation, w, h, x, y);
            // A texel only ever writes into its own column, and within a column later (southern) rows paint over
            // earlier ones: columns are independent, so blocks of columns run in parallel with identical results.
            var source = map.GroundRgba;
            var projected = new byte[source.Length];
            Array.Copy(source, projected, source.Length);
            const int block = 32;
            Parallel.For(0, (tw + block - 1) / block, new ParallelOptions { CancellationToken = cancel }, bx =>
            {
            int x0 = Math.Max(1, bx * block), x1 = Math.Min(tw - 1, bx * block + block);
            for (int ty = 1; ty < th - 1; ty++)
            {
                double y = ty / (double)res;
                for (int tx = x0; tx < x1; tx++)
                {
                    double x = tx / (double)res;
                    float e = E(x, y);
                    int i = ty * tw + tx, top = ty - (int)Math.Round(e * res);
                    if (top < 0) continue;
                    float slope = (E(x + 1, y) - E(x - 1, y)) * 0.035f + (E(x, y + 1) - E(x, y - 1)) * 0.055f;
                    float light = Math.Clamp(1.0f + slope + e * 0.0017f, 0.77f, 1.16f);
                    int target = (top * tw + tx) * 4;
                    for (int c = 0; c < 3; c++) projected[target + c] = (byte)Math.Clamp(source[i * 4 + c] * light, 0, 255);
                    projected[target + 3] = 255;
                    float below = E(x, (ty + 1) / (double)res);
                    int next = ty + 1 - (int)Math.Round(below * res);
                    for (int fy = top + 1; fy < next && fy < th; fy++)
                    {
                        if (e - below < 2.5f / res)
                        {
                            Array.Copy(projected, target, projected, (fy * tw + tx) * 4, 4);
                            continue;
                        }
                        double grain = Hash(map.Seed + 710, tx / 3, fy / 2);
                        var rock = grain > 0.6 ? Color.Color8(116, 102, 76) : Color.Color8(86, 84, 67);
                        if ((fy - top) % 5 == 0) rock = Color.Color8(67, 71, 59);
                        if (fy == top + 1) rock = Color.Color8(146, 139, 92);
                        PutClip(projected, tw, th, tx, fy, rock);
                    }
                }
            }
            });
            cancel.ThrowIfCancellationRequested();
            // Small cliff strips participate in the same depth ordering as trees and creatures. Strips are found on
            // the map grid; their pixels are copied from the projected image on the texel grid.
            for (int y = 1; y < h - 1; y += 2)
            {
                int start = -1;
                for (int x = 1; x < w; x++)
                {
                    bool cliff = x < w - 1 && map.Elevation[y * w + x] - map.Elevation[(y + 1) * w + x] > 4;
                    if (cliff && start < 0) start = x;
                    if ((!cliff || x == w - 1) && start >= 0)
                    {
                        int below = Math.Min(h - 1, y + 2);
                        int Top(int tx) => y * res - (int)(E(tx / (double)res, y) * res);
                        int Bottom(int tx) => (y + 2) * res - (int)(E(tx / (double)res, below) * res);
                        int minTop = y * res, maxBottom = 0;
                        for (int tx = start * res; tx < x * res; tx++)
                        {
                            minTop = Math.Min(minTop, Top(tx));
                            maxBottom = Math.Max(maxBottom, Bottom(tx));
                        }
                        int fh = maxBottom - minTop;
                        if (fh > 2 * res && minTop >= 0)
                        {
                            int fw = (x - start) * res;
                            var face = new ReliefFace { X = start * res, Y = maxBottom, Width = fw, Height = fh, Rgba = new byte[fw * fh * 4] };
                            for (int tx = start * res; tx < x * res; tx++)
                            {
                                int t = Top(tx), b = Bottom(tx);
                                for (int yy = t; yy < b; yy++)
                                    Array.Copy(projected, (yy * tw + tx) * 4, face.Rgba, ((yy - minTop) * fw + tx - face.X) * 4, 4);
                            }
                            map.Cliffs.Add(face);
                        }
                        start = -1;
                    }
                }
            }
            map.GroundRgba = projected;
        }

        /// <summary>
        /// Elevation for the images: bilinear on slopes, but inside a map cell that drops off a cliff towards the south
        /// the upper terrace holds until the cell ends. Interpolating across the step would put a texel row halfway down
        /// the cliff face (with its own grass colour, rim and strata); this way an HD cliff stays one clean face.
        /// </summary>
        private static float ImageElevation(float[] e, int w, int h, double x, double y)
        {
            int y0 = Math.Clamp((int)Math.Floor(y), 0, h - 1);
            if (y > y0 && y0 + 1 < h)
            {
                float upper = Sample(e, w, h, x, y0), lower = Sample(e, w, h, x, y0 + 1);
                if (upper - lower >= 2.5f) return upper;
            }
            return Sample(e, w, h, x, y);
        }

        /// <summary>
        /// Bilinear sample of a map-grid layer at a map point. Exact at whole coordinates, so texel (r*x, r*y) sees the
        /// value of cell (x, y) - the images at resolution 1 are the classic ones, bit for bit.
        /// </summary>
        private static float Sample(float[] f, int w, int h, double x, double y)
        {
            int x0 = Math.Clamp((int)Math.Floor(x), 0, w - 1), y0 = Math.Clamp((int)Math.Floor(y), 0, h - 1);
            float fx = (float)Math.Clamp(x - x0, 0, 1), fy = (float)Math.Clamp(y - y0, 0, 1);
            int x1 = Math.Min(x0 + 1, w - 1), y1 = Math.Min(y0 + 1, h - 1);
            float a = f[y0 * w + x0], b = f[y0 * w + x1], c = f[y1 * w + x0], d = f[y1 * w + x1];
            float top = fx == 0 ? a : a + (b - a) * fx;
            float bottom = fx == 0 ? c : c + (d - c) * fx;
            return fy == 0 ? top : top + (bottom - top) * fy;
        }

        private static void BuildClearance(TestMap map)
        {
            int w = map.Width, h = map.Height;
            var d = new byte[w * h];
            Array.Fill(d, (byte)250);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    bool water = map.IsWater(x, y);
                    if (x == 0 || y == 0 || x == w - 1 || y == h - 1 ||
                        map.IsWater(x - 1, y) != water || map.IsWater(x + 1, y) != water ||
                        map.IsWater(x, y - 1) != water || map.IsWater(x, y + 1) != water ||
                        (!water && (Math.Abs(map.Elevation[i] - map.Elevation[i - w]) > 2 || Math.Abs(map.Elevation[i] - map.Elevation[i - 1]) > 2))) d[i] = 0;
                }
            foreach (var p in map.Props)
            {
                var e = map.FootprintExtents(p);
                int rx = (int)Math.Ceiling(e.X), ry = (int)Math.Ceiling(e.Y);
                // static props block a square; scenery its elliptical base (the spot itself is always solid)
                bool square = map.SceneryOf(p) == null;
                for (int y = Math.Max(0, p.Y - ry); y <= Math.Min(h - 1, p.Y + ry); y++)
                    for (int x = Math.Max(0, p.X - rx); x <= Math.Min(w - 1, p.X + rx); x++)
                    {
                        float dx = (x - p.X) / Math.Max(1f, e.X), dy = (y - p.Y) / Math.Max(1f, e.Y);
                        if (square || dx * dx + dy * dy <= 1f) d[y * w + x] = 0;
                    }
            }
            // Chebyshev distance is conservative: a square footprint is fully clear of obstacles.
            for (int y = 1; y < h; y++)
                for (int x = 1; x < w - 1; x++)
                {
                    int i = y * w + x;
                    d[i] = (byte)Math.Min(d[i], 1 + Math.Min(Math.Min(d[i - 1], d[i - w]), Math.Min(d[i - w - 1], d[i - w + 1])));
                }
            for (int y = h - 2; y >= 0; y--)
                for (int x = w - 2; x > 0; x--)
                {
                    int i = y * w + x;
                    d[i] = (byte)Math.Min(d[i], 1 + Math.Min(Math.Min(d[i + 1], d[i + w]), Math.Min(d[i + w - 1], d[i + w + 1])));
                }
            map.Clearance = d;
        }
    }
}
