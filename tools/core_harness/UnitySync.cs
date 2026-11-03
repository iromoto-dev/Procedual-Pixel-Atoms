using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace PixelCreatures.Harness
{
    /// <summary>
    /// Keeps the Unity package (unity/com.proceduralpixel.creations) in sync with the repository:
    /// mirrors the engine independent core, the plantfolk extension, the core tests and the example presets
    /// into the package and writes a .meta file with a stable GUID for every file and folder (Unity needs
    /// them in packages installed from git). With --check nothing is written; differences are reported
    /// and the exit code is 1, so CI catches a package that drifted from the sources.
    /// </summary>
    public static class UnitySync
    {
        public const string PackageName = "com.proceduralpixel.creations";

        private static readonly (string from, string to, string ext)[] Mirrors =
        {
            ("addons/procedural_creatures/Core", "Runtime/Core", ".cs"),
            ("addons/procedural_creatures_plantfolk/Core", "Runtime/Plantfolk", ".cs"),
            ("tests/Core", "Tests/Editor/Core", ".cs"),
            ("addons/procedural_creatures/Samples", "Samples~/Presets", ".json"),
            ("addons/procedural_creatures_plantfolk/Samples", "Samples~/Presets", ".json"),
        };

        private static readonly (string from, string to)[] SingleFiles =
        {
            ("LICENSE", "LICENSE.md"),
            ("THIRD_PARTY_NOTICES.md", "THIRD_PARTY_NOTICES.md"),
        };

        public static int Run(string repoRoot, bool check)
        {
            repoRoot = Path.GetFullPath(repoRoot);
            string pkg = Path.Combine(repoRoot, "unity", PackageName);
            if (!Directory.Exists(pkg)) { Console.Error.WriteLine($"Package folder not found: {pkg}"); return 2; }
            var desired = new SortedDictionary<string, byte[]>(StringComparer.Ordinal);
            foreach (var (from, to, ext) in Mirrors)
            {
                string src = Path.Combine(repoRoot, from);
                if (!Directory.Exists(src)) continue;
                foreach (var file in Directory.GetFiles(src, "*" + ext, SearchOption.AllDirectories).OrderBy(f => f, StringComparer.Ordinal))
                {
                    string rel = Path.GetRelativePath(src, file).Replace('\\', '/');
                    desired[$"{to}/{rel}"] = Normalize(File.ReadAllBytes(file));
                }
            }
            foreach (var (from, to) in SingleFiles) desired[to] = Normalize(File.ReadAllBytes(Path.Combine(repoRoot, from)));

            var problems = new List<string>();
            // mirrored files: create, update, and remove files that no longer exist in the sources
            foreach (var kv in desired)
            {
                string path = Path.Combine(pkg, kv.Key);
                if (File.Exists(path) && File.ReadAllBytes(path).AsSpan().SequenceEqual(kv.Value)) continue;
                problems.Add((File.Exists(path) ? "changed: " : "missing: ") + kv.Key);
                if (!check) { Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllBytes(path, kv.Value); }
            }
            foreach (var (_, to, ext) in Mirrors.Distinct())
            {
                string dir = Path.Combine(pkg, to);
                if (!Directory.Exists(dir)) continue;
                foreach (var file in Directory.GetFiles(dir, "*" + ext, SearchOption.AllDirectories))
                {
                    string rel = Path.GetRelativePath(pkg, file).Replace('\\', '/');
                    if (desired.ContainsKey(rel)) continue;
                    problems.Add("stale: " + rel);
                    if (!check) File.Delete(file);
                }
            }
            if (!check) RemoveEmptyDirectories(pkg);

            // .meta files for every visible file and folder (folders ending in '~' are ignored by Unity)
            var metas = new SortedDictionary<string, string>(StringComparer.Ordinal);
            CollectMetas(pkg, pkg, desired, check, metas);
            foreach (var kv in metas)
            {
                string path = Path.Combine(pkg, kv.Key);
                string want = kv.Value;
                if (File.Exists(path) && File.ReadAllText(path) == want) continue;
                problems.Add((File.Exists(path) ? "changed: " : "missing: ") + kv.Key);
                if (!check) File.WriteAllText(path, want);
            }
            foreach (var meta in Directory.GetFiles(pkg, "*.meta", SearchOption.AllDirectories))
            {
                string rel = Path.GetRelativePath(pkg, meta).Replace('\\', '/');
                if (metas.ContainsKey(rel) || rel.Split('/').Any(p => p.EndsWith("~", StringComparison.Ordinal))) continue;
                problems.Add("stale: " + rel);
                if (!check) File.Delete(meta);
            }

            foreach (var p in problems) Console.WriteLine(p);
            if (check)
            {
                Console.WriteLine(problems.Count == 0 ? "Unity package is in sync." : $"Unity package is out of sync ({problems.Count} differences). Run: CoreHarness unity-sync <repo>");
                return problems.Count == 0 ? 0 : 1;
            }
            Console.WriteLine($"Unity package synced ({problems.Count} updates).");
            return 0;
        }

        /// <summary>Line endings as in the repository's .gitattributes (LF), so the check is platform independent.</summary>
        private static byte[] Normalize(byte[] data)
        {
            string s = Encoding.UTF8.GetString(data).Replace("\r\n", "\n");
            var bom = data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF;
            var body = Encoding.UTF8.GetBytes(bom ? s.TrimStart('﻿') : s);
            return bom ? new byte[] { 0xEF, 0xBB, 0xBF }.Concat(body).ToArray() : body;
        }

        private static void CollectMetas(string pkg, string dir, SortedDictionary<string, byte[]> desired, bool check, SortedDictionary<string, string> metas)
        {
            // in check mode the mirrored files may not exist on disk yet: include them virtually
            var entries = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var d in Directory.GetDirectories(dir)) entries.Add(Path.GetRelativePath(pkg, d).Replace('\\', '/') + "/");
            foreach (var f in Directory.GetFiles(dir)) entries.Add(Path.GetRelativePath(pkg, f).Replace('\\', '/'));
            if (check)
            {
                string relDir = dir == pkg ? string.Empty : Path.GetRelativePath(pkg, dir).Replace('\\', '/') + "/";
                foreach (var k in desired.Keys)
                {
                    if (!k.StartsWith(relDir, StringComparison.Ordinal)) continue;
                    string rest = k.Substring(relDir.Length);
                    int slash = rest.IndexOf('/');
                    entries.Add(slash < 0 ? k : relDir + rest.Substring(0, slash + 1));
                }
            }
            foreach (var e in entries)
            {
                bool folder = e.EndsWith("/", StringComparison.Ordinal);
                string rel = folder ? e.TrimEnd('/') : e;
                string name = rel.Substring(rel.LastIndexOf('/') + 1);
                if (name.StartsWith(".", StringComparison.Ordinal) || name.EndsWith(".meta", StringComparison.Ordinal)) continue;
                if (folder && name.EndsWith("~", StringComparison.Ordinal)) continue;
                metas[rel + ".meta"] = Meta(rel, folder);
                if (folder)
                {
                    string sub = Path.Combine(pkg, rel);
                    if (Directory.Exists(sub)) CollectMetas(pkg, sub, desired, check, metas);
                    else if (check) CollectVirtual(rel, desired, metas);
                }
            }
        }

        private static void CollectVirtual(string relDir, SortedDictionary<string, byte[]> desired, SortedDictionary<string, string> metas)
        {
            string prefix = relDir + "/";
            foreach (var k in desired.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)))
            {
                metas[k + ".meta"] = Meta(k, false);
                string rest = k.Substring(prefix.Length);
                var parts = rest.Split('/');
                string acc = relDir;
                for (int i = 0; i + 1 < parts.Length; i++) { acc += "/" + parts[i]; metas[acc + ".meta"] = Meta(acc, true); }
            }
        }

        private static void RemoveEmptyDirectories(string dir)
        {
            foreach (var d in Directory.GetDirectories(dir))
            {
                RemoveEmptyDirectories(d);
                bool empty = !Directory.EnumerateFileSystemEntries(d).Any(e => !e.EndsWith(".meta", StringComparison.Ordinal));
                if (empty && !Directory.EnumerateDirectories(d).Any())
                {
                    foreach (var m in Directory.GetFiles(d)) File.Delete(m);
                    Directory.Delete(d);
                    string meta = d + ".meta";
                    if (File.Exists(meta)) File.Delete(meta);
                }
            }
        }

        /// <summary>Stable GUID per package path, so every sync produces the same .meta files.</summary>
        public static string Guid(string relPath)
        {
            using var md5 = MD5.Create();
            var h = md5.ComputeHash(Encoding.UTF8.GetBytes(PackageName + "/" + relPath));
            return string.Concat(h.Select(b => b.ToString("x2")));
        }

        private static string Meta(string rel, bool folder)
        {
            string guid = Guid(rel);
            string importer;
            if (folder) return $"fileFormatVersion: 2\nguid: {guid}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n";
            string ext = Path.GetExtension(rel).ToLowerInvariant();
            if (ext == ".cs")
                return $"fileFormatVersion: 2\nguid: {guid}\nMonoImporter:\n  externalObjects: {{}}\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {{instanceID: 0}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n";
            if (ext == ".asmdef") importer = "AssemblyDefinitionImporter";
            else if (rel == "package.json") importer = "PackageManifestImporter";
            else if (ext == ".json" || ext == ".md" || ext == ".txt" || ext == ".xml") importer = "TextScriptImporter";
            else importer = "DefaultImporter";
            return $"fileFormatVersion: 2\nguid: {guid}\n{importer}:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n";
        }
    }
}
