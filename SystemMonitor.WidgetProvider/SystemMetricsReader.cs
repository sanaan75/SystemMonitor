using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;

namespace SystemMonitor.WidgetProvider;

internal sealed class SystemMetricsReader : IDisposable
{
    private readonly object _sync = new();
    private readonly GpuCounterQuery? _gpuCounters = GpuCounterQuery.TryCreate();

    private long? _previousIdleTime;
    private long? _previousKernelTime;
    private long? _previousUserTime;
    private NetworkSample? _previousNetworkSample;

    public SystemMetrics Read()
    {
        lock (_sync)
        {
            var cpuPercent = ReadCpuPercent();
            var memory = ReadMemory();
            var gpu = _gpuCounters?.Read() ?? (UsagePercent: (double?)null, DedicatedMemoryBytes: (ulong?)null);
            var network = ReadNetworkRates();

            return new SystemMetrics(
                CpuPercent: cpuPercent,
                MemoryUsedBytes: memory.UsedBytes,
                MemoryTotalBytes: memory.TotalBytes,
                GpuUtilizationPercent: gpu.UsagePercent,
                GpuDedicatedMemoryBytes: gpu.DedicatedMemoryBytes,
                NetworkReceivedBytesPerSecond: network.ReceivedBytesPerSecond,
                NetworkSentBytesPerSecond: network.SentBytesPerSecond,
                SampledAt: DateTimeOffset.Now);
        }
    }

    private double ReadCpuPercent()
    {
        if (!GetSystemTimes(out var idle, out var kernel, out var user))
        {
            return 0;
        }

        var idleTime = idle.ToInt64();
        var kernelTime = kernel.ToInt64();
        var userTime = user.ToInt64();

        if (_previousIdleTime is null || _previousKernelTime is null || _previousUserTime is null)
        {
            _previousIdleTime = idleTime;
            _previousKernelTime = kernelTime;
            _previousUserTime = userTime;
            return 0;
        }

        var idleDelta = Math.Max(0, idleTime - _previousIdleTime.Value);
        var totalDelta = Math.Max(0, kernelTime - _previousKernelTime.Value)
            + Math.Max(0, userTime - _previousUserTime.Value);

        _previousIdleTime = idleTime;
        _previousKernelTime = kernelTime;
        _previousUserTime = userTime;

        if (totalDelta <= 0)
        {
            return 0;
        }

        return Math.Clamp((totalDelta - idleDelta) * 100d / totalDelta, 0d, 100d);
    }

    private static (ulong UsedBytes, ulong TotalBytes) ReadMemory()
    {
        var status = new MemoryStatusEx
        {
            Length = (uint)Marshal.SizeOf<MemoryStatusEx>()
        };

        if (!GlobalMemoryStatusEx(ref status))
        {
            return (0, 0);
        }

        return (status.TotalPhys - status.AvailPhys, status.TotalPhys);
    }

    private (double ReceivedBytesPerSecond, double SentBytesPerSecond) ReadNetworkRates()
    {
        long totalReceived = 0;
        long totalSent = 0;

        foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (adapter.OperationalStatus != OperationalStatus.Up
                || adapter.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
            {
                continue;
            }

            try
            {
                var statistics = adapter.GetIPStatistics();
                totalReceived += Math.Max(0, statistics.BytesReceived);
                totalSent += Math.Max(0, statistics.BytesSent);
            }
            catch (NetworkInformationException exception)
            {
                Trace.TraceWarning($"Could not read network statistics for {adapter.Name}: {exception.Message}");
            }
        }

        var now = Stopwatch.GetTimestamp();
        var current = new NetworkSample(totalReceived, totalSent, now);
        var previous = _previousNetworkSample;
        _previousNetworkSample = current;

        if (previous is null)
        {
            return (0, 0);
        }

        var elapsedSeconds = (now - previous.Timestamp) / (double)Stopwatch.Frequency;
        if (elapsedSeconds <= 0)
        {
            return (0, 0);
        }

        return (
            Math.Max(0, totalReceived - previous.BytesReceived) / elapsedSeconds,
            Math.Max(0, totalSent - previous.BytesSent) / elapsedSeconds);
    }

    private static double NonNegative(double value) => double.IsFinite(value) ? Math.Max(0, value) : 0;

    public void Dispose()
    {
        _gpuCounters?.Dispose();
    }

    [DllImport("kernel32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(out NativeFileTime idleTime, out NativeFileTime kernelTime, out NativeFileTime userTime);

    [DllImport("kernel32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeFileTime
    {
        public uint LowDateTime;
        public uint HighDateTime;

        public readonly long ToInt64() => ((long)HighDateTime << 32) | LowDateTime;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhys;
        public ulong AvailPhys;
        public ulong TotalPageFile;
        public ulong AvailPageFile;
        public ulong TotalVirtual;
        public ulong AvailVirtual;
        public ulong AvailExtendedVirtual;
    }

    private sealed record NetworkSample(long BytesReceived, long BytesSent, long Timestamp);

    private sealed class GpuCounterQuery : IDisposable
    {
        private const uint PdhFmtDouble = 0x00000200;
        private const uint PdhFmtNoCap100 = 0x00000800;
        private const uint PdhMoreData = 0x800007D2;
        private const uint PdhStatusNewData = 0x00000001;

        private readonly IntPtr _query;
        private readonly IntPtr _utilizationCounter;
        private readonly IntPtr _dedicatedMemoryCounter;
        private bool _disposed;

        private GpuCounterQuery(IntPtr query, IntPtr utilizationCounter, IntPtr dedicatedMemoryCounter)
        {
            _query = query;
            _utilizationCounter = utilizationCounter;
            _dedicatedMemoryCounter = dedicatedMemoryCounter;
        }

        public static GpuCounterQuery? TryCreate()
        {
            if (PdhOpenQuery(null, IntPtr.Zero, out var query) != 0)
            {
                return null;
            }

            if (PdhAddEnglishCounter(query, @"\GPU Engine(*)\Utilization Percentage", 0, out var utilizationCounter) != 0)
            {
                PdhCloseQuery(query);
                return null;
            }

            var dedicatedMemoryCounter = PdhAddEnglishCounter(
                query,
                @"\GPU Adapter Memory(*)\Dedicated Usage",
                0,
                out var counter) == 0
                ? counter
                : IntPtr.Zero;

            return new GpuCounterQuery(query, utilizationCounter, dedicatedMemoryCounter);
        }

        public (double? UsagePercent, ulong? DedicatedMemoryBytes) Read()
        {
            if (_disposed || PdhCollectQueryData(_query) != 0)
            {
                return (null, null);
            }

            var utilizationValues = ReadCounterArray(_utilizationCounter);
            if (utilizationValues is null)
            {
                return (null, null);
            }

            var usagePercent = utilizationValues
                .Where(value => value.InstanceName.Contains("engtype_3D", StringComparison.OrdinalIgnoreCase))
                .Sum(value => value.Value);
            var dedicatedMemoryValues = _dedicatedMemoryCounter == IntPtr.Zero
                ? null
                : ReadCounterArray(_dedicatedMemoryCounter);
            ulong? dedicatedMemoryBytes = dedicatedMemoryValues is null
                ? null
                : (ulong)Math.Clamp(dedicatedMemoryValues.Sum(value => value.Value), 0d, ulong.MaxValue);

            return (Math.Clamp(usagePercent, 0d, 100d), dedicatedMemoryBytes);
        }

        private static List<CounterValue>? ReadCounterArray(IntPtr counter)
        {
            uint bufferSize = 0;
            uint itemCount = 0;
            var result = PdhGetFormattedCounterArray(
                counter,
                PdhFmtDouble | PdhFmtNoCap100,
                ref bufferSize,
                ref itemCount,
                IntPtr.Zero);

            if (result == 0 && itemCount == 0)
            {
                return [];
            }

            if (result != PdhMoreData)
            {
                return null;
            }

            for (var attempt = 0; attempt < 2; attempt++)
            {
                if (bufferSize == 0 || bufferSize > int.MaxValue)
                {
                    return [];
                }

                var buffer = Marshal.AllocHGlobal((int)bufferSize);
                try
                {
                    result = PdhGetFormattedCounterArray(
                        counter,
                        PdhFmtDouble | PdhFmtNoCap100,
                        ref bufferSize,
                        ref itemCount,
                        buffer);

                    if (result == PdhMoreData)
                    {
                        continue;
                    }

                    if (result != 0)
                    {
                        return null;
                    }

                    var values = new List<CounterValue>((int)itemCount);
                    var itemSize = Marshal.SizeOf<PdhFormattedCounterValueItem>();
                    for (var index = 0; index < itemCount; index++)
                    {
                        var itemPointer = IntPtr.Add(buffer, checked((int)index * itemSize));
                        var item = Marshal.PtrToStructure<PdhFormattedCounterValueItem>(itemPointer);
                        if ((item.Value.Status is 0 or PdhStatusNewData)
                            && item.Name != IntPtr.Zero
                            && double.IsFinite(item.Value.DoubleValue))
                        {
                            values.Add(new CounterValue(
                                Marshal.PtrToStringUni(item.Name) ?? string.Empty,
                                Math.Max(0d, item.Value.DoubleValue)));
                        }
                    }

                    return values;
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }

            return null;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                PdhCloseQuery(_query);
            }
        }

        [DllImport("pdh.dll", EntryPoint = "PdhOpenQueryW", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern uint PdhOpenQuery(string? dataSource, IntPtr userData, out IntPtr query);

        [DllImport("pdh.dll", EntryPoint = "PdhAddEnglishCounterW", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern uint PdhAddEnglishCounter(IntPtr query, string fullCounterPath, IntPtr userData, out IntPtr counter);

        [DllImport("pdh.dll", ExactSpelling = true)]
        private static extern uint PdhCollectQueryData(IntPtr query);

        [DllImport("pdh.dll", ExactSpelling = true)]
        private static extern uint PdhCloseQuery(IntPtr query);

        [DllImport("pdh.dll", EntryPoint = "PdhGetFormattedCounterArrayW", ExactSpelling = true)]
        private static extern uint PdhGetFormattedCounterArray(
            IntPtr counter,
            uint format,
            ref uint bufferSize,
            ref uint itemCount,
            IntPtr itemBuffer);

        [StructLayout(LayoutKind.Explicit, Size = 16)]
        private struct PdhFormattedCounterValue
        {
            [FieldOffset(0)]
            public uint Status;

            [FieldOffset(8)]
            public double DoubleValue;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PdhFormattedCounterValueItem
        {
            public IntPtr Name;
            public PdhFormattedCounterValue Value;
        }

        private sealed record CounterValue(string InstanceName, double Value);
    }
}

internal sealed record SystemMetrics(
    double CpuPercent,
    ulong MemoryUsedBytes,
    ulong MemoryTotalBytes,
    double? GpuUtilizationPercent,
    ulong? GpuDedicatedMemoryBytes,
    double NetworkReceivedBytesPerSecond,
    double NetworkSentBytesPerSecond,
    DateTimeOffset SampledAt);
