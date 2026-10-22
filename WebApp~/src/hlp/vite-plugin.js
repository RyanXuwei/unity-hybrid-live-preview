// Hybrid Live Preview signaling (package-owned; refreshed from the Unity package, do not edit).
import { WebSocketServer } from 'ws';

// Relays WebRTC signaling between Unity (role "unity") and browser tabs (role "viewer") on /signaling.
// Vite's own HMR socket only claims upgrades carrying its "vite-hmr" protocol, so both share the port.
export function hybridSignaling() {
  return {
    name: 'hybrid-live-preview-signaling',
    configureServer(server) {
      const wss = new WebSocketServer({ noServer: true });
      const viewers = new Map();
      let unity = null;
      let nextId = 1;

      const log = (text) => server.config.logger.info(`[signaling] ${text}`, { timestamp: true });
      const send = (ws, msg) => {
        if (ws && ws.readyState === ws.OPEN) ws.send(JSON.stringify(msg));
      };
      const broadcastStatus = () => {
        for (const viewer of viewers.values()) send(viewer, { type: 'status', unity: !!unity });
      };

      server.httpServer?.on('upgrade', (req, socket, head) => {
        if (!req.url?.startsWith('/signaling')) return;
        wss.handleUpgrade(req, socket, head, (ws) => wss.emit('connection', ws, req));
      });

      wss.on('connection', (ws) => {
        let role = null;
        let id = null;

        ws.on('message', (raw) => {
          let msg;
          try {
            msg = JSON.parse(raw.toString());
          } catch {
            return;
          }

          if (msg.type === 'hello') {
            role = msg.role === 'unity' ? 'unity' : 'viewer';
            if (role === 'unity') {
              if (unity && unity !== ws) unity.close();
              unity = ws;
              log('Unity connected');
              broadcastStatus();
            } else {
              id = `v${nextId++}`;
              viewers.set(id, ws);
              log(`viewer ${id} connected`);
              send(ws, { type: 'welcome', id, unity: !!unity });
            }
            return;
          }

          if (role === 'viewer' && ['offer', 'candidate', 'bye', 'viewport'].includes(msg.type)) {
            send(unity, { ...msg, id });
          } else if (role === 'unity' && ['answer', 'candidate'].includes(msg.type)) {
            send(viewers.get(msg.id), msg);
          }
        });

        ws.on('close', () => {
          if (role === 'unity' && unity === ws) {
            unity = null;
            log('Unity disconnected');
            broadcastStatus();
          } else if (role === 'viewer') {
            viewers.delete(id);
            log(`viewer ${id} disconnected`);
            send(unity, { type: 'bye', id });
          }
        });
      });
    },
  };
}
