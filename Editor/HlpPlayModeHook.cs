using UnityEditor;
using UnityEngine;

namespace RyanXuWei.HybridLivePreview.Editor
{
    [InitializeOnLoad]
    internal static class HlpPlayModeHook
    {
        const string ActiveKey = "RyanXuWei.HybridLivePreview.SessionActive";
        const double ViteTimeoutSeconds = 30;

        static HlpPlayModeHook()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.quitting += () =>
            {
                HlpProcesses.StopChrome();
                HlpProcesses.StopVite();
                HlpProcesses.StopBuildPreview();
            };
        }

        static bool Active
        {
            get => SessionState.GetBool(ActiveKey, false);
            set => SessionState.SetBool(ActiveKey, value);
        }

        static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            switch (state)
            {
                case PlayModeStateChange.ExitingEditMode:
                    Active = false;
                    if (!HlpSettings.AutoLaunch || !HlpWebApp.EnsureInstalled())
                        return;
                    HlpProcesses.StartVite();
                    Active = true;
                    break;

                case PlayModeStateChange.EnteredPlayMode:
                    if (!Active)
                        return;
                    if (!HlpWebGLBuild.IsWebTarget)
                        Debug.LogWarning($"[HybridLivePreview] The active Build Profile targets {HlpWebGLBuild.ActiveTargetName}, but Hybrid Live Preview only supports Web output. " +
                                         "Switch to the Web platform in File > Build Profiles so the preview matches what you ship.");
                    SpawnStreamer();
                    OpenChromeWhenViteIsUp();
                    break;

                case PlayModeStateChange.ExitingPlayMode:
                    if (Active)
                        HlpProcesses.StopChrome();
                    HlpGameView.Restore();
                    break;

                case PlayModeStateChange.EnteredEditMode:
                    if (Active && HlpSettings.StopViteOnExit)
                        HlpProcesses.StopVite();
                    Active = false;
                    break;
            }
        }

        static void SpawnStreamer()
        {
            var go = new GameObject("[HybridLivePreview] Streamer");
            // Survives SceneManager.LoadScene, so the page stays connected when the game reloads or changes scenes.
            Object.DontDestroyOnLoad(go);
            go.SetActive(false);
            var streamer = go.AddComponent<PreviewStreamer>();
            streamer.signalingUrl = HlpSettings.SignalingUrl;
            streamer.width = HlpSettings.StreamWidth;
            streamer.height = HlpSettings.StreamHeight;
            streamer.captureGameView = HlpSettings.CaptureGameView;
            if (HlpSettings.MatchGameView)
                streamer.Resized += HlpGameView.Match;
            else if (HlpSettings.CaptureGameView)
                Debug.LogWarning("[HybridLivePreview] Match Game View To Preview is off, so the Game view image is scaled to the stream size.");
            go.SetActive(true);
#if !ENABLE_INPUT_SYSTEM
            Debug.LogWarning("[HybridLivePreview] Browser input is not forwarded: set Player Settings > Active Input Handling to \"Input System Package\" or \"Both\".");
#endif
        }

        static void OpenChromeWhenViteIsUp()
        {
            var deadline = EditorApplication.timeSinceStartup + ViteTimeoutSeconds;
            var nextCheck = 0d;

            void Tick()
            {
                if (!EditorApplication.isPlaying)
                {
                    EditorApplication.update -= Tick;
                    return;
                }
                var now = EditorApplication.timeSinceStartup;
                if (now < nextCheck)
                    return;
                nextCheck = now + 0.5;

                if (HlpProcesses.IsPortOpen(HlpSettings.Port))
                {
                    EditorApplication.update -= Tick;
                    HlpProcesses.StartChrome(HlpSettings.PreviewUrl);
                }
                else if (now > deadline)
                {
                    EditorApplication.update -= Tick;
                    Debug.LogError($"[HybridLivePreview] Vite did not answer on port {HlpSettings.Port} within {ViteTimeoutSeconds}s. See {HlpProcesses.ViteLogPath}");
                }
            }

            EditorApplication.update += Tick;
        }
    }
}
