# Hybrid Live Preview for Unity

[English](README.md) · [繁體中文](README.zh-TW.md)

在 Unity 3D 場景上疊一層 HTML5 UI，一邊開發一邊即時看到結果。在 Unity Editor 按下 **Play**，Chrome 就會開啟，畫面就像跑 WebGL Build 一樣：3D 畫面透過 WebRTC 串流，你自己的 HTML／CSS／JS 疊在上面並支援 Vite 熱更新，滑鼠、鍵盤和觸控也會傳回 Unity。

不用打包 WebGL、不用等。改完 C# 或 CSS 直接看。

![架構](Documentation~/images/architecture.svg)

## 功能

- **一鍵啟動**：Play 時自動啟動 Vite 和專用的 Chrome 視窗；Stop 時全部關閉，並還原所有改過的設定。
- **跟 WebGL Build 一樣的畫面**：串流解析度跟著瀏覽器視窗大小與比例，上限是 *Player Settings → WebGL → Default Canvas Width／Height*，並像 WebGL 範本的 canvas 一樣填滿視窗。Game view 也會鎖成相同解析度，`Screen`、相機比例和 UI 版面都一致。
- **真正的 HTML UI**：網頁是由 Vite 提供的一般 HTML／CSS／JS，疊在 3D 畫面上，要用什麼框架都可以。
- **輸入傳回 Unity**：在 3D 畫面上的滑鼠、筆、觸控、滾輪和鍵盤，會注入 Input System 的虛擬裝置，`Mouse.current`、`Keyboard.current`、`Touchscreen.current`、Input Actions 和 `InputSystemUIInputModule` 都收得到。`Cursor.lockState` 會變成瀏覽器的 Pointer Lock。
- **不會進到 Build**：所有組件都只在 Editor 編譯，不會打包進任何 Player。

## 需求

| | |
|---|---|
| Unity | 6000.3 以上（Windows、Direct3D 12 實測） |
| Active Input Handling | *Input System Package* 或 *Both*（輸入轉發需要） |
| Node.js | 18 以上，`npm` 要在 PATH 裡 |
| 瀏覽器 | Google Chrome |

套件依賴 `com.unity.webrtc` 3.0.0 和 `com.unity.inputsystem` 1.11.2 以上，Unity 會自動安裝。

## 安裝

在 Unity 開啟 **Window → Package Manager → + → Install package from git URL…**，輸入：

```
https://github.com/RyanXuwei/unity-hybrid-live-preview.git#main
```

或加進 `Packages/manifest.json`：

```json
"com.ryanxuwei.hybrid-live-preview": "https://github.com/RyanXuwei/unity-hybrid-live-preview.git#main"
```

要固定版本，把 `#main` 換成 tag，例如 `#v0.5.0`。

## 快速開始

1. 開啟有 **MainCamera** 標籤相機的場景，按 **Play**。
2. 第一次會把網頁範本複製到 `<專案>/HybridPreviewWeb/` 並執行 `npm install`（約一分鐘）。
3. Chrome 開啟 `http://127.0.0.1:5173/`，角落的狀態小標籤變綠並顯示 **LIVE** 和串流解析度。
4. 先點一下 3D 畫面取得焦點，之後就能像在 WebGL Build 裡一樣用滑鼠和鍵盤操作。
5. 按 **Stop**，Chrome 和 Vite 會關閉，Game view 也會回到原本的大小。

![Play 與 Stop](Documentation~/images/lifecycle.svg)

## 開發自己的 UI

UI 放在 `<專案>/HybridPreviewWeb/`，請跟專案一起納入版控，並忽略 `node_modules/`。

![檔案歸屬](Documentation~/images/web-folder.svg)

在 `index.html` 的 `#app` 裡放 HTML：

```html
<main class="stage">
  <video id="stream" autoplay playsinline muted tabindex="-1"></video>
  <div id="app">
    <button class="my-button">開始</button>
  </div>
</main>
```

在 `src/main.js` 寫程式：

```js
import './style.css';
import { startHybridPreview } from './hlp/index.js';

const preview = startHybridPreview(document.getElementById('stream'));

document.querySelector('.my-button').addEventListener('click', () => {
  console.log('這個點擊留在網頁，不會傳進 Unity', preview.peer);
});
```

`#app` 的直接子元素會接收點擊，其他空白處的輸入會穿透到 3D 畫面。存檔後 Vite 會自動重新整理，串流也會自己重新連線。

![一頁三層](Documentation~/images/page-layers.svg)

### 狀態浮動框

角落的小標籤是套件提供的狀態框：拖曳標題列移動，點 **▸** 或雙擊展開連線與串流資訊，位置會記住。它在 Shadow DOM 裡，你的 CSS 不會影響它，它也不會影響你的 UI。要完全隱藏：

```js
startHybridPreview(document.getElementById('stream'), { panel: false });
```

`startHybridPreview` 會回傳即時連線狀態（也可從 `window.__hlp` 取得）：`signaling`、`unity`、`peer`、`codec`、`width`、`height`、`fps`、`framesDecoded`。

## 在 Unity 腳本讀取輸入

用 Input System 讀輸入，Editor 和瀏覽器的操作都收得到：

```csharp
using UnityEngine;
using UnityEngine.InputSystem;

public class Spin : MonoBehaviour
{
    InputAction drag;

    void Awake() => drag = new InputAction(binding: "<Mouse>/delta");
    void OnEnable() => drag.Enable();
    void OnDisable() => drag.Disable();

    void Update()
    {
        if (Mouse.current != null && Mouse.current.leftButton.isPressed)
            transform.Rotate(Vector3.up, -drag.ReadValue<Vector2>().x * 0.2f);
    }
}
```

舊版的 `UnityEngine.Input`（`Input.GetMouseButton`、`Input.GetKey`…）在 Editor 裡只讀得到取得焦點時的實體裝置，沒辦法接收轉發的輸入，請改用 Input System。

預覽執行期間，因為焦點在 Chrome 而不是 Editor，套件會暫時把 Input System 的 *Background Behavior* 設成 *Ignore Focus*，並把所有裝置輸入導向 Game view。Stop 時兩個設定都會還原。

## 選單

**Tools → Hybrid Live Preview**

| 項目 | 功能 |
|---|---|
| Auto Launch On Play | 開關整個預覽功能 |
| Match Game View To Preview | Play 時把 Game view 鎖成串流解析度 |
| Open Preview In Chrome | 重新開啟預覽視窗 |
| Install Web App | 立即複製範本並執行 `npm install` |
| Stop Vite And Chrome | 關閉殘留的程序 |
| About | 在 Console 顯示目前版本 |

## 結構

| 資料夾 | 組件 | 用途 |
|---|---|---|
| `Editor/` | `RyanXuWei.HybridLivePreview.Editor`（Editor 平台） | Play／Stop 生命週期、Vite 與 Chrome 程序、Game view 解析度、網頁安裝與同步 |
| `Streaming/` | `RyanXuWei.HybridLivePreview.Streaming`（僅 `UNITY_EDITOR`） | `PreviewStreamer`：鏡像相機、WebRTC 視訊軌、DataChannel |
| `Input/` | `RyanXuWei.HybridLivePreview.Input`（`UNITY_EDITOR` + `ENABLE_INPUT_SYSTEM`） | `RemoteInput`：把瀏覽器輸入注入 Input System 虛擬裝置 |
| `WebApp~/` | （Unity 不匯入） | Vite 範本、signaling 中繼、viewer、狀態浮動框 |

執行時的檔案放在 `<專案>/Library/HybridLivePreview/`：`vite.log`、Chrome 設定檔，以及用來清理程序的 PID 檔。

## 疑難排解

- **狀態框顯示「Vite offline」**：查看 `Library/HybridLivePreview/vite.log`，確認新開的終端機能執行 `node` 和 `npm`，且 5173 埠沒被佔用。
- **一直顯示「Waiting for Unity Play」**：確認場景有 MainCamera 標籤的相機，Console 有出現 `[HybridLivePreview] Signaling connected`。
- **輸入沒反應**：先點一下 3D 畫面取得鍵盤焦點；Active Input Handling 要是 *Input System Package* 或 *Both*；腳本要用 Input System 讀輸入。
- **找不到 Chrome**：會改開預設瀏覽器。Chrome 裝在特殊位置時，可用 `EditorPrefs` 的 `RyanXuWei.HybridLivePreview.ChromePath` 指定路徑。

## 為什麼不用 Unity Render Streaming？

`com.unity.renderstreaming` 3.1.0-exp.9 在 Unity 6000.3 編譯不過（使用了已移除的 `AndroidApiLevel22`）。本套件直接使用 `com.unity.webrtc` 3.0.0，並在 Vite dev server 裡內建一個小型 signaling 中繼。

## 授權

[MIT](LICENSE) © Ryan Hsu
