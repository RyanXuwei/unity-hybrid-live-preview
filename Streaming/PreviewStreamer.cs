using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Unity.WebRTC;
using UnityEngine;

namespace RyanXuWei.HybridLivePreview
{
    // Editor-only (asmdef define constraint UNITY_EDITOR): streams the Game view (or, as a fallback, a
    // mirror of Camera.main) as a WebRTC video track and answers browser offers relayed by the Vite
    // signaling plugin. The track follows the viewer's viewport aspect, capped at width x height, the
    // way a WebGL canvas follows the page.
    [AddComponentMenu("")]
    public sealed class PreviewStreamer : MonoBehaviour
    {
        public string signalingUrl = "ws://127.0.0.1:5173/signaling";
        public int width = 1280;
        public int height = 720;

        // Copies the Game view's final image, so every camera, reflection and post effect matches it.
        // Off: renders a separate mirror of Camera.main, which keeps working while the Game view is hidden.
        public bool captureGameView = true;

        public static PreviewStreamer Instance { get; private set; }
        public string Status { get; private set; } = "idle";
        public int ViewerCount => peers.Count;
        public Vector2Int StreamSize => trackSize;

        // Raised when the capture size changes, so the Editor can match the Game view to it.
        public event Action<Vector2Int> Resized;

        // Raised from OnEnable so optional add-ons (remote input) can attach to the streamer.
        public static event Action<PreviewStreamer> Created;

        // Raw JSON from the viewers' "input" data channels; may arrive off the main thread. A viewer
        // leaving is reported as {"t":"blur"} so held keys and buttons can be released.
        public event Action<string> InputReceived;

        // Raw JSON from the viewers' "bridge" data channels (HybridBridge messages); may arrive off the main thread.
        public event Action<string> BridgeReceived;

        // Viewers whose "bridge" channel is open.
        public int BridgeViewerCount
        {
            get
            {
                var count = 0;
                foreach (var channel in bridgeChannels.Values)
                {
                    if (channel.ReadyState == RTCDataChannelState.Open)
                        count++;
                }
                return count;
            }
        }

        readonly Dictionary<string, RTCPeerConnection> peers = new Dictionary<string, RTCPeerConnection>();
        readonly Dictionary<string, RTCDataChannel> channels = new Dictionary<string, RTCDataChannel>();
        readonly Dictionary<string, RTCDataChannel> bridgeChannels = new Dictionary<string, RTCDataChannel>();
        readonly Dictionary<string, List<RTCIceCandidateInit>> pendingCandidates = new Dictionary<string, List<RTCIceCandidateInit>>();
        readonly SemaphoreSlim sendLock = new SemaphoreSlim(1, 1);

        Camera source;
        Camera mirror;
        RenderTexture frame;
        RenderTexture grab;
        float lastGrab;
        bool warnedStall;
        VideoStreamTrack track;
        Vector2Int trackSize;
        ClientWebSocket socket;
        CancellationTokenSource cts;
        bool loggedOffline;

        bool savedRunInBackground;

        void OnEnable()
        {
            Instance = this;
            // The user works in Chrome, so the Editor is unfocused; without this Unity freezes the player loop
            // (frames and input) until the Editor is focused again.
            savedRunInBackground = Application.runInBackground;
            Application.runInBackground = true;
            Created?.Invoke(this);
        }

        void OnDisable()
        {
            Application.runInBackground = savedRunInBackground;
        }

        void Start()
        {
            StartCoroutine(WebRTC.Update());

            if (captureGameView)
            {
                Capture(new Vector2Int(width, height));
                lastGrab = Time.realtimeSinceStartup;
                StartCoroutine(GrabGameView());
                Debug.Log($"[HybridLivePreview] Streaming the Game view, up to {width}x{height} ({SystemInfo.graphicsDeviceType}).");
            }
            else
            {
                source = Camera.main;
                if (source == null)
                {
                    Status = "no Camera.main";
                    Debug.LogError("[HybridLivePreview] No Camera.main found; nothing to stream.");
                    return;
                }

                // A mirror camera keeps the Game view intact; CaptureStreamTrack redirects its camera to a RenderTexture.
                var go = new GameObject("[HybridLivePreview] Stream Camera");
                go.transform.SetParent(transform, false);
                mirror = go.AddComponent<Camera>();
                mirror.CopyFrom(source);
                Capture(new Vector2Int(width, height));
                Debug.Log($"[HybridLivePreview] Streaming a mirror of {source.name}, up to {width}x{height} ({SystemInfo.graphicsDeviceType}).");
            }
            cts = new CancellationTokenSource();
            _ = RunSignaling(cts.Token);
        }

        void Update()
        {
            // The Editor stops rendering a Game view hidden behind another tab, which freezes the stream.
            if (!captureGameView || peers.Count == 0)
                return;
            var stalled = Time.realtimeSinceStartup - lastGrab > 2f;
            if (stalled && !warnedStall)
                Debug.LogWarning("[HybridLivePreview] The Game view is not rendering (hidden behind another tab, or its Play Mode is not Play Focused?), so the stream is paused. Keep it visible, set the Game view Play Mode to Play Focused, or switch Tools > Hybrid Live Preview > Capture to Mirror Camera.");
            warnedStall = stalled;
        }

        void LateUpdate()
        {
            if (mirror == null)
                return;
            // A scene load destroys the camera being mirrored; follow the new scene's main camera.
            if (source == null)
                source = Camera.main;
            if (source == null)
                return;
            mirror.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            mirror.fieldOfView = source.fieldOfView;
            mirror.nearClipPlane = source.nearClipPlane;
            mirror.farClipPlane = source.farClipPlane;
            mirror.cullingMask = source.cullingMask;
        }

        void OnDestroy()
        {
            cts?.Cancel();
            foreach (var pc in peers.Values)
            {
                pc.Close();
                pc.Dispose();
            }
            peers.Clear();
            track?.Dispose();
            if (frame != null)
                Destroy(frame);
            if (grab != null)
                Destroy(grab);
            socket?.Abort();
            if (Instance == this)
                Instance = null;
        }

        // Scales the viewport down to fit width x height without changing its aspect; even sizes keep encoders happy.
        Vector2Int FitViewport(int viewportWidth, int viewportHeight)
        {
            if (viewportWidth <= 0 || viewportHeight <= 0)
                return new Vector2Int(width, height);
            var scale = Mathf.Min(1f, Mathf.Min((float)width / viewportWidth, (float)height / viewportHeight));
            int Even(float v) => Mathf.Max(2, Mathf.RoundToInt(v / 2f) * 2);
            return new Vector2Int(Even(viewportWidth * scale), Even(viewportHeight * scale));
        }

        void Capture(Vector2Int size)
        {
            if (size == trackSize || (!captureGameView && mirror == null))
                return;
            var previous = track;
            var previousFrame = frame;
            if (captureGameView)
            {
                frame = new RenderTexture(size.x, size.y, 0, WebRTC.GetSupportedRenderTextureFormat(SystemInfo.graphicsDeviceType));
                frame.Create();
                track = new VideoStreamTrack(frame);
            }
            else
            {
                track = mirror.CaptureStreamTrack(size.x, size.y);
                mirror.ResetAspect();
            }
            trackSize = size;

            // Each peer only sends this one video track.
            foreach (var pc in peers.Values)
            {
                foreach (var sender in pc.GetSenders())
                    sender.ReplaceTrack(track);
            }
            Resized?.Invoke(size);
            if (previous != null)
            {
                var texture = previous.Texture;
                previous.Dispose();
                if (texture != null && (mirror == null || texture != mirror.targetTexture))
                    Destroy(texture);
                if (previousFrame != null && previousFrame != frame)
                    Destroy(previousFrame);
                Debug.Log($"[HybridLivePreview] Stream resized to {size.x}x{size.y} to match the viewer viewport.");
            }
        }

        // Copies the finished Game view into the stream frame once everything (all cameras, UI, post) has rendered.
        IEnumerator GrabGameView()
        {
            var endOfFrame = new WaitForEndOfFrame();
            while (true)
            {
                yield return endOfFrame;
                if (frame == null)
                    continue;
                if (grab == null || grab.width != Screen.width || grab.height != Screen.height)
                {
                    if (grab != null)
                        Destroy(grab);
                    grab = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB32);
                    grab.Create();
                }
                ScreenCapture.CaptureScreenshotIntoRenderTexture(grab);
                // The back buffer comes back upside down where UVs start at the top (D3D, Metal, Vulkan).
                if (SystemInfo.graphicsUVStartsAtTop)
                    Graphics.Blit(grab, frame, new Vector2(1f, -1f), new Vector2(0f, 1f));
                else
                    Graphics.Blit(grab, frame);
                lastGrab = Time.realtimeSinceStartup;
            }
        }

        // Sends a JSON message to every viewer's input channel (e.g. cursor lock changes).
        public void SendToViewers(string json)
        {
            foreach (var channel in channels.Values)
            {
                if (channel.ReadyState == RTCDataChannelState.Open)
                    channel.Send(json);
            }
        }

        // Sends a HybridBridge envelope to every viewer's bridge channel.
        public void SendToBridge(string json)
        {
            foreach (var channel in bridgeChannels.Values)
            {
                if (channel.ReadyState == RTCDataChannelState.Open)
                    channel.Send(json);
            }
        }

        async Task RunSignaling(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                var ws = new ClientWebSocket();
                socket = ws;
                try
                {
                    Status = "connecting";
                    await ws.ConnectAsync(new Uri(signalingUrl), token);
                    loggedOffline = false;
                    Status = "waiting for viewer";
                    Debug.Log($"[HybridLivePreview] Signaling connected: {signalingUrl}");
                    await Send(new SignalMessage { type = "hello", role = "unity" });
                    await ReceiveLoop(ws, token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception e)
                {
                    Status = "signaling offline";
                    if (!loggedOffline)
                    {
                        loggedOffline = true;
                        Debug.Log($"[HybridLivePreview] Signaling unavailable ({e.Message}); retrying.");
                    }
                }
                finally
                {
                    ws.Dispose();
                }

                try { await Task.Delay(1000, token); }
                catch (OperationCanceledException) { break; }
            }
        }

        async Task ReceiveLoop(ClientWebSocket ws, CancellationToken token)
        {
            var buffer = new byte[16 * 1024];
            using var message = new MemoryStream();
            while (ws.State == WebSocketState.Open && !token.IsCancellationRequested)
            {
                var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), token);
                if (result.MessageType == WebSocketMessageType.Close)
                    break;
                message.Write(buffer, 0, result.Count);
                if (!result.EndOfMessage)
                    continue;
                var json = Encoding.UTF8.GetString(message.ToArray());
                message.SetLength(0);
                Handle(json);
            }
        }

        async Task Send(SignalMessage msg)
        {
            var ws = socket;
            if (ws == null || ws.State != WebSocketState.Open)
                return;
            var bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(msg));
            await sendLock.WaitAsync();
            try
            {
                await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
            }
            finally
            {
                sendLock.Release();
            }
        }

        void Post(SignalMessage msg) => _ = Send(msg);

        void Handle(string json)
        {
            SignalMessage msg;
            try { msg = JsonUtility.FromJson<SignalMessage>(json); }
            catch (Exception e)
            {
                Debug.LogWarning($"[HybridLivePreview] Bad signaling message: {e.Message}");
                return;
            }

            switch (msg.type)
            {
                case "offer":
                    Capture(FitViewport(msg.width, msg.height));
                    StartCoroutine(Answer(msg.id, msg.sdp));
                    break;
                case "viewport":
                    Capture(FitViewport(msg.width, msg.height));
                    break;
                case "candidate":
                    AddCandidate(msg);
                    break;
                case "bye":
                    ClosePeer(msg.id);
                    break;
            }
        }

        void AddCandidate(SignalMessage msg)
        {
            if (string.IsNullOrEmpty(msg.candidate) || !peers.TryGetValue(msg.id, out var pc))
                return;
            var init = new RTCIceCandidateInit
            {
                candidate = msg.candidate,
                sdpMid = msg.sdpMid,
                sdpMLineIndex = msg.sdpMLineIndex < 0 ? (int?)null : msg.sdpMLineIndex,
            };
            if (pendingCandidates.TryGetValue(msg.id, out var pending))
                pending.Add(init);
            else
                pc.AddIceCandidate(new RTCIceCandidate(init));
        }

        IEnumerator Answer(string id, string sdp)
        {
            ClosePeer(id);
            var config = new RTCConfiguration { iceServers = Array.Empty<RTCIceServer>() };
            var pc = new RTCPeerConnection(ref config);
            peers[id] = pc;
            pendingCandidates[id] = new List<RTCIceCandidateInit>();
            Status = "negotiating";

            pc.OnIceCandidate = c => Post(new SignalMessage
            {
                type = "candidate",
                id = id,
                candidate = c.Candidate,
                sdpMid = c.SdpMid,
                sdpMLineIndex = c.SdpMLineIndex ?? -1,
            });
            pc.OnDataChannel = channel =>
            {
                if (channel.Label == "input")
                {
                    channels[id] = channel;
                    channel.OnMessage = bytes => InputReceived?.Invoke(Encoding.UTF8.GetString(bytes));
                }
                else if (channel.Label == "bridge")
                {
                    bridgeChannels[id] = channel;
                    channel.OnMessage = bytes => BridgeReceived?.Invoke(Encoding.UTF8.GetString(bytes));
                }
            };
            pc.OnConnectionStateChange = state =>
            {
                Debug.Log($"[HybridLivePreview] Viewer {id}: {state}");
                if (state == RTCPeerConnectionState.Connected)
                    Status = "streaming";
                else if (state == RTCPeerConnectionState.Failed)
                    ClosePeer(id);
            };

            var offer = new RTCSessionDescription { type = RTCSdpType.Offer, sdp = sdp };
            var setRemote = pc.SetRemoteDescription(ref offer);
            yield return setRemote;
            if (Failed(id, setRemote, "setRemoteDescription"))
                yield break;

            RTCRtpTransceiver video = null;
            foreach (var t in pc.GetTransceivers())
            {
                if (t.Receiver.Track != null && t.Receiver.Track.Kind == TrackKind.Video)
                {
                    video = t;
                    break;
                }
            }
            if (video != null)
            {
                video.Sender.ReplaceTrack(track);
                video.Direction = RTCRtpTransceiverDirection.SendOnly;
            }
            else
            {
                pc.AddTrack(track, null);
            }

            if (pendingCandidates.TryGetValue(id, out var pending))
            {
                pendingCandidates.Remove(id);
                foreach (var init in pending)
                    pc.AddIceCandidate(new RTCIceCandidate(init));
            }

            var answerOp = pc.CreateAnswer();
            yield return answerOp;
            if (Failed(id, answerOp, "createAnswer"))
                yield break;

            var answer = answerOp.Desc;
            var setLocal = pc.SetLocalDescription(ref answer);
            yield return setLocal;
            if (Failed(id, setLocal, "setLocalDescription"))
                yield break;

            Post(new SignalMessage { type = "answer", id = id, sdp = answer.sdp });
        }

        bool Failed(string id, AsyncOperationBase op, string step)
        {
            if (!op.IsError)
                return false;
            Debug.LogError($"[HybridLivePreview] Viewer {id} {step} failed: {op.Error.message}");
            ClosePeer(id);
            return true;
        }

        void ClosePeer(string id)
        {
            if (string.IsNullOrEmpty(id))
                return;
            pendingCandidates.Remove(id);
            bridgeChannels.Remove(id);
            if (channels.Remove(id))
                InputReceived?.Invoke("{\"t\":\"blur\"}");
            if (!peers.TryGetValue(id, out var pc))
                return;
            peers.Remove(id);
            pc.Close();
            pc.Dispose();
            if (peers.Count == 0 && Status == "streaming")
                Status = "waiting for viewer";
        }
    }
}
