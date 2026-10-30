// Accessibility layer (Hybrid Live Preview sample). From the a11y:tree Unity sends, lays transparent real DOM
// elements (button, role=img, text) over the game for screen readers such as NVDA; pressing a button sends
// a11y:click back to Unity. Subtitles arrive as a11y:subtitle.
//
// Keyboard:
// - Game mode (default): focus is on the game view and every key goes to Unity. The game view is marked
//   role=application, so NVDA switches to focus mode and lets the keys through.
// - Browse mode: Tab moves into the accessible buttons. Tab, Shift+Tab, Enter and Space stay on the page,
//   other keys still go to Unity; Esc returns to game mode.
import './a11y.css';

const NAV_KEYS = new Set(['Tab', 'Enter', 'NumpadEnter', 'Space']);

const STRINGS = {
  en: {
    layer: 'Game interface',
    game: 'Game view. Press Tab to browse the interface buttons, Esc to return to the game.',
    skip: 'Back to the game (Esc)',
    descriptionSeparator: ', ',
    speakerSeparator: ': ',
  },
  zh: {
    layer: '遊戲介面',
    game: '遊戲畫面。按 Tab 瀏覽介面按鈕，按 Esc 回到遊戲。',
    skip: '回到遊戲畫面（Esc）',
    descriptionSeparator: '，',
    speakerSeparator: '：',
  },
};

// lang: language of the layer. Screen readers pick the voice from it, so a page marked lang="en" with Chinese
// UI text gets skipped or misread; pass the language your UI text is in (e.g. 'zh-TW').
// strings: overrides for the built-in texts above (English, or Chinese when lang starts with "zh").
export function startA11y({
  bridge = window.HybridBridge,
  stage = document.querySelector('.stage') ?? document.body,
  lang = document.documentElement.lang || 'en',
  strings = {},
} = {}) {
  const text = { ...(lang.toLowerCase().startsWith('zh') ? STRINGS.zh : STRINGS.en), ...strings };

  // The preview shows the #stream video; a WebGL build shows Unity's canvas.
  const game = () => document.getElementById('stream') ?? document.querySelector('canvas');

  const layer = document.createElement('div');
  layer.className = 'a11y-layer';
  layer.setAttribute('aria-label', text.layer);
  layer.setAttribute('role', 'group');

  const subtitle = document.createElement('div');
  subtitle.className = 'a11y-subtitle';
  subtitle.hidden = true;
  const politeLive = liveRegion('polite');
  const assertiveLive = liveRegion('assertive');

  const skip = document.createElement('button');
  skip.type = 'button';
  skip.className = 'a11y-skip';
  skip.textContent = text.skip;
  skip.addEventListener('click', backToGame);

  // appendChild, not append: the WebGL template's loading-bar.js replaces Element.prototype.append globally
  // (it only accepts SVG tag names).
  for (const el of [skip, layer, subtitle, politeLive, assertiveLive]) stage.appendChild(el);
  for (const el of [skip, layer, subtitle, politeLive, assertiveLive]) el.lang = lang;

  // The game view itself: announced as an application, so the reader passes keys through to the game.
  const markGame = () => {
    const el = game();
    if (!el || el.dataset.a11y) return;
    el.dataset.a11y = '1';
    el.setAttribute('role', 'application');
    el.setAttribute('aria-label', text.game);
    el.lang = lang;
    el.tabIndex = 0;
  };
  markGame();
  new MutationObserver(markGame).observe(stage, { childList: true });

  const elements = new Map();

  bridge.on('a11y:tree', (tree) => render(tree?.nodes ?? []));
  bridge.on('a11y:subtitle', showSubtitle);
  bridge.send('a11y:ready');

  function render(nodes) {
    const seen = new Set();
    let previous = null;
    for (const node of nodes) {
      seen.add(node.id);
      let el = elements.get(node.id);
      if (!el || el.dataset.role !== node.role) {
        el?.remove();
        el = create(node);
        elements.set(node.id, el);
      }
      update(el, node);
      // DOM order = Unity Hierarchy order, so the Tab order is right.
      const expected = previous ? previous.nextSibling : layer.firstChild;
      if (expected !== el) layer.insertBefore(el, expected);
      previous = el;
    }
    for (const [id, el] of elements) {
      if (seen.has(id)) continue;
      if (el === document.activeElement) game()?.focus({ preventScroll: true });
      el.remove();
      elements.delete(id);
    }
  }

  function create(node) {
    let el;
    if (node.role === 'button') {
      el = document.createElement('button');
      el.type = 'button';
      el.addEventListener('click', () => bridge.send('a11y:click', { id: node.id }));
      el.addEventListener('focus', () => bridge.send('a11y:focus', { id: node.id }));
    } else {
      el = document.createElement('div');
      if (node.role === 'image') el.setAttribute('role', 'img');
    }
    el.className = `a11y-node a11y-${node.role}`;
    el.dataset.role = node.role;
    return el;
  }

  function update(el, node) {
    const label = node.description ? `${node.label}${text.descriptionSeparator}${node.description}` : node.label;
    if (node.role === 'text') {
      if (el.textContent !== label) el.textContent = label;
    } else if (el.getAttribute('aria-label') !== label) {
      el.setAttribute('aria-label', label);
    }
    if (node.role === 'button') el.disabled = !!node.disabled;
    // Images and text with an A11yLabel can be reached with Tab too (A11yLabel.tabStop).
    else if (node.focusable) el.tabIndex = 0;
    else el.removeAttribute('tabindex');
    el.style.left = `${node.x * 100}%`;
    el.style.top = `${node.y * 100}%`;
    el.style.width = `${node.w * 100}%`;
    el.style.height = `${node.h * 100}%`;
  }

  let subtitleTimer = 0;
  function showSubtitle(line) {
    clearTimeout(subtitleTimer);
    const said = line?.text ?? '';
    if (!said) {
      subtitle.hidden = true;
      return;
    }
    subtitle.textContent = '';
    if (line.speaker) {
      const who = document.createElement('span');
      who.className = 'a11y-speaker';
      who.textContent = `${line.speaker}${text.speakerSeparator}`;
      subtitle.appendChild(who);
    }
    subtitle.appendChild(document.createTextNode(said));
    subtitle.hidden = false;
    const spoken = line.speaker ? `${line.speaker}${text.speakerSeparator}${said}` : said;
    announce(line.urgent ? assertiveLive : politeLive, spoken);
    if (line.seconds > 0) subtitleTimer = setTimeout(() => (subtitle.hidden = true), line.seconds * 1000);
  }

  // In browse mode, keys other than the navigation keys still go to the game: they are re-dispatched on the
  // game view element, which the preview and the build each handle with their own keyboard code.
  for (const type of ['keydown', 'keyup']) {
    layer.addEventListener(type, (e) => {
      if (e.key === 'Escape') {
        if (type === 'keydown') backToGame();
        e.preventDefault();
        return;
      }
      if (NAV_KEYS.has(e.code) || e.ctrlKey || e.altKey || e.metaKey) return;
      const el = game();
      if (!el) return;
      el.dispatchEvent(new KeyboardEvent(type, { key: e.key, code: e.code, shiftKey: e.shiftKey, bubbles: true }));
      e.preventDefault();
    });
  }

  // Tab, Enter and Space are handled here instead of relying on the browser: in a WebGL build Unity catches
  // these keys at page level (preventDefault), so focus would stick on the first element and buttons would not
  // press. Handle them in the capture phase and stop them going further.
  document.addEventListener(
    'keydown',
    (e) => {
      const active = document.activeElement;
      const inLayer = active === skip || layer.contains(active);
      if (e.key === 'Tab' && (active === game() || inLayer)) {
        const focusable = [...layer.querySelectorAll('button:not(:disabled), [tabindex="0"]')];
        if (!focusable.length) return;
        let next;
        if (active === game()) next = e.shiftKey ? focusable[focusable.length - 1] : focusable[0];
        else {
          const index = focusable.indexOf(active);
          next = focusable[index + (e.shiftKey ? -1 : 1)] ?? game();
        }
        e.stopImmediatePropagation();
        e.preventDefault();
        next?.focus({ preventScroll: true });
      } else if ((e.key === 'Enter' || e.key === ' ') && inLayer && active instanceof HTMLButtonElement) {
        e.stopImmediatePropagation();
        e.preventDefault();
        if (!e.repeat) active.click();
      }
    },
    true,
  );

  function backToGame() {
    game()?.focus({ preventScroll: true });
  }
}

function liveRegion(politeness) {
  const el = document.createElement('div');
  el.className = 'a11y-sr-only';
  el.setAttribute('aria-live', politeness);
  el.setAttribute('aria-atomic', 'true');
  return el;
}

// Cleared first so the reader repeats a line that comes twice in a row.
function announce(region, text) {
  region.textContent = '';
  setTimeout(() => (region.textContent = text), 50);
}
