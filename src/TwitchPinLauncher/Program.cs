using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace TwitchPinLauncher
{
    internal static class Program
    {
        internal const string AppName = "Twitch 追台工具";
        internal const string AppVersion = "v0.11.2";
        internal const string AppDisplayName = AppName + " " + AppVersion;
        private const string MutexName = "TwitchPinLauncher.SingleInstance";
        private static SynchronizationContext uiContext;
        private static ControlPanelForm controlPanel;
        private static string appDataDirectory;
        private static FileStream instanceLock;

        [STAThread]
        private static void Main()
        {
            ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (TryRequestExistingInstanceWindow(ReadSavedUrl()))
            {
                return;
            }

            if (IsAnotherAppProcessRunning())
            {
                TryRequestExistingInstanceWindowWithRetry();
                return;
            }

            if (!TryAcquireInstanceLock())
            {
                TryRequestExistingInstanceWindowWithRetry();
                return;
            }

            bool isFirstInstance;
            using (var mutex = new Mutex(true, MutexName, out isFirstInstance))
            {
                if (!isFirstInstance)
                {
                    ReleaseInstanceLock();
                    TryRequestExistingInstanceWindowWithRetry();
                    return;
                }

                try
                {
                    Application.Run(new TrayApplicationContext());
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }

                GC.KeepAlive(mutex);
                GC.KeepAlive(instanceLock);
            }
        }

        internal static string UrlFilePath
        {
            get
            {
                return Path.Combine(AppDataDirectory, "server.url");
            }
        }

        internal static string AppDataDirectory
        {
            get
            {
                if (appDataDirectory == null)
                {
                    appDataDirectory = ResolveWritableDataDirectory();
                }

                return appDataDirectory;
            }
        }

        internal static void SaveUrl(string url)
        {
            File.WriteAllText(UrlFilePath, url, Encoding.UTF8);
        }

        internal static string ReadSavedUrl()
        {
            try
            {
                if (File.Exists(UrlFilePath))
                {
                    var url = File.ReadAllText(UrlFilePath, Encoding.UTF8).Trim();
                    if (url.Length > 0)
                    {
                        return url;
                    }
                }
            }
            catch
            {
                // Use default below.
            }

            return "http://localhost:5173/";
        }

        internal static void OpenAppWindow(string url)
        {
            if (uiContext != null)
            {
                uiContext.Post(delegate { ShowControlPanel(url); }, null);
                return;
            }

            ShowControlPanel(url);
        }

        internal static void SetUiContext(SynchronizationContext context)
        {
            uiContext = context;
        }

        internal static void OpenOriginalGridWindow(List<string> channels)
        {
            if (uiContext != null)
            {
                uiContext.Post(delegate { ShowOriginalGrid(channels); }, null);
            }
        }

        private static void ShowControlPanel(string url)
        {
            if (controlPanel == null || controlPanel.IsDisposed)
            {
                controlPanel = new ControlPanelForm(url);
            }

            controlPanel.Show();
            controlPanel.WindowState = FormWindowState.Normal;
            controlPanel.Activate();
        }

        private static void ShowOriginalGrid(List<string> channels)
        {
            var form = new OriginalGridForm(channels);
            form.Show();
            form.Activate();
        }

        private static string ResolveWritableDataDirectory()
        {
            var localDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TwitchPin");
            var portableFlag = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "portable.flag");
            var portableDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");

            if (File.Exists(portableFlag))
            {
                try
                {
                    Directory.CreateDirectory(portableDir);
                    var probe = Path.Combine(portableDir, ".write-test");
                    File.WriteAllText(probe, "ok", Encoding.UTF8);
                    File.Delete(probe);
                    return portableDir;
                }
                catch
                {
                }
            }

            Directory.CreateDirectory(localDir);
            return localDir;
        }

        private static bool TryRequestExistingInstanceWindow(string url)
        {
            try
            {
                var request = (HttpWebRequest)WebRequest.Create(new Uri(new Uri(url), "show-window"));
                request.Method = "GET";
                request.Timeout = 1200;
                request.ReadWriteTimeout = 1200;

                using (var response = (HttpWebResponse)request.GetResponse())
                {
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        return true;
                    }
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        private static bool TryRequestExistingInstanceWindowWithRetry()
        {
            for (var attempt = 0; attempt < 8; attempt++)
            {
                if (TryRequestExistingInstanceWindow(ReadSavedUrl()))
                {
                    return true;
                }

                Thread.Sleep(250);
            }

            return false;
        }

        private static bool TryAcquireInstanceLock()
        {
            try
            {
                var lockPath = Path.Combine(AppDataDirectory, "app.lock");
                instanceLock = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                return true;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        internal static void ReleaseInstanceLock()
        {
            if (instanceLock == null)
            {
                return;
            }

            instanceLock.Dispose();
            instanceLock = null;
        }

        private static bool IsAnotherAppProcessRunning()
        {
            try
            {
                using (var current = Process.GetCurrentProcess())
                {
                    var currentPath = current.MainModule.FileName;
                    foreach (var process in Process.GetProcessesByName(current.ProcessName))
                    {
                        using (process)
                        {
                            if (process.Id == current.Id)
                            {
                                continue;
                            }

                            try
                            {
                                if (string.Equals(process.MainModule.FileName, currentPath, StringComparison.OrdinalIgnoreCase))
                                {
                                    return true;
                                }
                            }
                            catch
                            {
                                // Ignore processes whose module path cannot be inspected.
                            }
                        }
                    }
                }
            }
            catch
            {
                return false;
            }

            return false;
        }
    }

    internal sealed class ControlPanelForm : Form
    {
        private readonly string url;
        private readonly TableLayoutPanel viewerGrid;
        private readonly Panel viewerScrollPanel;
        private readonly Label viewerPlaceholder;
        private readonly Panel settingsPanel;
        private readonly WebView2 settingsWebView;
        private readonly ComboBox layoutComboBox;
        private readonly Button settingsButton;
        private readonly Dictionary<string, ViewerTile> viewerTiles = new Dictionary<string, ViewerTile>(StringComparer.OrdinalIgnoreCase);
        private List<string> activeViewerChannels = new List<string>();
        private CoreWebView2Environment viewerEnvironment;
        private string focusedChannel;
        private bool settingsWebViewInitialized;
        private bool navigationLockEnabled;
        private float programVolume = 0.01f;
        private bool programMuted;
        private readonly System.Windows.Forms.Timer playbackKeepAliveTimer;
        private bool playbackKeepAliveRunning;

        public ControlPanelForm(string url)
        {
            this.url = url;
            Text = Program.AppDisplayName;
            StartPosition = FormStartPosition.CenterScreen;
            Width = 1500;
            Height = 900;
            MinimumSize = new Size(960, 640);

            var viewerPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(5, 9, 13)
            };

            var toolbar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 46,
                BackColor = Color.FromArgb(16, 23, 32),
                Padding = new Padding(10, 7, 10, 7)
            };

            var titleLabel = new Label
            {
                Dock = DockStyle.Fill,
                ForeColor = Color.FromArgb(237, 242, 247),
                Font = new Font("Microsoft JhengHei UI", 10f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                Text = "Twitch 原站觀看"
            };

            settingsButton = new Button
            {
                Dock = DockStyle.Right,
                Width = 96,
                Text = "設定",
                BackColor = Color.FromArgb(45, 212, 191),
                ForeColor = Color.FromArgb(4, 47, 46),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft JhengHei UI", 9f, FontStyle.Bold)
            };
            settingsButton.FlatAppearance.BorderSize = 0;
            settingsButton.Click += delegate { ToggleSettingsPanel(); };

            layoutComboBox = new ComboBox
            {
                Dock = DockStyle.Right,
                BackColor = Color.FromArgb(28, 36, 48),
                ForeColor = Color.FromArgb(237, 242, 247),
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft JhengHei UI", 9f, FontStyle.Bold),
                Width = 112
            };
            layoutComboBox.Items.AddRange(new object[] { "自動排版", "1 欄", "2 欄", "3 欄", "4 欄", "5 欄" });
            layoutComboBox.SelectedIndex = 0;
            layoutComboBox.SelectedIndexChanged += delegate { RefreshViewerGridLayout(); };

            playbackKeepAliveTimer = new System.Windows.Forms.Timer
            {
                Interval = 15000
            };
            playbackKeepAliveTimer.Tick += async delegate { await KeepViewerPlaybackAliveAsync(); };
            playbackKeepAliveTimer.Start();

            toolbar.Controls.Add(titleLabel);
            toolbar.Controls.Add(layoutComboBox);
            toolbar.Controls.Add(settingsButton);

            viewerGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 1,
                RowCount = 1,
                BackColor = Color.FromArgb(5, 9, 13),
                Padding = new Padding(6),
                Visible = false
            };

            viewerScrollPanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Color.FromArgb(5, 9, 13)
            };
            viewerScrollPanel.Controls.Add(viewerGrid);
            viewerScrollPanel.Resize += delegate
            {
                if (activeViewerChannels.Count > 0)
                {
                    ConfigureViewerGridLayout(GetDisplayedChannelCount(activeViewerChannels));
                }
            };

            viewerPlaceholder = new Label
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(5, 9, 13),
                ForeColor = Color.FromArgb(205, 218, 235),
                Font = new Font("Microsoft JhengHei UI", 18f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                Text = "等待直播中的台主\r\n按「設定」加入台主；直播中時會載入 Twitch 原站頁。"
            };

            settingsWebView = new WebView2
            {
                Dock = DockStyle.Fill
            };

            settingsPanel = new Panel
            {
                Dock = DockStyle.Left,
                Width = 430,
                BackColor = Color.FromArgb(16, 23, 32),
                Visible = false
            };
            settingsPanel.Controls.Add(settingsWebView);

            viewerPanel.Controls.Add(viewerScrollPanel);
            viewerPanel.Controls.Add(viewerPlaceholder);
            Controls.Add(viewerPanel);
            Controls.Add(settingsPanel);
            Controls.Add(toolbar);
            toolbar.BringToFront();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                playbackKeepAliveTimer.Dispose();
                settingsWebView.Dispose();
            }

            base.Dispose(disposing);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            var task = EnsureSettingsWebViewAsync();
            GC.KeepAlive(task);
        }

        private void ToggleSettingsPanel()
        {
            settingsPanel.Visible = !settingsPanel.Visible;
            settingsButton.Text = settingsPanel.Visible ? "隱藏設定" : "設定";
            var task = EnsureSettingsWebViewAsync();
            GC.KeepAlive(task);
        }

        private async Task EnsureSettingsWebViewAsync()
        {
            if (settingsWebViewInitialized)
            {
                return;
            }

            settingsWebViewInitialized = true;
            try
            {
                viewerEnvironment = viewerEnvironment ?? await WebViewEnvironment.GetAsync();
                await settingsWebView.EnsureCoreWebView2Async(viewerEnvironment);
                await BrowserExtensionLoader.TryLoadAllAsync(settingsWebView.CoreWebView2.Profile);
                settingsWebView.CoreWebView2.WebMessageReceived += delegate(object sender, CoreWebView2WebMessageReceivedEventArgs e)
                {
                    HandleWebMessageJson(e.WebMessageAsJson);
                };
                settingsWebView.CoreWebView2.Navigate(url);
            }
            catch (Exception ex)
            {
                MessageBox.Show("WebView2 設定頁初始化失敗。請確認程式資料夾內有 WebView2Runtime，或此電腦已安裝 Microsoft Edge WebView2 Runtime。\n\n" + ex.Message, Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void HandleWebMessageJson(string json)
        {
            string browserExtensionAction;
            if (TryParseBrowserExtensionMessage(json, out browserExtensionAction))
            {
                var browserExtensionTask = HandleBrowserExtensionActionAsync(browserExtensionAction);
                GC.KeepAlive(browserExtensionTask);
                return;
            }

            string cacheAction;
            if (TryParseCacheActionMessage(json, out cacheAction))
            {
                var cacheTask = HandleCacheActionAsync(cacheAction);
                GC.KeepAlive(cacheTask);
                return;
            }

            bool lockEnabled;
            if (TryParseNavigationLockSettings(json, out lockEnabled))
            {
                navigationLockEnabled = lockEnabled;
                return;
            }

            float audioVolume;
            bool audioMuted;
            if (TryParseAudioSettings(json, out audioVolume, out audioMuted))
            {
                programVolume = audioVolume;
                programMuted = audioMuted;
                ApplyProgramAudioSettings();
                return;
            }

            var channels = ParseViewerChannels(json);
            if (channels == null)
            {
                return;
            }

            var updateTask = UpdateViewerGridAsync(channels);
            GC.KeepAlive(updateTask);
        }

        private async Task HandleCacheActionAsync(string action)
        {
            if (!String.Equals(action, "clear", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            try
            {
                viewerEnvironment = viewerEnvironment ?? await WebViewEnvironment.GetAsync();
                if (settingsWebView.CoreWebView2 == null)
                {
                    await settingsWebView.EnsureCoreWebView2Async(viewerEnvironment);
                }

                var deletedFolders = await WebViewCacheCleaner.ClearCacheAsync(settingsWebView.CoreWebView2.Profile);
                MessageBox.Show(
                    "已清除 WebView2 快取。\n\n保留項目：Twitch 登入 Cookie、Local Storage、IndexedDB、追蹤清單。\n清理資料夾：" + deletedFolders,
                    Program.AppName,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("清除快取失敗。\n\n" + ex.Message, Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task HandleBrowserExtensionActionAsync(string action)
        {
            try
            {
                viewerEnvironment = viewerEnvironment ?? await WebViewEnvironment.GetAsync();
                if (settingsWebView.CoreWebView2 == null)
                {
                    await settingsWebView.EnsureCoreWebView2Async(viewerEnvironment);
                }

                if (String.Equals(action, "open-folder", StringComparison.OrdinalIgnoreCase))
                {
                    var directory = BrowserExtensionLoader.EnsureExtensionsDirectory();
                    Process.Start("explorer.exe", directory);
                    return;
                }

                if (String.Equals(action, "reload", StringComparison.OrdinalIgnoreCase))
                {
                    BrowserExtensionLoader.Reload();
                    await BrowserExtensionLoader.TryLoadAllAsync(settingsWebView.CoreWebView2.Profile);
                    MessageBox.Show(BrowserExtensionLoader.StatusText(), Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                await BrowserExtensionLoader.TryLoadAllAsync(settingsWebView.CoreWebView2.Profile);
                MessageBox.Show(BrowserExtensionLoader.StatusText(), Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("擴充功能操作失敗。\n\n" + ex.Message, Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task UpdateViewerGridAsync(List<string> channels, bool forceLayout = false)
        {
            try
            {
                channels = NormalizeViewerChannels(channels);
                if (!forceLayout && SameChannels(activeViewerChannels, channels))
                {
                    return;
                }

                activeViewerChannels = channels;
                viewerEnvironment = viewerEnvironment ?? await WebViewEnvironment.GetAsync();

                var activeSet = new HashSet<string>(channels, StringComparer.OrdinalIgnoreCase);
                foreach (var login in new List<string>(viewerTiles.Keys))
                {
                    if (!activeSet.Contains(login))
                    {
                        var oldTile = viewerTiles[login];
                        viewerTiles.Remove(login);
                        oldTile.Container.Dispose();
                    }
                }

                if (!String.IsNullOrEmpty(focusedChannel) && !activeSet.Contains(focusedChannel))
                {
                    focusedChannel = null;
                }

                viewerGrid.SuspendLayout();
                viewerGrid.Controls.Clear();
                viewerGrid.Visible = channels.Count > 0;
                viewerPlaceholder.Visible = channels.Count == 0;
                ConfigureViewerGridLayout(GetDisplayedChannelCount(channels));

                for (var index = 0; index < channels.Count; index++)
                {
                    var login = channels[index];
                    if (!ShouldDisplayChannel(login))
                    {
                        continue;
                    }

                    ViewerTile tile;
                    if (!viewerTiles.TryGetValue(login, out tile))
                    {
                        tile = CreateViewerTile(login);
                        viewerTiles[login] = tile;
                        await tile.View.EnsureCoreWebView2Async(viewerEnvironment);
                        ConfigureTwitchWebView(tile.View);
                        await BrowserExtensionLoader.TryLoadAllAsync(tile.View.CoreWebView2.Profile);
                        tile.View.CoreWebView2.NavigationStarting += delegate(object sender, CoreWebView2NavigationStartingEventArgs e)
                        {
                            HandleTileNavigationStarting(tile, e);
                        };
                        tile.View.CoreWebView2.NavigationCompleted += delegate
                        {
                            ScheduleTileAudioSettings(tile);
                            ApplyProgramAudioSettings();
                            var keepAliveTask = KeepViewerPlaybackAliveAsync(tile.View);
                            GC.KeepAlive(keepAliveTask);
                        };
                        tile.View.CoreWebView2.Navigate("https://www.twitch.tv/" + login);
                    }

                    var displayIndex = viewerGrid.Controls.Count;
                    viewerGrid.Controls.Add(tile.Container, displayIndex % viewerGrid.ColumnCount, displayIndex / viewerGrid.ColumnCount);
                    UpdateTileFocusButton(tile);
                    ScheduleTileAudioSettings(tile);
                    var playbackTask = KeepViewerPlaybackAliveAsync(tile.View);
                    GC.KeepAlive(playbackTask);
                }

                viewerGrid.ResumeLayout(true);
                viewerGrid.BringToFront();
            }
            catch (Exception ex)
            {
                MessageBox.Show("WebView2 原站九宮格初始化失敗。請確認程式資料夾內有 WebView2Runtime，或此電腦已安裝 Microsoft Edge WebView2 Runtime。\n\n" + ex.Message, Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private ViewerTile CreateViewerTile(string login)
        {
            var tile = new ViewerTile(login);
            tile.VolumeSlider.ValueChanged += delegate
            {
                tile.Volume = tile.VolumeSlider.Value / 100.0;
                tile.VolumeLabel.Text = tile.Muted ? "靜音" : tile.VolumeSlider.Value + "%";
                if (tile.Volume > 0 && tile.Muted)
                {
                    tile.Muted = false;
                    tile.MuteButton.Text = "靜音";
                }

                ScheduleTileAudioSettings(tile);
            };

            tile.MuteButton.Click += delegate
            {
                tile.Muted = !tile.Muted;
                tile.MuteButton.Text = tile.Muted ? "解除" : "靜音";
                tile.VolumeLabel.Text = tile.Muted ? "靜音" : tile.VolumeSlider.Value + "%";
                ScheduleTileAudioSettings(tile);
            };

            tile.FocusButton.Click += delegate
            {
                if (String.Equals(focusedChannel, tile.Login, StringComparison.OrdinalIgnoreCase))
                {
                    focusedChannel = null;
                }
                else
                {
                    focusedChannel = tile.Login;
                }

                RefreshViewerGridLayout();
            };

            return tile;
        }

        private void HandleTileNavigationStarting(ViewerTile tile, CoreWebView2NavigationStartingEventArgs e)
        {
            if (!navigationLockEnabled || tile == null || String.IsNullOrEmpty(e.Uri))
            {
                return;
            }

            if (IsAllowedTileUri(tile.Login, e.Uri))
            {
                return;
            }

            e.Cancel = true;
            var resetTask = ResetTileNavigationAsync(tile);
            GC.KeepAlive(resetTask);
        }

        private async Task ResetTileNavigationAsync(ViewerTile tile)
        {
            try
            {
                await Task.Delay(100);
                if (tile.View == null || tile.View.IsDisposed || tile.View.CoreWebView2 == null)
                {
                    return;
                }

                var expected = "https://www.twitch.tv/" + tile.Login;
                if (!IsAllowedTileUri(tile.Login, tile.View.Source == null ? String.Empty : tile.View.Source.ToString()))
                {
                    tile.View.CoreWebView2.Navigate(expected);
                }
            }
            catch
            {
                // Navigation guard should never interrupt playback if WebView2 is navigating.
            }
        }

        private static bool IsAllowedTileUri(string login, string uriText)
        {
            if (String.IsNullOrWhiteSpace(login) || String.IsNullOrWhiteSpace(uriText))
            {
                return true;
            }

            Uri uri;
            if (!Uri.TryCreate(uriText, UriKind.Absolute, out uri))
            {
                return true;
            }

            if (uri.Scheme == "about" || uri.Scheme == "edge")
            {
                return true;
            }

            if (!String.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
                && !String.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var host = uri.Host.ToLowerInvariant();
            if (!String.Equals(host, "www.twitch.tv", StringComparison.OrdinalIgnoreCase)
                && !String.Equals(host, "twitch.tv", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var path = uri.AbsolutePath.Trim('/');
            if (path.Length == 0)
            {
                return false;
            }

            var firstSegment = path.Split('/')[0];
            return String.Equals(firstSegment, login, StringComparison.OrdinalIgnoreCase);
        }

        private async Task KeepViewerPlaybackAliveAsync()
        {
            if (playbackKeepAliveRunning)
            {
                return;
            }

            playbackKeepAliveRunning = true;
            try
            {
                foreach (var tile in new List<ViewerTile>(viewerTiles.Values))
                {
                    await KeepViewerPlaybackAliveAsync(tile.View);
                    await ApplyTileAudioSettingsAsync(tile);
                }

                ApplyProgramAudioSettings();
            }
            finally
            {
                playbackKeepAliveRunning = false;
            }
        }

        private async Task KeepViewerPlaybackAliveAsync(WebView2 view)
        {
            try
            {
                if (view == null || view.IsDisposed || view.CoreWebView2 == null)
                {
                    return;
                }

                await view.CoreWebView2.ExecuteScriptAsync(
                    "(function(){"
                    + "var videos=document.querySelectorAll('video');"
                    + "for(var i=0;i<videos.length;i++){"
                    + "var video=videos[i];"
                    + "try{"
                    + "if(video.paused||video.ended){var promise=video.play();if(promise&&promise.catch){promise.catch(function(){});}}"
                    + "}catch(e){}"
                    + "}"
                    + "})();");
            }
            catch
            {
                // Twitch pages can navigate or reload while the keepalive tick runs.
            }
        }

        private void ScheduleTileAudioSettings(ViewerTile tile)
        {
            var version = ++tile.AudioApplyVersion;
            var task = ApplyTileAudioSettingsWithRetryAsync(tile, version);
            GC.KeepAlive(task);
        }

        private async Task ApplyTileAudioSettingsWithRetryAsync(ViewerTile tile, int version)
        {
            for (var attempt = 0; attempt < 8 && version == tile.AudioApplyVersion; attempt++)
            {
                await ApplyTileAudioSettingsAsync(tile);
                if (attempt < 7)
                {
                    await Task.Delay(500);
                }
            }
        }

        private async Task ApplyTileAudioSettingsAsync(ViewerTile tile)
        {
            try
            {
                if (tile == null || tile.View == null || tile.View.IsDisposed || tile.View.CoreWebView2 == null)
                {
                    return;
                }

                tile.View.CoreWebView2.IsMuted = false;
                await tile.View.CoreWebView2.ExecuteScriptAsync(BuildTileAudioScript(tile.Volume, tile.Muted));
            }
            catch
            {
                // Twitch can replace video elements during navigation; the next retry will reapply audio settings.
            }
        }

        private static string BuildTileAudioScript(double volume, bool muted)
        {
            var volumeText = Math.Max(0, Math.Min(1, volume)).ToString("0.###", CultureInfo.InvariantCulture);
            var mutedText = muted ? "true" : "false";
            return "(function(){"
                + "var volume=" + volumeText + ", muted=" + mutedText + ";"
                + "var videos=document.querySelectorAll('video');"
                + "var AudioContext=window.AudioContext||window.webkitAudioContext;"
                + "for(var i=0;i<videos.length;i++){"
                + "var video=videos[i];"
                + "try{"
                + "video.muted=false;"
                + "if(!AudioContext){video.volume=muted?0:volume;continue;}"
                + "if(!window.__twitchPinAudioContext){window.__twitchPinAudioContext=new AudioContext();}"
                + "var context=window.__twitchPinAudioContext;"
                + "if(!video.__twitchPinAudio){"
                + "var source=context.createMediaElementSource(video);"
                + "var gain=context.createGain();"
                + "source.connect(gain);"
                + "gain.connect(context.destination);"
                + "video.__twitchPinAudio={gain:gain};"
                + "}"
                + "video.__twitchPinAudio.gain.gain.value=muted?0:volume;"
                + "if(context.state==='suspended'){context.resume().catch(function(){});}"
                + "}catch(e){}"
                + "}"
                + "})();";
        }

        private static List<string> ParseViewerChannels(string json)
        {
            if (String.IsNullOrEmpty(json) || json.IndexOf("\"viewer-channels\"", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return null;
            }

            var result = new List<string>();
            var arrayMatch = Regex.Match(json, "\"channels\"\\s*:\\s*\\[(?<items>.*?)\\]", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (!arrayMatch.Success)
            {
                return result;
            }

            foreach (Match itemMatch in Regex.Matches(arrayMatch.Groups["items"].Value, "\"(?<login>[^\"\\\\]*(?:\\\\.[^\"\\\\]*)*)\""))
            {
                result.Add(itemMatch.Groups["login"].Value);
            }

            return result;
        }

        private static bool TryParseNavigationLockSettings(string json, out bool enabled)
        {
            enabled = false;
            if (String.IsNullOrEmpty(json) || json.IndexOf("\"navigation-lock-settings\"", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return false;
            }

            var enabledMatch = Regex.Match(json, "\"enabled\"\\s*:\\s*(?<enabled>true|false)", RegexOptions.IgnoreCase);
            if (enabledMatch.Success)
            {
                enabled = String.Equals(enabledMatch.Groups["enabled"].Value, "true", StringComparison.OrdinalIgnoreCase);
            }

            return true;
        }

        private static bool TryParseBrowserExtensionMessage(string json, out string action)
        {
            action = String.Empty;
            if (String.IsNullOrEmpty(json) || json.IndexOf("\"browser-extension\"", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return false;
            }

            var actionMatch = Regex.Match(json, "\"action\"\\s*:\\s*\"(?<action>[^\"\\\\]*(?:\\\\.[^\"\\\\]*)*)\"", RegexOptions.IgnoreCase);
            if (actionMatch.Success)
            {
                action = Regex.Unescape(actionMatch.Groups["action"].Value);
            }

            return action.Length > 0;
        }

        private static bool TryParseCacheActionMessage(string json, out string action)
        {
            action = String.Empty;
            if (String.IsNullOrEmpty(json) || json.IndexOf("\"cache-action\"", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return false;
            }

            var actionMatch = Regex.Match(json, "\"action\"\\s*:\\s*\"(?<action>[^\"\\\\]*(?:\\\\.[^\"\\\\]*)*)\"", RegexOptions.IgnoreCase);
            if (actionMatch.Success)
            {
                action = Regex.Unescape(actionMatch.Groups["action"].Value);
            }

            return action.Length > 0;
        }

        private static bool TryParseAudioSettings(string json, out float volume, out bool muted)
        {
            volume = 0.01f;
            muted = false;
            if (String.IsNullOrEmpty(json) || json.IndexOf("\"audio-settings\"", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return false;
            }

            var volumeMatch = Regex.Match(json, "\"volume\"\\s*:\\s*(?<volume>-?\\d+(?:\\.\\d+)?)", RegexOptions.IgnoreCase);
            if (volumeMatch.Success)
            {
                float parsed;
                if (Single.TryParse(volumeMatch.Groups["volume"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed))
                {
                    volume = Math.Max(0f, Math.Min(1f, parsed));
                }
            }

            var mutedMatch = Regex.Match(json, "\"muted\"\\s*:\\s*(?<muted>true|false)", RegexOptions.IgnoreCase);
            if (mutedMatch.Success)
            {
                muted = String.Equals(mutedMatch.Groups["muted"].Value, "true", StringComparison.OrdinalIgnoreCase);
            }

            return true;
        }

        private void ApplyProgramAudioSettings()
        {
            try
            {
                using (var current = Process.GetCurrentProcess())
                {
                    AudioSessionVolumeManager.ApplyToProcessTree(current.Id, programVolume, programMuted);
                }
            }
            catch
            {
                // Program-wide volume is best-effort; playback should continue if Windows audio sessions are not ready.
            }
        }

        private static List<string> NormalizeViewerChannels(List<string> channels)
        {
            var result = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (channels == null)
            {
                return result;
            }

            foreach (var channel in channels)
            {
                var login = NormalizeChannel(channel);
                if (login.Length > 0 && seen.Add(login))
                {
                    result.Add(login);
                }

            }

            return result;
        }

        private static string NormalizeChannel(string value)
        {
            if (String.IsNullOrWhiteSpace(value))
            {
                return String.Empty;
            }

            var text = value.Trim().ToLowerInvariant();
            text = Regex.Replace(text, "^https?://(www\\.)?twitch\\.tv/", String.Empty, RegexOptions.IgnoreCase);
            text = text.Trim('/');
            var match = Regex.Match(text, "^[a-z0-9_]{3,25}$", RegexOptions.IgnoreCase);
            return match.Success ? match.Value.ToLowerInvariant() : String.Empty;
        }

        private static bool SameChannels(List<string> first, List<string> second)
        {
            if (first == null || second == null || first.Count != second.Count)
            {
                return false;
            }

            for (var index = 0; index < first.Count; index++)
            {
                if (!String.Equals(first[index], second[index], StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return true;
        }

        private bool ShouldDisplayChannel(string login)
        {
            return String.IsNullOrEmpty(focusedChannel) || String.Equals(focusedChannel, login, StringComparison.OrdinalIgnoreCase);
        }

        private int GetDisplayedChannelCount(List<string> channels)
        {
            if (String.IsNullOrEmpty(focusedChannel))
            {
                return channels.Count;
            }

            foreach (var channel in channels)
            {
                if (String.Equals(channel, focusedChannel, StringComparison.OrdinalIgnoreCase))
                {
                    return 1;
                }
            }

            return channels.Count;
        }

        private void RefreshViewerGridLayout()
        {
            var task = UpdateViewerGridAsync(activeViewerChannels, true);
            GC.KeepAlive(task);
        }

        private void UpdateTileFocusButton(ViewerTile tile)
        {
            tile.FocusButton.Text = String.Equals(focusedChannel, tile.Login, StringComparison.OrdinalIgnoreCase) ? "還原" : "放大";
        }

        private void ConfigureViewerGridLayout(int count)
        {
            var columns = 1;

            if (!String.IsNullOrEmpty(focusedChannel))
            {
                columns = 1;
            }
            else if (layoutComboBox.SelectedIndex == 1)
            {
                columns = 1;
            }
            else if (layoutComboBox.SelectedIndex == 2)
            {
                columns = 2;
            }
            else if (layoutComboBox.SelectedIndex == 3)
            {
                columns = 3;
            }
            else if (layoutComboBox.SelectedIndex == 4)
            {
                columns = 4;
            }
            else if (layoutComboBox.SelectedIndex == 5)
            {
                columns = 5;
            }
            else if (count > 16)
            {
                columns = 5;
            }
            else if (count > 9)
            {
                columns = 4;
            }
            else if (count > 4)
            {
                columns = 3;
            }
            else if (count > 2)
            {
                columns = 2;
            }
            else if (count > 1)
            {
                columns = 2;
            }

            columns = Math.Max(1, Math.Min(5, Math.Min(columns, Math.Max(1, count))));
            var rows = Math.Max(1, (int)Math.Ceiling(count / (double)columns));
            var availableHeight = Math.Max(320, viewerScrollPanel.ClientSize.Height - viewerGrid.Padding.Vertical - 8);
            int rowHeight;
            if (!String.IsNullOrEmpty(focusedChannel) || count <= 1)
            {
                rowHeight = availableHeight;
            }
            else if (rows <= 3)
            {
                rowHeight = Math.Max(260, availableHeight / rows);
            }
            else
            {
                rowHeight = 300;
            }

            viewerGrid.ColumnCount = columns;
            viewerGrid.RowCount = rows;
            viewerGrid.Width = Math.Max(320, viewerScrollPanel.ClientSize.Width - 4);
            viewerGrid.Height = Math.Max(availableHeight, rows * rowHeight + viewerGrid.Padding.Vertical);
            viewerGrid.ColumnStyles.Clear();
            viewerGrid.RowStyles.Clear();

            for (var column = 0; column < columns; column++)
            {
                viewerGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / columns));
            }

            for (var row = 0; row < rows; row++)
            {
                viewerGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, rowHeight));
            }
        }

        private static void ConfigureTwitchWebView(WebView2 view)
        {
            if (view.CoreWebView2 == null)
            {
                return;
            }

            view.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
            view.CoreWebView2.Settings.AreDevToolsEnabled = false;
            view.CoreWebView2.Settings.IsStatusBarEnabled = false;
            view.CoreWebView2.Settings.IsZoomControlEnabled = true;
        }

        private sealed class ViewerTile
        {
            public readonly string Login;
            public readonly Panel Container;
            public readonly WebView2 View;
            public readonly TrackBar VolumeSlider;
            public readonly Label VolumeLabel;
            public readonly Button MuteButton;
            public readonly Button FocusButton;
            public double Volume = 0.01;
            public bool Muted;
            public int AudioApplyVersion;

            public ViewerTile(string login)
            {
                Login = login;
                Container = new Panel
                {
                    Dock = DockStyle.Fill,
                    Margin = new Padding(4),
                    BackColor = Color.FromArgb(9, 14, 20),
                    BorderStyle = BorderStyle.FixedSingle
                };

                var controlBar = new Panel
                {
                    Dock = DockStyle.Bottom,
                    Height = 38,
                    BackColor = Color.FromArgb(16, 23, 32),
                    Padding = new Padding(10, 6, 8, 6)
                };

                var title = new Label
                {
                    Dock = DockStyle.Fill,
                    ForeColor = Color.FromArgb(237, 242, 247),
                    Font = new Font("Microsoft JhengHei UI", 9f, FontStyle.Bold),
                    TextAlign = ContentAlignment.MiddleLeft,
                    Text = "@" + login
                };

                var actions = new FlowLayoutPanel
                {
                    Dock = DockStyle.Right,
                    FlowDirection = FlowDirection.LeftToRight,
                    WrapContents = false,
                    Width = 305,
                    BackColor = Color.FromArgb(16, 23, 32),
                    Padding = new Padding(0),
                    Margin = new Padding(0)
                };

                VolumeLabel = new Label
                {
                    AutoSize = false,
                    Width = 38,
                    Height = 24,
                    ForeColor = Color.FromArgb(205, 218, 235),
                    TextAlign = ContentAlignment.MiddleCenter,
                    Text = "1%"
                };

                VolumeSlider = new TrackBar
                {
                    AutoSize = false,
                    Width = 96,
                    Height = 24,
                    Minimum = 0,
                    Maximum = 100,
                    TickStyle = TickStyle.None,
                    Value = 1
                };

                MuteButton = CreateTileButton("靜音", 54);
                FocusButton = CreateTileButton("放大", 54);

                actions.Controls.Add(VolumeLabel);
                actions.Controls.Add(VolumeSlider);
                actions.Controls.Add(MuteButton);
                actions.Controls.Add(FocusButton);

                View = new WebView2
                {
                    Dock = DockStyle.Fill,
                    Margin = new Padding(0)
                };

                controlBar.Controls.Add(title);
                controlBar.Controls.Add(actions);
                Container.Controls.Add(View);
                Container.Controls.Add(controlBar);
            }

            private static Button CreateTileButton(string text, int width)
            {
                var button = new Button
                {
                    Width = width,
                    Height = 24,
                    Text = text,
                    BackColor = Color.FromArgb(28, 36, 48),
                    ForeColor = Color.FromArgb(237, 242, 247),
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Microsoft JhengHei UI", 8f, FontStyle.Bold),
                    Margin = new Padding(4, 0, 0, 0)
                };
                button.FlatAppearance.BorderColor = Color.FromArgb(57, 71, 91);
                button.FlatAppearance.BorderSize = 1;
                return button;
            }
        }
    }

    internal sealed class SettingsForm : Form
    {
        private readonly string url;
        private readonly Action<string> messageHandler;
        private readonly WebView2 webView;
        private bool navigated;

        public SettingsForm(string url, Action<string> messageHandler)
        {
            this.url = url;
            this.messageHandler = messageHandler;

            Text = Program.AppDisplayName + " - 設定";
            StartPosition = FormStartPosition.CenterScreen;
            Width = 430;
            Height = 900;
            MinimumSize = new Size(360, 620);

            webView = new WebView2
            {
                Dock = DockStyle.Fill
            };
            Controls.Add(webView);
        }

        protected override async void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (!navigated)
            {
                navigated = true;
                await NavigateAsync();
            }
        }

        private async Task NavigateAsync()
        {
            try
            {
                var environment = await WebViewEnvironment.GetAsync();
                await webView.EnsureCoreWebView2Async(environment);
                webView.CoreWebView2.WebMessageReceived += delegate(object sender, CoreWebView2WebMessageReceivedEventArgs e)
                {
                    messageHandler(e.WebMessageAsJson);
                };
                webView.CoreWebView2.Navigate(url);
            }
            catch (Exception ex)
            {
                MessageBox.Show("WebView2 設定頁初始化失敗。請確認程式資料夾內有 WebView2Runtime，或此電腦已安裝 Microsoft Edge WebView2 Runtime。\n\n" + ex.Message, Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    internal sealed class OriginalGridForm : Form
    {
        private readonly List<string> channels;
        private readonly TableLayoutPanel grid;

        public OriginalGridForm(List<string> channels)
        {
            this.channels = new List<string>(channels);
            var columns = this.channels.Count > 16 ? 5 : this.channels.Count > 9 ? 4 : 3;
            columns = Math.Max(1, Math.Min(columns, Math.Max(1, this.channels.Count)));
            var rows = Math.Max(1, (int)Math.Ceiling(this.channels.Count / (double)columns));

            Text = Program.AppDisplayName + " - 原站多台觀看";
            StartPosition = FormStartPosition.CenterScreen;
            WindowState = FormWindowState.Maximized;
            MinimumSize = new Size(1024, 720);

            grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = columns,
                RowCount = rows,
                BackColor = Color.FromArgb(13, 17, 23),
                Padding = new Padding(6)
            };

            for (var column = 0; column < columns; column++)
            {
                grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / columns));
            }

            for (var row = 0; row < rows; row++)
            {
                grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / rows));
            }

            Controls.Add(grid);
        }

        protected override async void OnShown(EventArgs e)
        {
            base.OnShown(e);
            await PopulateAsync();
        }

        private async Task PopulateAsync()
        {
            try
            {
                var environment = await WebViewEnvironment.GetAsync();
                for (var index = 0; index < channels.Count; index++)
                {
                    var view = new WebView2
                    {
                        Dock = DockStyle.Fill,
                        Margin = new Padding(4)
                    };

                    grid.Controls.Add(view, index % grid.ColumnCount, index / grid.ColumnCount);
                    await view.EnsureCoreWebView2Async(environment);
                    ConfigureTwitchWebView(view);
                    await BrowserExtensionLoader.TryLoadAllAsync(view.CoreWebView2.Profile);
                    view.CoreWebView2.Navigate("https://www.twitch.tv/" + channels[index]);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("WebView2 九宮格初始化失敗。請確認程式資料夾內有 WebView2Runtime，或此電腦已安裝 Microsoft Edge WebView2 Runtime。\n\n" + ex.Message, Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static void ConfigureTwitchWebView(WebView2 view)
        {
            if (view.CoreWebView2 == null)
            {
                return;
            }

            view.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
            view.CoreWebView2.Settings.AreDevToolsEnabled = false;
            view.CoreWebView2.Settings.IsStatusBarEnabled = false;
            view.CoreWebView2.Settings.IsZoomControlEnabled = true;
        }
    }

    internal static class AudioSessionVolumeManager
    {
        public static void ApplyToProcessTree(int rootProcessId, float volume, bool muted)
        {
            volume = Math.Max(0f, Math.Min(1f, volume));
            var targetProcessIds = ProcessTree.GetProcessTreeIds(rootProcessId);
            var eventContext = Guid.Empty;

            IMMDeviceEnumerator enumerator = null;
            IMMDevice device = null;
            IAudioSessionManager2 manager = null;
            IAudioSessionEnumerator sessions = null;

            try
            {
                enumerator = (IMMDeviceEnumerator)(new MMDeviceEnumerator());
                if (enumerator.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eMultimedia, out device) != 0 || device == null)
                {
                    return;
                }

                var managerGuid = typeof(IAudioSessionManager2).GUID;
                object managerObject;
                if (device.Activate(ref managerGuid, 23, IntPtr.Zero, out managerObject) != 0 || managerObject == null)
                {
                    return;
                }

                manager = (IAudioSessionManager2)managerObject;
                if (manager.GetSessionEnumerator(out sessions) != 0 || sessions == null)
                {
                    return;
                }

                int count;
                if (sessions.GetCount(out count) != 0)
                {
                    return;
                }

                for (var index = 0; index < count; index++)
                {
                    IAudioSessionControl control = null;
                    IAudioSessionControl2 control2 = null;
                    ISimpleAudioVolume simpleVolume = null;
                    try
                    {
                        if (sessions.GetSession(index, out control) != 0 || control == null)
                        {
                            continue;
                        }

                        control2 = control as IAudioSessionControl2;
                        if (control2 == null)
                        {
                            continue;
                        }

                        uint sessionProcessId;
                        if (control2.GetProcessId(out sessionProcessId) != 0 || !targetProcessIds.Contains((int)sessionProcessId))
                        {
                            continue;
                        }

                        simpleVolume = control as ISimpleAudioVolume;
                        if (simpleVolume == null)
                        {
                            continue;
                        }

                        simpleVolume.SetMasterVolume(volume, ref eventContext);
                        simpleVolume.SetMute(muted, ref eventContext);
                    }
                    catch
                    {
                        // Ignore sessions that disappear while WebView2 is starting or navigating.
                    }
                    finally
                    {
                        GC.KeepAlive(simpleVolume);
                        GC.KeepAlive(control2);
                        ReleaseCom(control);
                    }
                }
            }
            catch
            {
                // Volume control is best-effort; playback must continue even if Windows audio COM fails.
            }
            finally
            {
                ReleaseCom(sessions);
                ReleaseCom(manager);
                ReleaseCom(device);
                ReleaseCom(enumerator);
            }
        }

        private static void ReleaseCom(object value)
        {
            if (value != null && Marshal.IsComObject(value))
            {
                Marshal.ReleaseComObject(value);
            }
        }

        private enum EDataFlow
        {
            eRender = 0,
            eCapture = 1,
            eAll = 2
        }

        private enum ERole
        {
            eConsole = 0,
            eMultimedia = 1,
            eCommunications = 2
        }

        [ComImport]
        [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
        private class MMDeviceEnumerator
        {
        }

        [ComImport]
        [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDeviceEnumerator
        {
            [PreserveSig]
            int EnumAudioEndpoints(EDataFlow dataFlow, int dwStateMask, out IntPtr ppDevices);

            [PreserveSig]
            int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice ppEndpoint);
        }

        [ComImport]
        [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDevice
        {
            [PreserveSig]
            int Activate(ref Guid iid, int dwClsCtx, IntPtr pActivationParams, [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);
        }

        [ComImport]
        [Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioSessionManager2
        {
            [PreserveSig]
            int GetAudioSessionControl(IntPtr audioSessionGuid, int streamFlags, out IAudioSessionControl sessionControl);

            [PreserveSig]
            int GetSimpleAudioVolume(IntPtr audioSessionGuid, int streamFlags, out ISimpleAudioVolume audioVolume);

            [PreserveSig]
            int GetSessionEnumerator(out IAudioSessionEnumerator sessionEnum);
        }

        [ComImport]
        [Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioSessionEnumerator
        {
            [PreserveSig]
            int GetCount(out int sessionCount);

            [PreserveSig]
            int GetSession(int sessionIndex, out IAudioSessionControl sessionControl);
        }

        [ComImport]
        [Guid("F4B1A599-7266-4319-A8CA-E70ACB11E8CD")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioSessionControl
        {
        }

        [ComImport]
        [Guid("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioSessionControl2
        {
            [PreserveSig]
            int GetState(out int state);

            [PreserveSig]
            int GetDisplayName(out IntPtr displayName);

            [PreserveSig]
            int SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string displayName, ref Guid eventContext);

            [PreserveSig]
            int GetIconPath(out IntPtr iconPath);

            [PreserveSig]
            int SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string iconPath, ref Guid eventContext);

            [PreserveSig]
            int GetGroupingParam(out Guid groupingParam);

            [PreserveSig]
            int SetGroupingParam(ref Guid groupingParam, ref Guid eventContext);

            [PreserveSig]
            int RegisterAudioSessionNotification(IntPtr newNotifications);

            [PreserveSig]
            int UnregisterAudioSessionNotification(IntPtr newNotifications);

            [PreserveSig]
            int GetSessionIdentifier(out IntPtr sessionId);

            [PreserveSig]
            int GetSessionInstanceIdentifier(out IntPtr sessionInstanceId);

            [PreserveSig]
            int GetProcessId(out uint processId);

            [PreserveSig]
            int IsSystemSoundsSession();

            [PreserveSig]
            int SetDuckingPreference([MarshalAs(UnmanagedType.Bool)] bool optOut);
        }

        [ComImport]
        [Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface ISimpleAudioVolume
        {
            [PreserveSig]
            int SetMasterVolume(float level, ref Guid eventContext);

            [PreserveSig]
            int GetMasterVolume(out float level);

            [PreserveSig]
            int SetMute([MarshalAs(UnmanagedType.Bool)] bool isMuted, ref Guid eventContext);

            [PreserveSig]
            int GetMute(out bool isMuted);
        }
    }

    internal static class ProcessTree
    {
        private const uint TH32CS_SNAPPROCESS = 0x00000002;
        private static readonly IntPtr InvalidHandleValue = new IntPtr(-1);

        public static HashSet<int> GetProcessTreeIds(int rootProcessId)
        {
            var processIds = new HashSet<int>();
            var parentMap = SnapshotParentMap();
            var queue = new Queue<int>();
            processIds.Add(rootProcessId);
            queue.Enqueue(rootProcessId);

            while (queue.Count > 0)
            {
                var parent = queue.Dequeue();
                foreach (var pair in parentMap)
                {
                    if (pair.Value == parent && processIds.Add(pair.Key))
                    {
                        queue.Enqueue(pair.Key);
                    }
                }
            }

            return processIds;
        }

        private static Dictionary<int, int> SnapshotParentMap()
        {
            var result = new Dictionary<int, int>();
            var snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
            if (snapshot == InvalidHandleValue)
            {
                return result;
            }

            try
            {
                var entry = new PROCESSENTRY32();
                entry.dwSize = (uint)Marshal.SizeOf(typeof(PROCESSENTRY32));
                if (!Process32First(snapshot, ref entry))
                {
                    return result;
                }

                do
                {
                    result[(int)entry.th32ProcessID] = (int)entry.th32ParentProcessID;
                }
                while (Process32Next(snapshot, ref entry));
            }
            finally
            {
                CloseHandle(snapshot);
            }

            return result;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool Process32First(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool Process32Next(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr hObject);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct PROCESSENTRY32
        {
            public uint dwSize;
            public uint cntUsage;
            public uint th32ProcessID;
            public IntPtr th32DefaultHeapID;
            public uint th32ModuleID;
            public uint cntThreads;
            public uint th32ParentProcessID;
            public int pcPriClassBase;
            public uint dwFlags;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szExeFile;
        }
    }

    internal static class WebViewEnvironment
    {
        private static Task<CoreWebView2Environment> environmentTask;
        private static string userDataFolder;

        public static string UserDataFolder
        {
            get
            {
                if (userDataFolder == null)
                {
                    userDataFolder = Path.Combine(Program.AppDataDirectory, "WebView2Profile");
                }

                return userDataFolder;
            }
        }

        public static Task<CoreWebView2Environment> GetAsync()
        {
            if (environmentTask == null)
            {
                var userData = UserDataFolder;
                Directory.CreateDirectory(userData);
                var options = new CoreWebView2EnvironmentOptions("--autoplay-policy=no-user-gesture-required --disable-background-timer-throttling --disable-renderer-backgrounding --disable-backgrounding-occluded-windows --disable-features=CalculateNativeWinOcclusion,IntensiveWakeUpThrottling")
                {
                    AreBrowserExtensionsEnabled = true
                };
                environmentTask = CoreWebView2Environment.CreateAsync(LocalRuntimePath(), userData, options);
            }

            return environmentTask;
        }

        private static string LocalRuntimePath()
        {
            var runtimePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WebView2Runtime");
            var executable = Path.Combine(runtimePath, "msedgewebview2.exe");
            if (File.Exists(executable))
            {
                return runtimePath;
            }

            if (!Directory.Exists(runtimePath))
            {
                return null;
            }

            try
            {
                var matches = Directory.GetFiles(runtimePath, "msedgewebview2.exe", SearchOption.AllDirectories);
                if (matches.Length > 0)
                {
                    return Path.GetDirectoryName(matches[0]);
                }
            }
            catch
            {
                // Fall back to the installed runtime below.
            }

            return null;
        }
    }

    internal static class WebViewCacheCleaner
    {
        private static readonly string[] CacheFolderRelativePaths = new[]
        {
            @"EBWebView\Default\Cache",
            @"EBWebView\Default\Code Cache",
            @"EBWebView\Default\GPUCache",
            @"EBWebView\Default\DawnGraphiteCache",
            @"EBWebView\Default\DawnWebGPUCache",
            @"EBWebView\Default\ShaderCache",
            @"EBWebView\Default\GrShaderCache",
            @"EBWebView\Default\Service Worker\CacheStorage"
        };

        public static async Task<string> ClearCacheAsync(CoreWebView2Profile profile)
        {
            if (profile == null)
            {
                throw new InvalidOperationException("WebView2 Profile 尚未初始化。");
            }

            await profile.ClearBrowsingDataAsync(CoreWebView2BrowsingDataKinds.DiskCache | CoreWebView2BrowsingDataKinds.CacheStorage);

            var deleted = new List<string>();
            var userDataFolder = WebViewEnvironment.UserDataFolder;
            foreach (var relativePath in CacheFolderRelativePaths)
            {
                if (TryDeleteCacheFolder(userDataFolder, relativePath))
                {
                    deleted.Add(relativePath);
                }
            }

            return deleted.Count == 0 ? "WebView2 API" : String.Join(", ", deleted.ToArray());
        }

        private static bool TryDeleteCacheFolder(string userDataFolder, string relativePath)
        {
            try
            {
                var root = Path.GetFullPath(userDataFolder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
                var target = Path.GetFullPath(Path.Combine(userDataFolder, relativePath));
                if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !Directory.Exists(target))
                {
                    return false;
                }

                Directory.Delete(target, true);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    internal static class BrowserExtensionLoader
    {
        private static readonly object Sync = new object();
        private static Task<List<BrowserExtensionLoadInfo>> loadTask;

        public static Task<List<BrowserExtensionLoadInfo>> TryLoadAllAsync(CoreWebView2Profile profile)
        {
            if (profile == null)
            {
                return Task.FromResult(new List<BrowserExtensionLoadInfo>());
            }

            lock (Sync)
            {
                if (loadTask == null)
                {
                    loadTask = LoadAllAsync(profile);
                }

                return loadTask;
            }
        }

        public static void Reload()
        {
            lock (Sync)
            {
                loadTask = null;
            }
        }

        public static string EnsureExtensionsDirectory()
        {
            var directory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Extensions");
            Directory.CreateDirectory(directory);
            return directory;
        }

        public static string StatusText()
        {
            var directory = EnsureExtensionsDirectory();
            var manifests = FindExtensionManifests();
            var builder = new StringBuilder();
            builder.AppendLine("擴充功能載入器已啟用。");
            builder.AppendLine();
            builder.AppendLine("資料夾：" + directory);
            builder.AppendLine("偵測到 manifest：" + manifests.Count);
            if (manifests.Count > 0)
            {
                builder.AppendLine();
                foreach (var manifest in manifests)
                {
                    builder.AppendLine("- " + manifest.Name + " (" + manifest.DirectoryName + ")");
                }
            }

            builder.AppendLine();
            builder.AppendLine("限制：僅支援已解壓縮的擴充功能資料夾，不支援 Chrome Web Store 一鍵安裝；WebView2 也不能開 chrome-extension:// 管理頁。");
            return builder.ToString();
        }

        private static string MatchJsonString(string json, string pattern)
        {
            var match = Regex.Match(json, pattern, RegexOptions.IgnoreCase);
            return match.Success ? Regex.Unescape(match.Groups[1].Value) : null;
        }

        private static async Task<List<BrowserExtensionLoadInfo>> LoadAllAsync(CoreWebView2Profile profile)
        {
            var loaded = new List<BrowserExtensionLoadInfo>();
            var manifests = FindExtensionManifests();
            if (manifests.Count == 0)
            {
                return loaded;
            }

            try
            {
                var extensions = await profile.GetBrowserExtensionsAsync();
                foreach (var manifest in manifests)
                {
                    var existing = FindExtensionByName(extensions, manifest.Name);
                    if (existing != null)
                    {
                        if (!existing.IsEnabled)
                        {
                            await existing.EnableAsync(true);
                        }

                        loaded.Add(new BrowserExtensionLoadInfo(existing.Name, existing.Id, manifest.Path, true, String.Empty));
                        continue;
                    }

                    try
                    {
                        var added = await profile.AddBrowserExtensionAsync(manifest.Path);
                        if (added != null && !added.IsEnabled)
                        {
                            await added.EnableAsync(true);
                        }

                        if (added != null)
                        {
                            loaded.Add(new BrowserExtensionLoadInfo(added.Name, added.Id, manifest.Path, true, String.Empty));
                        }
                    }
                    catch (Exception ex)
                    {
                        loaded.Add(new BrowserExtensionLoadInfo(manifest.Name, String.Empty, manifest.Path, false, ex.Message));
                    }
                }
            }
            catch (Exception ex)
            {
                foreach (var manifest in manifests)
                {
                    loaded.Add(new BrowserExtensionLoadInfo(manifest.Name, String.Empty, manifest.Path, false, ex.Message));
                }
            }

            return loaded;
        }

        private static CoreWebView2BrowserExtension FindExtensionByName(IReadOnlyList<CoreWebView2BrowserExtension> extensions, string name)
        {
            foreach (var extension in extensions)
            {
                if (String.Equals(extension.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return extension;
                }
            }

            return null;
        }

        private static List<ExtensionManifestInfo> FindExtensionManifests()
        {
            var result = new List<ExtensionManifestInfo>();
            var root = EnsureExtensionsDirectory();
            foreach (var directory in Directory.GetDirectories(root))
            {
                var manifestPath = Path.Combine(directory, "manifest.json");
                if (!File.Exists(manifestPath))
                {
                    continue;
                }

                var name = Path.GetFileName(directory);
                try
                {
                    var json = File.ReadAllText(manifestPath, Encoding.UTF8);
                    var manifestName = MatchJsonString(json, @"""name""\s*:\s*""([^""]+)""");
                    if (!String.IsNullOrWhiteSpace(manifestName) && !manifestName.StartsWith("__MSG_", StringComparison.OrdinalIgnoreCase))
                    {
                        name = manifestName;
                    }
                }
                catch
                {
                    // Folder name is good enough for status if manifest parsing fails.
                }

                result.Add(new ExtensionManifestInfo(name, Path.GetFileName(directory), directory));
            }

            result.Sort(delegate(ExtensionManifestInfo left, ExtensionManifestInfo right)
            {
                return StringComparer.OrdinalIgnoreCase.Compare(left.DirectoryName, right.DirectoryName);
            });
            return result;
        }
    }

    internal sealed class BrowserExtensionLoadInfo
    {
        public BrowserExtensionLoadInfo(string name, string id, string path, bool loaded, string error)
        {
            Name = name;
            Id = id;
            Path = path;
            Loaded = loaded;
            Error = error;
        }

        public string Name { get; private set; }

        public string Id { get; private set; }

        public string Path { get; private set; }

        public bool Loaded { get; private set; }

        public string Error { get; private set; }
    }

    internal sealed class ExtensionManifestInfo
    {
        public ExtensionManifestInfo(string name, string directoryName, string path)
        {
            Name = name;
            DirectoryName = directoryName;
            Path = path;
        }

        public string Name { get; private set; }

        public string DirectoryName { get; private set; }

        public string Path { get; private set; }
    }

    internal sealed class TrayApplicationContext : ApplicationContext
    {
        private readonly LocalStaticServer server;
        private readonly NotifyIcon trayIcon;

        public TrayApplicationContext()
        {
            Program.SetUiContext(SynchronizationContext.Current);

            server = new LocalStaticServer(5173);
            server.Start();
            Program.SaveUrl(server.Url);

            var menu = new ContextMenuStrip();
            menu.Items.Add("開啟 Twitch 追台工具", null, delegate { OpenApp(); });
            menu.Items.Add("結束", null, delegate { ExitThread(); });

            trayIcon = new NotifyIcon
            {
                Icon = SystemIcons.Application,
                Text = Program.AppDisplayName,
                ContextMenuStrip = menu,
                Visible = true
            };
            trayIcon.DoubleClick += delegate { OpenApp(); };

            OpenApp();
        }

        private void OpenApp()
        {
            Program.OpenAppWindow(server.Url);
        }

        protected override void ExitThreadCore()
        {
            trayIcon.Visible = false;
            trayIcon.Dispose();
            server.Dispose();
            Program.ReleaseInstanceLock();
            base.ExitThreadCore();
        }
    }

    internal sealed class LocalStaticServer : IDisposable
    {
        private const int MaxRequestBytes = 8192;
        private static readonly Dictionary<string, StaticResource> Resources = CreateResources();

        private readonly int preferredPort;
        private readonly object disposeLock = new object();
        private volatile bool disposed;
        private TcpListener listener;
        private Thread listenThread;

        public LocalStaticServer(int preferredPort)
        {
            this.preferredPort = preferredPort;
            Url = "";
        }

        public string Url { get; private set; }

        public void Start()
        {
            for (var port = preferredPort; port <= preferredPort + 30; port++)
            {
                try
                {
                    listener = new TcpListener(IPAddress.Loopback, port);
                    listener.Start();
                    Url = "http://localhost:" + port + "/";

                    listenThread = new Thread(ListenLoop);
                    listenThread.IsBackground = true;
                    listenThread.Start();
                    return;
                }
                catch (SocketException)
                {
                    if (listener != null)
                    {
                        listener.Stop();
                        listener = null;
                    }
                }
            }

            throw new InvalidOperationException("No free localhost port found between " + preferredPort + " and " + (preferredPort + 30) + ".");
        }

        private static Dictionary<string, StaticResource> CreateResources()
        {
            var map = new Dictionary<string, StaticResource>(StringComparer.OrdinalIgnoreCase);
            map["/"] = new StaticResource("wwwroot.index.html", "text/html; charset=utf-8");
            map["/index.html"] = new StaticResource("wwwroot.index.html", "text/html; charset=utf-8");
            map["/auth-callback"] = new StaticResource("wwwroot.index.html", "text/html; charset=utf-8");
            map["/styles.css"] = new StaticResource("wwwroot.styles.css", "text/css; charset=utf-8");
            map["/app.js"] = new StaticResource("wwwroot.app.js", "text/javascript; charset=utf-8");
            return map;
        }

        private void ListenLoop()
        {
            while (!disposed)
            {
                try
                {
                    var client = listener.AcceptTcpClient();
                    ThreadPool.QueueUserWorkItem(delegate { HandleClient(client); });
                }
                catch (SocketException)
                {
                    if (!disposed)
                    {
                        Thread.Sleep(100);
                    }
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
            }
        }

        private static void HandleClient(TcpClient client)
        {
            using (client)
            {
                try
                {
                    using (var stream = client.GetStream())
                    {
                        var request = ReadRequest(stream);
                        var requestLine = FirstLine(request);
                        if (string.IsNullOrWhiteSpace(requestLine))
                        {
                            WriteText(stream, 400, "Bad request", false);
                            return;
                        }

                        var parts = requestLine.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length < 2)
                        {
                            WriteText(stream, 400, "Bad request", false);
                            return;
                        }

                        var method = parts[0].ToUpperInvariant();
                        var headOnly = method == "HEAD";
                        if (method != "GET" && !headOnly)
                        {
                            WriteText(stream, 405, "Method not allowed", headOnly);
                            return;
                        }

                        var rawTarget = parts[1];
                        var path = NormalizePath(rawTarget);
                        if (path == "/show-window")
                        {
                            HandleShowWindow(stream, headOnly);
                            return;
                        }

                        if (path == "/open-original-grid")
                        {
                            HandleOpenOriginalGrid(stream, rawTarget, headOnly);
                            return;
                        }

                        if (path == "/twitch/channel-info")
                        {
                            HandleTwitchChannelInfo(stream, request, rawTarget, headOnly);
                            return;
                        }

                        StaticResource resource;
                        if (!Resources.TryGetValue(path, out resource))
                        {
                            WriteText(stream, 404, "Not found", headOnly);
                            return;
                        }

                        var bytes = LoadResource(resource.Name);
                        WriteResponse(stream, 200, resource.ContentType, bytes, headOnly);
                    }
                }
                catch
                {
                    // Browsers commonly disconnect speculative requests. Nothing useful to report.
                }
            }
        }

        private static void HandleShowWindow(Stream stream, bool headOnly)
        {
            Program.OpenAppWindow(Program.ReadSavedUrl());
            WriteText(stream, 200, "OK", headOnly);
        }

        private static void HandleOpenOriginalGrid(Stream stream, string rawTarget, bool headOnly)
        {
            var channels = ParseChannels(QueryValue(rawTarget, "channels"));
            if (channels.Count == 0)
            {
                WriteText(stream, 400, "No channels", headOnly);
                return;
            }

            Program.OpenOriginalGridWindow(channels);
            WriteText(stream, 200, "OK", headOnly);
        }

        private static void HandleTwitchChannelInfo(Stream stream, string requestText, string rawTarget, bool headOnly)
        {
            var channels = ParseChannels(QueryValue(rawTarget, "logins"));
            if (channels.Count == 0)
            {
                WriteJson(stream, 400, "{\"ok\":false,\"message\":\"No channels\"}", headOnly);
                return;
            }

            if (channels.Count > 100)
            {
                WriteJson(stream, 400, "{\"ok\":false,\"message\":\"Too many channels; max 100 per request\"}", headOnly);
                return;
            }

            var token = ExtractBearerToken(HeaderValue(requestText, "Authorization"));
            if (token.Length == 0)
            {
                WriteJson(stream, 401, "{\"ok\":false,\"message\":\"Missing Twitch token\"}", headOnly);
                return;
            }

            try
            {
                var tokenInfo = ValidateTwitchToken(token);
                if (tokenInfo.ClientId.Length == 0)
                {
                    WriteJson(stream, 401, "{\"ok\":false,\"message\":\"Token validation did not return a client_id\"}", headOnly);
                    return;
                }

                var usersJson = TwitchApiGet("https://api.twitch.tv/helix/users" + BuildRepeatedQuery("login", channels), token, tokenInfo.ClientId);
                var streamsJson = TwitchApiGet("https://api.twitch.tv/helix/streams" + BuildRepeatedQuery("user_login", channels), token, tokenInfo.ClientId);

                var responseJson = "{"
                    + "\"ok\":true,"
                    + "\"client_id\":\"" + JsonEscape(tokenInfo.ClientId) + "\","
                    + "\"token_login\":\"" + JsonEscape(tokenInfo.Login) + "\","
                    + "\"expires_in\":" + tokenInfo.ExpiresIn + ","
                    + "\"fetched_at\":\"" + DateTime.UtcNow.ToString("o") + "\","
                    + "\"users\":" + usersJson + ","
                    + "\"streams\":" + streamsJson
                    + "}";

                WriteJson(stream, 200, responseJson, headOnly);
            }
            catch (WebException ex)
            {
                WriteJson(stream, 502, "{\"ok\":false,\"message\":\"" + JsonEscape(TwitchErrorMessage(ex)) + "\"}", headOnly);
            }
            catch (Exception ex)
            {
                WriteJson(stream, 500, "{\"ok\":false,\"message\":\"" + JsonEscape(ex.Message) + "\"}", headOnly);
            }
        }

        private static TwitchTokenInfo ValidateTwitchToken(string token)
        {
            EnsureTls12();
            var request = (HttpWebRequest)WebRequest.Create("https://id.twitch.tv/oauth2/validate");
            request.Method = "GET";
            request.Timeout = 15000;
            request.ReadWriteTimeout = 15000;
            request.UserAgent = "TwitchPinTool";
            request.Accept = "application/json";
            request.KeepAlive = false;
            request.Headers["Authorization"] = "Bearer " + token;

            var json = ReadWebResponse(request);
            return new TwitchTokenInfo
            {
                ClientId = JsonStringValue(json, "client_id"),
                Login = JsonStringValue(json, "login"),
                ExpiresIn = JsonNumberValue(json, "expires_in")
            };
        }

        private static string TwitchApiGet(string url, string token, string clientId)
        {
            EnsureTls12();
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "GET";
            request.Timeout = 15000;
            request.ReadWriteTimeout = 15000;
            request.UserAgent = "TwitchPinTool";
            request.Accept = "application/json";
            request.KeepAlive = false;
            request.Headers["Authorization"] = "Bearer " + token;
            request.Headers["Client-Id"] = clientId;
            return ReadWebResponse(request);
        }

        private static string ReadWebResponse(HttpWebRequest request)
        {
            using (var response = (HttpWebResponse)request.GetResponse())
            using (var responseStream = response.GetResponseStream())
            using (var reader = new StreamReader(responseStream, Encoding.UTF8))
            {
                return reader.ReadToEnd();
            }
        }

        private static string TwitchErrorMessage(WebException ex)
        {
            var response = ex.Response as HttpWebResponse;
            if (response == null)
            {
                return ex.Message;
            }

            using (response)
            using (var responseStream = response.GetResponseStream())
            using (var reader = new StreamReader(responseStream, Encoding.UTF8))
            {
                var body = reader.ReadToEnd();
                var message = JsonStringValue(body, "message");
                if (message.Length > 0)
                {
                    return message;
                }

                return response.StatusCode + " " + response.StatusDescription;
            }
        }

        private static string BuildRepeatedQuery(string name, List<string> values)
        {
            var builder = new StringBuilder();
            for (var index = 0; index < values.Count; index++)
            {
                builder.Append(index == 0 ? "?" : "&");
                builder.Append(Uri.EscapeDataString(name));
                builder.Append("=");
                builder.Append(Uri.EscapeDataString(values[index]));
            }

            return builder.ToString();
        }

        private static string HeaderValue(string requestText, string name)
        {
            if (requestText == null)
            {
                return "";
            }

            var lines = requestText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            foreach (var line in lines)
            {
                var colon = line.IndexOf(':');
                if (colon <= 0)
                {
                    continue;
                }

                var key = line.Substring(0, colon).Trim();
                if (string.Equals(key, name, StringComparison.OrdinalIgnoreCase))
                {
                    return line.Substring(colon + 1).Trim();
                }
            }

            return "";
        }

        private static string ExtractBearerToken(string authorization)
        {
            if (string.IsNullOrWhiteSpace(authorization))
            {
                return "";
            }

            var value = authorization.Trim();
            if (value.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                return value.Substring("Bearer ".Length).Trim();
            }

            if (value.StartsWith("OAuth ", StringComparison.OrdinalIgnoreCase))
            {
                return value.Substring("OAuth ".Length).Trim();
            }

            return value;
        }

        private static string JsonStringValue(string json, string name)
        {
            if (string.IsNullOrEmpty(json))
            {
                return "";
            }

            var match = Regex.Match(json, "\"" + Regex.Escape(name) + "\"\\s*:\\s*\"((?:\\\\.|[^\"])*)\"");
            return match.Success ? JsonUnescape(match.Groups[1].Value) : "";
        }

        private static int JsonNumberValue(string json, string name)
        {
            if (string.IsNullOrEmpty(json))
            {
                return 0;
            }

            var match = Regex.Match(json, "\"" + Regex.Escape(name) + "\"\\s*:\\s*(\\d+)");
            if (!match.Success)
            {
                return 0;
            }

            int value;
            return int.TryParse(match.Groups[1].Value, out value) ? value : 0;
        }

        private static string JsonEscape(string value)
        {
            if (value == null)
            {
                return "";
            }

            var builder = new StringBuilder();
            foreach (var c in value)
            {
                switch (c)
                {
                    case '\\':
                        builder.Append("\\\\");
                        break;
                    case '"':
                        builder.Append("\\\"");
                        break;
                    case '\b':
                        builder.Append("\\b");
                        break;
                    case '\f':
                        builder.Append("\\f");
                        break;
                    case '\n':
                        builder.Append("\\n");
                        break;
                    case '\r':
                        builder.Append("\\r");
                        break;
                    case '\t':
                        builder.Append("\\t");
                        break;
                    default:
                        if (c < 32)
                        {
                            builder.Append("\\u");
                            builder.Append(((int)c).ToString("x4"));
                        }
                        else
                        {
                            builder.Append(c);
                        }
                        break;
                }
            }

            return builder.ToString();
        }

        private static string JsonUnescape(string value)
        {
            if (value == null || value.IndexOf('\\') < 0)
            {
                return value ?? "";
            }

            return Regex.Replace(value, "\\\\(?:[\"\\\\/bfnrt]|u[0-9a-fA-F]{4})", delegate(Match match)
            {
                var token = match.Value;
                switch (token)
                {
                    case "\\\"":
                        return "\"";
                    case "\\\\":
                        return "\\";
                    case "\\/":
                        return "/";
                    case "\\b":
                        return "\b";
                    case "\\f":
                        return "\f";
                    case "\\n":
                        return "\n";
                    case "\\r":
                        return "\r";
                    case "\\t":
                        return "\t";
                    default:
                        if (token.StartsWith("\\u", StringComparison.OrdinalIgnoreCase))
                        {
                            return ((char)Convert.ToInt32(token.Substring(2), 16)).ToString();
                        }

                        return token;
                }
            });
        }

        private static void EnsureTls12()
        {
            ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
        }

        private static List<string> ParseChannels(string rawChannels)
        {
            var channels = new List<string>();
            if (string.IsNullOrWhiteSpace(rawChannels))
            {
                return channels;
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var parts = rawChannels.Split(new[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                var channel = NormalizeChannel(part);
                if (channel.Length == 0 || seen.Contains(channel))
                {
                    continue;
                }

                seen.Add(channel);
                channels.Add(channel);
            }

            return channels;
        }

        private static string NormalizeChannel(string value)
        {
            if (value == null)
            {
                return "";
            }

            var builder = new StringBuilder();
            foreach (var c in value.Trim().ToLowerInvariant())
            {
                if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_')
                {
                    builder.Append(c);
                }
            }

            var channel = builder.ToString();
            if (channel.Length < 3 || channel.Length > 25)
            {
                return "";
            }

            return channel;
        }

        private static string QueryValue(string rawTarget, string name)
        {
            var question = rawTarget.IndexOf('?');
            if (question < 0 || question == rawTarget.Length - 1)
            {
                return "";
            }

            var query = rawTarget.Substring(question + 1);
            var pairs = query.Split('&');
            foreach (var pair in pairs)
            {
                var equals = pair.IndexOf('=');
                var key = equals >= 0 ? pair.Substring(0, equals) : pair;
                if (!string.Equals(Uri.UnescapeDataString(key), name, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var value = equals >= 0 ? pair.Substring(equals + 1) : "";
                return Uri.UnescapeDataString(value.Replace("+", " "));
            }

            return "";
        }

        private static string ReadRequest(NetworkStream stream)
        {
            var bytes = new List<byte>();
            var buffer = new byte[1024];

            while (bytes.Count < MaxRequestBytes)
            {
                var read = stream.Read(buffer, 0, buffer.Length);
                if (read <= 0)
                {
                    break;
                }

                for (var i = 0; i < read; i++)
                {
                    bytes.Add(buffer[i]);
                }

                var text = Encoding.ASCII.GetString(bytes.ToArray());
                if (text.Contains("\r\n\r\n") || text.Contains("\n\n"))
                {
                    return text;
                }
            }

            return Encoding.ASCII.GetString(bytes.ToArray());
        }

        private static string FirstLine(string request)
        {
            if (request == null)
            {
                return "";
            }

            var index = request.IndexOf('\n');
            if (index < 0)
            {
                return request.Trim();
            }

            return request.Substring(0, index).Trim();
        }

        private static string NormalizePath(string rawPath)
        {
            Uri absolute;
            if (Uri.TryCreate(rawPath, UriKind.Absolute, out absolute))
            {
                rawPath = absolute.PathAndQuery;
            }

            var pathOnly = rawPath.Split('?')[0];
            if (string.IsNullOrWhiteSpace(pathOnly) || pathOnly == "/")
            {
                return "/";
            }

            return Uri.UnescapeDataString(pathOnly);
        }

        private static byte[] LoadResource(string name)
        {
            var assembly = Assembly.GetExecutingAssembly();
            using (var resource = assembly.GetManifestResourceStream(name))
            {
                if (resource == null)
                {
                    throw new InvalidOperationException("Missing embedded resource: " + name);
                }

                using (var buffer = new MemoryStream())
                {
                    resource.CopyTo(buffer);
                    return buffer.ToArray();
                }
            }
        }

        private static void WriteText(Stream stream, int status, string text, bool headOnly)
        {
            WriteResponse(stream, status, "text/plain; charset=utf-8", Encoding.UTF8.GetBytes(text), headOnly);
        }

        private static void WriteJson(Stream stream, int status, string json, bool headOnly)
        {
            WriteResponse(stream, status, "application/json; charset=utf-8", Encoding.UTF8.GetBytes(json), headOnly);
        }

        private static void WriteResponse(Stream stream, int status, string contentType, byte[] body, bool headOnly)
        {
            var headers = string.Join("\r\n", new[]
            {
                "HTTP/1.1 " + status + " " + ReasonPhrase(status),
                "Content-Type: " + contentType,
                "Content-Length: " + body.Length,
                "Cache-Control: no-store",
                "Connection: close",
                "",
                ""
            });

            var headerBytes = Encoding.ASCII.GetBytes(headers);
            stream.Write(headerBytes, 0, headerBytes.Length);
            if (!headOnly)
            {
                stream.Write(body, 0, body.Length);
            }
        }

        private static string ReasonPhrase(int status)
        {
            switch (status)
            {
                case 200:
                    return "OK";
                case 400:
                    return "Bad Request";
                case 404:
                    return "Not Found";
                case 405:
                    return "Method Not Allowed";
                default:
                    return "Internal Server Error";
            }
        }

        public void Dispose()
        {
            lock (disposeLock)
            {
                if (disposed)
                {
                    return;
                }

                disposed = true;
                if (listener != null)
                {
                    listener.Stop();
                }
            }
        }

        private sealed class StaticResource
        {
            public StaticResource(string name, string contentType)
            {
                Name = name;
                ContentType = contentType;
            }

            public string Name { get; private set; }
            public string ContentType { get; private set; }
        }

        private sealed class TwitchTokenInfo
        {
            public string ClientId { get; set; }
            public string Login { get; set; }
            public int ExpiresIn { get; set; }
        }
    }
}
