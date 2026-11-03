# Iframe Embed (parent page talks to Unity)

Shows how a parent page that embeds your Unity page with `<iframe>` talks to Unity through HybridBridge, and how
the page refuses to be embedded by other sites. It follows the package's architecture rule: **the state lives in
Unity**; the H5 UI inside the iframe and the parent page around it only show that state and ask for changes.

```
parent page (embed/host.html)          iframe: your Unity page (index.html)            Unity
connectHybridBridge(iframe)  ── postMessage ──>  enableParentRelay()  ── HybridBridge ──>  EmbedDemo
unity.on('embed:state') <─── postMessage ───  forwards every Unity message <── HybridBridge.Send
```

## Set up

1. Import this sample from **Package Manager > Hybrid Live Preview > Samples**.
2. The parent page is copied to `HybridPreviewWeb/public/embed/` automatically (`host.html` plus the package's
   `hybrid-bridge-host.js`), or run **Tools > Hybrid Live Preview > Iframe Embed > Install Host Page**.
   Play once first if `HybridPreviewWeb` doesn't exist.
3. Run **Tools > Hybrid Live Preview > Iframe Embed > Add enableParentRelay() To main.js**, or add it yourself:

   ```js
   import { enableParentRelay, guardEmbedding } from './hlp/index.js';
   guardEmbedding();      // only pages on this origin may embed this page
   enableParentRelay();   // let the parent page talk to Unity
   ```

4. Press Play, then open `http://127.0.0.1:5173/embed/host.html`. The score counts up in Unity; Pause and Reset in
   the parent page change it, and both pages show the same state. After **Build WebGL** the page is at
   `embed/host.html` next to `index.html`.

`EmbedDemo` creates itself after the first scene loads; there is nothing to add to your scenes. Delete the
imported sample folder to remove it.

## Messages

| Type | Direction | Payload |
|---|---|---|
| `embed:get` | page → Unity | none; Unity answers with `embed:state` |
| `embed:pause` | page → Unity | `{ paused: true }` |
| `embed:reset` | page → Unity | none |
| `embed:state` | Unity → pages | `{ score, paused, scene }` |

## Use it in your own parent page

Copy `hybrid-bridge-host.js` (it is also in `HybridPreviewWeb/src/hlp/`) into the parent site:

```js
import { connectHybridBridge } from './hybrid-bridge-host.js';
const unity = connectHybridBridge(document.querySelector('iframe'));
unity.send('embed:pause', { paused: true });
const off = unity.on('embed:state', (state) => { ... });
unity.connected;      // true once the game page answered
unity.disconnect();
```

Same API as `HybridBridge` (`send`, `on`, `off`, `on('*', ...)` for every message). Messages sent before the
game page is ready are queued (up to 256).

## Security

- **Origin check.** Both sides accept and post messages only for the listed origins: same origin by default.
  For a parent on another site, list it on both sides:
  `enableParentRelay({ origins: ['https://www.example.com'] })` and
  `connectHybridBridge(iframe, { origin: 'https://game.example.com' })`.
- **Limit what goes through.** `enableParentRelay({ types: ['embed:state'], toUnity: ['embed:get', 'embed:pause'] })`
  forwards only those types to the parent and only those types to Unity; the rest is dropped.
- **Who may embed the page.** `guardEmbedding()` blanks the page when an unlisted site embeds it (Chrome, Edge,
  Safari). The reliable protection is an HTTP header from your web server, which also covers Firefox:
  `Content-Security-Policy: frame-ancestors 'self'` (add other sites after `'self'`). See the package README.
