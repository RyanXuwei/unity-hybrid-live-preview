using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace RyanXuWei.HybridLivePreview.Editor
{
    // Sets the Game view to a fixed resolution equal to the stream, so Screen.width/height, camera aspect
    // and UI layout match the browser canvas and forwarded pointer positions land on the same pixels.
    // GameView and GameViewSizes are internal, hence the reflection.
    internal static class HlpGameView
    {
        const string Label = "Hybrid Live Preview";
        const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        static readonly Assembly EditorAssembly = typeof(EditorWindow).Assembly;
        static readonly Type GameViewType = EditorAssembly.GetType("UnityEditor.GameView");
        static readonly Type SizesType = EditorAssembly.GetType("UnityEditor.GameViewSizes");
        static readonly Type SizeType = EditorAssembly.GetType("UnityEditor.GameViewSize");
        static readonly Type SizeKindType = EditorAssembly.GetType("UnityEditor.GameViewSizeType");

        static int restoreIndex = -1;

        public static void Match(Vector2Int size)
        {
            try
            {
                var index = EnsureSize(size);
                foreach (var window in Resources.FindObjectsOfTypeAll(GameViewType))
                {
                    if (restoreIndex < 0)
                        restoreIndex = (int)GameViewType.GetProperty("selectedSizeIndex", Any).GetValue(window);
                    GameViewType.GetMethod("SizeSelectionCallback", Any).Invoke(window, new object[] { index, null });
                    ((EditorWindow)window).Repaint();
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[HybridLivePreview] Could not set the Game view to {size.x}x{size.y}: {e.Message}");
            }
        }

        public static void Restore()
        {
            if (restoreIndex < 0)
                return;
            try
            {
                foreach (var window in Resources.FindObjectsOfTypeAll(GameViewType))
                    GameViewType.GetMethod("SizeSelectionCallback", Any).Invoke(window, new object[] { restoreIndex, null });
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[HybridLivePreview] Could not restore the Game view size: {e.Message}");
            }
            restoreIndex = -1;
        }

        // Reuses one custom "Hybrid Live Preview" fixed resolution in the current platform's size list.
        static int EnsureSize(Vector2Int size)
        {
            var sizes = typeof(ScriptableSingleton<>).MakeGenericType(SizesType).GetProperty("instance").GetValue(null);
            var groupType = SizesType.GetProperty("currentGroupType", Any).GetValue(sizes);
            var group = SizesType.GetMethod("GetGroup", Any).Invoke(sizes, new[] { groupType });
            var groupClass = group.GetType();

            var count = (int)groupClass.GetMethod("GetTotalCount", Any).Invoke(group, null);
            for (var i = 0; i < count; i++)
            {
                var existing = groupClass.GetMethod("GetGameViewSize", Any).Invoke(group, new object[] { i });
                if ((string)SizeType.GetProperty("baseText", Any).GetValue(existing) != Label)
                    continue;
                SizeType.GetProperty("width", Any).SetValue(existing, size.x);
                SizeType.GetProperty("height", Any).SetValue(existing, size.y);
                return i;
            }

            var fixedResolution = Enum.Parse(SizeKindType, "FixedResolution");
            var created = Activator.CreateInstance(SizeType, fixedResolution, size.x, size.y, Label);
            groupClass.GetMethod("AddCustomSize", Any).Invoke(group, new[] { created });
            return (int)groupClass.GetMethod("GetTotalCount", Any).Invoke(group, null) - 1;
        }
    }
}
