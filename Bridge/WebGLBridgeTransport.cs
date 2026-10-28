#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
using AOT;
using UnityEngine;

namespace RyanXuWei.HybridLivePreview
{
    // WebGL builds: connects HybridBridge to window.HybridBridge through HybridBridge.jslib.
    internal sealed class WebGLBridgeTransport : IHybridBridgeTransport
    {
        delegate void ReceiveCallback(string type, string json);

        [DllImport("__Internal")]
        static extern void HlpBridgeConnect(ReceiveCallback receive);

        [DllImport("__Internal")]
        static extern void HlpBridgePost(string type, string json);

        // After the first scene has enabled its objects, so their handlers see messages the page queued.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Connect()
        {
            HybridBridge.Attach(new WebGLBridgeTransport());
            HlpBridgeConnect(Receive);
        }

        [MonoPInvokeCallback(typeof(ReceiveCallback))]
        static void Receive(string type, string json) => HybridBridge.Deliver(type, json);

        public void Post(string type, string json) => HlpBridgePost(type, json);
    }
}
#endif
