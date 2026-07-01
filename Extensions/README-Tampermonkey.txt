Tampermonkey support

Place the unpacked Tampermonkey extension folder here:

Extensions\Tampermonkey\manifest.json

At runtime the app checks:

dist\Extensions\Tampermonkey\manifest.json

If the manifest exists, the WebView2 profile loads the extension before opening
Twitch original pages. If the folder is missing, Twitch viewing continues without
Tampermonkey.

Compatibility note:

WebView2 can load unpacked browser extensions, but Microsoft Edge blocks direct
top-level navigation to chrome-extension:// internal management pages in this
app. The in-app Tampermonkey button reports extension status instead of opening
the blocked options page.

Tampermonkey Manifest V3 builds can show an "Allow User Scripts" warning.
Chrome exposes that toggle on the extension details page, but WebView2 does not
expose the same extension-management UI. If that warning appears, this
Tampermonkey version is loaded but cannot fully run user scripts inside this app.
