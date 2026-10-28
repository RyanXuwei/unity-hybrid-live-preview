// Hybrid Live Preview, final WebGL build (package-owned; refreshed from the Unity package, do not edit).
// Tools > Hybrid Live Preview > Build WebGL writes Library/HybridLivePreview/webgl-build.json after the Unity
// player build. This plugin reads it to build the page into the output folder, tell startHybridPreview
// where the player files are, copy the player template's files next to the page, and serve compressed files in `vite preview`.
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
      mergePlayer(manifest.playerDir, manifest.outDir);
    },
    configurePreviewServer(server) {
      server.middlewares.use((req, res, next) => serveCompressed(manifest.outDir, req, res, next));
    },
  };
}

// Copies everything the Unity WebGL template wrote next to the page: Build/, StreamingAssets/, TemplateData/ and
// any custom files the template ships (environmentSetting.js, stomp.min.js...). Only the template's own
// index.html is skipped, because your page replaces it; files your own build already produced win.
export function mergePlayer(playerDir, outDir) {
  for (const entry of fs.readdirSync(playerDir)) {
    if (entry === 'index.html') continue;
    fs.cpSync(path.join(playerDir, entry), path.join(outDir, entry), { recursive: true, force: false, errorOnExist: false });
  }
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
