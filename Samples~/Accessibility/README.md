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

## Options

```js
startA11y({
  lang: 'zh-TW',           // default: <html lang>, else 'en'. Built-in texts are English, or Chinese for zh*.
  strings: { skip: '…' },  // override layer, game, skip, descriptionSeparator, speakerSeparator
  stage: document.querySelector('.stage'), // where the layer is added
});
```

## Things that already bit us

- **Set `lang` to your UI's language.** The template page is `lang="en"`; NVDA then uses an English voice and
  skips Chinese text.
- The WebGL template's `loading-bar.js` replaces `Element.prototype.append`; the layer uses `appendChild`.
  Do the same in your own page code.
- In a WebGL build Unity swallows Tab/Enter/Space at page level, so the layer handles them itself in the
  capture phase. Don't remove that listener.

## Messages

| Type | Direction | Data |
|---|---|---|
| `a11y:tree` | Unity → page | `{ nodes: [{ id, role, label, description, disabled, focusable, x, y, w, h }] }`, positions as 0–1 fractions from the top-left |
| `a11y:click` / `a11y:focus` | page → Unity | `{ id }` |
| `a11y:ready` | page → Unity | none; Unity resends the tree |
| `a11y:subtitle` | Unity → page | `{ text, speaker, seconds, urgent }` |
