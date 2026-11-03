using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace RyanXuWei.HybridLivePreview.IframeEmbed.Editor
{
    // Copies the sample's parent page into HybridPreviewWeb/public/embed/ (served at /embed/host.html in the
    // Play preview and copied next to the WebGL build), together with the package's hybrid-bridge-host.js.
    // Done once automatically when the web app exists and the folder is missing; after that the files belong to
    // the project and are only replaced from the menu.
    [InitializeOnLoad]
    internal static class EmbedWebInstaller
    {
        const string PackageName = "com.ryanxuwei.hybrid-live-preview";
        const string MenuRoot = "Tools/Hybrid Live Preview/Iframe Embed/";
        const string Log = "[HLP Iframe Embed] ";
        const string ImportLine = "import { enableParentRelay, guardEmbedding } from './hlp/index.js';";

        static string WebRoot => Path.Combine(Path.GetDirectoryName(Application.dataPath), "HybridPreviewWeb");
        static string TargetFolder => Path.Combine(WebRoot, "public", "embed");
        static string HostScript => Path.Combine(WebRoot, "src", "hlp", "hybrid-bridge-host.js");
        static string MainJs => Path.Combine(WebRoot, "src", "main.js");

        static EmbedWebInstaller()
        {
            EditorApplication.delayCall += () =>
            {
                if (Directory.Exists(WebRoot) && !Directory.Exists(TargetFolder) && Copy())
                    Debug.Log(Log + "Copied the parent page to HybridPreviewWeb/public/embed. " +
                              "Run Tools > Hybrid Live Preview > Iframe Embed > Add enableParentRelay() To main.js, then open /embed/host.html.");
            };
        }

        [MenuItem(MenuRoot + "Install Host Page", priority = 210)]
        static void InstallMenu()
        {
            if (!Directory.Exists(WebRoot))
            {
                EditorUtility.DisplayDialog("Iframe Embed", "HybridPreviewWeb does not exist yet. Run Tools > Hybrid Live Preview > Install Web App first.", "OK");
                return;
            }

            if (Directory.Exists(TargetFolder) && !EditorUtility.DisplayDialog("Iframe Embed",
                    "Replace HybridPreviewWeb/public/embed with the sample's version? Your changes to those files will be lost.", "Replace", "Cancel"))
                return;

            if (Copy())
                Debug.Log(Log + "Installed HybridPreviewWeb/public/embed.");
        }

        // Adds guardEmbedding() and enableParentRelay(); main.js is the project's, so only on request.
        [MenuItem(MenuRoot + "Add enableParentRelay() To main.js", priority = 211)]
        static void AddToMainJs()
        {
            if (!File.Exists(MainJs))
            {
                EditorUtility.DisplayDialog("Iframe Embed", "HybridPreviewWeb/src/main.js was not found.", "OK");
                return;
            }

            string source = File.ReadAllText(MainJs);
            if (source.Contains("enableParentRelay"))
            {
                EditorUtility.DisplayDialog("Iframe Embed", "main.js already uses enableParentRelay().", "OK");
                return;
            }

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
                     "// Iframe Embed: only same-origin pages may embed this page, and they talk to Unity through" + newline +
                     "// hybrid-bridge-host.js. For another site, pass { origins: ['https://www.example.com'] } to both." + newline +
                     "guardEmbedding();" + newline +
                     "enableParentRelay();" + newline;
            File.WriteAllText(MainJs, source);
            Debug.Log(Log + "Added guardEmbedding() and enableParentRelay() to HybridPreviewWeb/src/main.js.");
        }

        static bool Copy()
        {
            string source = FindWebFiles();
            if (source == null)
            {
                Debug.LogError(Log + "Could not find the sample's Web~ folder. Re-import the Iframe Embed sample.");
                return false;
            }

            string host = File.Exists(HostScript) ? HostScript : FindPackageFile("WebApp~/src/hlp/hybrid-bridge-host.js");
            if (host == null)
            {
                Debug.LogError(Log + "hybrid-bridge-host.js was not found. Press Play once so src/hlp is up to date, then try again.");
                return false;
            }

            Directory.CreateDirectory(TargetFolder);
            foreach (string file in Directory.GetFiles(source))
                File.Copy(file, Path.Combine(TargetFolder, Path.GetFileName(file)), true);
            File.Copy(host, Path.Combine(TargetFolder, "hybrid-bridge-host.js"), true);
            return true;
        }

        // The imported sample's own Web~ first (Assets/Samples/...), then the copy inside the package.
        static string FindWebFiles()
        {
            foreach (string guid in AssetDatabase.FindAssets("EmbedWebInstaller t:MonoScript"))
            {
                string script = Path.GetFullPath(AssetDatabase.GUIDToAssetPath(guid));
                string web = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(script)), "Web~");
                if (Directory.Exists(web))
                    return web;
            }

            string packaged = FindPackageFile("Samples~/IframeEmbed/Web~");
            return packaged != null && Directory.Exists(packaged) ? packaged : null;
        }

        static string FindPackageFile(string relative)
        {
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/" + PackageName);
            if (package == null)
                return null;
            string path = Path.Combine(package.resolvedPath, relative);
            return File.Exists(path) || Directory.Exists(path) ? path : null;
        }
    }
}
