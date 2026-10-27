# Changelog

All notable changes to this package are documented here. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the package uses [Semantic Versioning](https://semver.org/).

## [0.6.0] - 2026-10-27

### Changed
- The stream now copies the Game view's final image instead of rendering a second camera, so planar reflections, post effects, camera stacks and screen-space UI look exactly as they do in the Game view. It also saves one camera render per frame. Keep the Game view visible while previewing; a hidden Game view pauses the stream and logs a warning.

### Added
- **Tools > Hybrid Live Preview > Capture** switches between **Game View Output** (default) and **Mirror Camera (Fallback)**, the previous behaviour, which keeps streaming while the Game view is hidden. The choice applies from the next Play.

## [0.5.0] - 2026-10-22

### Changed
- Package ID is now `com.ryanxuwei.hybrid-live-preview`; assemblies and namespaces are `RyanXuWei.HybridLivePreview.*`. Editor preferences start from defaults after upgrading.
- Published under the MIT License.
- Status panel labels are in English.

## [0.4.0] - 2026-10-22

### Changed
- The demo overlay card and test button are gone. Connection status and stream stats live in one draggable, collapsible panel (collapsed by default, position remembered per browser), isolated in a shadow root.
- The web app is split in two: `src/hlp/` is owned by the package and refreshed on every Play; `index.html` (`#app`), `src/main.js`, `src/style.css` and `vite.config.js` belong to your project and are never overwritten.

### Added
- `startHybridPreview(video, { panel: false })` hides the status panel.

## [0.3.0] - 2026-10-22

### Added
- Browser input forwarding: mouse, pen, touch, wheel and keyboard events on the preview travel over a WebRTC data channel and are replayed on virtual Input System devices, the same way a WebGL canvas feeds Unity.
- `Cursor.lockState` in Unity puts the page into Pointer Lock.
- During Play the Game view switches to a fixed resolution equal to the stream, so `Screen`, camera aspect and input coordinates match; it is restored on Stop.

## [0.2.0] - 2026-10-22

### Changed
- The stream follows the browser window's aspect in device pixels, capped at the WebGL Default Canvas Width/Height, and fills the window like the WebGL template canvas. Resizing swaps the capture size without reconnecting.

## [0.1.2] - 2026-10-22

### Fixed
- Chrome is closed gracefully on Stop and no longer shows the "Restore pages?" bubble.

## [0.1.1] - 2026-10-22

### Fixed
- Vite and Chrome PIDs persist in `Library/HybridLivePreview/`, so a restarted Editor still stops them.

## [0.1.0] - 2026-10-21

### Added
- Play starts Vite and Chrome; Stop closes them.
- `PreviewStreamer` streams `Camera.main` over WebRTC (`com.unity.webrtc` 3.0.0) through a mirror camera, leaving the Game view untouched.
- Web template with a Vite signaling relay.
