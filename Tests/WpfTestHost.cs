using System;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests;

// Compiled UserControls resolve constructor-time StaticResources through Application.
// WPF permits only one Application per process, so these tests share its dedicated STA.
[TestClass]
public sealed class WpfTestHost
{
    private static readonly Lazy<Dispatcher> Ui = new(() =>
    {
        using var ready = new ManualResetEventSlim();
        Dispatcher? dispatcher = null;
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                _ = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                dispatcher = Dispatcher.CurrentDispatcher;
            }
            catch (Exception e) { failure = e; }
            finally { ready.Set(); }
            if (dispatcher != null) Dispatcher.Run();
        }) { IsBackground = true, Name = "RemoteX WPF tests" };
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        if (!ready.Wait(TimeSpan.FromSeconds(30))) throw new TimeoutException("WPF test host did not start.");
        if (failure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        return dispatcher!;
    });

    public static void Run(Action action)
    {
        var operation = Ui.Value.InvokeAsync(action);
        operation.Task.WaitAsync(TimeSpan.FromSeconds(30)).GetAwaiter().GetResult();
    }

    [AssemblyCleanup]
    public static void Shutdown()
    {
        if (Ui.IsValueCreated && !Ui.Value.HasShutdownStarted)
            Ui.Value.Invoke(() => Application.Current.Shutdown());
    }
}
