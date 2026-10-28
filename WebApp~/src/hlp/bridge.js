// Hybrid Live Preview bridge (package-owned; refreshed from the Unity package, do not edit).
// window.HybridBridge is the page's single channel to Unity; its Unity-side twin is the C# class
// HybridBridge. A message is a type name plus a JSON-serializable payload. HybridBridge.jslib creates
// the same object in WebGL builds, so UI code runs unchanged in the preview and in a build.
//
//   HybridBridge.send('door', { id: 'A1', open: true });
//   const off = HybridBridge.on('sensor', (data) => { ... });  // off() stops listening
//
// Messages sent before Unity is connected are queued (up to 256) and delivered once it is.

export function createHybridBridge() {
  const MAX_QUEUED = 256;
  const handlers = new Map();
  let outbox = [];
  let transport = null;

  const bridge = {
    send(type, payload) {
      if (!type) throw new Error('[HybridBridge] A message needs a type.');
      const json = JSON.stringify(payload === undefined ? null : payload);
      if (transport) transport.post(type, json);
      else {
        if (outbox.length === MAX_QUEUED) outbox.shift();
        outbox.push([type, json]);
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
      return transport !== null;
    },
  };

  // Transport hooks, hidden from enumeration: the page only needs send/on/off.
  Object.defineProperties(bridge, {
    _attach: {
      value(next) {
        transport = next;
        const queued = outbox;
        outbox = [];
        for (const [type, json] of queued) next.post(type, json);
      },
    },
    _detach: {
      value(previous) {
        if (transport === previous) transport = null;
      },
    },
    _deliver: {
      value(type, json) {
        let payload = null;
        try {
          payload = JSON.parse(json);
        } catch {
          payload = json;
        }
        for (const handler of [...(handlers.get(type) ?? [])]) {
          try {
            handler(payload, type);
          } catch (e) {
            console.error(`[HybridBridge] handler for "${type}" failed`, e);
          }
        }
      },
    },
  });
  return bridge;
}

export const bridge = (window.HybridBridge ??= createHybridBridge());
