using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace SystemMonitor.DesktopWidget;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        DesktopLog.Write("Application startup requested.");
        DispatcherUnhandledException += (_, args) => DesktopLog.Write($"Unhandled UI exception: {args.Exception}");
        AppDomain.CurrentDomain.UnhandledException += (_, args) => DesktopLog.Write($"Unhandled process exception: {args.ExceptionObject}");
        base.OnStartup(e);
    }
}

internal static class DesktopLog
{
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SystemMonitorWidget",
        "desktop-widget.log");

    public static void Write(string message)
    {
        try
        {
            var directory = Path.GetDirectoryName(LogPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.AppendAllText(LogPath, $"{DateTimeOffset.Now:O} {message}{Environment.NewLine}");
        }
        catch (Exception)
        {
            // Logging must never prevent the desktop widget from opening.
        }
    }
}
