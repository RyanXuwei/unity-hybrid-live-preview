// Hybrid Live Preview entry (package-owned; refreshed from the Unity package, do not edit).
import { startViewer } from './viewer.js';
import { mountPanel } from './panel.js';

// Starts the Unity stream in `video` and shows the collapsible status panel.
// Returns the live connection state (also exposed as window.__hlp).
export function startHybridPreview(video, { panel = true } = {}) {
  const update = panel ? mountPanel() : null;
  const state = startViewer(video, { onChange: (s) => update?.(s) });
  window.__hlp = state;
  return state;
}
