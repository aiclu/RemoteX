using System;
using System.IO;
using System.Reflection;
using System.Windows.Markup;
using _1RM.View.Host.ProtocolHosts;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.Service;

[TestClass]
public class TerminalInitializationTests
{
    [TestMethod]
    public void MissingDllIsRecognizedThroughXamlReflectionAndTypeInitializers()
    {
        var error = new XamlParseException("constructor failed",
            new TargetInvocationException(new TypeInitializationException("TerminalContainer",
                new DllNotFoundException("Dll was not found."))));
        Assert.AreEqual(TerminalInitialization.MissingDependency, TerminalInitialization.MessageKey(error));
        Assert.AreEqual(TerminalInitialization.MissingDependency,
            TerminalInitialization.MessageKey(new FileNotFoundException()));
        Assert.AreEqual(TerminalInitialization.MissingDependency,
            TerminalInitialization.MessageKey(new AggregateException(new InvalidOperationException(), error)));
    }

    [TestMethod]
    public void DamagedOrMismatchedDependenciesAreNotReportedAsMissingCache()
    {
        foreach (var error in new Exception[] {
            new BadImageFormatException(), new EntryPointNotFoundException(), new FileLoadException() })
            Assert.AreEqual(TerminalInitialization.InvalidDependency,
                TerminalInitialization.MessageKey(new TargetInvocationException(error)));
    }

    [TestMethod]
    public void AuthenticationTimeoutAndOtherFailuresDoNotGetMissingDllAdvice()
    {
        foreach (var error in new Exception[] {
            new UnauthorizedAccessException(), new TimeoutException(), new InvalidOperationException("Dll was not found.") })
            Assert.AreEqual(TerminalInitialization.Failed, TerminalInitialization.MessageKey(error));
    }

    [TestMethod]
    public void InitializationFailureCleansUpBeforeShowingOriginalErrorEvenWhenCleanupThrows()
    {
        var failure = new DllNotFoundException();
        var cleaned = false;
        var shown = 0;
        Assert.IsFalse(TerminalInitialization.TryRun(() => throw failure,
            () => { cleaned = true; throw new InvalidOperationException("cleanup failed"); },
            error => { Assert.IsTrue(cleaned); Assert.AreSame(failure, error); shown++; }));
        Assert.AreEqual(1, shown);
    }

    [TestMethod]
    public void SuccessfulInitializationDoesNotCleanUpOrShowErrors()
    {
        var initialized = false;
        Assert.IsTrue(TerminalInitialization.TryRun(() => initialized = true,
            () => Assert.Fail("Successful connection was closed"), _ => Assert.Fail("Unexpected error")));
        Assert.IsTrue(initialized);
    }

    [TestMethod]
    public void DiagnosticsExcludeExceptionMessagesAndDataThatMayContainCredentials()
    {
        var error = new XamlParseException("synthetic-password", new DllNotFoundException("synthetic-private-key"));
        error.Data["command"] = "synthetic-startup-secret";
        var diagnostic = TerminalInitialization.Diagnostic(error);
        StringAssert.Contains(diagnostic, nameof(DllNotFoundException));
        StringAssert.Contains(diagnostic, "HRESULT");
        Assert.IsFalse(diagnostic.Contains("synthetic-", StringComparison.Ordinal));
    }
}
