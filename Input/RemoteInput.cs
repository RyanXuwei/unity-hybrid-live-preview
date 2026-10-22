using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace RyanXuWei.HybridLivePreview
{
    // Editor-only (asmdef: UNITY_EDITOR + ENABLE_INPUT_SYSTEM): replays the viewer's pointer, wheel and
    // keyboard events on virtual Input System devices, so scripts and UI see them as if the Game view
    // were the WebGL canvas. Positions map onto Screen, which the Editor sizes to match the stream.
    [AddComponentMenu("")]
    internal sealed class RemoteInput : MonoBehaviour
    {
#pragma warning disable 0649 // filled by JsonUtility
        [Serializable]
        sealed class Message
        {
            public string t;
            public string k;
            public string c;
            public string key;
            public int id;
            public int bs;
            public float x;
            public float y;
            public float mx;
            public float my;
            public float dx;
            public float dy;
            public float p;
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
            if (streamer.GetComponent<RemoteInput>() == null)
                streamer.gameObject.AddComponent<RemoteInput>();
        }

        readonly ConcurrentQueue<string> inbox = new ConcurrentQueue<string>();
        readonly HashSet<Key> keys = new HashSet<Key>();
        readonly Dictionary<int, int> touches = new Dictionary<int, int>();

        PreviewStreamer streamer;
        Mouse mouse;
        Keyboard keyboard;
        Touchscreen touchscreen;
        Vector2 mousePosition;
        ushort mouseButtons;
        int nextTouchId = 1;

        InputSettings.BackgroundBehavior savedBackground;
        InputSettings.EditorInputBehaviorInPlayMode savedEditorBehavior;

        string sentCursor;
        int sentViewers;

        void OnEnable()
        {
            streamer = GetComponent<PreviewStreamer>();
            streamer.InputReceived += inbox.Enqueue;

            mouse = InputSystem.AddDevice<Mouse>("HybridLivePreview Mouse");
            keyboard = InputSystem.AddDevice<Keyboard>("HybridLivePreview Keyboard");
            touchscreen = InputSystem.AddDevice<Touchscreen>("HybridLivePreview Touchscreen");

            // The user is in Chrome, so the Editor is never focused; without this the Input System drops
            // or reroutes the remote events.
            var settings = InputSystem.settings;
            savedBackground = settings.backgroundBehavior;
            savedEditorBehavior = settings.editorInputBehaviorInPlayMode;
            settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        }

        void OnDisable()
        {
            if (streamer != null)
                streamer.InputReceived -= inbox.Enqueue;

            var settings = InputSystem.settings;
            settings.backgroundBehavior = savedBackground;
            settings.editorInputBehaviorInPlayMode = savedEditorBehavior;

            foreach (var device in new InputDevice[] { mouse, keyboard, touchscreen })
            {
                if (device != null && device.added)
                    InputSystem.RemoveDevice(device);
            }
        }

        void Update()
        {
            while (inbox.TryDequeue(out var json))
            {
                Message msg;
                try { msg = JsonUtility.FromJson<Message>(json); }
                catch (Exception) { continue; }
                if (msg != null)
                    Handle(msg);
            }
            SyncCursor();
        }

        void Handle(Message msg)
        {
            switch (msg.t)
            {
                case "pd":
                case "pm":
                case "pu":
                case "pc":
                    if (msg.k == "touch")
                        Touch(msg);
                    else
                        Pointer(msg);
                    break;
                case "wh":
                    InputSystem.QueueStateEvent(mouse, new MouseState
                    {
                        position = mousePosition,
                        scroll = new Vector2(-msg.dx, -msg.dy),
                        buttons = mouseButtons,
                    });
                    break;
                case "kd":
                case "ku":
                    Key(msg);
                    break;
                case "blur":
                    ReleaseAll();
                    break;
            }
        }

        static Vector2 ToScreen(float x, float y) => new Vector2(x * Screen.width, (1f - y) * Screen.height);

        static Vector2 ToScreenDelta(float x, float y) => new Vector2(x * Screen.width, -y * Screen.height);

        void Pointer(Message msg)
        {
            // Browser buttons: 1 left, 2 right, 4 middle, 8 back, 16 forward.
            ushort buttons = 0;
            if ((msg.bs & 1) != 0) buttons |= (ushort)(1 << (int)MouseButton.Left);
            if ((msg.bs & 2) != 0) buttons |= (ushort)(1 << (int)MouseButton.Right);
            if ((msg.bs & 4) != 0) buttons |= (ushort)(1 << (int)MouseButton.Middle);
            if ((msg.bs & 8) != 0) buttons |= (ushort)(1 << (int)MouseButton.Back);
            if ((msg.bs & 16) != 0) buttons |= (ushort)(1 << (int)MouseButton.Forward);

            mousePosition = ToScreen(msg.x, msg.y);
            mouseButtons = buttons;
            if (msg.t == "pd")
                mouse.MakeCurrent();
            InputSystem.QueueStateEvent(mouse, new MouseState
            {
                position = mousePosition,
                delta = ToScreenDelta(msg.mx, msg.my),
                buttons = buttons,
            });
        }

        void Touch(Message msg)
        {
            TouchPhase phase;
            switch (msg.t)
            {
                case "pd": phase = TouchPhase.Began; break;
                case "pm": phase = TouchPhase.Moved; break;
                case "pu": phase = TouchPhase.Ended; break;
                default: phase = TouchPhase.Canceled; break;
            }

            if (!touches.TryGetValue(msg.id, out var touchId))
            {
                if (phase != TouchPhase.Began)
                    return;
                touchId = nextTouchId++;
                touches[msg.id] = touchId;
            }
            if (phase == TouchPhase.Ended || phase == TouchPhase.Canceled)
                touches.Remove(msg.id);

            if (phase == TouchPhase.Began)
                touchscreen.MakeCurrent();
            InputSystem.QueueStateEvent(touchscreen, new TouchState
            {
                touchId = touchId,
                phase = phase,
                position = ToScreen(msg.x, msg.y),
                delta = ToScreenDelta(msg.mx, msg.my),
                pressure = msg.p,
            });
        }

        void Key(Message msg)
        {
            var down = msg.t == "kd";
            if (WebKeyMap.TryGet(msg.c, out var key))
            {
                var changed = down ? keys.Add(key) : keys.Remove(key);
                if (changed)
                {
                    if (down)
                        keyboard.MakeCurrent();
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(ToArray(keys)));
                }
            }
            if (down && !string.IsNullOrEmpty(msg.key))
                InputSystem.QueueTextEvent(keyboard, msg.key[0]);
        }

        void ReleaseAll()
        {
            if (keys.Count > 0)
            {
                keys.Clear();
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            }
            if (mouseButtons != 0)
            {
                mouseButtons = 0;
                InputSystem.QueueStateEvent(mouse, new MouseState { position = mousePosition });
            }
            foreach (var touchId in touches.Values)
                InputSystem.QueueStateEvent(touchscreen, new TouchState { touchId = touchId, phase = TouchPhase.Canceled });
            touches.Clear();
        }

        // Mirrors Cursor.lockState/visible to the page, which uses pointer lock the way a WebGL build does.
        void SyncCursor()
        {
            var locked = Cursor.lockState != CursorLockMode.None;
            var json = $"{{\"t\":\"cursor\",\"lock\":{(locked ? "true" : "false")},\"visible\":{(Cursor.visible ? "true" : "false")}}}";
            if (json == sentCursor && streamer.ViewerCount == sentViewers)
                return;
            sentCursor = json;
            sentViewers = streamer.ViewerCount;
            streamer.SendToViewers(json);
        }

        static Key[] ToArray(HashSet<Key> set)
        {
            var array = new Key[set.Count];
            set.CopyTo(array);
            return array;
        }
    }
}
