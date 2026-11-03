using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PixelCreatures.Core;
using PixelCreatures.Core.Export;
using PixelCreatures.Core.Motion;
using PixelCreatures.Core.Palettes;
using PixelCreatures.Core.Rendering;

namespace PixelCreatures.Harness
{
    /// <summary>
    /// Renders labelled contact sheets (PNG) of sampled creations for visual quality reviews:
    /// one row per family (or per archetype with --by-archetype), a fixed uncurated seed list, an idle
    /// pose after the spring chains have settled, and an integer pixel upscale.
    /// </summary>
    public static class ContactSheet
    {
        public static int Run(string outDir, string[] args)
        {
            string filter = args.Length > 0 && args[0] != "all" ? args[0] : string.Empty;
            int count = args.Length > 1 ? int.Parse(args[1]) : 8;
            int scale = args.Length > 2 ? int.Parse(args[2]) : 2;
            bool byArch = args.Contains("--by-archetype");
            bool walk = args.Contains("--walk");
            int dir = 7;
            foreach (var a in args) if (a.StartsWith("--dir=")) dir = int.Parse(a.Substring(6));
            double density = 1;
            double action = -1;
            foreach (var a in args) if (a.StartsWith("--action=")) action = double.Parse(a.Substring(9), System.Globalization.CultureInfo.InvariantCulture);
            foreach (var a in args) if (a.StartsWith("--density=")) density = double.Parse(a.Substring(10), System.Globalization.CultureInfo.InvariantCulture);
            ulong seed0 = 101;
            foreach (var a in args) if (a.StartsWith("--seed=")) seed0 = ulong.Parse(a.Substring(7));

            var reg = Program.CreateRegistry();
            var factory = new CreatureFactory(reg);
            var families = reg.Families.Where(f => filter.Length == 0 || filter.Split(',').Contains(f.Id)).ToList();
            var rows = new List<(string label, List<CreatureModel> models)>();
            foreach (var f in families)
            {
                if (!byArch)
                {
                    var list = new List<CreatureModel>();
                    for (int i = 0; i < count; i++) list.Add(factory.Build(GenomeFactory.Sample(f, seed0 + (ulong)i * 101)));
                    rows.Add((f.DisplayName, list));
                    continue;
                }
                foreach (var arch in f.Archetypes)
                {
                    var list = new List<CreatureModel>();
                    ulong s = seed0;
                    int guard = 0;
                    while (list.Count < count && guard++ < 4000)
                    {
                        var g = GenomeFactory.Sample(f, s);
                        s += 7;
                        if (ArchetypeOf(f, s - 7) != arch.Id) continue;
                        list.Add(factory.Build(g));
                    }
                    rows.Add((f.DisplayName + " / " + arch.Label, list));
                }
            }

            int grid = 0;
            foreach (var a in args) if (a.StartsWith("--grid=")) grid = int.Parse(a.Substring(7));
            if (grid > 0)
            {
                // wrap all creations into rows of 'grid' cells
                var all = new List<CreatureModel>();
                foreach (var r in rows) all.AddRange(r.models);
                rows.Clear();
                for (int i = 0; i < all.Count; i += grid)
                    rows.Add(($"{families[0].DisplayName} {i / grid + 1}", all.GetRange(i, Math.Min(grid, all.Count - i))));
            }
            // render every cell first to size the grid
            var cells = new List<List<(byte[] rgba, int w, int h, int ox, int oy, string name)>>();
            foreach (var (_, models) in rows)
            {
                var row = new List<(byte[], int, int, int, int, string)>();
                foreach (var m in models)
                {
                    var cell = RenderCell(reg, m, dir, walk, density, action);
                    row.Add(cell);
                    // crop to content later; track maximum extents around the origin
                }
                cells.Add(row);
            }
            // content bounds relative to the origin, per row (small props are not lost next to giants)
            var rowDims = new List<(int left, int right, int up, int down)>();
            foreach (var row in cells)
            {
                int left = 0, right = 0, up = 0, down = 0;
                foreach (var (rgba, w, h, ox, oy, _) in row)
                {
                    Bounds(rgba, w, h, out int x0, out int y0, out int x1, out int y1);
                    if (x1 < x0) continue;
                    left = Math.Max(left, ox - x0); right = Math.Max(right, x1 - ox);
                    up = Math.Max(up, oy - y0); down = Math.Max(down, y1 - oy);
                }
                rowDims.Add((left, right, up, down));
            }
            int labelH = 12;
            int width = 8, height = 8;
            for (int r = 0; r < rows.Count; r++)
            {
                var (l, rr, u, d) = rowDims[r];
                width = Math.Max(width, cells[r].Count * (l + rr + 6) * scale + 8);
                height += (u + d + 6) * scale + 10 + labelH;
            }
            var canvas = new ImageCanvas(width, height, new Rgba(38, 44, 52, 255));
            var bg = new Rgba(58, 72, 64, 255);
            var bg2 = new Rgba(62, 77, 68, 255);
            int y = 4;
            for (int r = 0; r < rows.Count; r++)
            {
                var (left, right, up, down) = rowDims[r];
                int cellW = (left + right + 6) * scale, cellH = (up + down + 6) * scale + 10;
                canvas.Text(6, y + 3, rows[r].label.ToUpperInvariant(), new Rgba(230, 230, 220, 255), 1);
                for (int c = 0; c < cells[r].Count; c++)
                {
                    int x = 4 + c * cellW;
                    canvas.Fill(x, y + labelH, cellW - 2, cellH - 2, ((r + c) & 1) == 0 ? bg : bg2);
                    var (rgba, w, h, ox, oy, name) = cells[r][c];
                    int dx = x + (left + 3) * scale - ox * scale;
                    int dy = y + labelH + (up + 3) * scale - oy * scale;
                    canvas.Blit(rgba, w, h, dx, dy, scale);
                    canvas.Text(x + 2, y + labelH + cellH - 10, name.ToUpperInvariant(), new Rgba(200, 210, 200, 255), 1);
                }
                y += cellH + labelH;
            }
            Directory.CreateDirectory(outDir);
            string file = Path.Combine(outDir, $"sheet_{(filter.Length == 0 ? "all" : filter.Replace(',', '_'))}{(byArch ? "_arch" : "")}{(walk ? "_walk" : "")}{(grid > 0 ? "_g" + grid : "")}_d{dir}_x{density}.png");
            File.WriteAllBytes(file, canvas.ToPng());
            Console.WriteLine($"{file} ({width}x{height})");
            return 0;
        }

        private static string ArchetypeOf(ICreatureFamily f, ulong seed)
        {
            var archRng = Rng.Derive(seed, "archetype." + f.Id);
            var weights = new List<double>();
            foreach (var a in f.Archetypes) weights.Add(a.Weight);
            return f.Archetypes[archRng.Weighted(weights)].Id;
        }

        private static (byte[], int, int, int, int, string) RenderCell(CreatureRegistry reg, CreatureModel m, int dir, bool walk, double density, double action = -1)
        {
            var motor = new CreatureMotor(m, reg);
            double yaw = PixelCamera.DirectionYaw(dir);
            motor.Teleport(Vec3.Zero, yaw);
            motor.FacingOverride = yaw;
            if (walk) motor.DesiredVelocity = new Vec3(DMath.Cos(yaw), 0, -DMath.Sin(yaw)) * m.Motion.WalkSpeed;
            for (int i = 0; i < 75; i++) motor.Step(FixedClock.DefaultStep);
            if (action >= 0)
            {
                // trigger the action and capture it after 'action' seconds
                motor.TriggerAction();
                for (int i = 0; i < (int)Math.Round(action * 60); i++) motor.Step(FixedClock.DefaultStep);
            }
            var c = m.Anatomy.Canvas.Scaled(density);
            var frame = new PixelFrame(c.Width, c.Height) { OriginX = c.OriginX, OriginY = c.OriginY };
            var view = ViewSpec.Default(motor.Yaw, density);
            view.RootOffset = new Vec3(0, motor.Altitude, 0);
            new CreatureRenderer().Render(m.Anatomy, m.Palette, m.Surface, motor.Pose, view, frame);
            var lut = new byte[256 * 4];
            m.Palette.BuildLut(lut, 0);
            var rgba = new byte[frame.Width * frame.Height * 4];
            frame.ToRgba(lut, rgba, m.Palette.WaterTint);
            string arch = m.Anatomy.Traits.Count > 0 ? m.Anatomy.Traits[0] : m.Genome.Name;
            return (rgba, frame.Width, frame.Height, c.OriginX, c.OriginY, arch);
        }

        private static void Bounds(byte[] rgba, int w, int h, out int x0, out int y0, out int x1, out int y1)
        {
            x0 = w; y0 = h; x1 = -1; y1 = -1;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    if (rgba[(y * w + x) * 4 + 3] != 0)
                    {
                        if (x < x0) x0 = x;
                        if (x > x1) x1 = x;
                        if (y < y0) y0 = y;
                        if (y > y1) y1 = y;
                    }
        }
    }
}
