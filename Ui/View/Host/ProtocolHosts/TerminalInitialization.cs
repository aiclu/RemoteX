using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Shawn.Utils;

namespace _1RM.View.Host.ProtocolHosts;

internal static class TerminalInitialization
{
    internal const string MissingDependency = "TerminalDependencyMissing";
    internal const string InvalidDependency = "TerminalDependencyInvalid";
    internal const string Failed = "TerminalInitializationFailed";

    // XAML/reflection/type initializers can wrap a native loader failure several times.
    internal static string MessageKey(Exception error)
    {
        var errors = ExceptionChain(error).ToArray();
        if (errors.Any(e => e is DllNotFoundException or FileNotFoundException))
            return MissingDependency;
        if (errors.Any(e => e is BadImageFormatException or EntryPointNotFoundException or FileLoadException))
            return InvalidDependency;
        return Failed;
    }

    internal static bool TryRun(Action initialize, Action cleanup, Action<Exception> showFailure)
    {
        try
        {
            initialize();
            return true;
        }
        catch (Exception error)
        {
            LogFailure("initialize", error);
            Cleanup(cleanup);
            showFailure(error);
            return false;
        }
    }

    internal static void Cleanup(Action cleanup)
    {
        try { cleanup(); }
        catch (Exception error) { LogFailure("cleanup", error); }
    }

    internal static string Diagnostic(Exception error) => string.Join(Environment.NewLine,
        ExceptionChain(error).Select(e => $"{e.GetType().FullName} (HRESULT 0x{e.HResult:X8})\n{e.StackTrace}"));

    internal static void LogFailure(string stage, Exception error)
    {
        // Do not log exception messages, connection parameters, credentials or commands.
        SimpleLogHelper.Error($"Terminal {stage} failed: {Diagnostic(error)}");
    }

    private static IEnumerable<Exception> ExceptionChain(Exception error)
    {
        yield return error;
        if (error is AggregateException aggregate)
        {
            foreach (var inner in aggregate.InnerExceptions)
            foreach (var nested in ExceptionChain(inner))
                yield return nested;
        }
        else if (error.InnerException != null)
        {
            foreach (var nested in ExceptionChain(error.InnerException))
                yield return nested;
        }
    }
}
