using System.Runtime.InteropServices;
using UnityEngine;

namespace RyanXuWei.HybridLivePreview
{
    // WebGL builds made with Build WebGL: like the preview's video, the canvas only takes keys while it has
    // focus, so text fields in the H5 UI keep theirs. Other pages (plain Unity templates) are left alone.
    internal static class HybridPageKeyboard
    {
        [DllImport("__Internal")]
        static extern bool HlpIsHybridPage();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Apply()
        {
            if (HlpIsHybridPage())
                WebGLInput.captureAllKeyboardInput = false;
        }
    }
}
