using System.Globalization;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using Microsoft.Win32;
using SystemMonitor.WidgetProvider;
using Forms = System.Windows.Forms;

namespace SystemMonitor.DesktopWidget;

public partial class MainWindow : Window
{
    private readonly SystemMetricsReader _metricsReader = new();
    private readonly DispatcherTimer _refreshTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    private readonly Forms.NotifyIcon _trayIcon = new();
    private Forms.ToolStripMenuItem _trayStartupItem = null!;
    private bool _allowExit;

    private const int WmMouseActivate = 0x0021;
    private const int MaNoActivate = 3;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoActivate = 0x0010;

    public MainWindow()
    {
        InitializeComponent();

        ConfigureTrayIcon();
        StartWithWindowsMenuItem.IsChecked = StartupRegistration.IsEnabled();

        var workArea = SystemParameters.WorkArea;
        Left = Math.Max(workArea.Left + 12, workArea.Right - Width - 24);
        Top = workArea.Top + 20;
        Loaded += (_, _) =>
        {
            PlaceBehindForegroundWindow();
            DesktopLog.Write($"Widget window shown on the desktop at {Left:0},{Top:0}.");
        };

        _refreshTimer.Tick += RefreshTimer_Tick;
        _refreshTimer.Start();
        RefreshMetrics(this, EventArgs.Empty);
        Closing += MainWindow_Closing;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        HwndSource.FromHwnd(new WindowInteropHelper(this).Handle)?.AddHook(WindowProc);
    }

    private void ConfigureTrayIcon()
    {
        var trayMenu = new Forms.ContextMenuStrip();
        trayMenu.Items.Add("Show widget", null, (_, _) => ShowWidget());

        _trayStartupItem = new Forms.ToolStripMenuItem("Start with Windows")
        {
            CheckOnClick = true,
            Checked = StartupRegistration.IsEnabled()
        };
        _trayStartupItem.CheckedChanged += (_, _) => SetStartupEnabled(_trayStartupItem.Checked);
        trayMenu.Items.Add(_trayStartupItem);
        trayMenu.Items.Add(new Forms.ToolStripSeparator());
        trayMenu.Items.Add("Exit", null, (_, _) => ExitApplication());

        _trayIcon.Icon = System.Drawing.SystemIcons.Application;
        _trayIcon.Text = "System Monitor";
        _trayIcon.ContextMenuStrip = trayMenu;
        _trayIcon.DoubleClick += (_, _) => ShowWidget();
        _trayIcon.Visible = true;
    }

    private void ShowWidget()
    {
        Show();
        WindowState = WindowState.Normal;
        PlaceBehindForegroundWindow();
    }

    private void HideWidget() => Hide();

    private void PlaceBehindForegroundWindow()
    {
        var widgetHandle = new WindowInteropHelper(this).Handle;
        var foregroundHandle = GetForegroundWindow();
        if (widgetHandle == IntPtr.Zero || foregroundHandle == widgetHandle)
        {
            return;
        }

        var className = new System.Text.StringBuilder(256);
        _ = GetClassName(foregroundHandle, className, className.Capacity);
        var foregroundIsDesktop = className.ToString() is "Progman" or "WorkerW" or "SHELLDLL_DefView";
        var insertAfter = foregroundIsDesktop ? IntPtr.Zero : foregroundHandle;
        const uint flags = SwpNoMove | SwpNoSize | SwpNoActivate;

        if (!SetWindowPos(widgetHandle, insertAfter, 0, 0, 0, 0, flags))
        {
            DesktopLog.Write($"Could not place widget behind the foreground window (error {Marshal.GetLastWin32Error()}).");
        }
    }

    private static IntPtr WindowProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == WmMouseActivate)
        {
            handled = true;
            return new IntPtr(MaNoActivate);
        }

        return IntPtr.Zero;
    }

    private void SetStartupEnabled(bool enabled)
    {
        if (!StartupRegistration.TrySetEnabled(enabled))
        {
            var currentState = StartupRegistration.IsEnabled();
            StartWithWindowsMenuItem.IsChecked = currentState;
            if (_trayStartupItem is not null)
            {
                _trayStartupItem.Checked = currentState;
            }

            Forms.MessageBox.Show(
                "Windows did not allow changing the startup setting. You can add a shortcut to this app in the Startup folder instead.",
                "System Monitor",
                Forms.MessageBoxButtons.OK,
                Forms.MessageBoxIcon.Information);
            return;
        }

        StartWithWindowsMenuItem.IsChecked = enabled;
        if (_trayStartupItem is not null)
        {
            _trayStartupItem.Checked = enabled;
        }
    }

    private void ExitApplication()
    {
        _allowExit = true;
        Close();
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (!_allowExit)
        {
            e.Cancel = true;
            HideWidget();
            return;
        }

        _refreshTimer.Stop();
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        _metricsReader.Dispose();
    }

    private void RefreshTimer_Tick(object? sender, EventArgs e) => RefreshMetrics(sender, e);

    private void RefreshMetrics(object? sender, EventArgs e)
    {
        var metrics = _metricsReader.Read();
        var memoryPercent = metrics.MemoryTotalBytes == 0
            ? 0
            : Math.Clamp(metrics.MemoryUsedBytes * 100d / metrics.MemoryTotalBytes, 0d, 100d);

        CpuValue.Text = $"{metrics.CpuPercent:0}%";
        CpuBar.Value = metrics.CpuPercent;
        MemoryPercent.Text = $"{memoryPercent:0}%";
        MemoryBar.Value = memoryPercent;
        MemoryUsed.Text = $"{FormatBytes(metrics.MemoryUsedBytes)} / {FormatBytes(metrics.MemoryTotalBytes)}";
        GpuPercent.Text = metrics.GpuUtilizationPercent is double gpuPercent ? $"{gpuPercent:0}%" : "—";
        GpuMemory.Text = metrics.GpuDedicatedMemoryBytes is ulong gpuMemory ? FormatBytes(gpuMemory) : "—";
        NetworkDown.Text = FormatRate(metrics.NetworkReceivedBytesPerSecond);
        NetworkUp.Text = FormatRate(metrics.NetworkSentBytesPerSecond);
        UpdatedAt.Text = $"Updated {metrics.SampledAt.ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture)}  ·  Right-click for options";
    }

    private static string FormatRate(double bytesPerSecond)
    {
        if (!double.IsFinite(bytesPerSecond) || bytesPerSecond <= 0)
        {
            return "0 B/s";
        }

        var units = new[] { "B/s", "KB/s", "MB/s", "GB/s" };
        var value = bytesPerSecond;
        var unitIndex = 0;
        while (value >= 1024 && unitIndex < units.Length - 1)
        {
            value /= 1024;
            unitIndex++;
        }

        return $"{value:0.#} {units[unitIndex]}";
    }

    private static string FormatBytes(ulong bytes)
    {
        if (bytes == 0)
        {
            return "—";
        }

        var units = new[] { "B", "KB", "MB", "GB", "TB", "PB" };
        var value = (double)bytes;
        var unitIndex = 0;
        while (value >= 1024 && unitIndex < units.Length - 1)
        {
            value /= 1024;
            unitIndex++;
        }

        return $"{value:0.#} {units[unitIndex]}";
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not System.Windows.Controls.Button
            && e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void StartWithWindowsMenuItem_Click(object sender, RoutedEventArgs e) => SetStartupEnabled(StartWithWindowsMenuItem.IsChecked == true);

    private void HideWidget_Click(object sender, RoutedEventArgs e) => HideWidget();

    private void Exit_Click(object sender, RoutedEventArgs e) => ExitApplication();

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", EntryPoint = "GetClassNameW", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int GetClassName(IntPtr window, System.Text.StringBuilder className, int maxCount);

    [DllImport("user32.dll", SetLastError = true, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter, int x, int y, int width, int height, uint flags);

    private sealed class StartupRegistration
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "SystemMonitorDesktopWidget";

        public static bool IsEnabled()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
                var registeredPath = key?.GetValue(ValueName) as string;
                var currentPath = Environment.ProcessPath;
                return !string.IsNullOrWhiteSpace(registeredPath)
                    && !string.IsNullOrWhiteSpace(currentPath)
                    && string.Equals(registeredPath.Trim('"'), currentPath, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception exception) when (exception is System.Security.SecurityException or UnauthorizedAccessException)
            {
                System.Diagnostics.Trace.TraceWarning($"Could not read the startup setting: {exception.Message}");
                return false;
            }
        }

        public static bool TrySetEnabled(bool enabled)
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
                if (enabled)
                {
                    var executablePath = Environment.ProcessPath;
                    if (string.IsNullOrWhiteSpace(executablePath))
                    {
                        return false;
                    }

                    key.SetValue(ValueName, $"\"{executablePath}\"", RegistryValueKind.String);
                }
                else
                {
                    key.DeleteValue(ValueName, throwOnMissingValue: false);
                }

                return true;
            }
            catch (Exception exception) when (exception is System.Security.SecurityException or UnauthorizedAccessException)
            {
                System.Diagnostics.Trace.TraceWarning($"Could not update the startup setting: {exception.Message}");
                return false;
            }
        }
    }
}
