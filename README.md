# Twitch 追台工具

Windows 本機 Twitch 多台追蹤工具。

可以一次追蹤多個台主，直播中自動顯示，未開台自動隱藏，並在程式內用 Twitch 原站頁觀看直播。

## 下載

請到右側或上方的 **Releases** 下載最新版 zip。

下載後解壓縮，執行：

```text
Twitch 追台工具.exe
```

## 主要功能

- 一次追蹤多個 Twitch 台主
- 未開台自動隱藏，開台後顯示
- 多台直播自動排版
- 可手動選擇排版欄數
- 每台可獨立放大
- 每台可獨立調整音量
- 可固定台主頁，避免直播格跳到其他頁面
- 可清除 WebView2 快取，不影響登入與追蹤清單

## 第一次使用

1. 開啟 `Twitch 追台工具.exe`。
2. 打開設定。
3. 輸入 Twitch Client ID。
4. 點「登入取得 Token」。
5. 完成 Twitch 登入。
6. 輸入要追蹤的台主帳號。
7. 點「加入追蹤」。

忠誠點累積以 Twitch 原站登入狀態為準，請確認程式內 Twitch 頁面已登入並保持播放。

## 資料與隱私

本工具不會上傳你的資料。

以下資料只存在你的電腦：

- Twitch Access Token
- Twitch Client ID
- 追蹤清單
- WebView2 登入資料

預設本機資料位置：

```text
%LOCALAPPDATA%\TwitchPin
```

## 常見問題

### 打不開程式

請確認電腦已安裝 Microsoft Edge WebView2 Runtime。多數 Windows 10 / 11 電腦已內建。

### 忠誠點沒有累積

請確認：

- 程式內 Twitch 原站頁已登入
- 直播頁面有正常播放
- Twitch 帳號本身符合該台忠誠點累積條件

### 清除快取會登出嗎？

不會。設定裡的「清除快取」只清暫存快取，不會清除 Twitch 登入、Token 或追蹤清單。
