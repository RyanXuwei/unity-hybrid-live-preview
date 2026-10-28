using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RyanXuWei.HybridLivePreview.Editor
{
    // One-click final build: the Unity WebGL player and the HybridPreviewWeb page, merged into one folder.
    // The page is built by Vite (src/hlp/vite.build.config.js); in it startHybridPreview runs Unity in a
    // canvas instead of the preview stream, so Play preview and the build show the same page.
    internal static class HlpWebGLBuild
    {
        const int PreviewPort = 4173;
        const double PreviewTimeoutSeconds = 30;

        static string WorkDir => Path.Combine(HlpWebApp.ProjectRoot, "Library", "HybridLivePreview");
        // Unity refuses to build into Library/, so the intermediate player goes to the OS temp folder (one per
        // project) and is deleted once it has been merged; only the final site is left in the project.
        static string PlayerDir => Path.Combine(Path.GetTempPath(), "HybridLivePreview",
            Math.Abs(HlpWebApp.ProjectRoot.ToLowerInvariant().GetHashCode()).ToString("x"), "WebGLPlayer");
        static string ManifestPath => Path.Combine(WorkDir, "webgl-build.json");
        static string LogPath => Path.Combine(WorkDir, "vite-build.log");

        // Read by src/hlp/webgl-build-plugin.js.
        [Serializable]
        sealed class Manifest
        {
            public string playerDir;
            public string outDir;
            public Page page;
        }

        // What src/hlp/webgl.js needs to start the player; maxWidth/maxHeight cap the canvas like the preview stream.
        [Serializable]
        sealed class Page
        {
            public string loaderUrl;
            public string dataUrl;
            public string frameworkUrl;
            public string codeUrl;
            public string companyName;
            public string productName;
            public string productVersion;
            public int maxWidth;
            public int maxHeight;
        }

        // The last build, whatever its version.
        public static bool HasBuild
        {
            get
            {
                if (!File.Exists(ManifestPath))
                    return false;
                var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(ManifestPath));
                return manifest != null && !string.IsNullOrEmpty(manifest.outDir) && File.Exists(Path.Combine(manifest.outDir, "index.html"));
            }
        }

        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[HybridLivePreview] Stop Play Mode before building.");
                return;
            }
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL))
            {
                Debug.LogError("[HybridLivePreview] WebGL Build Support is not installed; add it to this Editor in Unity Hub.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            var scenes = Scenes();
            if (scenes.Length == 0)
            {
                Debug.LogError("[HybridLivePreview] Nothing to build: add scenes to the build profile or save the open scene.");
                return;
            }
            if (!HlpWebApp.EnsureInstalled())
                return;

            var outDir = HlpSettings.WebGLOutputFor(PlayerSettings.bundleVersion);
            if (Directory.Exists(PlayerDir))
                Directory.Delete(PlayerDir, true);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = PlayerDir,
                target = BuildTarget.WebGL,
                targetGroup = BuildTargetGroup.WebGL,
                options = BuildOptions.None,
            });
            if (report.summary.result != BuildResult.Succeeded)
            {
                Debug.LogError($"[HybridLivePreview] Unity WebGL build {report.summary.result}; see the errors above.");
                return;
            }

            var page = DescribePlayer();
            if (page == null)
                return;
            File.WriteAllText(ManifestPath, JsonUtility.ToJson(new Manifest { playerDir = PlayerDir, outDir = outDir, page = page }, true));
            if (!RunViteBuild())
                return;

            DeletePlayer();
            Debug.Log($"[HybridLivePreview] WebGL build ready: {outDir}. Open it with Tools > Hybrid Live Preview > Preview WebGL Build.");
            WarnAboutCompression();
            EditorUtility.RevealInFinder(Path.Combine(outDir, "index.html"));
        }

        // Serves the merged build with `vite preview` and opens it in the preview Chrome.
        public static void Preview()
        {
            if (!HasBuild)
            {
                Debug.LogError("[HybridLivePreview] No WebGL build yet; run Tools > Hybrid Live Preview > Build WebGL first.");
                return;
            }
            HlpProcesses.StartBuildPreview(PreviewPort);
            var url = $"http://127.0.0.1:{PreviewPort}/";
            var deadline = EditorApplication.timeSinceStartup + PreviewTimeoutSeconds;
            var nextCheck = 0d;

            void Tick()
            {
                var now = EditorApplication.timeSinceStartup;
                if (now < nextCheck)
                    return;
                nextCheck = now + 0.5;
                if (HlpProcesses.IsPortOpen(PreviewPort))
                {
                    EditorApplication.update -= Tick;
                    HlpProcesses.StartChrome(url);
                }
                else if (now > deadline)
                {
                    EditorApplication.update -= Tick;
                    Debug.LogError($"[HybridLivePreview] The build preview did not answer on port {PreviewPort}. See {HlpProcesses.BuildPreviewLogPath}");
                }
            }

            EditorApplication.update += Tick;
        }

        // The merged site holds its own copy of the player files.
        static void DeletePlayer()
        {
            try
            {
                if (Directory.Exists(PlayerDir))
                    Directory.Delete(PlayerDir, true);
            }
            catch (IOException e)
            {
                Debug.LogWarning($"[HybridLivePreview] Could not delete the intermediate player {PlayerDir}: {e.Message}");
            }
        }

        static string[] Scenes()
        {
            var listed = EditorBuildSettings.scenes.Where(s => s.enabled && File.Exists(s.path)).Select(s => s.path).ToArray();
            if (listed.Length > 0)
                return listed;
            var open = SceneManager.GetActiveScene().path;
            if (string.IsNullOrEmpty(open))
                return Array.Empty<string>();
            Debug.LogWarning($"[HybridLivePreview] The build profile lists no scenes; building the open scene {open}.");
            return new[] { open };
        }

        // File names depend on the build name, "Name Files As Hashes" and compression, so they are looked up.
        static Page DescribePlayer()
        {
            var buildDir = Path.Combine(PlayerDir, "Build");
            var files = Directory.Exists(buildDir) ? Directory.GetFiles(buildDir).Select(Path.GetFileName).ToArray() : Array.Empty<string>();
            string Find(string marker) => files.FirstOrDefault(f => f.Contains(marker));
            var loader = Find(".loader.js");
            var data = Find(".data");
            var framework = Find(".framework.js");
            var code = Find(".wasm");
            if (loader == null || data == null || framework == null || code == null)
            {
                Debug.LogError($"[HybridLivePreview] Unexpected WebGL player files in {buildDir}: {string.Join(", ", files)}");
                return null;
            }
            return new Page
            {
                loaderUrl = "Build/" + loader,
                dataUrl = "Build/" + data,
                frameworkUrl = "Build/" + framework,
                codeUrl = "Build/" + code,
                companyName = PlayerSettings.companyName,
                productName = PlayerSettings.productName,
                productVersion = PlayerSettings.bundleVersion,
                maxWidth = HlpSettings.StreamWidth,
                maxHeight = HlpSettings.StreamHeight,
            };
        }

        static bool RunViteBuild()
        {
            EditorUtility.DisplayProgressBar("Hybrid Live Preview", "Building the web page (vite build)...", 0.9f);
            try
            {
                using var process = HlpProcesses.Shell("npx vite build --config src/hlp/vite.build.config.js", HlpWebApp.WebRoot, redirect: true);
                var stdout = process.StandardOutput.ReadToEndAsync();
                var stderr = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(600000))
                {
                    process.Kill();
                    Debug.LogError("[HybridLivePreview] vite build timed out after 10 minutes.");
                    return false;
                }
                File.WriteAllText(LogPath, stdout.Result + stderr.Result);
                if (process.ExitCode != 0)
                {
                    Debug.LogError($"[HybridLivePreview] vite build failed ({process.ExitCode}); see {LogPath}\n{stderr.Result}");
                    return false;
                }
                return true;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        static void WarnAboutCompression()
        {
            var format = PlayerSettings.WebGL.compressionFormat;
            if (format == WebGLCompressionFormat.Disabled || PlayerSettings.WebGL.decompressionFallback)
                return;
            Debug.Log($"[HybridLivePreview] The player uses {format} compression without Decompression Fallback, so the web server must send " +
                      "Content-Encoding headers for Build/*.br or *.gz. Preview WebGL Build does; opening index.html from disk does not work.");
        }
    }
}
