using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using ClashXW.Models;
using ClashXW.Native;
using ClashXW.Services;

namespace ClashXW
{
    internal class TrayApplicationContext : ApplicationContext
    {
        private readonly NotifyIcon _notifyIcon;
        private readonly MessageWindow _messageWindow;

        private ClashApiService? _apiService;
        private ClashProcessService? _clashProcessService;
        private Win32MenuBuilder? _menuBuilder;
        private readonly string? _executablePath;
        private string _currentConfigPath;

        // Cache for menu state
        private ClashConfig? _cachedConfigs;
        private ProxiesResponse? _cachedProxies;
        private ProxyProvidersResponse? _cachedProxyProviders;
        private DashboardForm? _dashboardForm;
        private StatusToastForm? _statusToast;
        private bool _isSystemProxyEnabled;
        private bool _isTunEnabled;

        public TrayApplicationContext()
        {
            ConfigManager.EnsureDefaultConfigExists();

            _executablePath = Path.Combine(AppContext.BaseDirectory, "ClashAssets", "clash.exe");
            _currentConfigPath = ConfigManager.GetCurrentConfigPath();

            // Initialize services
            _clashProcessService = new ClashProcessService(_executablePath);
            _menuBuilder = new Win32MenuBuilder();

            // Create a message window for menu handling
            _messageWindow = new MessageWindow();
            _messageWindow.ThemeChanged += UpdateTrayIcon;

            // Create notify icon
            _notifyIcon = new NotifyIcon
            {
                Icon = LoadIcon(),
                Text = "ClashXW",
                Visible = true
            };
            _notifyIcon.MouseUp += OnNotifyIconMouseUp;

            StartClashCore();
            InitializeApiService();
        }

        private Icon LoadIcon()
        {
            // Select icon based on TUN state
            var resourceName = _isTunEnabled ? "ClashXW.Resources.icon_tun.ico" : "ClashXW.Resources.icon.ico";
            Logger.Info($"LoadIcon: TUN={_isTunEnabled}, SystemProxy={_isSystemProxyEnabled}, DarkMode={DarkModeHelper.IsDarkModeEnabled}, Resource={resourceName}");

            var assembly = Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream(resourceName);
            Icon baseIcon;

            if (stream != null)
            {
                baseIcon = new Icon(stream);
            }
            else
            {
                // Fallback to default icon
                var defaultStream = assembly.GetManifestResourceStream("ClashXW.Resources.icon.ico");
                baseIcon = defaultStream != null ? new Icon(defaultStream) : SystemIcons.Application;
            }

            // Invert icon colors for dark mode (black icon -> white icon)
            if (DarkModeHelper.IsDarkModeEnabled)
            {
                baseIcon = InvertIconColors(baseIcon);
            }

            // Apply lightening filter when system proxy is off
            if (!_isSystemProxyEnabled)
            {
                return ApplyLighteningFilter(baseIcon);
            }

            return baseIcon;
        }

        private Icon ApplyLighteningFilter(Icon icon)
        {
            using var originalBitmap = icon.ToBitmap();
            var lightenedBitmap = new Bitmap(originalBitmap.Width, originalBitmap.Height, PixelFormat.Format32bppArgb);

            // Color matrix to lighten the image (increase brightness and reduce saturation slightly)
            var colorMatrix = new ColorMatrix(new float[][]
            {
                new float[] { 1.0f, 0, 0, 0, 0 },
                new float[] { 0, 1.0f, 0, 0, 0 },
                new float[] { 0, 0, 1.0f, 0, 0 },
                new float[] { 0, 0, 0, 0.5f, 0 },  // Reduce alpha to 50%
                new float[] { 0.3f, 0.3f, 0.3f, 0, 1 }  // Add brightness
            });

            using var attributes = new ImageAttributes();
            attributes.SetColorMatrix(colorMatrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);

            using var g = Graphics.FromImage(lightenedBitmap);
            g.DrawImage(originalBitmap,
                new Rectangle(0, 0, lightenedBitmap.Width, lightenedBitmap.Height),
                0, 0, originalBitmap.Width, originalBitmap.Height,
                GraphicsUnit.Pixel, attributes);

            var result = Icon.FromHandle(lightenedBitmap.GetHicon());
            icon.Dispose();
            return result;
        }

        private Icon InvertIconColors(Icon icon)
        {
            using var originalBitmap = icon.ToBitmap();
            var invertedBitmap = new Bitmap(originalBitmap.Width, originalBitmap.Height, PixelFormat.Format32bppArgb);

            // Color matrix to invert RGB while preserving alpha
            var colorMatrix = new ColorMatrix(new float[][]
            {
                new float[] { -1, 0, 0, 0, 0 },
                new float[] { 0, -1, 0, 0, 0 },
                new float[] { 0, 0, -1, 0, 0 },
                new float[] { 0, 0, 0, 1, 0 },   // Keep alpha unchanged
                new float[] { 1, 1, 1, 0, 1 }    // Add 1 to RGB to complete inversion
            });

            using var attributes = new ImageAttributes();
            attributes.SetColorMatrix(colorMatrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);

            using var g = Graphics.FromImage(invertedBitmap);
            g.DrawImage(originalBitmap,
                new Rectangle(0, 0, invertedBitmap.Width, invertedBitmap.Height),
                0, 0, originalBitmap.Width, originalBitmap.Height,
                GraphicsUnit.Pixel, attributes);

            var result = Icon.FromHandle(invertedBitmap.GetHicon());
            icon.Dispose();
            return result;
        }

        private void UpdateTrayIcon()
        {
            var oldIcon = _notifyIcon.Icon;
            _notifyIcon.Icon = LoadIcon();

            // Dispose the old icon if it's not a system icon
            if (oldIcon != null && oldIcon != SystemIcons.Application)
            {
                oldIcon.Dispose();
            }
        }

        private void StartClashCore()
        {
            if (_clashProcessService == null) return;

            try
            {
                _clashProcessService.Start(_currentConfigPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to start Clash process:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                ExitThread();
            }
        }

        private void InitializeApiService()
        {
            var apiDetails = ConfigManager.ReadApiDetails(_currentConfigPath);
            if (apiDetails != null)
            {
                _apiService = new ClashApiService(apiDetails.BaseUrl, apiDetails.Secret);
            }
            else
            {
                _apiService = null;
                MessageBox.Show($"Failed to read API details from {_currentConfigPath}. API features will be disabled.",
                    "Config Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private async void OnNotifyIconMouseUp(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left && e.Button != MouseButtons.Right) return;

            var clickPoint = Cursor.Position;

            // Fetch fresh data before showing menu
            await RefreshCachedDataAsync();

            ShowContextMenu(clickPoint.X, clickPoint.Y);
        }

        private async Task RefreshCachedDataAsync()
        {
            if (_apiService == null) return;

            try
            {
                var configsTask = _apiService.GetConfigsAsync();
                var proxiesTask = _apiService.GetProxiesAsync();

                await Task.WhenAll(configsTask, proxiesTask);

                _cachedConfigs = await configsTask;
                _cachedProxies = await proxiesTask;

                try
                {
                    _cachedProxyProviders = await _apiService.GetProxyProvidersAsync();
                }
                catch (Exception ex)
                {
                    _cachedProxyProviders = null;
                    Debug.WriteLine($"Failed to fetch proxy providers: {ex.Message}");
                }

                // Sync TUN and system proxy states
                var tunChanged = false;
                var proxyChanged = false;

                var newTunState = _cachedConfigs?.Tun?.Enable ?? false;
                if (_isTunEnabled != newTunState)
                {
                    _isTunEnabled = newTunState;
                    tunChanged = true;
                }

                var proxyAddress = _cachedConfigs != null ? GetProxyAddress(_cachedConfigs) : null;
                var newProxyState = proxyAddress != null && SystemProxyManager.IsProxyEnabled(proxyAddress);
                if (_isSystemProxyEnabled != newProxyState)
                {
                    _isSystemProxyEnabled = newProxyState;
                    proxyChanged = true;
                }

                if (tunChanged || proxyChanged)
                {
                    UpdateTrayIcon();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to fetch API data: {ex.Message}");
            }
        }

        private void ShowContextMenu(int x, int y)
        {
            if (_menuBuilder == null) return;

            using var menu = _menuBuilder.BuildMenu(
                _cachedConfigs,
                _cachedProxies,
                _cachedProxyProviders,
                _currentConfigPath,
                onModeSelected: OnModeSelected,
                onProxyNodeSelected: OnProxyNodeSelected,
                onTestGroupLatency: OnTestGroupLatency,
                onSystemProxyToggle: OnSystemProxyToggle,
                onTunModeToggle: OnTunModeToggle,
                onOpenDashboard: OnOpenDashboard,
                onTestLatency: OnTestLatency,
                onUpdateProviders: OnUpdateProviders,
                onConfigSelected: OnConfigSelected,
                onReloadConfig: OnReloadConfig,
                onEditConfig: OnEditConfig,
                onOpenConfigFolder: OnOpenConfigFolder,
                onExit: OnExit
            );

            var commandId = menu.Show(_messageWindow.Handle, x, y);
            if (commandId.HasValue)
            {
                menu.ExecuteCommand(commandId.Value);
            }
        }

        private async void OnModeSelected(string mode)
        {
            if (_apiService == null) return;

            try
            {
                await _apiService.UpdateModeAsync(mode);
            }
            catch (Exception ex)
            {
                ShowBalloonTip("Error", $"Failed to set mode: {ex.Message}", ToolTipIcon.Error);
            }
        }

        private async void OnProxyNodeSelected(string groupName, string nodeName)
        {
            if (_apiService == null) return;

            try
            {
                await _apiService.SelectProxyNodeAsync(groupName, nodeName);
            }
            catch (Exception ex)
            {
                ShowBalloonTip("Error", $"Failed to set proxy node: {ex.Message}", ToolTipIcon.Error);
            }
        }

        private async void OnTestGroupLatency(string groupName)
        {
            if (_apiService == null) return;

            try
            {
                var group = _cachedProxies?.Proxies?.GetValueOrDefault(groupName);
                if (group == null) return;

                var groupNodeNames = new System.Collections.Generic.HashSet<string>(
                    group.All ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
                var matchingProviders = GetHttpProviders()
                    .Where(provider => provider.Proxies.Any(node => groupNodeNames.Contains(node.Name)))
                    .ToList();

                if (matchingProviders.Count > 0)
                {
                    await Task.WhenAll(matchingProviders.Select(provider =>
                        _apiService.TestProxyProviderLatencyAsync(provider.Name)));
                }
                else if (IsAutoGroup(group))
                {
                    await _apiService.TestGroupLatencyAsync(groupName);
                }
                else
                {
                    var tasks = new System.Collections.Generic.List<Task>();
                    foreach (var nodeName in group.All ?? (IReadOnlyList<string>)Array.Empty<string>())
                    {
                        tasks.Add(_apiService.TestProxyLatencyAsync(nodeName));
                    }
                    await Task.WhenAll(tasks);
                }

                await RefreshCachedDataAsync();
                var (available, total) = GetProviderLatencySummary(groupNodeNames);
                var message = total > 0
                    ? $"{groupName}: {available}/{total} provider nodes available"
                    : $"Latency test completed for {groupName}";
                ShowBalloonTip("Latency Test", message, ToolTipIcon.Info);
            }
            catch (Exception ex)
            {
                ShowBalloonTip("Error", $"Failed to test latency for {groupName}: {ex.Message}", ToolTipIcon.Error);
            }
        }

        private static bool IsAutoGroup(ProxyNode proxy)
        {
            return proxy.Type.Equals("Fallback", StringComparison.OrdinalIgnoreCase)
                || proxy.Type.Equals("URLTest", StringComparison.OrdinalIgnoreCase);
        }

        private async void OnSystemProxyToggle(bool enable)
        {
            if (_apiService == null) return;

            try
            {
                var configs = await _apiService.GetConfigsAsync();
                if (configs == null) return;

                var proxyAddress = GetProxyAddress(configs);
                if (proxyAddress == null)
                {
                    ShowBalloonTip("Error", "Proxy port not configured in Clash.", ToolTipIcon.Error);
                    return;
                }

                if (enable)
                {
                    SystemProxyManager.SetProxy(proxyAddress);
                }
                else
                {
                    SystemProxyManager.DisableProxy();
                }

                _isSystemProxyEnabled = enable;
                UpdateTrayIcon();
            }
            catch (Exception ex)
            {
                ShowBalloonTip("Error", $"Failed to toggle system proxy: {ex.Message}", ToolTipIcon.Error);
            }
        }

        private async void OnTunModeToggle(bool enable)
        {
            if (_apiService == null) return;

            try
            {
                await _apiService.UpdateTunModeAsync(enable);

                // Verify actual state after toggle
                var configs = await _apiService.GetConfigsAsync();
                var actualTunState = configs?.Tun?.Enable ?? false;

                if (actualTunState != enable)
                {
                    ShowBalloonTip("Error", "Failed to set TUN mode. Check log.", ToolTipIcon.Error);
                }

                if (_isTunEnabled != actualTunState)
                {
                    _isTunEnabled = actualTunState;
                    UpdateTrayIcon();
                }
            }
            catch (Exception ex)
            {
                ShowBalloonTip("Error", $"Failed to set TUN mode: {ex.Message}", ToolTipIcon.Error);
            }
        }

        private void OnOpenDashboard()
        {
            var apiDetails = ConfigManager.ReadApiDetails(_currentConfigPath);
            if (apiDetails == null || string.IsNullOrEmpty(apiDetails.DashboardUrl)) return;

            // Show existing window or create new
            if (_dashboardForm != null && !_dashboardForm.IsDisposed)
            {
                if (_dashboardForm.WindowState == FormWindowState.Minimized)
                {
                    _dashboardForm.WindowState = FormWindowState.Normal;
                }

                _dashboardForm.Activate();
                return;
            }

            _dashboardForm = new DashboardForm(apiDetails.DashboardUrl);
            DashboardWindowPlacementManager.Restore(_dashboardForm);
            _dashboardForm.Show();

            // OLD IMPLEMENTATION (preserved):
            // try
            // {
            //     Process.Start(new ProcessStartInfo(apiDetails.DashboardUrl) { UseShellExecute = true });
            // }
            // catch (Exception ex)
            // {
            //     ShowBalloonTip("Error", $"Failed to open dashboard: {ex.Message}", ToolTipIcon.Error);
            // }
        }

        private async void OnTestLatency()
        {
            if (_apiService == null) return;

            try
            {
                var providers = GetHttpProviders();
                if (providers.Count == 0)
                {
                    ShowBalloonTip("Latency Test", "No HTTP provider nodes found", ToolTipIcon.Info);
                    return;
                }

                await Task.WhenAll(providers.Select(provider =>
                    _apiService.TestProxyProviderLatencyAsync(provider.Name)));
                await RefreshCachedDataAsync();

                var (available, total) = GetProviderLatencySummary();
                ShowBalloonTip("Latency Test", $"{available}/{total} provider nodes available", ToolTipIcon.Info);
            }
            catch (Exception ex)
            {
                ShowBalloonTip("Latency Test Failed", $"Provider nodes were not updated: {ex.Message}", ToolTipIcon.Error);
            }
        }

        private System.Collections.Generic.List<ProxyProvider> GetHttpProviders()
        {
            return _cachedProxyProviders?.Providers.Values
                .Where(provider => provider.VehicleType.Equals("HTTP", StringComparison.OrdinalIgnoreCase))
                .ToList() ?? new System.Collections.Generic.List<ProxyProvider>();
        }

        private (int Available, int Total) GetProviderLatencySummary(
            System.Collections.Generic.HashSet<string>? nodeFilter = null)
        {
            var nodes = GetHttpProviders()
                .SelectMany(provider => provider.Proxies)
                .Where(node => nodeFilter == null || nodeFilter.Contains(node.Name))
                .GroupBy(node => node.Name, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();
            var available = nodes.Count(node => node.History?.LastOrDefault()?.Delay > 0);
            return (available, nodes.Count);
        }

        private async void OnUpdateProviders()
        {
            if (_apiService == null) return;

            try
            {
                var updatedProviders = await _apiService.UpdateProxyProvidersAsync();
                if (updatedProviders.Count == 0)
                {
                    ShowBalloonTip("Info", "No HTTP providers configured", ToolTipIcon.Info);
                    return;
                }

                await RefreshCachedDataAsync();
                ShowBalloonTip("Success", $"Updated {updatedProviders.Count} provider(s)", ToolTipIcon.Info);
            }
            catch (Exception ex)
            {
                ShowBalloonTip("Error", $"Failed to update providers: {ex.Message}", ToolTipIcon.Error);
            }
        }

        private async void OnConfigSelected(string newPath)
        {
            if (_apiService == null) return;

            try
            {
                await _apiService.ReloadConfigAsync(newPath);
                _currentConfigPath = newPath;
                ConfigManager.SetCurrentConfigPath(newPath);
                InitializeApiService();
            }
            catch (Exception ex)
            {
                ShowBalloonTip("Error", $"Failed to switch configuration: {ex.Message}", ToolTipIcon.Error);
            }
        }

        private async void OnReloadConfig()
        {
            if (_apiService == null || string.IsNullOrEmpty(_currentConfigPath)) return;

            try
            {
                await _apiService.ReloadConfigAsync(_currentConfigPath);
                ShowBalloonTip("Success", "Configuration reloaded", ToolTipIcon.Info);
            }
            catch (Exception ex)
            {
                ShowBalloonTip("Error", $"Failed to reload configuration: {ex.Message}", ToolTipIcon.Error);
            }
        }

        private void OnEditConfig()
        {
            if (string.IsNullOrEmpty(_currentConfigPath) || !File.Exists(_currentConfigPath))
            {
                ShowBalloonTip("Error", $"Config file not found at: {_currentConfigPath}", ToolTipIcon.Error);
                return;
            }

            try
            {
                // open the config file with default program that is associated with extension "yml" and "yaml"
                Process.Start(new ProcessStartInfo(_currentConfigPath)
                {
                    UseShellExecute = true,
                    Verb = "open",
                });
            }
            catch (Exception ex)
            {
                ShowBalloonTip("Error", $"Failed to open config file: {ex.Message}", ToolTipIcon.Error);
            }
        }

        private void OnOpenConfigFolder()
        {
            if (string.IsNullOrEmpty(_currentConfigPath)) return;

            var configFolder = Path.GetDirectoryName(_currentConfigPath);
            if (configFolder == null || !Directory.Exists(configFolder)) return;

            try
            {
                Process.Start(new ProcessStartInfo(configFolder) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                ShowBalloonTip("Error", $"Failed to open config folder: {ex.Message}", ToolTipIcon.Error);
            }
        }

        private void OnExit()
        {
            // Check if system proxy was enabled and disable it
            if (_cachedConfigs != null)
            {
                var proxyAddress = GetProxyAddress(_cachedConfigs);
                if (proxyAddress != null && SystemProxyManager.IsProxyEnabled(proxyAddress))
                {
                    SystemProxyManager.DisableProxy();
                }
            }

            _clashProcessService?.Dispose();
            _statusToast?.Close();
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _messageWindow.DestroyHandle();
            ExitThread();
        }

        private string? GetProxyAddress(ClashConfig configs)
        {
            var mixedPort = configs.MixedPort;
            if (mixedPort > 0) return $"127.0.0.1:{mixedPort}";

            var socksPort = configs.SocksPort;
            if (socksPort > 0) return $"socks=127.0.0.1:{socksPort}";

            return null;
        }

        private void ShowBalloonTip(string title, string text, ToolTipIcon icon)
        {
            try
            {
                _statusToast?.Close();
                _statusToast = new StatusToastForm(title, text, icon);
                _statusToast.Show();
                Logger.Info($"Status popup displayed: Title={title}, Icon={icon}, TextLength={text.Length}");
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to show status popup: Title={title}, Icon={icon}", ex);
                // Keep the system notification as a fallback if creating a window fails.
                try
                {
                    _notifyIcon.BalloonTipTitle = title;
                    _notifyIcon.BalloonTipText = text;
                    _notifyIcon.BalloonTipIcon = icon;
                    _notifyIcon.ShowBalloonTip(5000);
                }
                catch (Exception fallbackError)
                {
                    Logger.Error("Failed to show fallback notification", fallbackError);
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _statusToast?.Dispose();
                _notifyIcon.Dispose();
                _messageWindow.DestroyHandle();
                _clashProcessService?.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// A hidden window used for Win32 menu operations and receiving system broadcast messages.
    /// Note: Message-only windows (HWND_MESSAGE) do NOT receive broadcast messages like
    /// WM_SETTINGCHANGE, so we use a regular hidden window instead.
    /// </summary>
    internal class MessageWindow : NativeWindow
    {
        private const int WM_SETTINGCHANGE = 0x001A;

        public event Action? ThemeChanged;

        public MessageWindow()
        {
            CreateHandle(new CreateParams
            {
                Caption = "ClashXW_MessageWindow",
                Style = 0, // Not visible
            });
            Logger.Info($"MessageWindow created: Handle={Handle}");
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_SETTINGCHANGE)
            {
                var section = Marshal.PtrToStringUni(m.LParam);
                Logger.Info($"MessageWindow: WM_SETTINGCHANGE received, section={section ?? "(null)"}");
                if (section == "ImmersiveColorSet")
                {
                    DarkModeHelper.RefreshDarkModeState();
                    ThemeChanged?.Invoke();
                }
            }
            base.WndProc(ref m);
        }
    }
}
