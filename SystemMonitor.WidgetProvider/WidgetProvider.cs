using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Windows.Widgets.Providers;

namespace SystemMonitor.WidgetProvider;

internal sealed class WidgetProvider : IWidgetProvider
{
    internal const string DefinitionId = "SystemMonitor_Widget";
    private static readonly ConcurrentDictionary<string, WidgetSession> Sessions = new();
    private static readonly object InitializationLock = new();
    private static readonly object StateLock = new();
    private static readonly ManualResetEvent LastWidgetRemoved = new(false);
    private static readonly SystemMetricsReader MetricsReader = new();
    private static bool _initialized;

    public WidgetProvider()
    {
        InitializeExistingWidgets();
    }

    internal static void InitializeExistingWidgets()
    {
        lock (InitializationLock)
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;
        }

        try
        {
            foreach (var widgetInfo in WidgetManager.GetDefault().GetWidgetInfos())
            {
                var context = widgetInfo.WidgetContext;
                if (context.DefinitionId == DefinitionId)
                {
                    AddSession(context.Id, context.DefinitionId);
                }
            }
        }
        catch (Exception exception)
        {
            Trace.TraceWarning($"Could not restore pinned widgets: {exception}");
        }
    }

    internal static void WaitForLastWidgetToBeRemoved()
    {
        while (true)
        {
            LastWidgetRemoved.WaitOne();
            Thread.Sleep(TimeSpan.FromSeconds(5));

            lock (StateLock)
            {
                if (Sessions.IsEmpty)
                {
                    return;
                }

                LastWidgetRemoved.Reset();
            }
        }
    }

    public void CreateWidget(WidgetContext widgetContext)
    {
        if (widgetContext.DefinitionId != DefinitionId)
        {
            return;
        }

        var session = AddSession(widgetContext.Id, widgetContext.DefinitionId);
        UpdateWidget(session.Id);
    }

    public void DeleteWidget(string widgetId, string customState)
    {
        lock (StateLock)
        {
            if (Sessions.TryRemove(widgetId, out var session))
            {
                lock (session.Sync)
                {
                    session.Timer?.Dispose();
                    session.Timer = null;
                    session.IsActive = false;
                }
            }

            if (Sessions.IsEmpty)
            {
                LastWidgetRemoved.Set();
            }
        }
    }

    public void Activate(WidgetContext widgetContext)
    {
        if (widgetContext.DefinitionId != DefinitionId)
        {
            return;
        }

        var session = AddSession(widgetContext.Id, widgetContext.DefinitionId);
        lock (session.Sync)
        {
            session.IsActive = true;
            session.Timer ??= new Timer(
                static state => UpdateWidget((string)state!),
                session.Id,
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(2));
        }

        UpdateWidget(session.Id);
    }

    public void Deactivate(string widgetId)
    {
        if (!Sessions.TryGetValue(widgetId, out var session))
        {
            return;
        }

        lock (session.Sync)
        {
            session.IsActive = false;
            session.Timer?.Dispose();
            session.Timer = null;
        }
    }

    public void OnActionInvoked(WidgetActionInvokedArgs actionInvokedArgs)
    {
        // The monitor card is read-only, so it doesn't define any actions.
    }

    public void OnWidgetContextChanged(WidgetContextChangedArgs contextChangedArgs)
    {
        var context = contextChangedArgs.WidgetContext;
        if (context.DefinitionId != DefinitionId || !Sessions.ContainsKey(context.Id))
        {
            return;
        }

        UpdateWidget(context.Id);
    }

    private static WidgetSession AddSession(string id, string definitionId)
    {
        lock (StateLock)
        {
            LastWidgetRemoved.Reset();
            return Sessions.GetOrAdd(id, static (widgetId, state) => new WidgetSession(widgetId, state), definitionId);
        }
    }

    private static void UpdateWidget(string widgetId)
    {
        if (!Sessions.TryGetValue(widgetId, out var session) || session.DefinitionId != DefinitionId)
        {
            return;
        }

        try
        {
            var metrics = MetricsReader.Read();
            var request = new WidgetUpdateRequestOptions(widgetId)
            {
                Template = WidgetCardBuilder.BuildTemplate(),
                Data = WidgetCardBuilder.BuildData(metrics),
                CustomState = string.Empty
            };

            WidgetManager.GetDefault().UpdateWidget(request);
        }
        catch (Exception exception)
        {
            Trace.TraceError($"Could not update widget {widgetId}: {exception}");
        }
    }

    private sealed class WidgetSession(string id, string definitionId)
    {
        public string Id { get; } = id;
        public string DefinitionId { get; } = definitionId;
        public object Sync { get; } = new();
        public bool IsActive { get; set; }
        public Timer? Timer { get; set; }
    }
}
