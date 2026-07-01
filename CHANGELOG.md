# 更新紀錄

本文件記錄 Twitch 追台工具的主要功能更新，方便之後整理到 GitHub Release 或專案 README。

格式參考 Keep a Changelog，版本號採語意化版本概念：功能增加升 minor，修正升 patch。

## v0.11.0 - 2026-07-01

### Removed
- 移除 Tampermonkey 專用設定區塊、安裝入口與狀態檢查。
- 移除本機腳本載入器、設定區塊與 `Scripts` 建置輸出。
- 移除輸出資料夾內自動點擊忠誠點寶箱的舊擴充功能殘留。

### Changed
- WebView2 只保留通用「擴充功能」載入器，掃描 `dist\Extensions\*\manifest.json`。

## v0.10.0 - 2026-07-01

### Added
- 擴充功能載入器改為掃描 `dist\Extensions\*\manifest.json`，支援多個已解壓縮的 browser extensions。
- 設定頁新增「擴充功能」區塊，可開啟資料夾、重新載入擴充、檢查狀態。

### Notes
- 仍不支援 Chrome Web Store 一鍵安裝，也不能開啟 `chrome-extension://` 管理頁；這是 WebView2 / Microsoft Edge 限制。

## v0.9.0 - 2026-07-01

### Added
- 新增「本機腳本」載入器，可從 `dist\Scripts` 載入 `.user.js` / `.js`。
- 設定頁新增開啟腳本資料夾、重新載入腳本、檢查狀態。
- 本機腳本只注入程式內 Twitch 原站觀看頁，不注入設定頁。

### Notes
- 本機腳本載入器不提供 Tampermonkey 的 `GM_*` API，也不內建任何腳本。

## v0.8.7 - 2026-07-01

### Fixed
- Tampermonkey 狀態按鈕不再導向會被 Microsoft Edge / WebView2 封鎖的 `chrome-extension://` 管理頁。
- 按鈕改為顯示 Tampermonkey 載入狀態、Extension ID、Manifest Version 與 WebView2 限制說明。

## v0.8.6 - 2026-06-30

### Fixed
- 補回設定頁的「程式整體音量」滑桿與「一鍵靜音」按鈕。
- 補上 WinForms 原生端對 `audio-settings` 訊息的接收與套用，會調整本程式與 WebView2 子行程的 Windows 音訊 session。

## v0.8.5 - 2026-06-30

### Changed
- 將每個直播格的音量控制退回上一版行為：預設 1%，載入直播後自動套用音量。
- 每格音量控制改回 WebAudio 增益層，不再直接改 Twitch 影片元素的音量。

## v0.8.4 - 2026-06-30

### Changed
- Tampermonkey 管理頁改為依 `manifest_version` 判斷：Manifest V2 會開啟管理頁，Manifest V3 會顯示 WebView2 相容性提示。
- Tampermonkey 管理頁路徑會優先讀取 manifest 的 `options_page` 或 `options_ui.page`。

## v0.8.3 - 2026-06-30

### Fixed
- 修正直播預設沒聲音問題：不再於頁面載入時套用 WebAudio 增益層。
- 每個直播格的音量預設改為 100%，只有使用者調整滑桿或按靜音後才套用音量設定。
- 音量控制改為簡化的 video 音量設定，不再建立可能被 WebView2 暫停的 AudioContext。
- Tampermonkey 管理頁按鈕不再硬開 WebView2 不相容的管理頁，改為顯示相容性說明。

## v0.8.2 - 2026-06-30

### Changed
- 補充 Tampermonkey 5.5 / Manifest V3 在 WebView2 中的限制說明。
- 若 Tampermonkey 顯示「允許使用者腳本」提示，代表 WebView2 沒有 Chrome 對應的擴充功能詳細設定頁可啟用該權限。

### Fixed
- 保留 v0.8.1 的直播格底部控制列調整，避免音量控制擋住 Twitch 原站頁頂部導覽列。

## v0.8.1 - 2026-06-30

### Fixed
- 修正 Tampermonkey 管理頁開啟路徑，改為 `options.html#nav=dashboard`。
- Tampermonkey 載入後若 extension 處於停用狀態，會嘗試自動啟用。
- 將每個直播格的音量控制列移到底部，避免擋住 Twitch 原站頁頂部導覽列。

### Notes
- Tampermonkey MV3 在 WebView2 的管理頁仍可能有相容性限制；若管理頁空白，可改用設定裡的 `.user.js` 腳本網址安裝入口。

## v0.8.0 - 2026-06-30

### Added
- 新增 Tampermonkey 腳本安裝入口。
- 設定面板新增「Tampermonkey」區塊。
- 可從程式開啟 Tampermonkey 管理頁。
- 可貼上 `http(s)` 的 `.user.js` 腳本網址，由 Tampermonkey 開啟安裝頁。

### Notes
- 仍需先將解壓縮的 Tampermonkey 放到 `dist/Extensions/Tampermonkey/manifest.json`。
- 程式不內建任何使用者腳本；請只安裝可信任且符合使用需求的腳本。

## v0.7.0 - 2026-06-30

### Added
- 將設定頁移回主視窗同一頁，改為左側設定面板。
- 設定面板預設自動隱藏，可用主視窗右上角「設定 / 隱藏設定」切換。
- 新增「固定每個台主頁」設定，使用者可自行決定是否啟用。
- 啟用固定台主頁後，每個直播格只允許留在自己的 Twitch 台主頁，若跳到其他 Twitch 頁或外部網站會被導回原台。

### Changed
- 版本號更新為 v0.7.0，顯示於主視窗、設定頁與系統匣提示。

## v0.6.0 - 2026-06-30

### Added
- 新增正式版本號顯示。
- 主視窗標題列、設定頁標題、設定視窗與系統匣提示顯示版本號。

## v0.5.0 - 2026-06-30

### Added
- 解除一次最多觀看 9 台的限制。
- 主視窗觀看區支援上下捲動。
- 排版選單新增 4 欄與 5 欄。
- 自動排版調整為：
  - 1 台：1 欄
  - 2-4 台：2 欄
  - 5-9 台：3 欄
  - 10-16 台：4 欄
  - 17 台以上：5 欄

### Changed
- 前端不再只送出前 9 台。
- 原生端不再截斷觀看清單。

## v0.4.0 - 2026-06-30

### Added
- 新增 WebView2 Browser Extension 載入支援。
- 程式會嘗試載入 `dist/Extensions/Tampermonkey/manifest.json` 的解壓縮 Tampermonkey extension。
- 新增 `Extensions/README-Tampermonkey.txt` 說明放置方式。

### Notes
- 若 Tampermonkey 資料夾不存在或載入失敗，程式會略過，不影響 Twitch 原站觀看。

## v0.3.0 - 2026-06-30

### Added
- 每個台新增獨立控制列。
- 每個台可獨立調整音量、靜音。
- 每個台可獨立放大，放大後可還原。
- 主視窗新增排版選單：自動排版、1 欄、2 欄、3 欄。

### Changed
- 移除主視窗一鍵低畫質功能與相關 Twitch 畫質操作腳本。
- 設定頁移除原本全域「程式音量」面板，避免與每台獨立音量混淆。

### Notes
- 每台音量控制不會點擊 Twitch 播放器內建音量 UI，而是對各 WebView 頁面套用獨立音訊增益與 WebView 靜音。

## v0.2.0 - 2026-06-29

### Added
- 新增主視窗「一鍵低畫質」按鈕。
- 支援對九宮格內 Twitch 原站頁嘗試切換到最低解析度。

### Changed
- 第二次按下低畫質按鈕的行為先由「切回自動畫質」調整為「切回 720p」。

### Fixed
- 加強 Twitch 畫質選單偵測，支援非 button 選單項目與重試。

## v0.1.0 - 2026-06-28

### Added
- 建立 Twitch 追台工具基本版本。
- 支援使用 Twitch Client ID 與 Access Token 查詢台主資訊。
- 支援追蹤多個台主。
- 直播中台主會顯示於主視窗原站觀看區。
- 未開台台主可自動隱藏。
- 使用 Twitch 原站頁觀看，保留使用者原站登入狀態。
- 新增 WebView2 portable runtime 打包與 `dist/Twitch 追台工具.exe` 輸出。

### Changed
- 程式名稱改為「Twitch 追台工具」。
- 從早期監視器模式調整為主視窗九宮格觀看。

### Removed
- 移除不使用或不適合保留的領取助手相關 UI。
