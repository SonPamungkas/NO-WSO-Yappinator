using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace WSOYappinator
{
    internal static class AudioPackDiscovery
    {
        public static string[] Discover(string root)
        {
            if (!Directory.Exists(root)) return Array.Empty<string>();

            List<string> packs = new();

            if (HasPriorities(root))
                packs.Add(".");

            try
            {
                foreach (string dir in Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories))
                {
                    if (!HasPriorities(dir)) continue;
                    packs.Add(Path.GetRelativePath(root, dir));
                }
            }
            catch (Exception ex)
            {
                Plugin.I.Log.LogError($"Failed scanning for packs under {root}: {ex}");
            }

            return packs
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static bool HasPriorities(string dir)
            => Directory.EnumerateFiles(dir, "*", SearchOption.TopDirectoryOnly)
                .Any(f => string.Equals(Path.GetFileName(f), FileEventPriorityLoader.PrioritiesFileName, StringComparison.OrdinalIgnoreCase));
    }
}