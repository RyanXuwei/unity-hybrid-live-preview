# Accessibility (screen reader layer)

Makes a uGUI game readable by screen readers (NVDA, VoiceOver, JAWS) in the Play preview and in the WebGL build.
Unity sends the visible buttons, images and text to the page; the page lays transparent, real HTML elements
over the game at the same positions. Screen readers read them, Tab moves between them, and Enter/Space presses
the Unity button. Subtitles from C# are shown as HTML captions and read aloud.

Requires uGUI (`com.unity.ugui`, which includes TextMeshPro).

## Set up

1. Import this sample from **Package Manager > Hybrid Live Preview > Samples**.
2. The page files are copied to `HybridPreviewWeb/src/a11y/` automatically (or run
   **Tools > Hybrid Live Preview > Accessibility > Install Web Files**; Play once first if `HybridPreviewWeb` doesn't exist).
3. Run **Tools > Hybrid Live Preview > Accessibility > Add startA11y() To main.js**, or add it yourself:

   ```js
   import { startA11y } from './a11y/a11y.js';
   startA11y({ lang: 'zh-TW' }); // the language your UI text is in
   ```

4. Press Play. `A11yBridge` creates itself after the first scene loads; there is nothing to add to your scenes.

## What gets read

| Unity object | Read as | Notes |
|---|---|---|
| `Button` | button | Name = `A11yLabel.label`, else the first child text, else the GameObject name. Its child text is not read again. |
| `TMP_Text` / `Text` | text | Hidden (disabled or alpha 0) text is skipped. |
| Anything with an `A11yLabel` label | image | Images without a label are decoration and skipped. |
| `A11yLabel` role **Ignore** | nothing | Skips the object and its children. |

Add **UI > Accessibility > A11y Label** to override the role, name the element, add a description, or make an
image/text reachable with Tab (`tabStop`). Switching the role to Text or Button fills an empty Label from the
object's text; the context menu **Fill Label From Text** does it on demand.

Order follows the Hierarchy (canvases by sorting order), so Tab order matches the screen structure.

## Subtitles

```csharp
using RyanXuWei.HybridLivePreview.Accessibility;

A11ySubtitle.Show("Welcome to the village", "Elder");          // 4 seconds, polite
A11ySubtitle.Show("The bridge is collapsing!", urgent: true);   // interrupts the reader
A11ySubtitle.Clear();
```

## Keyboard

- **Game mode** (default): focus is on the game view, which is marked `role="application"`; every key goes to Unity.
- **Browse mode**: Tab enters the interface. Tab / Shift+Tab / Enter / Space stay on the page, other keys still
  reach Unity. Esc goes back to the game. A "Back to the game" link appears for keyboard users.
- **Screen changes**: when the focused element disappears (for example after pressing Start), focus moves to
  the first new element of the next screen, so screen readers read it and keyboard users don't land on `<body>`.
  Buttons that are temporarily non-interactable use `aria-disabled` instead of `disabled`, so they keep focus.

## Options

```js
startA11y({
  lang: 'zh-TW',           // default: <html lang>, else 'en'. Built-in texts are English, or Chinese for zh*.
  strings: { skip: '…' },  // override layer, game, skip, descriptionSeparator, speakerSeparator
  stage: document.querySelector('.stage'), // where the layer is added
  title: document.title,    // screen-reader-only <h1>, added when the page has none
});
```

## Things that already bit us

- **Set `lang` to your UI's language.** The template page is `lang="en"`; NVDA then uses an English voice and
  skips Chinese text.
- The WebGL template's `loading-bar.js` replaces `Element.prototype.append`; the layer uses `appendChild`.
  Do the same in your own page code.
- Don't put the role in the description (e.g. "button"): screen readers already say it, so the user hears
  "Mute, button, button".
- In a WebGL build Unity swallows Tab/Enter/Space at page level, so the layer handles them itself in the
  capture phase. Don't remove that listener.

## Passing an accessibility check (Freego, AA)

Taiwan's [Freego](https://accessibility.moda.gov.tw/) checker (Java 17 runtime; set `JAVA_HOME` if it reports
"Java Runtime Environment not found") checks the deployed page. What the layer already covers, and what is yours:

| Check | Covered by |
|---|---|
| HM1130100C heading structure | The layer adds a screen-reader-only `<h1>` (`title` option) when the page has none. |
| CS2140401C relative font sizes | The layer's CSS uses em/%. **Yours:** every stylesheet, including the WebGL template's `TemplateData/style.css` (e.g. `font-size: 18px` → `1.125em`). |
| HM2310200C language of parts | The layer only sets `lang` where it differs from `<html lang>`. **Yours:** set `<html lang>` in the template and `index.html` to your UI's language. |

The software check is only part of an AA review; the manual review covers keyboard use and what the reader
says. Walk through every screen with Tab / Enter / Esc and a screen reader before submitting.

## Deploying and re-testing

- Scenes loaded through **Addressables** are only updated after rebuilding the Addressables content; a player
  build alone keeps the old scene (and its A11yLabel settings).
- A PWA template whose `ServiceWorker.js` serves every request cache-first keeps returning visitors (and the
  checker's browser) on old files, including the Addressables `catalog.bin`, until the version number changes.
  Bump the version on each deploy, or make `index.html` and `catalog.bin`/`catalog.hash` network-first. Clear
  the site data or use a private window when re-testing.

## Messages

| Type | Direction | Data |
|---|---|---|
| `a11y:tree` | Unity → page | `{ nodes: [{ id, role, label, description, disabled, focusable, x, y, w, h }] }`, positions as 0–1 fractions from the top-left |
| `a11y:click` / `a11y:focus` | page → Unity | `{ id }` |
| `a11y:ready` | page → Unity | none; Unity resends the tree |
| `a11y:subtitle` | Unity → page | `{ text, speaker, seconds, urgent }` |
