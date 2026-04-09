using Microsoft.CommandPalette.Extensions;
using Shmuelie.WinRTServer;
using Shmuelie.WinRTServer.CsWinRT;
using System;
using System.Diagnostics;
using System.Threading;
using xRate.Core.Helpers;

namespace xRateExt;

public class Program
{
    [MTAThread]
    public static void Main(string[] args)
    {
        PathHelper.Initialize();

        if (args.Length > 0 && args[0] == "-RegisterProcessAsComServer")
        {
            global::Shmuelie.WinRTServer.ComServer server = new();

            ManualResetEvent extensionDisposedEvent = new(false);

            xRateExt extensionInstance = new(extensionDisposedEvent);
            server.RegisterClass<xRateExt, IExtension>(() => extensionInstance);
            server.Start();

            extensionDisposedEvent.WaitOne();
            server.Stop();
            server.UnsafeDispose();
        }
    }
}