using System;
using System.Collections.Generic;
using System.IO;

namespace WSOYappinator
{
    public sealed class FileEventPriorityLoader
    {
        public const string PrioritiesFileName = "eventPriorities.txt";
        public Dictionary<string, int> Load(string audioSetFolder)
        {
            Dictionary<string, int> dict = new(StringComparer.OrdinalIgnoreCase);
            string path = Path.Combine(audioSetFolder, "eventPriorities.txt");

            if (!File.Exists(path))
            {
                Plugin.I.Log.LogWarning($"No eventPriorities.txt found in: {audioSetFolder}");
                return dict;
            }
            try
            {
                foreach (string raw in File.ReadAllLines(path))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith("#")) continue;

                    string[] parts = line.Split('=', 2, StringSplitOptions.None);
                    if (parts.Length != 2) continue;

                    string key = parts[0].Trim();
                    if (int.TryParse(parts[1].Trim(), out int pri))
                        dict[key] = pri;
                    else
                        Plugin.I.Log.LogWarning($"bad priority for [{key}]: {parts[1]}");
                }
            }
            catch (Exception ex)
            {
                Plugin.I.Log.LogError($"Failed reading {PrioritiesFileName} from {audioSetFolder}: {ex}");
            }

            Plugin.I.Log.LogInfo($"loaded {dict.Count} priorities from {path}");
            return dict;
        }
    }
}