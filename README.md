# Hybrid Live Preview for Unity

[English](README.md) · [繁體中文](README.zh-TW.md)

Build an HTML5 UI on top of a Unity 3D scene and see the result live while you work. Press **Play** in the Unity Editor and Chrome opens with your game running the way a WebGL build would: the 3D view is streamed over WebRTC, your own HTML/CSS/JS sits on top of it with Vite hot reload, and mouse, keyboard and touch input flow back into Unity.

No WebGL build, no waiting. Edit a C# script or a CSS file and look again.

![How Hybrid Live Preview works](Documentation~/images/architecture.svg)

## Features

- **One button.** Play starts a Vite dev server and a dedicated Chrome window; Stop closes both and restores everything it changed.
- **Looks like the WebGL build.** The stream follows the browser window's size and aspect, capped at *Player Settings → WebGL → Default Canvas Width/Height*, and fills the window like the WebGL template canvas. The Game view is locked to the same resolution, so `Screen`, camera aspect and UI layout match.
- **Same pixels as the Game view.** The stream copies the Game view's finished image, so reflections, post-processing, camera stacks and Screen Space - Overlay canvases look the same in Chrome.
- **Real HTML UI.** Your page is plain HTML/CSS/JS served by Vite, layered over the 3D view. Use any framework you like.
- **Input goes back to Unity.** Pointer (mouse, pen, touch), wheel and keyboard events on the 3D view are replayed on virtual Input System devices, so `Mouse.current`, `Keyboard.current`, `Touchscreen.current`, Input Actions and `InputSystemUIInputModule` all respond. `Cursor.lockState` turns into browser Pointer Lock.
- **Stays out of your builds.** Every assembly is Editor-only; nothing from this package is compiled into a player.

## Requirements

| | |
|---|---|
| Unity | 6000.3 or newer (tested on Windows, Direct3D 12) |
| Active Input Handling | *Input System Package* or *Both* (needed for input forwarding) |
| Node.js | 18 or newer, with `npm` on `PATH` |
| Browser | Google Chrome |

The package depends on `com.unity.webrtc` 3.0.0 and `com.unity.inputsystem` 1.11.2 or newer; Unity installs them automatically.

## Install

In Unity, open **Window → Package Manager → + → Install package from git URL…** and enter:

```
https://github.com/RyanXuwei/unity-hybrid-live-preview.git#main
```

Or add it to `Packages/manifest.json`:

```json
"com.ryanxuwei.hybrid-live-preview": "https://github.com/RyanXuwei/unity-hybrid-live-preview.git#main"
```

Use a tag such as `#v0.5.0` instead of `#main` to pin a version.

## Quick start

1. Open a scene with a camera tagged **MainCamera** and press **Play**.
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

## Menu

**Tools → Hybrid Live Preview**

| Item | What it does |
|---|---|
| Auto Launch On Play | Turns the whole preview on or off. |
| Match Game View To Preview | Locks the Game view to the stream resolution during Play. |
| Capture → Game View Output / Mirror Camera (Fallback) | **Game View Output** (default) streams exactly what the Game view shows. **Mirror Camera** renders `Camera.main` a second time and keeps streaming while the Game view is hidden, but effects computed only for the main camera, such as planar reflections, may not match. Applies from the next Play. |
| Open Preview In Chrome | Reopens the preview window. |
| Install Web App | Copies the template and runs `npm install` now. |
| Stop Vite And Chrome | Closes leftover processes. |
| About | Logs the installed version. |

## How it fits together

| Folder | Assembly | Role |
|---|---|---|
| `Editor/` | `RyanXuWei.HybridLivePreview.Editor` (Editor platform) | Play/Stop lifecycle, Vite and Chrome processes, Game view sizing, web app install and sync. |
| `Streaming/` | `RyanXuWei.HybridLivePreview.Streaming` (`UNITY_EDITOR` only) | `PreviewStreamer`: Game view capture (or mirror camera), WebRTC video track, data channel. |
| `Input/` | `RyanXuWei.HybridLivePreview.Input` (`UNITY_EDITOR` + `ENABLE_INPUT_SYSTEM`) | `RemoteInput`: replays browser input on virtual Input System devices. |
| `WebApp~/` | (not imported by Unity) | Vite template, signaling relay, viewer, status panel. |

Runtime files live in `<YourProject>/Library/HybridLivePreview/`: `vite.log`, the Chrome profile, and PID files used for cleanup.

## Troubleshooting

- **The panel says "Vite offline".** Check `Library/HybridLivePreview/vite.log`. Make sure `node` and `npm` run in a new terminal and that port 5173 is free.
- **The panel stays on "Waiting for Unity Play".** Make sure the scene has a camera tagged MainCamera and that the Console shows `[HybridLivePreview] Signaling connected`.
- **Input does nothing.** Click the 3D view first so it has keyboard focus, set *Active Input Handling* to *Input System Package* or *Both*, and read input through the Input System.
- **The preview freezes.** Unity stops rendering a Game view that is hidden behind another tab, and the Console warns about it. Keep the Game view visible, or switch *Capture* to *Mirror Camera*.
- **Reflections or effects look different only in Chrome.** *Capture* is set to *Mirror Camera*; switch back to *Game View Output*.
- **Chrome was not found.** The default browser opens instead; set the path with `EditorPrefs` key `RyanXuWei.HybridLivePreview.ChromePath` if Chrome is installed somewhere unusual.

## Why not Unity Render Streaming?

`com.unity.renderstreaming` 3.1.0-exp.9 does not compile on Unity 6000.3 (it uses the removed `AndroidApiLevel22`). This package uses `com.unity.webrtc` 3.0.0 directly with its own small signaling relay inside the Vite dev server.

## License

[MIT](LICENSE) © Ryan Hsu
