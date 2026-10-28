// Hybrid Live Preview entry (package-owned; refreshed from the Unity package, do not edit).
import { bridge } from './bridge.js';
import { startViewer } from './viewer.js';
import { mountPanel } from './panel.js';
import { startUnityCanvas } from './webgl.js';

// window.HybridBridge, also importable: import { bridge } from './hlp/index.js'.
export { bridge };

// Starts the Unity stream in `video` and shows the collapsible status panel.
// Returns the live connection state (also exposed as window.__hlp).
// In the final build (Tools > Hybrid Live Preview > Build WebGL) Unity itself runs in a canvas instead.
export function startHybridPreview(video, { panel = true } = {}) {
  if (typeof __HLP_WEBGL_BUILD__ !== 'undefined') {
    const player = startUnityCanvas(video, __HLP_WEBGL_BUILD__);
    window.__hlp = player;
    return player;
  }
  const update = panel ? mountPanel() : null;
  const state = startViewer(video, { onChange: (s) => update?.(s) });
  window.__hlp = state;
  return state;
}
