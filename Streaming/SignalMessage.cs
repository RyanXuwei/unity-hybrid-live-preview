using System;

namespace RyanXuWei.HybridLivePreview
{
    // Flat envelope for the preview signaling channel; JsonUtility needs fields, not properties.
    [Serializable]
    internal sealed class SignalMessage
    {
        public string type;
        public string role;
        public string id;
        public string sdp;
        public string candidate;
        public string sdpMid;
        public int sdpMLineIndex = -1;
        public int width;
        public int height;
    }
}
