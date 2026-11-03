// Hybrid Live Preview: parent-page side of the iframe relay (package-owned; refreshed from the Unity package).
// This file is NOT used by your Unity page. Copy it into the site that embeds the Unity page with <iframe>.
// It has no dependencies; load it as a module.
//
//   <iframe id="game" src="game/index.html" allow="fullscreen; autoplay"></iframe>
//   <script type="module">
//     import { connectHybridBridge } from './hybrid-bridge-host.js';
//     const unity = connectHybridBridge(document.getElementById('game'));   // same origin (default)
//     // other origin: connectHybridBridge(iframe, { origin: 'https://game.example.com' })
//     unity.send('door', { id: 'A1', open: true });            // C#: HybridBridge.On<Door>("door", ...)
//     const off = unity.on('sensor', (data) => { ... });        // C#: HybridBridge.Send("sensor", reading)
//   </script>
//
// The embedded page must call enableParentRelay() (src/hlp/embed.js); for another origin it lists this site's.
// Messages sent before the relay is ready are queued (up to 256) and delivered once it is.

const PROTOCOL = 'hlp-bridge';
const MAX_QUEUED = 256;

export function connectHybridBridge(iframe, { origin = location.origin } = {}) {
  if (!iframe || !iframe.contentWindow) throw new Error('[HybridBridge host] Pass the <iframe> element.');
  if (!origin || origin === '*')
    throw new Error("[HybridBridge host] Pass the embedded page's origin, e.g. { origin: 'https://game.example.com' }.");

  const handlers = new Map();
  let outbox = [];
  let ready = false;

  const target = () => iframe.contentWindow;
  const post = (message) => target()?.postMessage({ hlp: PROTOCOL, v: 1, ...message }, origin);

  // Handshake: the parent says hello (now and on every iframe load); the page answers ready with ack.
  // A page that starts later announces ready without ack, and the parent answers hello.
  // (Before the iframe has loaded, Chrome may log one "target origin does not match" line; it is harmless.)
  function hello() {
    ready = false;
    post({ kind: 'hello' });
  }

  function deliver(type, json) {
    let payload = null;
    try {
      payload = JSON.parse(json);
    } catch {
      payload = json;
    }
    const list = [...(handlers.get(type) ?? []), ...(handlers.get('*') ?? [])];
    for (const handler of list) {
      try {
        handler(payload, type);
      } catch (e) {
        console.error(`[HybridBridge host] handler for "${type}" failed`, e);
      }
    }
  }

  function onMessage(event) {
    const data = event.data;
    if (event.source !== target() || event.origin !== origin || !data || data.hlp !== PROTOCOL) return;
    if (data.kind === 'ready') {
      if (!data.ack) {
        hello();
        return;
      }
      ready = true;
      const queued = outbox;
      outbox = [];
      for (const message of queued) post(message);
    } else if (data.kind === 'msg' && typeof data.type === 'string') {
      deliver(data.type, data.json);
    }
  }

  window.addEventListener('message', onMessage);
  iframe.addEventListener('load', hello);
  hello();

  const bridge = {
    send(type, payload) {
      if (!type) throw new Error('[HybridBridge host] A message needs a type.');
      const message = { kind: 'msg', type, json: JSON.stringify(payload === undefined ? null : payload) };
      if (ready) post(message);
      else {
        if (outbox.length === MAX_QUEUED) outbox.shift();
        outbox.push(message);
      }
    },
    on(type, handler) {
      if (!handlers.has(type)) handlers.set(type, []);
      handlers.get(type).push(handler);
      return () => bridge.off(type, handler);
    },
    off(type, handler) {
      const list = handlers.get(type);
      const index = list ? list.indexOf(handler) : -1;
      if (index >= 0) list.splice(index, 1);
    },
    get connected() {
      return ready;
    },
    disconnect() {
      window.removeEventListener('message', onMessage);
      iframe.removeEventListener('load', hello);
      ready = false;
      outbox = [];
    },
  };
  return bridge;
}
