// Hybrid Live Preview, final WebGL build (package-owned; refreshed from the Unity package, do not edit).
// Replaces the preview's video element with Unity's canvas and starts the player. The canvas gets the same
// box and the same resolution rule as the preview stream, so the page looks and behaves as it did in Play.

export function startUnityCanvas(video, build) {
  // Tells the player (HybridPage.jslib) it runs inside this page, so it shares the keyboard with the UI.
  window.__hlpPage = true;

  const canvas = document.createElement('canvas');
  canvas.id = video.id;
  canvas.className = video.className;
  canvas.tabIndex = -1;
  video.replaceWith(canvas);
  // Like the preview video, the 3D view takes the keyboard once clicked; text fields in #app keep theirs.
  canvas.addEventListener('pointerdown', () => canvas.focus({ preventScroll: true }));
  canvas.addEventListener('contextmenu', (e) => e.preventDefault());

  // The preview's rule: the window in device pixels, scaled down to fit the WebGL Default Canvas size.
  function fit() {
    const dpr = window.devicePixelRatio || 1;
    const width = Math.round(innerWidth * dpr);
    const height = Math.round(innerHeight * dpr);
    const scale = Math.min(1, build.maxWidth / width, build.maxHeight / height);
    const even = (v) => Math.max(2, Math.round(v / 2) * 2);
    canvas.width = even(width * scale);
    canvas.height = even(height * scale);
  }
  fit();
  window.addEventListener('resize', fit);

  const state = { unity: null, progress: 0, error: null };
  const bar = createProgressBar(canvas.parentElement ?? document.body);

  const script = document.createElement('script');
  script.src = build.loaderUrl;
  script.onload = () => {
    window
      .createUnityInstance(
        canvas,
        {
          dataUrl: build.dataUrl,
          frameworkUrl: build.frameworkUrl,
          codeUrl: build.codeUrl,
          streamingAssetsUrl: 'StreamingAssets',
          companyName: build.companyName,
          productName: build.productName,
          productVersion: build.productVersion,
          // fit() owns the canvas resolution.
          matchWebGLToCanvasSize: false,
        },
        (progress) => {
          state.progress = progress;
          bar.set(progress);
        },
      )
      .then((instance) => {
        state.unity = instance;
        bar.remove();
      })
      .catch((error) => {
        state.error = error;
        bar.fail(String(error));
        console.error('[hlp] Unity failed to start', error);
      });
  };
  script.onerror = () => bar.fail(`Could not load ${build.loaderUrl}`);
  document.body.appendChild(script);
  return state;
}

function createProgressBar(parent) {
  const box = document.createElement('div');
  box.style.cssText =
    'position:absolute;left:50%;bottom:48px;width:min(320px,60vw);transform:translateX(-50%);' +
    'height:4px;border-radius:2px;background:rgba(255,255,255,.15);overflow:hidden;pointer-events:none;' +
    'font:12px system-ui,sans-serif;color:#f3f5f8';
  const fill = document.createElement('div');
  fill.style.cssText = 'height:100%;width:0;background:#4ade80;transition:width .2s';
  box.appendChild(fill);
  parent.appendChild(box);
  return {
    set: (p) => (fill.style.width = `${Math.round(p * 100)}%`),
    remove: () => box.remove(),
    fail: (text) => {
      box.style.height = 'auto';
      box.style.background = 'none';
      box.textContent = text;
    },
  };
}
