using System;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace WSOYappinator
{
    public static class UnityWebRequestAudioLoader
    {
        public static bool SupportsFile(string filePath)
        {
            return Path.GetExtension(filePath).ToLowerInvariant() is ".wav" or ".ogg" or ".mp3";
        }

        public static AudioClip LoadClip(string path)
        {
            AudioType type = GetAudioType(path);
            if (type == AudioType.UNKNOWN) return null;

            UnityWebRequest loader = UnityWebRequestMultimedia.GetAudioClip(path, type);
            loader.SendWebRequest();

            while (true)
            {
                if (loader.isDone) break;
            }

            if (loader.error != null)
            {
                Plugin.I.Log.LogError(loader.error);
                return null;
            }
            AudioClip clip = DownloadHandlerAudioClip.GetContent(loader);
            if (clip && clip.loadState == AudioDataLoadState.Loaded)
            {
                clip.name = path.TrimStart(Plugin.I.Info.Location.ToCharArray());
                return clip;
            }

            Plugin.I.Log.LogError($"Failed to load clip:{path}");
            return null;

        }

        private static AudioType GetAudioType(string path) => Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".wav" => AudioType.WAV,
            ".ogg" => AudioType.OGGVORBIS,
            ".mp3" => AudioType.MPEG,
            _ => AudioType.UNKNOWN,
        };
    }
}