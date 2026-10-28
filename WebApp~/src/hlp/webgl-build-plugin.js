// Hybrid Live Preview, final WebGL build (package-owned; refreshed from the Unity package, do not edit).
// Tools > Hybrid Live Preview > Build WebGL writes Library/HybridLivePreview/webgl-build.json after the Unity
// player build. This plugin reads it to build the page into the output folder, tell startHybridPreview
// where the player files are, copy them next to the page, and serve compressed files in `vite preview`.
import fs from 'node:fs';
import path from 'node:path';

const MANIFEST = path.resolve(process.cwd(), '../Library/HybridLivePreview/webgl-build.json');

function readManifest() {
  if (!fs.existsSync(MANIFEST)) {
    throw new Error(`[hlp] ${MANIFEST} not found. Run Tools > Hybrid Live Preview > Build WebGL in Unity.`);
  }
  return JSON.parse(fs.readFileSync(MANIFEST, 'utf8'));
}

export function hybridWebGLBuild() {
  let manifest;
  return {
    name: 'hybrid-live-preview-webgl-build',
    config() {
      manifest = readManifest();
      return {
        base: './',
        build: { outDir: manifest.outDir, emptyOutDir: true },
        define: { __HLP_WEBGL_BUILD__: JSON.stringify(manifest.page) },
      };
    },
    closeBundle() {
      for (const folder of ['Build', 'StreamingAssets']) {
        const from = path.join(manifest.playerDir, folder);
        if (fs.existsSync(from)) fs.cpSync(from, path.join(manifest.outDir, folder), { recursive: true });
      }
    },
    configurePreviewServer(server) {
      server.middlewares.use((req, res, next) => serveCompressed(manifest.outDir, req, res, next));
    },
  };
}

// Unity's Brotli/Gzip files (without Decompression Fallback) need Content-Encoding, as on a real server.
const ENCODINGS = { '.br': 'br', '.gz': 'gzip' };
const TYPES = { '.js': 'application/javascript', '.wasm': 'application/wasm', '.data': 'application/octet-stream' };

function serveCompressed(root, req, res, next) {
  const url = decodeURIComponent((req.url ?? '').split('?')[0]);
  const encoding = ENCODINGS[path.extname(url)];
  if (!encoding) return next();
  const base = path.resolve(root);
  const file = path.resolve(base, `.${url}`);
  if (!file.startsWith(base + path.sep) || !fs.existsSync(file)) return next();
  const inner = path.extname(url.slice(0, -path.extname(url).length));
  res.setHeader('Content-Encoding', encoding);
  res.setHeader('Content-Type', TYPES[inner] ?? 'application/octet-stream');
  fs.createReadStream(file).pipe(res);
}
