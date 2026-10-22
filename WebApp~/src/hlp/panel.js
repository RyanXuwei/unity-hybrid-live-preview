// Hybrid Live Preview status panel (package-owned; refreshed from the Unity package, do not edit).
// A small floating box in its own shadow root, so its styles never mix with the page's UI. It can be
// dragged by its header and collapsed to a pill; position and state are remembered per browser.

const STORE = 'hlp.panel';

const CSS = `
:host { all: initial; }
.panel {
  position: fixed;
  z-index: 2147483000;
  min-width: 150px;
  font: 12px/1.4 system-ui, "Segoe UI", "Microsoft JhengHei", sans-serif;
  color: #f3f5f8;
  background: rgba(14, 18, 26, 0.72);
  backdrop-filter: blur(10px);
  border: 1px solid rgba(255, 255, 255, 0.14);
  border-radius: 10px;
  box-shadow: 0 4px 16px rgba(0, 0, 0, 0.25);
  user-select: none;
}
.head {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 5px 6px 5px 10px;
  cursor: grab;
  touch-action: none;
}
.head:active { cursor: grabbing; }
.dot { width: 8px; height: 8px; border-radius: 50%; flex: none; }
.live .dot { background: #2fbf71; }
.wait .dot { background: #e0a030; }
.down .dot { background: #e05252; }
.label { font-weight: 600; white-space: nowrap; }
button {
  all: unset;
  margin-left: auto;
  width: 20px;
  height: 20px;
  border-radius: 6px;
  text-align: center;
  line-height: 20px;
  cursor: pointer;
  color: #c8cfda;
}
button:hover { background: rgba(255, 255, 255, 0.12); }
dl {
  margin: 0;
  padding: 4px 12px 10px;
  display: grid;
  grid-template-columns: auto auto;
  gap: 2px 14px;
  font: 11px/1.45 ui-monospace, Consolas, monospace;
  border-top: 1px solid rgba(255, 255, 255, 0.1);
}
dt { color: #9aa4b2; }
dd { margin: 0; }
.collapsed dl { display: none; }
`;

function load() {
  try {
    return JSON.parse(localStorage.getItem(STORE)) ?? {};
  } catch {
    return {};
  }
}

function save(prefs) {
  try {
    localStorage.setItem(STORE, JSON.stringify(prefs));
  } catch {
    // Storage can be unavailable; the panel just forgets its place.
  }
}

export function mountPanel() {
  const host = document.createElement('hlp-panel');
  const root = host.attachShadow({ mode: 'open' });
  root.innerHTML = `<style>${CSS}</style>
    <div class="panel wait" part="panel">
      <div class="head" title="Drag to move">
        <span class="dot"></span><span class="label">HLP</span>
        <button type="button" title="Expand or collapse"></button>
      </div>
      <dl></dl>
    </div>`;
  document.body.appendChild(host);

  const panel = root.querySelector('.panel');
  const head = root.querySelector('.head');
  const label = root.querySelector('.label');
  const toggle = root.querySelector('button');
  const list = root.querySelector('dl');

  const prefs = { collapsed: true, x: 12, y: 12, ...load() };

  function place() {
    const maxX = Math.max(0, innerWidth - panel.offsetWidth);
    const maxY = Math.max(0, innerHeight - panel.offsetHeight);
    prefs.x = Math.min(Math.max(0, prefs.x), maxX);
    prefs.y = Math.min(Math.max(0, prefs.y), maxY);
    panel.style.left = `${prefs.x}px`;
    panel.style.top = `${prefs.y}px`;
  }

  function setCollapsed(collapsed) {
    prefs.collapsed = collapsed;
    panel.classList.toggle('collapsed', collapsed);
    toggle.textContent = collapsed ? '▸' : '▾';
    place();
    save(prefs);
  }

  toggle.addEventListener('click', () => setCollapsed(!prefs.collapsed));
  toggle.addEventListener('pointerdown', (e) => e.stopPropagation());

  head.addEventListener('pointerdown', (e) => {
    const startX = e.clientX - prefs.x;
    const startY = e.clientY - prefs.y;
    head.setPointerCapture(e.pointerId);
    const move = (ev) => {
      prefs.x = ev.clientX - startX;
      prefs.y = ev.clientY - startY;
      place();
    };
    const up = () => {
      head.removeEventListener('pointermove', move);
      head.removeEventListener('pointerup', up);
      head.removeEventListener('pointercancel', up);
      save(prefs);
    };
    head.addEventListener('pointermove', move);
    head.addEventListener('pointerup', up);
    head.addEventListener('pointercancel', up);
  });
  head.addEventListener('dblclick', () => setCollapsed(!prefs.collapsed));
  window.addEventListener('resize', place);

  setCollapsed(prefs.collapsed);

  return function update(state) {
    const live = state.peer === 'connected' && state.framesDecoded > 0;
    let tone = 'wait';
    let text;
    if (live) {
      tone = 'live';
      text = `LIVE ${state.width}×${state.height}`;
    } else if (state.signaling !== 'open') {
      tone = 'down';
      text = 'Vite offline';
    } else if (!state.unity) {
      text = 'Waiting for Unity Play';
    } else {
      text = `Connecting (${state.peer})`;
    }
    panel.className = `panel ${tone}${prefs.collapsed ? ' collapsed' : ''}`;
    label.textContent = text;

    const rows = {
      signaling: state.signaling,
      unity: state.unity ? 'online' : 'offline',
      peer: state.peer,
      codec: state.codec,
      resolution: state.width ? `${state.width}×${state.height}` : '-',
      fps: state.fps ? state.fps.toFixed(0) : '-',
      frames: state.framesDecoded,
    };
    list.innerHTML = Object.entries(rows)
      .map(([k, v]) => `<dt>${k}</dt><dd>${v}</dd>`)
      .join('');
    place();
  };
}
