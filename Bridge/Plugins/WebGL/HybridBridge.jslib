// Hybrid Live Preview bridge for WebGL builds: creates window.HybridBridge (the same object the
// Editor preview's src/hlp/bridge.js creates) and wires it to the C# HybridBridge.
var HybridBridgePlugin = {
  $HlpBridge: {
    get: function () {
      if (window.HybridBridge) return window.HybridBridge;
      var MAX_QUEUED = 256;
      var handlers = {};
      var outbox = [];
      var transport = null;
      var bridge = {
        send: function (type, payload) {
          if (!type) throw new Error('[HybridBridge] A message needs a type.');
          var json = JSON.stringify(payload === undefined ? null : payload);
          if (transport) transport.post(type, json);
          else {
            if (outbox.length === MAX_QUEUED) outbox.shift();
            outbox.push([type, json]);
          }
        },
        on: function (type, handler) {
          (handlers[type] = handlers[type] || []).push(handler);
          return function () { bridge.off(type, handler); };
        },
        off: function (type, handler) {
          var list = handlers[type];
          var index = list ? list.indexOf(handler) : -1;
          if (index >= 0) list.splice(index, 1);
        }
      };
      Object.defineProperty(bridge, 'connected', { get: function () { return transport !== null; } });
      Object.defineProperties(bridge, {
        _attach: {
          value: function (next) {
            transport = next;
            var queued = outbox;
            outbox = [];
            for (var i = 0; i < queued.length; i++) next.post(queued[i][0], queued[i][1]);
          }
        },
        _detach: {
          value: function (previous) {
            if (transport === previous) transport = null;
          }
        },
        _deliver: {
          value: function (type, json) {
            var payload = null;
            try { payload = JSON.parse(json); } catch (e) { payload = json; }
            var list = (handlers[type] || []).slice();
            for (var i = 0; i < list.length; i++) {
              try { list[i](payload, type); }
              catch (e) { console.error('[HybridBridge] handler for "' + type + '" failed', e); }
            }
          }
        }
      });
      window.HybridBridge = bridge;
      return bridge;
    }
  },

  HlpBridgeConnect__deps: ['$HlpBridge'],
  HlpBridgeConnect: function (receive) {
    HlpBridge.get()._attach({
      post: function (type, json) {
        var typePtr = stringToNewUTF8(type);
        var jsonPtr = stringToNewUTF8(json);
        try {
          {{{ makeDynCall('vii', 'receive') }}}(typePtr, jsonPtr);
        } finally {
          _free(typePtr);
          _free(jsonPtr);
        }
      }
    });
  },

  HlpBridgePost__deps: ['$HlpBridge'],
  HlpBridgePost: function (type, json) {
    HlpBridge.get()._deliver(UTF8ToString(type), UTF8ToString(json));
  }
};

mergeInto(LibraryManager.library, HybridBridgePlugin);
