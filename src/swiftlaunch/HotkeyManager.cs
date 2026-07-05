using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace SwiftLaunch
{
    public class HotkeyManager : IDisposable
    {
        public event EventHandler? HotkeyPressed;

        private const int WM_HOTKEY = 0x0312;
        private const int HOTKEY_ID = 9000;

        // Modifier keys
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_SHIFT = 0x0004;
        private const uint MOD_ALT = 0x0001;
        private const uint MOD_WIN = 0x0008;

        // VK codes
        private const uint VK_SPACE = 0x20;
        private const uint VK_OEM_3 = 0xC0; // backtick/tilde

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private HwndSource? _source;
        private IntPtr _hwnd;
        private bool _registered;

        public void Register()
        {
            // Create an invisible message-only window
            var helper = new WindowInteropHelper(new Window());
            helper.EnsureHandle();
            _hwnd = helper.Handle;

            // Actually we'll use a dedicated message pump
            CreateMessageWindow();
        }

        private void CreateMessageWindow()
        {
            // Use a hidden WPF window as our message receiver
            var win = new Window
            {
                Width = 0, Height = 0,
                WindowStyle = WindowStyle.None,
                ShowInTaskbar = false,
                AllowsTransparency = true,
                Opacity = 0
            };
            win.Show();
            win.Hide();

            var helper = new WindowInteropHelper(win);
            _hwnd = helper.Handle;
            _source = HwndSource.FromHwnd(_hwnd);
            _source.AddHook(WndProc);

            // Try Ctrl+Space first, fall back to Ctrl+Shift+Space
            _registered = RegisterHotKey(_hwnd, HOTKEY_ID, MOD_CONTROL, VK_SPACE);
            if (!_registered)
            {
                // Ctrl+Space may be taken; try Alt+Space
                _registered = RegisterHotKey(_hwnd, HOTKEY_ID, MOD_ALT, VK_SPACE);
                if (!_registered)
                {
                    // Try Ctrl+Shift+Space as last resort
                    _registered = RegisterHotKey(_hwnd, HOTKEY_ID, MOD_CONTROL | MOD_SHIFT, VK_SPACE);
                }
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
            {
                HotkeyPressed?.Invoke(this, EventArgs.Empty);
                handled = true;
            }
            return IntPtr.Zero;
        }

        public void Unregister()
        {
            if (_registered)
            {
                UnregisterHotKey(_hwnd, HOTKEY_ID);
                _registered = false;
            }
        }

        public void Dispose()
        {
            Unregister();
            _source?.Dispose();
        }
    }
}
