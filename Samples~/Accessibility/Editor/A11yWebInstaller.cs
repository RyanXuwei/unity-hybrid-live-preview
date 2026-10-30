using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace RyanXuWei.HybridLivePreview.Accessibility.Editor
{
    // Copies the sample's page files (Web~) into HybridPreviewWeb/src/a11y. Done once automatically when the web
    // app exists and the folder is missing; after that the files belong to the project and are only replaced
    // from the menu.
    [InitializeOnLoad]
    internal static class A11yWebInstaller
    {
        const string PackageName = "com.ryanxuwei.hybrid-live-preview";
        const string MenuRoot = "Tools/Hybrid Live Preview/Accessibility/";
        const string ImportLine = "import { startA11y } from './a11y/a11y.js';";

        static string WebRoot => Path.Combine(Path.GetDirectoryName(Application.dataPath), "HybridPreviewWeb");
        static string TargetFolder => Path.Combine(WebRoot, "src", "a11y");
        static string MainJs => Path.Combine(WebRoot, "src", "main.js");

        static A11yWebInstaller()
        {
            EditorApplication.delayCall += () =>
            {
                if (Directory.Exists(WebRoot) && !Directory.Exists(TargetFolder) && Copy())
                    Debug.Log("[HLP Accessibility] Copied the page files to HybridPreviewWeb/src/a11y. " +
                              "Run Tools > Hybrid Live Preview > Accessibility > Add startA11y() To main.js, or add it yourself.");
            };
        }

        [MenuItem(MenuRoot + "Install Web Files", priority = 200)]
        static void InstallMenu()
        {
            if (!Directory.Exists(WebRoot))
            {
                EditorUtility.DisplayDialog("Accessibility", "HybridPreviewWeb does not exist yet. Run Tools > Hybrid Live Preview > Install Web App first.", "OK");
                return;
            }

            if (Directory.Exists(TargetFolder) && !EditorUtility.DisplayDialog("Accessibility",
                    "Replace HybridPreviewWeb/src/a11y with the sample's version? Your changes to those files will be lost.", "Replace", "Cancel"))
                return;

            if (Copy())
                Debug.Log("[HLP Accessibility] Installed HybridPreviewWeb/src/a11y.");
        }

        // Adds the import and the startA11y() call; main.js is the project's, so only on request.
        [MenuItem(MenuRoot + "Add startA11y() To main.js", priority = 201)]
        static void AddToMainJs()
        {
            if (!File.Exists(MainJs))
            {
                EditorUtility.DisplayDialog("Accessibility", "HybridPreviewWeb/src/main.js was not found.", "OK");
                return;
            }

            string source = File.ReadAllText(MainJs);
            if (source.Contains("startA11y"))
            {
                EditorUtility.DisplayDialog("Accessibility", "main.js already uses startA11y().", "OK");
                return;
            }

            if (!Directory.Exists(TargetFolder) && !Copy())
                return;

            string newline = source.Contains("\r\n") ? "\r\n" : "\n";
            // After the last top-level import line, so comments that mention "import" don't count.
            var imports = Regex.Matches(source, @"^import .*$", RegexOptions.Multiline);
            int insertAt = 0;
            if (imports.Count > 0)
            {
                Match last = imports[imports.Count - 1];
                int lineEnd = source.IndexOf('\n', last.Index);
                if (lineEnd < 0)
                {
                    source += newline;
                    lineEnd = source.Length - 1;
                }

                insertAt = lineEnd + 1;
            }
            source = source.Insert(insertAt, ImportLine + newline);
            source = source.TrimEnd() + newline + newline +
                     "// Screen reader layer for the Unity UI (Accessibility sample). Pass { lang: 'zh-TW' } etc. to match your UI text." + newline +
                     "startA11y();" + newline;
            File.WriteAllText(MainJs, source);
            Debug.Log("[HLP Accessibility] Added startA11y() to HybridPreviewWeb/src/main.js.");
        }

        static bool Copy()
        {
            string source = FindWebFiles();
            if (source == null)
            {
                Debug.LogError("[HLP Accessibility] Could not find the sample's Web~ folder. Re-import the Accessibility sample.");
                return false;
            }

            Directory.CreateDirectory(TargetFolder);
            foreach (string file in Directory.GetFiles(source))
                File.Copy(file, Path.Combine(TargetFolder, Path.GetFileName(file)), true);
            return true;
        }

        // The imported sample's own Web~ first (Assets/Samples/...), then the copy inside the package.
        static string FindWebFiles()
        {
            foreach (string guid in AssetDatabase.FindAssets("A11yWebInstaller t:MonoScript"))
            {
                string script = Path.GetFullPath(AssetDatabase.GUIDToAssetPath(guid));
                string web = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(script)), "Web~");
                if (Directory.Exists(web))
                    return web;
            }

            var package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/" + PackageName);
            string packaged = package == null ? null : Path.Combine(package.resolvedPath, "Samples~", "Accessibility", "Web~");
            return packaged != null && Directory.Exists(packaged) ? packaged : null;
        }
    }
}
