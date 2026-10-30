using System.Runtime.InteropServices;
using Microsoft.Windows.Widgets.Providers;
using WinRT;

namespace SystemMonitor.WidgetProvider;

internal static class Program
{
    internal const string WidgetProviderClassId = "A12D9783-0A6D-48CF-B2D7-6B75135AA2F7";

    private const uint ClsctxLocalServer = 0x4;
    private const uint RegclsMultipleUse = 0x1;
    private const int CoInitMultithreaded = 0x0;

    [DllImport("ole32.dll", ExactSpelling = true)]
    private static extern int CoInitializeEx(IntPtr reserved, int coInit);

    [DllImport("ole32.dll", ExactSpelling = true)]
    private static extern void CoUninitialize();

    [DllImport("ole32.dll", ExactSpelling = true)]
    private static extern int CoRegisterClassObject(
        [MarshalAs(UnmanagedType.LPStruct)] Guid classId,
        [MarshalAs(UnmanagedType.IUnknown)] object classFactory,
        uint classContext,
        uint flags,
        out uint registrationCookie);

    [DllImport("ole32.dll", ExactSpelling = true)]
    private static extern int CoRevokeClassObject(uint registrationCookie);

    private static int Main()
    {
        var coInitResult = CoInitializeEx(IntPtr.Zero, CoInitMultithreaded);
        if (coInitResult < 0)
        {
            Marshal.ThrowExceptionForHR(coInitResult);
        }

        try
        {
            ComWrappersSupport.InitializeComWrappers();
            WidgetProvider.InitializeExistingWidgets();

            var factory = new WidgetProviderFactory<WidgetProvider>();
            var classId = Guid.Parse(WidgetProviderClassId);
            Marshal.ThrowExceptionForHR(
                CoRegisterClassObject(
                    classId,
                    factory,
                    ClsctxLocalServer,
                    RegclsMultipleUse,
                    out var registrationCookie));

            try
            {
                WidgetProvider.WaitForLastWidgetToBeRemoved();
            }
            finally
            {
                var revokeResult = CoRevokeClassObject(registrationCookie);
                if (revokeResult < 0)
                {
                    System.Diagnostics.Trace.TraceError(
                        $"CoRevokeClassObject failed: 0x{revokeResult:X8}");
                }

                GC.KeepAlive(factory);
            }

            return 0;
        }
        catch (Exception exception)
        {
            System.Diagnostics.Trace.TraceError(exception.ToString());
            return Marshal.GetHRForException(exception);
        }
        finally
        {
            CoUninitialize();
        }
    }

    [ComImport]
    [ComVisible(false)]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("00000001-0000-0000-C000-000000000046")]
    private interface IClassFactory
    {
        [PreserveSig]
        int CreateInstance(IntPtr outerUnknown, ref Guid interfaceId, out IntPtr instance);

        [PreserveSig]
        int LockServer(bool shouldLock);
    }

    [ComVisible(true)]
    private sealed class WidgetProviderFactory<T> : IClassFactory where T : IWidgetProvider, new()
    {
        private const int ClassENoAggregation = unchecked((int)0x80040110);

        public int CreateInstance(IntPtr outerUnknown, ref Guid interfaceId, out IntPtr instance)
        {
            instance = IntPtr.Zero;
            if (outerUnknown != IntPtr.Zero)
            {
                return ClassENoAggregation;
            }

            IntPtr provider = IntPtr.Zero;
            try
            {
                provider = MarshalInspectable<IWidgetProvider>.FromManaged(new T());
                return Marshal.QueryInterface(provider, in interfaceId, out instance);
            }
            catch (Exception exception)
            {
                System.Diagnostics.Trace.TraceError(exception.ToString());
                return Marshal.GetHRForException(exception);
            }
            finally
            {
                if (provider != IntPtr.Zero)
                {
                    Marshal.Release(provider);
                }
            }
        }

        int IClassFactory.LockServer(bool shouldLock) => 0;
    }
}
