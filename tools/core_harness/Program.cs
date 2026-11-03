using System;
using System.IO;
using PixelCreatures.Core;
using PixelCreatures.Core.Genetics;
using PixelCreatures.Core.Serialization;

namespace PixelCreatures.Harness
{
    public static class Program
    {
        public static CreatureRegistry CreateRegistry()
        {
            var registry = CreatureRegistry.CreateDefault();
            registry.DiscoverExtensions(typeof(Program).Assembly);
            return registry;
        }

        public static int Main(string[] args)
        {
            string command = args.Length > 0 ? args[0] : "help";
            string outDir = Path.GetFullPath(args.Length > 1 ? args[1] : "reports");
            if (command == "test") Directory.CreateDirectory(outDir);
            switch (command)
            {
                case "test":
                {
                    // test <outDir> [filter|all] [seedsPerFamily]  ("all" = no filter; empty arguments get lost in some shells)
                    string filter = args.Length > 2 && args[2] != "all" && args[2] != "*" ? args[2] : string.Empty;
                    var runner = new PixelCreatures.Tests.TestRunner { Log = Console.WriteLine, Filter = filter };
                    PixelCreatures.Tests.CoreTests.RunAll(runner, Program.CreateRegistry(), args.Length > 3 ? int.Parse(args[3]) : 100);
                    File.WriteAllText(Path.Combine(outDir, "core_test_report.json"), runner.ReportJson("console-harness " + Environment.OSVersion));
                    Console.WriteLine(runner.Summary());
                    return runner.Failed == 0 ? 0 : 1;
                }
                case "goldens":
                {
                    var rng = new Rng(42);
                    Console.WriteLine($"Pcg42 = {{ {rng.NextUInt()}u, {rng.NextUInt()}u, {rng.NextUInt()}u, {rng.NextUInt()}u }}");
                    Console.WriteLine($"Fnv = {StableHash.Fnv1a64("Procedural Pixel Creatures")}UL");
                    Console.WriteLine($"Derived = {StableHash.Derive(123456789UL, "stream.anatomy")}UL");
                    Console.WriteLine($"exp1 bits = 0x{BitConverter.DoubleToInt64Bits(DMath.Exp(1.0)):X16}L");
                    var reg = Program.CreateRegistry();
                    Console.WriteLine("Golden.Values:");
                    foreach (var (key, value) in PixelCreatures.Tests.CoreTests.GoldenValues(reg, new CreatureFactory(reg)))
                        Console.WriteLine($"            [\"{key}\"] = 0x{value:x16}UL,");
                    return 0;
                }
                case "samples":
                {
                    // samples <samplesRoot> family[,family]: one sample per archetype (at least six) as ppc.creature presets
                    var reg = Program.CreateRegistry();
                    var ser = new GenomeSerializer(reg);
                    foreach (string id in (args.Length > 2 ? args[2] : "").Split(',', StringSplitOptions.RemoveEmptyEntries))
                    {
                        var f = reg.GetFamily(id);
                        string dir = Path.Combine(outDir, id);
                        Directory.CreateDirectory(dir);
                        var picked = new System.Collections.Generic.List<CreatureGenome>();
                        var seen = new System.Collections.Generic.HashSet<string>();
                        for (ulong seed = 1; seed < 5000 && (seen.Count < f.Archetypes.Count || picked.Count < 6); seed++)
                        {
                            var g = GenomeFactory.Sample(f, seed * 7919UL);
                            string arch = g.GetChoice("form");
                            if (seen.Contains(arch) && (seen.Count < f.Archetypes.Count || picked.Count >= 6)) continue;
                            seen.Add(arch);
                            picked.Add(g);
                        }
                        for (int i = 0; i < picked.Count; i++)
                            File.WriteAllText(Path.Combine(dir, $"{id}_{i + 1:00}.json"), ser.Serialize(picked[i]));
                        Console.WriteLine($"{id}: {picked.Count} samples");
                    }
                    return 0;
                }
                case "sheet":
                    // sheet <outDir> [family[,family]|all] [count] [scale] [--by-archetype] [--walk] [--dir=N] [--seed=N]
                    return ContactSheet.Run(outDir, args.Length > 2 ? args[2..] : Array.Empty<string>());
                default:
                    Console.WriteLine("CoreHarness test <outdir> [filter|all] [seedsPerFamily]");
                    Console.WriteLine("CoreHarness sheet <outdir> [family|all] [count] [scale] [--by-archetype] [--walk] [--dir=N]");
                    Console.WriteLine("CoreHarness goldens  (print deterministic baseline values)");
                    return command == "help" ? 0 : 1;
            }
        }
    }
}
