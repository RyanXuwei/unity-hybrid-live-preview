# Hybrid Live Preview for Unity

[English](README.md) · [繁體中文](README.zh-TW.md)

Build an HTML5 UI on top of a Unity 3D scene and see the result live while you work. Press **Play** in the Unity Editor and Chrome opens with your game running the way a WebGL build would: the 3D view is streamed over WebRTC, your own HTML/CSS/JS sits on top of it with Vite hot reload, and mouse, keyboard and touch input flow back into Unity.

No WebGL build, no waiting. Edit a C# script or a CSS file and look again.

![How Hybrid Live Preview works](Documentation~/images/architecture.svg)

## Demo

See it running on real hardware (YouTube):

[![Hybrid Live Preview demo on YouTube](https://img.youtube.com/vi/6tSYVJvjHwY/hqdefault.jpg)](https://youtu.be/6tSYVJvjHwY)

## Features

- **One button.** Play starts a Vite dev server and a dedicated Chrome window; Stop closes both and restores everything it changed.
- **Looks like the WebGL build.** The stream follows the browser window's size and aspect, capped at *Player Settings → WebGL → Default Canvas Width/Height*, and fills the window like the WebGL template canvas. The Game view is locked to the same resolution, so `Screen`, camera aspect and UI layout match.
- **Same pixels as the Game view.** The stream copies the Game view's finished image, so reflections, post-processing, camera stacks and Screen Space - Overlay canvases look the same in Chrome.
- **Real HTML UI.** Your page is plain HTML/CSS/JS served by Vite, layered over the 3D view. Use any framework you like.
- **Input goes back to Unity.** Pointer (mouse, pen, touch), wheel and keyboard events on the 3D view are replayed on virtual Input System devices, so `Mouse.current`, `Keyboard.current`, `Touchscreen.current`, Input Actions and `InputSystemUIInputModule` all respond. `Cursor.lockState` turns into browser Pointer Lock.
- **One channel for data.** `HybridBridge` (C#) and `window.HybridBridge` (page) exchange typed JSON messages; the same API works in the preview and in a WebGL build through a jslib.
- **One click to the final site.** *Build WebGL* builds the Unity player and your Vite page and merges them; the built page behaves like the preview.
- **Stays out of your builds.** Everything except the small bridge and WebGL page assemblies is Editor-only.

## Requirements

| | |
|---|---|
| Unity | 6000.3 or newer (tested on Windows, Direct3D 12) |
| Active Input Handling | *Input System Package* or *Both* (needed for input forwarding) |
| Node.js | 18 or newer, with `npm` on `PATH` |
| Browser | Google Chrome |
| Unity module | *WebGL Build Support* (only for **Build WebGL**) |

The package depends on `com.unity.webrtc` 3.0.0 and `com.unity.inputsystem` 1.11.2 or newer; Unity installs them automatically.

### Works in any project

Nothing in the package is tied to a particular project, and there is nothing to move, rename or configure per project:

- `HybridPreviewWeb/` is created at the project root on the first Play or build. The package keeps `src/hlp/` up to date and never touches your own files.
- Product name, company, version and the canvas size cap are read from that project's Player Settings.
- The Unity build's file names (build name, *Name Files As Hashes*, Brotli/Gzip/none) are detected after each build, not hard-coded.
- Builds use the scenes in the build profile, or the open scene when it lists none.
- Working files go to `Library/HybridLivePreview/`, the final site to `Builds/HybridWebGL/<version>/`, both inside the project.

Keep the two things the template starts with: the `<video id="stream">` element in `index.html` and the `startHybridPreview(...)` call in `src/main.js`. Everything else in the page is yours.

## Install

In Unity, open **Window → Package Manager → + → Install package from git URL…** and enter:

```
https://github.com/RyanXuwei/unity-hybrid-live-preview.git#main
```

Or add it to `Packages/manifest.json`:

```json
"com.ryanxuwei.hybrid-live-preview": "https://github.com/RyanXuwei/unity-hybrid-live-preview.git#main"
```

Use a tag such as `#v0.8.0` instead of `#main` to pin a version.

## Quick start

1. Open a scene and press **Play**. (A camera tagged **MainCamera** is only needed for the *Mirror Camera* capture mode.)
2. On the first run the web template is copied to `<YourProject>/HybridPreviewWeb/` and `npm install` runs (about a minute).
3. Chrome opens `http://127.0.0.1:5173/`. The small status pill in the corner turns green and shows **LIVE** with the stream size.
4. Click the 3D view to give it focus, then use the mouse and keyboard as you would in a WebGL build.
5. Press **Stop**. Chrome and Vite close, and the Game view returns to its previous size.

![Play and Stop](Documentation~/images/lifecycle.svg)

## Build your UI

Your UI lives in `<YourProject>/HybridPreviewWeb/`. Commit it with your project and ignore `node_modules/`.

![Who owns which file](Documentation~/images/web-folder.svg)

Put your markup inside `#app` in `index.html`:

```html
<main class="stage">
  <video id="stream" autoplay playsinline muted tabindex="-1"></video>
  <div id="app">
    <button class="my-button">Start</button>
  </div>
</main>
```

and your code in `src/main.js`:

```js
import './style.css';
import { startHybridPreview } from './hlp/index.js';

const preview = startHybridPreview(document.getElementById('stream'));

document.querySelector('.my-button').addEventListener('click', () => {
  console.log('clicked in the page, not in Unity', preview.peer);
});
```

Direct children of `#app` receive clicks; everywhere else, input passes through to the 3D view. Vite reloads the page as you save, and the stream reconnects on its own.

![Three layers on one page](Documentation~/images/page-layers.svg)

### The status panel

The pill in the corner belongs to the package. Drag it by its header, click **▸** or double-click to expand the connection and stream stats, and it remembers where you left it. It renders inside a shadow root, so your CSS never affects it and it never affects yours. To hide it completely:

```js
startHybridPreview(document.getElementById('stream'), { panel: false });
```

`startHybridPreview` returns the live connection state (also available as `window.__hlp`): `signaling`, `unity`, `peer`, `codec`, `width`, `height`, `fps`, `framesDecoded`.

## Input in your Unity scripts

Read input through the Input System and it works from both the Editor and the browser:

```csharp
using UnityEngine;
using UnityEngine.InputSystem;

public class Spin : MonoBehaviour
{
    InputAction drag;

    void Awake() => drag = new InputAction(binding: "<Mouse>/delta");
    void OnEnable() => drag.Enable();
    void OnDisable() => drag.Disable();

    void Update()
    {
        if (Mouse.current != null && Mouse.current.leftButton.isPressed)
            transform.Rotate(Vector3.up, -drag.ReadValue<Vector2>().x * 0.2f);
    }
}
```

The legacy `UnityEngine.Input` API (`Input.GetMouseButton`, `Input.GetKey`, …) cannot receive forwarded input inside the Editor; it only reads the physical devices of the focused Editor window. Port those scripts to the Input System.

While the preview runs, the package temporarily sets the Input System's *Background Behavior* to *Ignore Focus* and routes all device input to the Game view, because Chrome, not the Editor, has focus. Both settings are restored on Stop.

## Talk between the page and Unity (HybridBridge)

`HybridBridge` is the one channel for data between your H5 page and Unity. Each side has a single global entry point: the static C# class `HybridBridge` and the page's `window.HybridBridge`. A message is a type name plus a JSON payload. The same code runs in the Editor preview (over a WebRTC data channel) and in a WebGL build (through `HybridBridge.jslib`); only the transport underneath changes.

Page side (any script, no import needed):

```js
HybridBridge.send('door', { id: 'A1', open: true });

const off = HybridBridge.on('sensor', (data) => {
  document.querySelector('#temp').textContent = data.celsius;
});
// off() stops listening.
```

Unity side:

```csharp
using System;
using RyanXuWei.HybridLivePreview;
using UnityEngine;

public class Door : MonoBehaviour
{
    [Serializable] class DoorCommand { public string id; public bool open; }
    [Serializable] class Reading { public float celsius; }

    IDisposable subscription;

    void OnEnable() => subscription = HybridBridge.On<DoorCommand>("door", cmd => Debug.Log($"{cmd.id} open={cmd.open}"));
    void OnDisable() => subscription?.Dispose();

    void Report(float celsius) => HybridBridge.Send("sensor", new Reading { celsius = celsius });
}
```

| C# | Page | Notes |
|---|---|---|
| `HybridBridge.Send(type, payload)` | `HybridBridge.send(type, payload)` | C# payloads go through `JsonUtility` (a `[Serializable]` class or struct). `HybridBridge.Send(type)` sends no payload. |
| `HybridBridge.SendJson(type, json)` | | Payload that is already JSON (arrays, strings, numbers…). |
| `HybridBridge.On<T>(type, handler)` | `HybridBridge.on(type, (payload, type) => …)` | Returns `IDisposable` in C#, an `off()` function on the page. |
| `HybridBridge.OnJson(type, handler)` | `HybridBridge.off(type, handler)` | `OnJson` hands over the raw JSON string. |
| `HybridBridge.IsConnected` | `HybridBridge.connected` | Whether the other side is reachable right now. |

- Either side can send at any time. Until the other side is connected, messages wait in a queue (up to 256, oldest dropped first) and are delivered once it is.
- A message nobody listens to is dropped; Unity logs one warning per unknown type. Subscribe in `OnEnable` and, when the page loads, ask Unity for the state you need (for example `HybridBridge.send('getState')`) instead of relying on messages sent before the page existed.
- The bridge assembly (`RyanXuWei.HybridLivePreview.Bridge`) is referenced automatically and is the only part of the package that ships in player builds.

## Accessibility sample (screen readers)

The package ships an **Accessibility** sample: **Window → Package Manager → Hybrid Live Preview → Samples → Accessibility → Import**. It makes uGUI screens readable by NVDA, VoiceOver and JAWS, in the Play preview and in the WebGL build:

- Unity sends the visible `Button`s, `TMP_Text`/`Text` and labelled images to the page; the page lays transparent, real HTML elements over the game at the same positions. Tab moves between them, Enter/Space presses the Unity button, Esc returns to the game.
- `A11ySubtitle.Show("…", "Speaker")` shows a caption and reads it aloud.
- Add **UI → Accessibility → A11y Label** to name a button, give an image alt text, add a description, or hide an element from screen readers.

After the import, the page files are copied to `HybridPreviewWeb/src/a11y/`. Turn the layer on with **Tools → Hybrid Live Preview → Accessibility → Add startA11y() To main.js**, or add `import { startA11y } from './a11y/a11y.js';` and `startA11y({ lang: 'zh-TW' });` yourself. Set `lang` to the language of your UI text; screen readers choose the voice from it. Details are in the sample's `README.md`.

## Build the final WebGL site

**Tools → Hybrid Live Preview → Build WebGL (Unity + Web UI)** makes the deployable result in one step:

1. Builds the Unity WebGL player (the scenes in the build profile, or the open scene if it lists none).
2. Builds `HybridPreviewWeb` with Vite on top of your `vite.config.js`.
3. Merges both into `<YourProject>/Builds/HybridWebGL/<version>/` (the Player Settings version, so builds of different versions never overwrite each other): your `index.html`, `assets/`, and everything the Unity WebGL template produced next to it: `Build/`, `StreamingAssets/`, `TemplateData/` and any custom files the template ships (for example `environmentSetting.js`). The page itself is the WebGL template's `index.html` with your UI added (see *Web build page* below).

Your UI code does not change between Play and the build: `HybridBridge` connects through the jslib, and `#app` sits on top of the Unity canvas. As in the preview, the canvas only takes keyboard input after it is clicked, so text fields in your UI keep working.

### Web build page

**Tools → Hybrid Live Preview → Web Build Page** chooses how the final `index.html` is made:

- **Inject Into WebGL Template** (default): the `index.html` of the WebGL template selected in Player Settings stays the page, so its canvas, loader, scripts, service worker and styling are exactly what you designed in that template. The package only adds what your UI needs: your scripts and styles into `<head>`, and the `#app` layer on top of the canvas at the end of `<body>`. Put `<!-- HLP:head -->` and `<!-- HLP:body -->` in the template if you want to choose those two spots; without them the content goes before `</head>` and `</body>`. The template needs no placeholder, and your `HybridPreviewWeb/index.html` is not copied over it.
- **Replace WebGL Template Page**: `HybridPreviewWeb/index.html` becomes the page and `startHybridPreview` starts Unity in a canvas that takes the place of the preview video. If the template's `index.html` did extra setup, copy those lines into your `index.html`.

The layer is `pointer-events: none` like in the preview, so clicks pass through to Unity except on elements that set `pointer-events: auto`.

**Preview WebGL Build** serves that folder at `http://127.0.0.1:4173/` and opens it in the preview Chrome. With Brotli or Gzip compression and *Decompression Fallback* off, your web server must send `Content-Encoding` for `Build/*.br` / `*.gz` (the preview server does); the page cannot be opened straight from disk.

## Menu

**Tools → Hybrid Live Preview**

| Item | What it does |
|---|---|
| Auto Launch On Play | Turns the whole preview on or off. |
| Match Game View To Preview | Locks the Game view to the stream resolution during Play. |
| Capture → Game View Output / Mirror Camera (Fallback) | **Game View Output** (default) streams exactly what the Game view shows. **Mirror Camera** renders `Camera.main` a second time and keeps streaming while the Game view is hidden, but effects computed only for the main camera, such as planar reflections, may not match. Applies from the next Play. |
| Open Preview In Chrome | Reopens the preview window. |
| Build WebGL (Unity + Web UI) | Builds the Unity player and the web page and merges them into `Builds/HybridWebGL/<version>`. |
| Preview WebGL Build | Serves the last build at `http://127.0.0.1:4173/` and opens it in Chrome. |
| Install Web App | Copies the template and runs `npm install` now. |
| Accessibility → Install Web Files / Add startA11y() To main.js | Only with the Accessibility sample imported: (re)copies its page files to `src/a11y/`, or turns the layer on in `main.js`. |
| Stop Vite And Chrome | Closes leftover processes. |
| About | Logs the installed version. |

## How it fits together

| Folder | Assembly | Role |
|---|---|---|
| `Editor/` | `RyanXuWei.HybridLivePreview.Editor` (Editor platform) | Play/Stop lifecycle, Vite and Chrome processes, Game view sizing, web app install and sync. |
| `Streaming/` | `RyanXuWei.HybridLivePreview.Streaming` (`UNITY_EDITOR` only) | `PreviewStreamer`: Game view capture (or mirror camera), WebRTC video track, data channels; the preview's bridge transport. |
| `Bridge/` | `RyanXuWei.HybridLivePreview.Bridge` (all platforms, auto-referenced) | `HybridBridge` API; `HybridBridge.jslib` and its transport for WebGL builds. |
| `WebGL/` | `RyanXuWei.HybridLivePreview.WebGL` (WebGL players only) | Shares the keyboard with the page in sites made by Build WebGL. |
| `Input/` | `RyanXuWei.HybridLivePreview.Input` (`UNITY_EDITOR` + `ENABLE_INPUT_SYSTEM`) | `RemoteInput`: replays browser input on virtual Input System devices. |
| `WebApp~/` | (not imported by Unity) | Vite template, signaling relay, viewer, status panel. |

Runtime files live in `<YourProject>/Library/HybridLivePreview/`: `vite.log`, the Chrome profile, and PID files used for cleanup.

## Troubleshooting

- **The panel says "Vite offline".** Check `Library/HybridLivePreview/vite.log`. Make sure `node` and `npm` run in a new terminal and that port 5173 is free.
- **The panel stays on "Waiting for Unity Play".** Make sure the scene has a camera tagged MainCamera and that the Console shows `[HybridLivePreview] Signaling connected`.
- **Input does nothing.** Click the 3D view first so it has keyboard focus, set *Active Input Handling* to *Input System Package* or *Both*, and read input through the Input System.
- **Input does nothing or the picture freezes unless the Editor is focused.** The preview turns on *Run In Background* for you during Play. Also check the Game view toolbar: its Play Mode must be *Play Focused* (*Play Unfocused* makes keyboard input stop working).
- **The preview freezes.** Unity stops rendering a Game view that is hidden behind another tab, and the Console warns about it. Keep the Game view visible, or switch *Capture* to *Mirror Camera*.
- **Reflections or effects look different only in Chrome.** *Capture* is set to *Mirror Camera*; switch back to *Game View Output*.
- **The Console warns that the active Build Profile is not Web.** The package only supports Web output. Switch to the Web platform in *File > Build Profiles*; **Build WebGL** offers to switch for you.
- **Chrome was not found.** The default browser opens instead; set the path with `EditorPrefs` key `RyanXuWei.HybridLivePreview.ChromePath` if Chrome is installed somewhere unusual.

## Why not Unity Render Streaming?

`com.unity.renderstreaming` 3.1.0-exp.9 does not compile on Unity 6000.3 (it uses the removed `AndroidApiLevel22`). This package uses `com.unity.webrtc` 3.0.0 directly with its own small signaling relay inside the Vite dev server.

## For AI assistants

Two Markdown guides let any AI assistant pick up the hybrid architecture quickly:

- [`WebApp~/src/hlp/AI-GUIDE.md`](WebApp~/src/hlp/AI-GUIDE.md): building an app with the package (file ownership, page layout, HybridBridge rules, recipes, troubleshooting). It is copied into every project as `HybridPreviewWeb/src/hlp/AI-GUIDE.md`, so point your assistant there.
- [`AGENTS.md`](AGENTS.md): changing the package itself.

## License

[MIT](LICENSE) © Ryan Hsu
