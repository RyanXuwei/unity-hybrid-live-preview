namespace RyanXuWei.HybridLivePreview
{
    // Carries HybridBridge messages to the page: the jslib in WebGL builds, a WebRTC data channel in
    // the Editor preview. Incoming messages are handed to HybridBridge.Deliver on the main thread.
    internal interface IHybridBridgeTransport
    {
        void Post(string type, string json);
    }
}
