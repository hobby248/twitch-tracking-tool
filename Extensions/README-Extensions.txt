Browser extension support

Put unpacked extensions here, one folder per extension:

Extensions\ExtensionName\manifest.json

At runtime the app checks:

dist\Extensions\*\manifest.json

The app can load unpacked browser extensions into the shared WebView2 profile
before opening Twitch original pages.

Limitations:

- Chrome Web Store one-click install is not supported.
- chrome-extension:// internal management/options pages are blocked by
  WebView2/Microsoft Edge in this app.
- After adding a new extension folder, click "重新載入擴充" in the app.
