using UnityEditor;
using UnityEngine;

namespace RyanXuWei.HybridLivePreview.Editor
{
    internal static class HybridLivePreviewMenu
    {
        const string PackageName = "com.ryanxuwei.hybrid-live-preview";
        const string Root = "Tools/Hybrid Live Preview/";
        const string AutoLaunchItem = Root + "Auto Launch On Play";

        [MenuItem(AutoLaunchItem, priority = 0)]
        static void ToggleAutoLaunch() => HlpSettings.AutoLaunch = !HlpSettings.AutoLaunch;

        [MenuItem(AutoLaunchItem, true)]
        static bool ToggleAutoLaunchValidate()
        {
            Menu.SetChecked(AutoLaunchItem, HlpSettings.AutoLaunch);
            return true;
        }

        const string MatchGameViewItem = Root + "Match Game View To Preview";

        [MenuItem(MatchGameViewItem, priority = 1)]
        static void ToggleMatchGameView() => HlpSettings.MatchGameView = !HlpSettings.MatchGameView;

        [MenuItem(MatchGameViewItem, true)]
        static bool ToggleMatchGameViewValidate()
        {
            Menu.SetChecked(MatchGameViewItem, HlpSettings.MatchGameView);
            return true;
        }

        [MenuItem(Root + "Open Preview In Chrome", priority = 20)]
        static void OpenPreview() => HlpProcesses.StartChrome(HlpSettings.PreviewUrl);

        [MenuItem(Root + "Install Web App", priority = 21)]
        static void InstallWebApp() => HlpWebApp.EnsureInstalled();

        [MenuItem(Root + "Stop Vite And Chrome", priority = 22)]
        static void StopAll()
        {
            HlpProcesses.StopChrome();
            HlpProcesses.StopVite();
        }

        [MenuItem(Root + "About", priority = 40)]
        static void About()
        {
            var info = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(HybridLivePreviewMenu).Assembly);
            Debug.Log($"[HybridLivePreview] {PackageName} {(info != null ? info.version : "unknown")}");
        }
    }
}
