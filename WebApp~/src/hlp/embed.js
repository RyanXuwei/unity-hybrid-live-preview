// Hybrid Live Preview iframe relay (package-owned; refreshed from the Unity package, do not edit).
// When this page (Unity + your UI) is embedded in another site with <iframe>, enableParentRelay() lets
// that parent page talk to Unity through postMessage, using the same send/on/off API as HybridBridge.
// The parent side is hybrid-bridge-host.js (copy it into the parent site).
//
//   import { enableParentRelay } from './hlp/index.js';
//   enableParentRelay();                                          // parent on the same origin (default)
//   enableParentRelay({ origins: ['https://www.example.com'] });  // or list the parent site's origin(s)
//
// Unity messages are forwarded to the parent (all of them, or only `types`); messages from the parent go
// to Unity (all of them, or only `toUnity`). Only the listed origins are accepted and posted to.
// Works in the Play preview and the build. Does nothing when the page is not inside an iframe.

import { bridge } from './bridge.js';

export const RELAY_PROTOCOL = 'hlp-bridge';
const MAX_QUEUED = 256;

export function enableParentRelay({ origins = [location.origin], types = null, toUnity = null } = {}) {
  if (window.parent === window) return { embedded: false, disable() {} };
  if (!Array.isArray(origins) || origins.length === 0)
    throw new Error("[HybridBridge] enableParentRelay needs origins, e.g. ['https://www.example.com'].");
  const anyOrigin = origins.includes('*');
  if (anyOrigin)
    console.warn("[HybridBridge] enableParentRelay origins '*' accepts messages from any site. Use it only for testing.");

  const allowed = (origin) => anyOrigin || origins.includes(origin);
  // The browser's own record of who embeds us (Chrome, Edge, Safari). An unlisted parent gets nothing.
  const parentSeen = embeddingOrigin();
  if (parentSeen && !allowed(parentSeen)) {
    console.warn(`[HybridBridge] Parent relay is off: this page is embedded by ${parentSeen}, which is not in origins.`);
    return { embedded: true, connected: false, disable() {} };
  }
  const passes = (list, type) => !list || list.includes(type);
  let parentOrigin = null; // learned from the parent's hello
  let outbox = [];

  function post(message) {
    window.parent.postMessage({ hlp: RELAY_PROTOCOL, v: 1, ...message }, parentOrigin);
  }

  // ack: true answers the parent's hello; without it, it asks a parent that is already waiting for one.
  function announceReady(target, ack) {
    window.parent.postMessage({ hlp: RELAY_PROTOCOL, v: 1, kind: 'ready', ack }, target);
  }

  // Unity -> parent. Queued until the parent has said hello, so nothing goes to an unchecked origin.
  const off = bridge.on('*', (payload, type) => {
    if (!passes(types, type)) return;
    const json = JSON.stringify(payload === undefined ? null : payload);
    if (parentOrigin) post({ kind: 'msg', type, json });
    else {
      if (outbox.length === MAX_QUEUED) outbox.shift();
      outbox.push({ kind: 'msg', type, json });
    }
  });

  // Parent -> Unity.
  function onMessage(event) {
    const data = event.data;
    if (event.source !== window.parent || !data || data.hlp !== RELAY_PROTOCOL) return;
    if (!allowed(event.origin)) {
      console.warn(`[HybridBridge] Ignored a relay message from ${event.origin} (not in origins).`);
      return;
    }
    if (data.kind === 'hello') {
      parentOrigin = event.origin;
      announceReady(parentOrigin, true);
      const queued = outbox;
      outbox = [];
      for (const message of queued) post(message);
    } else if (data.kind === 'msg' && typeof data.type === 'string' && data.type) {
      if (!passes(toUnity, data.type)) {
        console.warn(`[HybridBridge] Ignored "${data.type}" from the parent page (not in toUnity).`);
        return;
      }
      let payload = null;
      try {
        payload = JSON.parse(data.json);
      } catch {
        payload = null;
      }
      bridge.send(data.type, payload);
    }
  }
  window.addEventListener('message', onMessage);

  // Tell an already-waiting parent we are here; it answers with hello. postMessage drops it for any
  // origin that is not the parent's, so the parent's address never has to be guessed. The message
  // carries no data, so with '*' it may go to any origin.
  for (const origin of anyOrigin ? ['*'] : origins) announceReady(origin, false);

  return {
    embedded: true,
    get connected() {
      return parentOrigin !== null;
    },
    disable() {
      off();
      window.removeEventListener('message', onMessage);
      parentOrigin = null;
      outbox = [];
    },
  };
}

// The origin of the page that embeds this one, or null when it is not embedded or the browser does not say
// (Firefox has no location.ancestorOrigins).
function embeddingOrigin() {
  if (window.parent === window) return null;
  const ancestors = location.ancestorOrigins;
  return ancestors && ancestors.length > 0 ? ancestors[0] : null;
}

// Client-side guard against other sites embedding this page (clickjacking). Call it first in main.js:
//   guardEmbedding();                                     // only same-origin parents may embed this page
//   guardEmbedding({ origins: ['https://www.example.com'] });
// It blanks the page when an unlisted site embeds it. Browsers without ancestorOrigins (Firefox) are not
// covered, so also send the HTTP header Content-Security-Policy: frame-ancestors 'self' (see README).
export function guardEmbedding({ origins = [location.origin] } = {}) {
  if (window.parent === window) return true;
  const parent = embeddingOrigin();
  const ancestors = location.ancestorOrigins ? [...location.ancestorOrigins] : [];
  if (ancestors.every((origin) => origins.includes(origin))) return true;
  console.error(`[HybridBridge] This page may not be embedded by ${parent}. Stopped.`);
  document.documentElement.innerHTML = '';
  window.stop();
  throw new Error('[HybridBridge] Embedded by an unlisted site.');
}
