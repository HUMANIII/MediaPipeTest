using System;
using UnityEditor;
using UnityEngine;

namespace MediaPipeTest.CRT.AudioPreview
{
    internal static class CrtAudioHistory
    {
        internal static string PreferenceKey => KeyForProject(Application.dataPath);
        internal static string KeyForProject(string dataPath) => "MediaPipeTest.CRT.AudioPreview.LastPlayed." +
            Hash128.Compute(dataPath.Replace('\\', '/').TrimEnd('/').ToLowerInvariant());

        internal static string Session
        {
            get
            {
                string key = PreferenceKey + ".Session";
                string value = SessionState.GetString(key, "");
                if (string.IsNullOrEmpty(value)) { value = Guid.NewGuid().ToString("N"); SessionState.SetString(key, value); }
                return value;
            }
        }

        internal static AudioClip LastPlayed()
        {
            string guid = EditorPrefs.GetString(PreferenceKey, "");
            if (string.IsNullOrEmpty(guid)) return null;
            string path = AssetDatabase.GUIDToAssetPath(guid);
            return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        internal static void RememberPlayed(AudioClip clip)
        {
            string path = AssetDatabase.GetAssetPath(clip);
            if (!string.IsNullOrEmpty(path)) EditorPrefs.SetString(PreferenceKey, AssetDatabase.AssetPathToGUID(path));
        }
    }
}
