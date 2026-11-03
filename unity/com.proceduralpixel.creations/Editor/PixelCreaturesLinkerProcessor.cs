// Procedural Pixel Creations for Unity - keeps reflection-discovered extensions alive under IL2CPP.
//
// Families, shape blocks and motion modules of extensions are found by reflection ([CreatureExtension]).
// Managed code stripping cannot see that, so every assembly that references the core is preserved via
// an additional link.xml generated at build time (Runtime/link.xml covers the package's own assemblies).

#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.UnityLinker;

namespace PixelCreatures.Unity.Editor
{
    internal sealed class PixelCreaturesLinkerProcessor : IUnityLinkerProcessor
    {
        public int callbackOrder => 0;

        public string GenerateAdditionalLinkXmlFile(BuildReport report, UnityLinkerBuildPipelineData data)
        {
            string core = typeof(PixelCreatures.Core.CreatureRegistry).Assembly.GetName().Name ?? "PixelCreatures.Core";
            // only assemblies that are part of this player build: naming an assembly the linker does not
            // get as input fails the build with older linkers
            string input = data?.inputDirectory ?? string.Empty;
            var names = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic && (a.GetName().Name == core || References(a, core)))
                .Select(a => a.GetName().Name)
                .Where(n => !string.IsNullOrEmpty(n) && InBuild(input, n!))
                .Distinct()
                .OrderBy(n => n, StringComparer.Ordinal);
            var sb = new StringBuilder("<linker>\n");
            foreach (var n in names) sb.Append("  <assembly fullname=\"").Append(n).Append("\" preserve=\"all\"/>\n");
            sb.Append("</linker>\n");
            string dir = Path.Combine("Temp", "PixelCreatures");
            Directory.CreateDirectory(dir);
            string path = Path.GetFullPath(Path.Combine(dir, "link.xml"));
            File.WriteAllText(path, sb.ToString());
            return path;
        }

        private static bool InBuild(string inputDirectory, string name)
        {
            if (string.IsNullOrEmpty(inputDirectory) || !Directory.Exists(inputDirectory))
                return !name.EndsWith(".Editor", StringComparison.Ordinal) && !name.EndsWith("-Editor", StringComparison.Ordinal) && !name.Contains("Tests");
            return File.Exists(Path.Combine(inputDirectory, name + ".dll"));
        }

        private static bool References(System.Reflection.Assembly a, string core)
        {
            try { return a.GetReferencedAssemblies().Any(r => r.Name == core); }
            catch (Exception) { return false; }
        }

        // required by older Unity versions of the interface, unused by newer ones
        public void OnBeforeRun(BuildReport report, UnityLinkerBuildPipelineData data) { }
        public void OnAfterRun(BuildReport report, UnityLinkerBuildPipelineData data) { }
    }
}
