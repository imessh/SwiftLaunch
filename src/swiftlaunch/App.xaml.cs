using System;
using System.Windows;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;

using WinForms = System.Windows.Forms;
using Drawing  = System.Drawing;

namespace SwiftLaunch
{
    public partial class App : System.Windows.Application
    {
        private WinForms.NotifyIcon? _trayIcon;
        private LauncherWindow? _launcherWindow;
        private HotkeyManager? _hotkeyManager;
        private FolderIndexer? _indexer;
        private Mutex? _mutex;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Single instance check
            _mutex = new Mutex(true, "SwiftLaunchSingleInstance", out bool isNew);
            if (!isNew)
            {
                System.Windows.MessageBox.Show("SwiftLaunch is already running in the system tray.",
                    "SwiftLaunch", MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown();
                return;
            }

            // ── FEATURE 2: register for Windows startup on first launch if not already set ──
            if (!StartupManager.IsEnabled())
                StartupManager.Enable();

            _indexer = new FolderIndexer();
            _indexer.StartBackgroundIndex();

            SetupTrayIcon();

            _hotkeyManager = new HotkeyManager();
            _hotkeyManager.HotkeyPressed += OnHotkeyPressed;
            _hotkeyManager.Register();

            // Check for updates in the background — does not block startup
            _ = CheckForUpdateAsync();
        }

        private async Task CheckForUpdateAsync()
        {
            var update = await UpdateService.CheckForUpdateAsync().ConfigureAwait(false);
            if (update is null) return;

            // Marshal back to the UI thread before touching the window
            Dispatcher.Invoke(() =>
            {
                // Create the launcher window now if it hasn't been created yet,
                // so we have somewhere to show the banner.
                if (_launcherWindow == null || !_launcherWindow.IsLoaded)
                    _launcherWindow = new LauncherWindow(_indexer!);

                _launcherWindow.ShowUpdateBanner(update);
            });
        }

        private void SetupTrayIcon()
        {
            var contextMenu = new WinForms.ContextMenuStrip();
            contextMenu.Items.Add("Open SwiftLaunch", null, (s, e) => ShowLauncher());
            contextMenu.Items.Add("Re-index Folders",  null, (s, e) => _indexer?.ForceReindex());
            contextMenu.Items.Add(new WinForms.ToolStripSeparator());
            contextMenu.Items.Add("Run on Startup", null, OnStartupToggle);
            contextMenu.Items.Add(new WinForms.ToolStripSeparator());
            contextMenu.Items.Add("Exit", null, (s, e) => ExitApp());

            contextMenu.Opening += (s, e) =>
            {
                if (contextMenu.Items[3] is WinForms.ToolStripMenuItem startupItem)
                    startupItem.Checked = StartupManager.IsEnabled();
            };

            _trayIcon = new WinForms.NotifyIcon
            {
                Text             = "SwiftLaunch (Ctrl+Space)",
                Visible          = true,
                ContextMenuStrip = contextMenu,
                Icon             = CreateTrayIcon()
            };
            _trayIcon.DoubleClick += (s, e) => ShowLauncher();
        }

        private Drawing.Icon CreateTrayIcon()
        {
            var bitmap = new Drawing.Bitmap(16, 16);
            using var g = Drawing.Graphics.FromImage(bitmap);
            g.Clear(Drawing.Color.Transparent);
            g.FillRectangle(new Drawing.SolidBrush(Drawing.Color.FromArgb(99, 102, 241)), 0, 0, 16, 16);
            g.FillPolygon(new Drawing.SolidBrush(Drawing.Color.White), new Drawing.Point[]
            {
                new(9, 1), new(5, 8), new(8, 8), new(6, 15), new(12, 6), new(9, 6)
            });
            return Drawing.Icon.FromHandle(bitmap.GetHicon());
        }

        private void OnHotkeyPressed(object? sender, EventArgs e) => ShowLauncher();

        private void ShowLauncher()
        {
            Dispatcher.Invoke(() =>
            {
                if (_launcherWindow == null || !_launcherWindow.IsLoaded)
                    _launcherWindow = new LauncherWindow(_indexer!);

                if (_launcherWindow.IsVisible)
                    _launcherWindow.Hide();
                else
                    _launcherWindow.ShowAndActivate();
            });
        }

        private void OnStartupToggle(object? sender, EventArgs e)
        {
            if (StartupManager.IsEnabled())
                StartupManager.Disable();
            else
                StartupManager.Enable();
        }

        private void ExitApp()
        {
            _hotkeyManager?.Unregister();
            _trayIcon?.Dispose();
            _indexer?.Dispose();
            _mutex?.ReleaseMutex();
            Shutdown();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _trayIcon?.Dispose();
            base.OnExit(e);
        }
    }
}
