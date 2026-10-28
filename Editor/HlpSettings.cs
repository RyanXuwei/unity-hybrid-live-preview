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

        // Parent folder of the final sites Build WebGL writes, one subfolder per Player Settings version; a
        // relative path starts at the project folder.
        public static string WebGLOutput
        {
            get => EditorPrefs.GetString(Prefix + "WebGLOutput", "Builds/HybridWebGL");
            set => EditorPrefs.SetString(Prefix + "WebGLOutput", value);
        }

        // Build WebGL keeps the WebGL template's own index.html and injects the UI layer into it (default); off
        // replaces that page with HybridPreviewWeb's index.html.
        public static bool WebGLInjectTemplate
        {
            get => EditorPrefs.GetBool(Prefix + "WebGLInjectTemplate", true);
            set => EditorPrefs.SetBool(Prefix + "WebGLInjectTemplate", value);
        }

        // Builds of different versions sit side by side instead of replacing each other.
        public static string WebGLOutputFor(string version)
        {
            var name = string.IsNullOrWhiteSpace(version) ? "0" : version.Trim();
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return Path.GetFullPath(Path.Combine(HlpWebApp.ProjectRoot, WebGLOutput, name));
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
