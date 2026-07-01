# Twitch 追台工具

這是一個 Windows 本機 App。雙擊 `Twitch 追台工具.exe` 後會在系統匣常駐，並開啟獨立 App 視窗。現在版本是「原站播放 + Token 資訊模式」：觀看、登入與忠誠點累積使用 Twitch 原站頁；台主資訊、直播狀態、標題、分類與觀眾數用 Twitch token 查詢。

嚴格來說，Twitch 原站登入、播放和忠誠點累積都需要瀏覽器核心，所以無法做成完全不帶任何 runtime 的純單檔 exe。這個專案支援的是「可攜資料夾版」：exe 旁邊放 `WebView2Runtime` 後，就會優先使用自帶 runtime，而不是依賴使用者電腦已安裝的 WebView2。

## 直接使用 exe

建置完成的檔案在：

```text
dist\Twitch 追台工具.exe
```

雙擊後會開啟 WebView2 Windows 程式視窗。底層本機控制台網址是：

```text
http://localhost:5173/
```

若 5173 被占用，程式會自動改用後面的可用 port。要關閉程式，請到系統匣的「Twitch 追台工具」圖示按右鍵，選「結束」。

若 `dist\WebView2Runtime\msedgewebview2.exe` 存在，程式會優先使用自帶的 WebView2 Runtime。若沒有這個資料夾，才會改用電腦已安裝的 Microsoft Edge WebView2 Runtime。

程式資料會優先存在 exe 旁邊：

```text
dist\Data
```

如果資料夾沒有寫入權限，才會退回 `%LOCALAPPDATA%\TwitchPin`。

## 第一次使用

1. 雙擊 `dist\Twitch 追台工具.exe`。
2. 在 Twitch Developer Console 將程式顯示的 `OAuth Redirect URL` 加到 App 的 Redirect URL。
3. 在「登入」貼上 Twitch Client ID。
4. 點「登入取得 Token」，完成 Twitch OAuth 登入。
5. 在「新增台主」輸入一個或多個帳號，例如 `twitch, riotgames`。
6. 點「加入追蹤」。

Client ID 不是 Access Token，不能單獨查 Helix API。程式會用 Client ID 開啟 Twitch OAuth，登入成功後取得 Access Token，再查台主資料。

## 多台追蹤

- 可一次貼多個帳號、`@帳號`，或 Twitch 頻道網址。
- 工具內不再顯示內嵌播放器或內嵌聊天室。
- 「原站九宮格」會在程式內用 WebView2 直接開啟 `twitch.tv/台主` 原站頁，最多排列成 3x3。
- Token 只用來查 Twitch API 的台主資訊、直播狀態、標題、分類、觀眾數與頭像。
- Token 回報未開台或查無的台主會自動從主畫面與原站九宮格隱藏，但仍保留在追蹤清單。
- 每個直播頁仍使用 Twitch 原站頁，不使用 `player.twitch.tv` 內嵌播放器。
- 忠誠點累積以 Twitch 原站頁為準，請確認原站頁已登入並保持播放。

## 領取助手

左側「領取助手」可快速開啟：

- 忠誠點累積模式：開啟追蹤台主的 Twitch 原站頁並排列視窗，用於累積忠誠點。
- 原站九宮格：直接開啟追蹤台主的 Twitch 原站頁並排列視窗。
- Twitch 原站登入頁。忠誠點需要原站登入狀態。
- 直播中或清單第一個台主的 Twitch 原站頁面，用於手動領取忠誠點 bonus chest。
- Drops 庫存頁面，用於手動查看與領取 Drops。
- 全部追蹤台主的 Twitch 原站頁面。

若點數不增加，請先按「登入 Twitch 原站」，再按「忠誠點累積模式」，並保持 Twitch 原站分頁播放。

目前不做自動點擊領取。

## 重新建置 exe

目前不需要安裝 Node、npm、Electron 或 .NET SDK。建置腳本會使用 Windows 內建的 .NET Framework C# 編譯器：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build-exe.ps1
```

輸出會覆蓋 `dist\Twitch 追台工具.exe`，並保留一份相容用的 `dist\TwitchPin.exe`。

WebView2 SDK DLL 會從這裡引用：

```text
D:\TwitchPinDeps\NuGet\Microsoft.Web.WebView2
```

## 建置可攜資料夾版

到 Microsoft 官方 WebView2 下載頁取得 Fixed Version Runtime：

```text
https://developer.microsoft.com/en-us/microsoft-edge/webview2/
```

選擇 Fixed Version、Windows、x64，下載 zip 或 cab 後，可以用一行命令整理並打包：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build-portable.ps1 -RuntimeSource "D:\Downloads\Microsoft.WebView2.FixedVersionRuntime.x64.zip"
```

如果想分兩步，也可以先把 Microsoft 官方 WebView2 Fixed Version Runtime 整理到：

```text
D:\TwitchPinDeps\WebView2Runtime
```

命令範例：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\prepare-webview2-runtime.ps1 -Source "D:\Downloads\Microsoft.WebView2.FixedVersionRuntime.x64.zip"
```

這個資料夾底下必須找得到：

```text
D:\TwitchPinDeps\WebView2Runtime\...\msedgewebview2.exe
```

如果解壓後多了一層版本資料夾也可以，建置腳本和 App 會往下尋找 `msedgewebview2.exe`。

然後執行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build-portable.ps1
```

成功後可攜版會在：

```text
dist
```

可攜版至少包含：

```text
dist\Twitch 追台工具.exe
dist\Microsoft.Web.WebView2.Core.dll
dist\Microsoft.Web.WebView2.WinForms.dll
dist\WebView2Loader.dll
dist\WebView2Runtime\...\msedgewebview2.exe
```

整個 `dist` 資料夾要一起帶走，不能只拿單一 exe。

## 備用網頁模式

如果只想用本機網頁伺服器，也可以執行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\start.ps1
```

停止：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\stop.ps1
```

## 權限與資料

- 使用 Twitch OAuth 取得 Access Token。
- Twitch access token 只存在本機 WebView `localStorage`，用於查台主資訊。
- 追蹤清單只存在本機 WebView `localStorage`。
- 目前版本沒有後端資料庫，也不會上傳追蹤清單。
