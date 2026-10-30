using System.Globalization;
using System.Text.Json;

namespace SystemMonitor.WidgetProvider;

internal static class WidgetCardBuilder
{
    private const string TemplateJson = """
        {
          "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
          "type": "AdaptiveCard",
          "version": "1.5",
          "fallbackText": "System Monitor is collecting system usage data.",
          "body": [
            {
              "type": "ColumnSet",
              "spacing": "none",
              "columns": [
                {
                  "type": "Column",
                  "width": "stretch",
                  "items": [
                    { "type": "TextBlock", "text": "SYSTEM MONITOR", "size": "small", "weight": "bolder", "color": "accent", "spacing": "none" }
                  ]
                },
                {
                  "type": "Column",
                  "width": "auto",
                  "verticalContentAlignment": "center",
                  "items": [
                    { "type": "TextBlock", "text": "${updatedAt}", "size": "small", "isSubtle": true, "spacing": "none" }
                  ]
                }
              ]
            },
            {
              "type": "Container",
              "style": "emphasis",
              "spacing": "small",
              "$when": "${$host.widgetSize==\"large\"}",
              "items": [
                {
                  "type": "ColumnSet",
                  "columns": [
                    { "type": "Column", "width": "stretch", "items": [ { "type": "TextBlock", "text": "CPU", "size": "small", "weight": "bolder", "color": "accent", "spacing": "none" } ] },
                    { "type": "Column", "width": "auto", "items": [ { "type": "TextBlock", "text": "${cpuLabel}", "size": "large", "weight": "bolder", "spacing": "none" } ] }
                  ]
                },
                { "type": "TextBlock", "text": "Overall processor activity", "size": "small", "isSubtle": true, "spacing": "none" },
                { "type": "TextBlock", "text": "${cpuBar}", "size": "small", "color": "accent", "fontType": "monospace", "spacing": "small", "wrap": false }
              ]
            },
            {
              "type": "Container",
              "style": "emphasis",
              "spacing": "small",
              "$when": "${$host.widgetSize==\"large\"}",
              "items": [
                {
                  "type": "ColumnSet",
                  "columns": [
                    { "type": "Column", "width": "stretch", "items": [ { "type": "TextBlock", "text": "MEMORY", "size": "small", "weight": "bolder", "color": "accent", "spacing": "none" } ] },
                    { "type": "Column", "width": "auto", "items": [ { "type": "TextBlock", "text": "${memoryPercent}%", "size": "large", "weight": "bolder", "spacing": "none" } ] }
                  ]
                },
                { "type": "TextBlock", "text": "${memoryUsed} used of ${memoryTotal}", "size": "small", "isSubtle": true, "spacing": "none" },
                { "type": "TextBlock", "text": "${memoryBar}", "size": "small", "color": "accent", "fontType": "monospace", "spacing": "small", "wrap": false }
              ]
            },
            {
              "type": "Container",
              "style": "emphasis",
              "spacing": "small",
              "$when": "${$host.widgetSize==\"large\"}",
              "items": [
                { "type": "TextBlock", "text": "GPU  ·  ${gpuLabel}", "size": "small", "weight": "bolder", "color": "accent", "spacing": "none" },
                { "type": "TextBlock", "text": "3D engine utilization", "size": "small", "spacing": "small", "wrap": true },
                { "type": "TextBlock", "text": "Dedicated memory: ${gpuDedicatedMemory}", "size": "small", "isSubtle": true, "spacing": "none", "wrap": true },
                { "type": "TextBlock", "text": "${gpuBar}", "size": "small", "color": "accent", "fontType": "monospace", "spacing": "small", "wrap": false }
              ]
            },
            {
              "type": "Container",
              "style": "emphasis",
              "spacing": "small",
              "$when": "${$host.widgetSize==\"large\"}",
              "items": [
                { "type": "TextBlock", "text": "NETWORK", "size": "small", "weight": "bolder", "color": "accent", "spacing": "none" },
                {
                  "type": "ColumnSet",
                  "spacing": "small",
                  "columns": [
                    { "type": "Column", "width": "stretch", "items": [ { "type": "TextBlock", "text": "↓  ${networkDown}", "size": "medium", "weight": "bolder", "spacing": "none" }, { "type": "TextBlock", "text": "RECEIVED", "size": "small", "isSubtle": true, "spacing": "none" } ] },
                    { "type": "Column", "width": "stretch", "items": [ { "type": "TextBlock", "text": "↑  ${networkUp}", "size": "medium", "weight": "bolder", "spacing": "none" }, { "type": "TextBlock", "text": "SENT", "size": "small", "isSubtle": true, "spacing": "none" } ] }
                  ]
                }
              ]
            },
            {
              "type": "ColumnSet",
              "spacing": "small",
              "$when": "${$host.widgetSize==\"medium\"}",
              "columns": [
                { "type": "Column", "width": "stretch", "style": "emphasis", "items": [ { "type": "TextBlock", "text": "CPU", "size": "small", "weight": "bolder", "color": "accent", "spacing": "none" }, { "type": "TextBlock", "text": "${cpuLabel}", "size": "large", "weight": "bolder", "spacing": "small" }, { "type": "TextBlock", "text": "${cpuBar}", "size": "small", "color": "accent", "fontType": "monospace", "spacing": "small", "wrap": false } ] },
                { "type": "Column", "width": "stretch", "style": "emphasis", "items": [ { "type": "TextBlock", "text": "MEMORY", "size": "small", "weight": "bolder", "color": "accent", "spacing": "none" }, { "type": "TextBlock", "text": "${memoryPercent}%", "size": "large", "weight": "bolder", "spacing": "small" }, { "type": "TextBlock", "text": "${memoryUsed} / ${memoryTotal}", "size": "small", "isSubtle": true, "spacing": "none", "wrap": true }, { "type": "TextBlock", "text": "${memoryBar}", "size": "small", "color": "accent", "fontType": "monospace", "spacing": "small", "wrap": false } ] }
              ]
            },
            {
              "type": "ColumnSet",
              "spacing": "small",
              "$when": "${$host.widgetSize==\"medium\"}",
              "columns": [
                { "type": "Column", "width": "stretch", "style": "emphasis", "items": [ { "type": "TextBlock", "text": "GPU · ${gpuLabel}", "size": "small", "weight": "bolder", "color": "accent", "spacing": "none" }, { "type": "TextBlock", "text": "3D ${gpuLabel} · VRAM ${gpuDedicatedMemory}", "size": "small", "spacing": "small", "wrap": true }, { "type": "TextBlock", "text": "${gpuBar}", "size": "small", "color": "accent", "fontType": "monospace", "spacing": "small", "wrap": false } ] },
                { "type": "Column", "width": "stretch", "style": "emphasis", "items": [ { "type": "TextBlock", "text": "NETWORK", "size": "small", "weight": "bolder", "color": "accent", "spacing": "none" }, { "type": "TextBlock", "text": "↓ ${networkDown}", "size": "small", "weight": "bolder", "spacing": "small" }, { "type": "TextBlock", "text": "↑ ${networkUp}", "size": "small", "isSubtle": true, "spacing": "none" } ] }
              ]
            },
            { "type": "TextBlock", "text": "${sampleStatus}", "size": "small", "isSubtle": true, "spacing": "small", "wrap": true }
          ]
        }
        """;

    public static string BuildTemplate() => TemplateJson;

    public static string BuildData(SystemMetrics metrics)
    {
        var memoryPercent = metrics.MemoryTotalBytes == 0
            ? 0
            : Math.Clamp((int)Math.Round(metrics.MemoryUsedBytes * 100d / metrics.MemoryTotalBytes), 0, 100);
        var data = new
        {
            cpuLabel = $"{metrics.CpuPercent:0}%",
            cpuBar = FormatBar(metrics.CpuPercent),
            memoryPercent,
            memoryUsed = FormatBytes(metrics.MemoryUsedBytes),
            memoryTotal = FormatBytes(metrics.MemoryTotalBytes),
            memoryBar = FormatBar(memoryPercent),
            gpuLabel = metrics.GpuUtilizationPercent is double gpuPercent ? $"{gpuPercent:0}%" : "—",
            gpuDedicatedMemory = metrics.GpuDedicatedMemoryBytes is ulong gpuMemory ? FormatBytes(gpuMemory) : "—",
            gpuBar = FormatBar(metrics.GpuUtilizationPercent ?? 0d),
            networkDown = FormatRate(metrics.NetworkReceivedBytesPerSecond),
            networkUp = FormatRate(metrics.NetworkSentBytesPerSecond),
            updatedAt = metrics.SampledAt.ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture),
            sampleStatus = "Updates while the Widgets Board is active"
        };

        return JsonSerializer.Serialize(data);
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

    private static string FormatBar(double percent)
    {
        const int segmentCount = 16;
        var filledSegments = Math.Clamp((int)Math.Round(percent / 100d * segmentCount), 0, segmentCount);
        return new string('■', filledSegments) + new string('□', segmentCount - filledSegments);
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
}
