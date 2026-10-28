# Working on this package (for AI assistants)

This repository is the Unity package `com.ryanxuwei.hybrid-live-preview`. Two guides exist:

- **Using the package in a game project** (building an H5 UI, HybridBridge messages, building WebGL):
  [`WebApp~/src/hlp/AI-GUIDE.md`](WebApp~/src/hlp/AI-GUIDE.md). It is copied into every project as
  `HybridPreviewWeb/src/hlp/AI-GUIDE.md`.
- **Changing the package itself**: this file.

## Layout

| Path | Assembly / role | Compiled for |
|---|---|---|
| `Editor/` | `RyanXuWei.HybridLivePreview.Editor`: Play/Stop lifecycle (`HlpPlayModeHook`), Vite/Chrome processes (`HlpProcesses`), settings (`HlpSettings`, EditorPrefs prefix `RyanXuWei.HybridLivePreview.`), Game view sizing (`HlpGameView`), web template install/sync (`HlpWebApp`), one-click build (`HlpWebGLBuild`), menu (`HybridLivePreviewMenu`) | Editor |
| `Streaming/` | `RyanXuWei.HybridLivePreview.Streaming`: `PreviewStreamer` (signaling, WebRTC peers, Game view capture or mirror camera, data channels `input` and `bridge`), `PreviewBridgeTransport` | Editor only (`UNITY_EDITOR` define constraint) |
| `Input/` | `RyanXuWei.HybridLivePreview.Input`: `RemoteInput` replays browser input on virtual Input System devices; `WebKeyMap` | Editor only (`UNITY_EDITOR` + `ENABLE_INPUT_SYSTEM`) |
| `Bridge/` | `RyanXuWei.HybridLivePreview.Bridge`: public `HybridBridge` API, `IHybridBridgeTransport`, WebGL transport + `Plugins/WebGL/HybridBridge.jslib` | All platforms, auto-referenced (ships in players) |
| `WebGL/` | `RyanXuWei.HybridLivePreview.WebGL`: keyboard sharing for pages made by Build WebGL + `Plugins/HybridPage.jslib` | WebGL players only |
| `WebApp~/` | Vite template copied to `<project>/HybridPreviewWeb` (Unity ignores `~` folders). `src/hlp/` is package-owned and re-synced on every Play/build; everything else becomes the project's | n/a |
| `Documentation~/images/` | SVG diagrams used by the READMEs | n/a |

## How the pieces connect

1. **Play**: `HlpPlayModeHook` installs/syncs the web app, starts Vite (port 5173, `src/hlp/vite-plugin.js`
   adds the `/signaling` WebSocket relay), spawns `PreviewStreamer`, then opens Chrome.
2. **Video**: the page (`src/hlp/viewer.js`) sends an offer with its viewport size; `PreviewStreamer`
   answers, sizes the track (`FitViewport`, capped at the WebGL Default Canvas size), and the Editor locks
   the Game view to that size. Default capture copies the Game view each frame
   (`ScreenCapture.CaptureScreenshotIntoRenderTexture`); the fallback renders a mirror of `Camera.main`.
3. **Input**: data channel `input`, JSON messages `pd/pm/pu/pc/wh/kd/ku/blur`, handled by `RemoteInput`.
4. **Bridge**: data channel `bridge`, envelopes `{type, data}` where `data` is a JSON string. JS side
   `src/hlp/bridge.js` (global `window.HybridBridge`), C# side `HybridBridge` + `PreviewBridgeTransport`.
   In WebGL builds `HybridBridge.jslib` reuses an existing `window.HybridBridge` or creates an identical one.
   Keep `bridge.js` and the jslib core behaviourally identical.
5. **Build WebGL**: `HlpWebGLBuild` builds the player into `Builds/HybridWebGL-UnityPlayer` (Unity refuses `Library/`), writes
   `Library/HybridLivePreview/webgl-build.json`, runs `vite build --config src/hlp/vite.build.config.js`;
   `webgl-build-plugin.js` defines `__HLP_WEBGL_BUILD__` and copies `Build/` + `StreamingAssets/` into the
   output. In that bundle `startHybridPreview` calls `startUnityCanvas` (`src/hlp/webgl.js`).

## Rules

- Never put `UnityEditor` code in `Bridge/` or `WebGL/`; they compile into players.
- One responsibility per class: the public API (`HybridBridge`) never knows its transports; transports
  never contain message logic.
- Every new file and folder needs a committed `.meta` (git packages are read-only, so Unity cannot create
  them in a user's project). Easiest: install the working copy with `file:<path>` in a test project,
  let Unity generate the metas, commit them.
- Nothing may depend on a specific project: derive paths from `HlpWebApp.ProjectRoot`, read names and
  sizes from `PlayerSettings`, detect build file names instead of assuming them.
- Never overwrite project-owned web files (`index.html`, `src/main.js`, `src/style.css`, `vite.config.js`,
  anything outside `src/hlp/`). Change behaviour through `src/hlp/` instead.
- Code comments are short, in English, and explain why. Match the surrounding style.
- For each release: bump `package.json`, add a `CHANGELOG.md` entry (Keep a Changelog), update both
  `README.md` and `README.zh-TW.md`, and update `WebApp~/src/hlp/AI-GUIDE.md` when usage changes.
- Commits and tags are authored as `Ryan Hsu <865a002@gmail.com>`; tags are `vX.Y.Z`.

## Testing a change

1. In a test project, point `Packages/manifest.json` at the working copy (`file:` path) and let it compile
   with no errors or warnings from this package.
2. Press Play: Chrome shows LIVE, input reaches Unity, `HybridBridge` messages flow both ways.
3. Switch Capture to Mirror Camera and Play again.
4. Run Build WebGL, then Preview WebGL Build; the page must look and behave like the Play preview.
5. Switch the manifest back to the git URL before committing the test project.
