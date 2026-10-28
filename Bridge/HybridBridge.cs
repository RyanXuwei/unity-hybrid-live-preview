using System;
using System.Collections.Generic;
using UnityEngine;

namespace RyanXuWei.HybridLivePreview
{
    // The single channel between Unity and the H5 page; its page-side twin is window.HybridBridge.
    // A message is a type name plus a JSON payload. Code is identical in the Editor preview and in a
    // WebGL build; only the transport underneath changes. Messages sent before the page is connected
    // are queued (up to MaxQueued, oldest dropped first) and delivered once it is.
    public static class HybridBridge
    {
        const int MaxQueued = 256;

        static readonly Dictionary<string, List<Action<string>>> handlers = new Dictionary<string, List<Action<string>>>();
        static readonly Queue<KeyValuePair<string, string>> outbox = new Queue<KeyValuePair<string, string>>();
        static readonly HashSet<string> unhandledLogged = new HashSet<string>();
        static IHybridBridgeTransport transport;

        // True while a page is connected; until then sent messages wait in the queue.
        public static bool IsConnected => transport != null;

        // Sends `payload` (a [Serializable] class or struct, serialized with JsonUtility) to the page.
        public static void Send<T>(string type, T payload) => SendJson(type, JsonUtility.ToJson(payload));

        // Sends a message without a payload; the page receives null.
        public static void Send(string type) => SendJson(type, "null");

        // Sends a payload that is already JSON (any value: object, array, string, number...).
        public static void SendJson(string type, string json)
        {
            if (string.IsNullOrEmpty(type))
                throw new ArgumentException("A message needs a type.", nameof(type));
            json = string.IsNullOrEmpty(json) ? "null" : json;
            if (transport != null)
            {
                transport.Post(type, json);
                return;
            }
            if (outbox.Count == MaxQueued)
                outbox.Dequeue();
            outbox.Enqueue(new KeyValuePair<string, string>(type, json));
        }

        // Calls `handler` with the payload deserialized by JsonUtility.FromJson<T> (objects only).
        // Dispose the result to stop listening, e.g. in OnDisable.
        public static IDisposable On<T>(string type, Action<T> handler)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));
            return OnJson(type, json => handler(JsonUtility.FromJson<T>(json)));
        }

        // Calls `handler` with the raw JSON payload.
        public static IDisposable OnJson(string type, Action<string> handler)
        {
            if (string.IsNullOrEmpty(type))
                throw new ArgumentException("A message needs a type.", nameof(type));
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));
            if (!handlers.TryGetValue(type, out var list))
                handlers[type] = list = new List<Action<string>>();
            list.Add(handler);
            return new Subscription(type, handler);
        }

        internal static void Attach(IHybridBridgeTransport value)
        {
            transport = value;
            while (transport == value && outbox.Count > 0)
            {
                var message = outbox.Dequeue();
                value.Post(message.Key, message.Value);
            }
        }

        internal static void Detach(IHybridBridgeTransport value)
        {
            if (transport == value)
                transport = null;
        }

        // Main thread only.
        internal static void Deliver(string type, string json)
        {
            if (string.IsNullOrEmpty(type))
                return;
            if (!handlers.TryGetValue(type, out var list) || list.Count == 0)
            {
                if (unhandledLogged.Add(type))
                    Debug.LogWarning($"[HybridBridge] No handler for \"{type}\" from the page.");
                return;
            }
            // Copy so handlers can subscribe or unsubscribe while running.
            foreach (var handler in list.ToArray())
            {
                try
                {
                    handler(string.IsNullOrEmpty(json) ? "null" : json);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }

        // Static state survives Play sessions when domain reload is disabled.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            handlers.Clear();
            outbox.Clear();
            unhandledLogged.Clear();
            transport = null;
        }

        sealed class Subscription : IDisposable
        {
            string type;
            Action<string> handler;

            public Subscription(string type, Action<string> handler)
            {
                this.type = type;
                this.handler = handler;
            }

            public void Dispose()
            {
                if (handler != null && handlers.TryGetValue(type, out var list))
                    list.Remove(handler);
                handler = null;
                type = null;
            }
        }
    }
}
