// Procedural Pixel Creations for Unity - keeps reflection-discovered extensions alive under IL2CPP.
//
// Extensions ([CreatureExtension] classes that register families, shape blocks, palette rules, biomes and
// motion modules) are created by reflection. Managed code stripping cannot see that, so this processor
// writes a link.xml at build time that preserves exactly those classes (not the rest of the game code) and
// the package's own assemblies. Extensions in assemblies the editor does not load (player-only assemblies)
// need a link.xml entry or [Preserve] of their own.

#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Text;
using PixelCreatures.Core;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.UnityLinker;

namespace PixelCreatures.Unity.Editor
{
    internal sealed class PixelCreaturesLinkerProcessor : IUnityLinkerProcessor
    {
        private static readonly string[] PackageAssemblies = { "PixelCreatures.Core", "PixelCreatures.Plantfolk", "PixelCreatures.Unity" };

        public int callbackOrder => 0;

        public string GenerateAdditionalLinkXmlFile(BuildReport report, UnityLinkerBuildPipelineData data)
        {
            string input = data?.inputDirectory ?? string.Empty;
            var sb = new StringBuilder("<linker>\n");
            foreach (var asm in PackageAssemblies)
                if (InBuild(input, asm)) sb.Append("  <assembly fullname=\"").Append(asm).Append("\" preserve=\"all\"/>\n");
            var extensions = TypeCache.GetTypesWithAttribute<CreatureExtensionAttribute>()
                .Where(t => !t.IsAbstract && t.Assembly.GetName().Name is string n && !PackageAssemblies.Contains(n) && InBuild(input, n))
                .GroupBy(t => t.Assembly.GetName().Name!)
                .OrderBy(g => g.Key, StringComparer.Ordinal);
            foreach (var group in extensions)
            {
                sb.Append("  <assembly fullname=\"").Append(group.Key).Append("\">\n");
                foreach (var t in group.OrderBy(t => t.FullName, StringComparer.Ordinal))
                    sb.Append("    <type fullname=\"").Append(XmlEscape((t.FullName ?? t.Name).Replace('+', '/'))).Append("\" preserve=\"all\"/>\n");
                sb.Append("  </assembly>\n");
            }
            sb.Append("</linker>\n");
            string dir = Path.Combine("Temp", "PixelCreatures");
            Directory.CreateDirectory(dir);
            string path = Path.GetFullPath(Path.Combine(dir, "link.xml"));
            File.WriteAllText(path, sb.ToString());
            return path;
        }

        /// <summary>Only assemblies that are part of this player build (naming others fails the build with older linkers).</summary>
        private static bool InBuild(string inputDirectory, string name)
        {
            if (string.IsNullOrEmpty(inputDirectory) || !Directory.Exists(inputDirectory))
                return !name.EndsWith(".Editor", StringComparison.Ordinal) && !name.EndsWith("-Editor", StringComparison.Ordinal) && !name.Contains("Tests");
            return File.Exists(Path.Combine(inputDirectory, name + ".dll"));
        }

        private static string XmlEscape(string s) => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");

        // required by older Unity versions of the interface, unused by newer ones
        public void OnBeforeRun(BuildReport report, UnityLinkerBuildPipelineData data) { }
        public void OnAfterRun(BuildReport report, UnityLinkerBuildPipelineData data) { }
    }
}
