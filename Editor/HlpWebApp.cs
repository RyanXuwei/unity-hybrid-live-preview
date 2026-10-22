using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace RyanXuWei.HybridLivePreview.Editor
{
    // The package ships its web template in WebApp~ (ignored by Unity). A git-installed package lives in a
    // read-only cache, so the template is copied into <project>/HybridPreviewWeb and installed there.
    // After that the folder belongs to the project, except src/hlp/, which the package keeps up to date.
    internal static class HlpWebApp
    {
        public const string FolderName = "HybridPreviewWeb";
        const string OwnedFolder = "src/hlp";

        public static string ProjectRoot => Path.GetDirectoryName(Application.dataPath);
        public static string WebRoot => Path.Combine(ProjectRoot, FolderName);

        static string TemplateRoot
        {
            get
            {
                var info = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(HlpWebApp).Assembly);
                return info == null ? null : Path.Combine(info.resolvedPath, "WebApp~");
            }
        }

        public static bool EnsureInstalled()
        {
            if (!File.Exists(Path.Combine(WebRoot, "package.json")))
            {
                var template = TemplateRoot;
                if (template == null || !Directory.Exists(template))
                {
                    Debug.LogError($"[HybridLivePreview] Web template not found at {template}.");
                    return false;
                }
                CopyDirectory(template, WebRoot);
                Debug.Log($"[HybridLivePreview] Copied web template to {WebRoot}");
            }
            else
            {
                SyncOwnedFiles();
            }

            if (Directory.Exists(Path.Combine(WebRoot, "node_modules")))
                return true;
            return NpmInstall();
        }

        // Refreshes the package-owned src/hlp/ files when the package was updated.
        static void SyncOwnedFiles()
        {
            var template = TemplateRoot;
            var source = template == null ? null : Path.Combine(template, OwnedFolder);
            if (source == null || !Directory.Exists(source))
                return;
            var destination = Path.Combine(WebRoot, OwnedFolder);
            Directory.CreateDirectory(destination);
            var updated = 0;
            foreach (var file in Directory.GetFiles(source))
            {
                var target = Path.Combine(destination, Path.GetFileName(file));
                if (File.Exists(target) && File.ReadAllText(target) == File.ReadAllText(file))
                    continue;
                File.Copy(file, target, true);
                updated++;
            }
            if (updated > 0)
                Debug.Log($"[HybridLivePreview] Updated {updated} file(s) in {destination}");
        }

        static bool NpmInstall()
        {
            EditorUtility.DisplayProgressBar("Hybrid Live Preview", "npm install (first run only)...", 0.5f);
            try
            {
                using var process = HlpProcesses.Shell("npm install --no-audit --no-fund", WebRoot, redirect: true);
                var stdout = process.StandardOutput.ReadToEndAsync();
                var stderr = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(300000))
                {
                    process.Kill();
                    Debug.LogError("[HybridLivePreview] npm install timed out after 5 minutes.");
                    return false;
                }
                if (process.ExitCode != 0)
                {
                    Debug.LogError($"[HybridLivePreview] npm install failed ({process.ExitCode}):\n{stderr.Result}\n{stdout.Result}");
                    return false;
                }
                Debug.Log($"[HybridLivePreview] npm install finished.\n{stdout.Result}");
                return true;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        static void CopyDirectory(string source, string destination)
        {
            Directory.CreateDirectory(destination);
            foreach (var file in Directory.GetFiles(source))
                File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), true);
            foreach (var dir in Directory.GetDirectories(source))
            {
                if (Path.GetFileName(dir) == "node_modules")
                    continue;
                CopyDirectory(dir, Path.Combine(destination, Path.GetFileName(dir)));
            }
        }
    }
}
