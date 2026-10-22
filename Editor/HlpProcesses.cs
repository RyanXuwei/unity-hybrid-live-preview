using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace RyanXuWei.HybridLivePreview.Editor
{
    // Owns the Vite and Chrome processes this package starts. PIDs are written to Library/HybridLivePreview
    // so they survive the domain reload on entering Play Mode and an Editor restart that skipped cleanup.
    internal static class HlpProcesses
    {
        const string ViteName = "vite";
        const string ChromeName = "chrome";

        static string WorkDir => Path.Combine(HlpWebApp.ProjectRoot, "Library", "HybridLivePreview");
        public static string ViteLogPath => Path.Combine(WorkDir, "vite.log");

        public static bool IsPortOpen(int port)
        {
            try
            {
                using var client = new TcpClient();
                var connect = client.ConnectAsync("127.0.0.1", port);
                return connect.Wait(200) && client.Connected;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static void StartVite()
        {
            var port = HlpSettings.Port;
            if (IsPortOpen(port))
            {
                var owned = ReadPid(ViteName);
                Debug.Log(owned != 0
                    ? $"[HybridLivePreview] Reusing Vite started earlier (pid {owned}) on port {port}."
                    : $"[HybridLivePreview] Port {port} is served by another process; using it without managing it.");
                return;
            }
            Directory.CreateDirectory(WorkDir);
            var process = Shell($"npm run dev -- --port {port} --strictPort > \"{ViteLogPath}\" 2>&1", HlpWebApp.WebRoot, redirect: false);
            WritePid(ViteName, process.Id);
            Debug.Log($"[HybridLivePreview] Vite starting (pid {process.Id}) on port {port}. Log: {ViteLogPath}");
        }

        public static void StartChrome(string url)
        {
            var chrome = HlpSettings.ChromePath;
            if (string.IsNullOrEmpty(chrome) || !File.Exists(chrome))
            {
                Debug.LogWarning("[HybridLivePreview] Chrome not found; opening the default browser instead.");
                Application.OpenURL(url);
                return;
            }

            // A dedicated profile makes this Chrome its own process tree, so Stop can close it without
            // touching the user's normal browser windows.
            var profile = Path.Combine(WorkDir, "chrome-profile");
            var args = $"--user-data-dir=\"{profile}\" --no-first-run --no-default-browser-check --hide-crash-restore-bubble " +
                       $"--autoplay-policy=no-user-gesture-required --new-window --window-size=1280,800 \"{url}\"";
            StopChrome();
            var process = Process.Start(new ProcessStartInfo(chrome, args) { UseShellExecute = false });
            WritePid(ChromeName, process.Id);
            Debug.Log($"[HybridLivePreview] Chrome opened (pid {process.Id}): {url}");
        }

        public static void StopChrome() => Kill(ChromeName, "Chrome", graceful: true);
        public static void StopVite() => Kill(ViteName, "Vite", graceful: false);

        static string PidPath(string name) => Path.Combine(WorkDir, name + ".pid");

        static void WritePid(string name, int pid)
        {
            Directory.CreateDirectory(WorkDir);
            File.WriteAllText(PidPath(name), pid.ToString());
        }

        // Returns the recorded PID only while that process is still alive.
        static int ReadPid(string name)
        {
            var path = PidPath(name);
            if (!File.Exists(path) || !int.TryParse(File.ReadAllText(path).Trim(), out var pid))
                return 0;
            try
            {
                using var process = Process.GetProcessById(pid);
                if (!process.HasExited)
                    return pid;
            }
            catch (ArgumentException)
            {
            }
            File.Delete(path);
            return 0;
        }

        static void Kill(string name, string label, bool graceful)
        {
            var pid = ReadPid(name);
            if (pid == 0)
                return;
            File.Delete(PidPath(name));
            try
            {
                if (Application.platform == RuntimePlatform.WindowsEditor)
                {
                    // Chrome is asked to close its windows first so it exits cleanly; Vite runs without a window
                    // and only stops with /F. /T takes the whole tree: cmd.exe -> npm -> node, Chrome's children.
                    if (graceful)
                        TaskKill(pid, force: false);
                    if (!graceful || !WaitForExit(pid, 3000))
                        TaskKill(pid, force: true);
                }
                else
                {
                    Process.GetProcessById(pid).Kill();
                }
                Debug.Log($"[HybridLivePreview] Stopped {label} (pid {pid}).");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[HybridLivePreview] Could not stop {label} (pid {pid}): {e.Message}");
            }
        }

        static void TaskKill(int pid, bool force)
        {
            using var taskkill = Process.Start(new ProcessStartInfo("taskkill", $"/PID {pid} /T{(force ? " /F" : "")}")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            });
            taskkill.WaitForExit(5000);
        }

        static bool WaitForExit(int pid, int milliseconds)
        {
            try
            {
                using var process = Process.GetProcessById(pid);
                return process.WaitForExit(milliseconds);
            }
            catch (ArgumentException)
            {
                return true;
            }
        }

        public static Process Shell(string command, string workingDirectory, bool redirect)
        {
            var windows = Application.platform == RuntimePlatform.WindowsEditor;
            var info = new ProcessStartInfo(windows ? "cmd.exe" : "/bin/sh", windows ? $"/c {command}" : $"-c \"{command}\"")
            {
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = redirect,
                RedirectStandardError = redirect,
            };
            return Process.Start(info);
        }
    }
}
