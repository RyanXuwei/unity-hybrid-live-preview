using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace RyanXuWei.HybridLivePreview.Editor
{
    internal static class HlpSettings
    {
        const string Prefix = "RyanXuWei.HybridLivePreview.";

        public static bool AutoLaunch
        {
            get => EditorPrefs.GetBool(Prefix + "AutoLaunch", true);
            set => EditorPrefs.SetBool(Prefix + "AutoLaunch", value);
        }

        public static bool StopViteOnExit
        {
            get => EditorPrefs.GetBool(Prefix + "StopViteOnExit", true);
            set => EditorPrefs.SetBool(Prefix + "StopViteOnExit", value);
        }

        // Locks the Game view to the stream size so Screen, camera aspect and forwarded input match the browser.
        public static bool MatchGameView
        {
            get => EditorPrefs.GetBool(Prefix + "MatchGameView", true);
            set => EditorPrefs.SetBool(Prefix + "MatchGameView", value);
        }

        // Streams the Game view's final image; off falls back to a mirror camera that renders Camera.main again.
        public static bool CaptureGameView
        {
            get => EditorPrefs.GetBool(Prefix + "CaptureGameView", true);
            set => EditorPrefs.SetBool(Prefix + "CaptureGameView", value);
        }

        public static int Port
        {
            get => EditorPrefs.GetInt(Prefix + "Port", 5173);
            set => EditorPrefs.SetInt(Prefix + "Port", value);
        }

        // Upper bound for the stream; follows the WebGL "Default Canvas Width/Height" unless overridden here.
        public static int StreamWidth
        {
            get => EditorPrefs.GetInt(Prefix + "StreamWidth", PlayerSettings.defaultWebScreenWidth);
            set => EditorPrefs.SetInt(Prefix + "StreamWidth", value);
        }

        public static int StreamHeight
        {
            get => EditorPrefs.GetInt(Prefix + "StreamHeight", PlayerSettings.defaultWebScreenHeight);
            set => EditorPrefs.SetInt(Prefix + "StreamHeight", value);
        }

        public static string ChromePath
        {
            get
            {
                var path = EditorPrefs.GetString(Prefix + "ChromePath", "");
                return string.IsNullOrEmpty(path) ? FindChrome() : path;
            }
            set => EditorPrefs.SetString(Prefix + "ChromePath", value);
        }

        // 127.0.0.1 rather than localhost: Node may bind localhost to ::1 only.
        public static string PreviewUrl => $"http://127.0.0.1:{Port}/";
        public static string SignalingUrl => $"ws://127.0.0.1:{Port}/signaling";

        static string FindChrome()
        {
            string[] candidates =
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Google\Chrome\Application\chrome.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"Google\Chrome\Application\chrome.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Google\Chrome\Application\chrome.exe"),
                "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome",
            };
            foreach (var candidate in candidates)
            {
                if (File.Exists(candidate))
                    return candidate;
            }
            return null;
        }
    }
}
