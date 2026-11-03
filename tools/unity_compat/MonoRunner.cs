// Procedural Pixel Creatures - core test runner for the Unity compatibility check.
//
// scripts/unity-check.sh compiles the engine independent core, the plantfolk extension and the core
// tests the way Unity does (Roslyn, C# 9, .NET Standard 2.1 API surface) and runs them on Mono, the
// runtime Unity's editor and Mono player builds use. The golden values must match bit for bit.

using System;
using PixelCreatures.Core;

namespace PixelCreatures.UnityCompat
{
    public static class MonoRunner
    {
        public static int Main(string[] args)
        {
            int seeds = args.Length > 0 ? int.Parse(args[0]) : 20;
            string filter = args.Length > 1 ? args[1] : string.Empty;
            var registry = CreatureRegistry.CreateDefault();
            registry.DiscoverExtensions(typeof(MonoRunner).Assembly);
            var runner = new PixelCreatures.Tests.TestRunner { Log = Console.WriteLine, Filter = filter };
            PixelCreatures.Tests.CoreTests.RunAll(runner, registry, seeds);
            Console.WriteLine(runner.Summary());
            return runner.Failed == 0 ? 0 : 1;
        }
    }
}
