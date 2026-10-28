// Hybrid Live Preview, final WebGL build (package-owned; refreshed from the Unity package, do not edit).
// Tools > Hybrid Live Preview > Build WebGL writes Library/HybridLivePreview/webgl-build.json after the Unity
// player build. This plugin reads it to build the page into the output folder, tell startHybridPreview
// where the player files are, copy the player template's files next to the page, merge the page into the
// template's index.html (or replace it), and serve compressed files in `vite preview`.
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
        define: { __HLP_WEBGL_BUILD__: JSON.stringify({ ...manifest.page, mode: manifest.mode ?? 'template' }) },
      };
    },
    closeBundle() {
      const index = path.join(manifest.outDir, 'index.html');
      // In template mode the player's index.html becomes the page and receives yours; read both first.
      const merged =
        manifest.mode === 'replace'
          ? null
          : injectIntoTemplate(fs.readFileSync(path.join(manifest.playerDir, 'index.html'), 'utf8'), fs.readFileSync(index, 'utf8'));
      mergePlayer(manifest.playerDir, manifest.outDir);
      if (merged !== null) fs.writeFileSync(index, merged);
    },
    configurePreviewServer(server) {
      server.middlewares.use((req, res, next) => serveCompressed(manifest.outDir, req, res, next));
    },
  };
}

// Copies everything the Unity WebGL template wrote next to the page: Build/, StreamingAssets/, TemplateData/ and
// any custom files the template ships (environmentSetting.js, stomp.min.js...). The template's own
// index.html is skipped here (see closeBundle); files your own build already produced win.
export function mergePlayer(playerDir, outDir) {
  for (const entry of fs.readdirSync(playerDir)) {
    if (entry === 'index.html') continue;
    fs.cpSync(path.join(playerDir, entry), path.join(outDir, entry), { recursive: true, force: false, errorOnExist: false });
  }
}

const HEAD_MARKER = '<!-- HLP:head -->';
const BODY_MARKER = '<!-- HLP:body -->';

// Template mode: the Unity WebGL template's index.html stays the host page (its canvas, loader, scripts and any
// setup of its own). What your Vite page needs is added to it: scripts and styles into <head>, the UI layer on top
// of the canvas at the end of <body>. The preview <video> is dropped; the template's canvas takes its place.
// Put <!-- HLP:head --> / <!-- HLP:body --> in the template to choose the spot; otherwise the content goes
// before </head> and </body>.
export function injectIntoTemplate(templateHtml, viteHtml) {
  const head = /<head[^>]*>([\s\S]*?)<\/head>/i.exec(viteHtml)?.[1] ?? '';
  const body = /<body[^>]*>([\s\S]*?)<\/body>/i.exec(viteHtml)?.[1] ?? '';

  // The template owns charset, viewport and title.
  const headParts = head
    .replace(/<meta\s+charset[^>]*>/gi, '')
    .replace(/<meta\s+name=["']viewport["'][^>]*>/gi, '')
    .replace(/<title>[\s\S]*?<\/title>/gi, '')
    .trim();
  // Module scripts must be in <head> or <body> alike; keep any that Vite put in the body with the UI.
  const bodyParts = body.replace(/<video\b[^>]*\bid=["']stream["'][^>]*>[\s\S]*?<\/video>/i, '').trim();

  const headBlock = `<script>window.__hlpPage = true;</script>\n${headParts}\n`;
  // pointer-events cascades: only elements your page marks as interactive take clicks, the rest reaches Unity.
  const bodyBlock =
    `<div id="hlp-ui" style="position:fixed;inset:0;overflow:hidden;pointer-events:none;z-index:10">\n${bodyParts}\n</div>\n`;

  return insert(insert(templateHtml, HEAD_MARKER, /<\/head>/i, headBlock), BODY_MARKER, /<\/body>/i, bodyBlock);
}

function insert(html, marker, closingTag, block) {
  if (html.includes(marker)) return html.replace(marker, () => block);
  const match = closingTag.exec(html);
  if (!match) throw new Error(`[hlp] The WebGL template's index.html has no ${marker} marker and no ${closingTag.source.replace('\\', '')}.`);
  return html.slice(0, match.index) + block + html.slice(match.index);
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
