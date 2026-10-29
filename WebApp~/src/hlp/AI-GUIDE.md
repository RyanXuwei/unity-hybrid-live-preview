# Hybrid Live Preview: guide for AI assistants

Read this before editing a Unity project that uses `com.ryanxuwei.hybrid-live-preview`. The package keeps
this file in `<project>/HybridPreviewWeb/src/hlp/AI-GUIDE.md` and refreshes it on every Play, so it always
matches the installed version.

## What the architecture is

The app is a **hybrid**: Unity renders the 3D view, and an ordinary HTML/CSS/JS page (built with Vite)
draws the UI on top of it. The two halves talk only through **HybridBridge**.

| | Editor (Play) | Final WebGL build |
|---|---|---|
| 3D view in the page | `<video id="stream">` showing the Game view over WebRTC | Unity's canvas, which replaces that video |
| Input to Unity | Browser events forwarded to virtual Input System devices | Native Unity WebGL input |
| HybridBridge transport | WebRTC data channel `bridge` | `HybridBridge.jslib` |
| Started by | Pressing Play (Vite + Chrome start automatically) | **Tools > Hybrid Live Preview > Build WebGL (Unity + Web UI)** |

`startHybridPreview(video)` in `src/main.js` picks the right mode itself. The same page code runs in both.

## Who owns which file

| Path (inside the Unity project) | Owner | Rule |
|---|---|---|
| `HybridPreviewWeb/index.html` | project | Edit freely, but keep `<video id="stream">` and `<div id="app">`. |
| `HybridPreviewWeb/src/main.js` | project | Edit freely, but keep the `startHybridPreview(document.getElementById('stream'))` call. |
| `HybridPreviewWeb/src/**` (except `src/hlp/`) | project | Your UI code, components, styles. |
| `HybridPreviewWeb/public/` | project | Static files (images, fonts), copied as-is. |
| `HybridPreviewWeb/vite.config.js` | project | Must keep `hybridSignaling()` in `plugins`; it may export an object or a function. |
| `HybridPreviewWeb/package.json` | project | Add npm dependencies here (`npm install <pkg>` in `HybridPreviewWeb`). |
| `HybridPreviewWeb/src/hlp/` | **package** | Never edit. Overwritten from the package on every Play and build. |
| `Assets/**` | project | Unity scripts. Use `HybridBridge` to talk to the page. |
| `Packages/com.ryanxuwei.hybrid-live-preview` | **package** | Read-only (installed from git). Change it in the package repo instead. |
| `Library/HybridLivePreview/`, `Builds/HybridWebGL/<version>/` | generated | Logs, Chrome profile, final site. Do not edit or commit. |

Commit `HybridPreviewWeb/` with the project, excluding `node_modules/`.

## Page layout rules

- `#app` covers the 3D view with `pointer-events: none`; its direct children get `pointer-events: auto`.
  Anything you put in `#app` receives clicks; empty areas pass input to Unity.
- Keep UI elements inside `#app` (or give them `pointer-events: auto` explicitly).
- The 3D view only receives keyboard input after it is clicked, so text inputs in the UI work normally.
- Size the UI with CSS relative to the window. The 3D view always fills the window; its resolution is
  the window size in device pixels, capped at Player Settings > WebGL > Default Canvas Width/Height.

## HybridBridge: the only channel between page and Unity

A message is a **type** (string) plus a **JSON payload**. Each side has one global entry point.

Page (global, no import needed):

```js
HybridBridge.send('door:open', { id: 'A1' });            // to Unity
const off = HybridBridge.on('sensor', (data, type) => {   // from Unity
  console.log(data.celsius);
});
off();                                                    // stop listening
HybridBridge.connected;                                   // true once Unity is reachable
```

Unity (namespace `RyanXuWei.HybridLivePreview`, assembly auto-referenced):

```csharp
using System;
using RyanXuWei.HybridLivePreview;
using UnityEngine;

public class DoorController : MonoBehaviour
{
    [Serializable] class OpenDoor { public string id; }
    [Serializable] class Reading { public float celsius; }

    IDisposable subscription;

    void OnEnable()  => subscription = HybridBridge.On<OpenDoor>("door:open", msg => Open(msg.id));
    void OnDisable() => subscription?.Dispose();

    void Open(string id) { /* ... */ }
    void Report(float c) => HybridBridge.Send("sensor", new Reading { celsius = c });
}
```

Full API:

| C# | Page |
|---|---|
| `HybridBridge.Send<T>(type, payload)` (JsonUtility; `[Serializable]` class/struct) | `HybridBridge.send(type, payload)` (any JSON value) |
| `HybridBridge.Send(type)` (no payload, page gets `null`) | |
| `HybridBridge.SendJson(type, json)` (payload already JSON) | |
| `HybridBridge.On<T>(type, handler)` returns `IDisposable` | `HybridBridge.on(type, handler)` returns `off()` |
| `HybridBridge.OnJson(type, handler)` (raw JSON string) | `HybridBridge.off(type, handler)` |
| `HybridBridge.IsConnected` | `HybridBridge.connected` |

Rules that avoid most bugs:

1. **Payloads from C# must be objects.** `JsonUtility` cannot serialize a bare `int`, `string`, array or
   `List<T>` at the top level. Wrap them: `[Serializable] class Items { public List<Item> items; }`.
   Use `SendJson` / `OnJson` for anything else.
2. **Subscribe in `OnEnable`, dispose in `OnDisable`.** Handlers run on the main thread.
3. **Messages queue until the other side connects** (max 256, oldest dropped). A message that arrives
   when nobody listens is dropped (Unity warns once per type).
4. **The page must ask for state when it loads** (e.g. `HybridBridge.send('menu:get')`), and Unity answers
   with the current state. Do not rely on messages Unity sent before the page existed: the page can
   reload at any time, while Unity keeps running.
5. **Name types `area:action`** (`door:open`, `menu:list`) and document each one at the top of the C#
   script that owns it: direction, type, payload shape.
6. **One responsibility per script.** A MonoBehaviour owns one feature's messages; the page module for
   that feature owns the matching UI. Do not build a central "message router" on top of HybridBridge;
   HybridBridge already is the router.

## Recipe: add a feature that spans page and Unity

1. Decide the message types and payload shapes (write them as a comment table in the C# script).
2. Unity: add a MonoBehaviour in `Assets/` that subscribes in `OnEnable` and sends state with
   `HybridBridge.Send`. Put it on a GameObject in the scene (and save the scene).
3. Page: add a module under `HybridPreviewWeb/src/` (for example `src/features/door.js` plus CSS) that
   builds its DOM inside `#app`, calls `HybridBridge.on(...)`, then `HybridBridge.send('<area>:get')`.
4. Import and start the module from `src/main.js`.
5. Press Play. Vite hot-reloads page edits instantly; C# edits need Unity to recompile and Play again.
6. When it works, run **Build WebGL** and check the result with **Preview WebGL Build**.

A working example in this style: an activation menu that toggles the children of a GameObject
(`ActivationMenuBridge.cs` with `activation:get` / `activation:set` / `activation:list`, and
`src/demo/activation-menu.js`).

## Unity-side requirements

- Read input through the **Input System** (`Mouse.current`, `Keyboard.current`, Input Actions,
  `InputSystemUIInputModule`). The legacy `UnityEngine.Input` API does not receive browser input in the
  Editor preview. Player Settings > Active Input Handling must be *Input System Package* or *Both*.
- The preview streams the **Game view**. Keep it visible during Play (a hidden Game view pauses the
  stream). **Tools > Hybrid Live Preview > Capture > Mirror Camera** is a fallback that renders
  `Camera.main` separately.
- The Game view toolbar Play Mode must be *Play Focused*; *Play Unfocused* breaks keyboard input. The
  preview sets `Application.runInBackground = true` during Play and restores it afterwards, so the game
  keeps running while Chrome has focus.
- Do not reference `UnityEditor` from runtime scripts.

## Building and shipping

- **Build WebGL (Unity + Web UI)**: builds the Unity player (scenes from the build profile, or the open
  scene), runs `vite build` with `src/hlp/vite.build.config.js` (your `vite.config.js` merged in), and
  writes the deployable site to `<project>/Builds/HybridWebGL/<version>/` (the Player Settings version).
- **Preview WebGL Build**: serves that folder at `http://127.0.0.1:4173/`.
- With Brotli/Gzip compression and *Decompression Fallback* off, the web server must send
  `Content-Encoding` for `Build/*.br` / `*.gz`.
- The WebGL template chosen in Player Settings still builds, and everything it produced is copied next to
  the page (`Build/`, `StreamingAssets/`, `TemplateData/`, custom files such as `environmentSetting.js`),
  except its `index.html`.
- **Tools > Hybrid Live Preview > Web Build Page** picks how the final `index.html` is made. *Inject Into
  WebGL Template* (default) keeps the template's `index.html` and adds your scripts and styles to `<head>` and
  your `#app` layer at the end of `<body>` (optional markers `<!-- HLP:head -->` / `<!-- HLP:body -->` in the
  template choose the spot). *Replace WebGL Template Page* makes `HybridPreviewWeb/index.html` the page, so
  any setup the template did must be copied there. Your UI code is the same in both.

## Troubleshooting

| Symptom | Look at |
|---|---|
| Panel says "Vite offline" | `Library/HybridLivePreview/vite.log`; Node.js/npm on PATH; port 5173 free. |
| Page loads, Unity never answers | Is Unity in Play? Console for `[HybridLivePreview]` and `[HybridBridge]` lines. |
| `[HybridBridge] No handler for "x"` | The C# subscriber is missing, disabled, or subscribed after the message arrived. |
| C# handler receives empty object | Payload shape does not match the `[Serializable]` class fields (names are case-sensitive). |
| Input does nothing in the preview | Script uses legacy `Input`; or the 3D view was not clicked first. |
| Build fails at `vite build` | `Library/HybridLivePreview/vite-build.log`. |
| Built page shows nothing | Check the browser console; compressed builds need `Content-Encoding` headers. |
