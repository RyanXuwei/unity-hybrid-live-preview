using System;
using System.Collections.Concurrent;
using UnityEngine;

namespace RyanXuWei.HybridLivePreview
{
    // Editor preview: carries HybridBridge messages over the viewers' "bridge" data channels. It stays
    // detached until a viewer's channel is open, so messages sent earlier wait in HybridBridge's queue.
    [AddComponentMenu("")]
    internal sealed class PreviewBridgeTransport : MonoBehaviour, IHybridBridgeTransport
    {
#pragma warning disable 0649 // filled by JsonUtility
        [Serializable]
        sealed class Envelope
        {
            public string type;
            public string data;
        }
#pragma warning restore 0649

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Register()
        {
            PreviewStreamer.Created -= Attach;
            PreviewStreamer.Created += Attach;
        }

        static void Attach(PreviewStreamer streamer)
        {
            if (streamer.GetComponent<PreviewBridgeTransport>() == null)
                streamer.gameObject.AddComponent<PreviewBridgeTransport>();
        }

        readonly ConcurrentQueue<string> inbox = new ConcurrentQueue<string>();
        PreviewStreamer streamer;
        bool attached;

        void OnEnable()
        {
            streamer = GetComponent<PreviewStreamer>();
            streamer.BridgeReceived += inbox.Enqueue;
        }

        void OnDisable()
        {
            streamer.BridgeReceived -= inbox.Enqueue;
            HybridBridge.Detach(this);
            attached = false;
        }

        void Update()
        {
            while (inbox.TryDequeue(out var raw))
            {
                Envelope envelope;
                try { envelope = JsonUtility.FromJson<Envelope>(raw); }
                catch (Exception e)
                {
                    Debug.LogWarning($"[HybridBridge] Bad message from the page: {e.Message}");
                    continue;
                }
                if (envelope != null)
                    HybridBridge.Deliver(envelope.type, envelope.data);
            }

            var open = streamer.BridgeViewerCount > 0;
            if (open == attached)
                return;
            attached = open;
            if (open)
                HybridBridge.Attach(this);
            else
                HybridBridge.Detach(this);
        }

        public void Post(string type, string json) =>
            streamer.SendToBridge(JsonUtility.ToJson(new Envelope { type = type, data = json }));
    }
}
