using Microsoft.VisualStudio.TestTools.UnitTesting;
using Orchid.Windows;

namespace Orchid.Windows.Tests;

[TestClass]
public class CrashLogTests
{
    [TestMethod]
    public void TryWrite_AppendsExceptionDetailsToPersistentFile()
    {
        // Arrange
        var directory = Path.Combine(Path.GetTempPath(), "Orchid-log-test-" + Guid.NewGuid());
        var log = new CrashLog(directory);
        Exception failure;
        try
        {
            throw new InvalidOperationException("App initialization failed", new IOException("Missing resource"));
        }
        catch (InvalidOperationException exception)
        {
            failure = exception;
        }

        try
        {
            // Act
            var started = log.TryWrite("Startup", "App started");
            var saved = log.TryWrite("Main", failure.ToString());
            var contents = File.ReadAllText(log.FilePath);

            // Assert
            Assert.IsTrue(started);
            Assert.IsTrue(saved);
            StringAssert.Contains(contents, "Startup");
            StringAssert.Contains(contents, "Main");
            StringAssert.Contains(contents, "InvalidOperationException: App initialization failed");
            StringAssert.Contains(contents, "IOException: Missing resource");
            StringAssert.Contains(contents, nameof(TryWrite_AppendsExceptionDetailsToPersistentFile));
            StringAssert.Contains(contents, $"PID {Environment.ProcessId}");
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void TryWrite_UnwritableDestinationDoesNotCauseAnotherException()
    {
        // Arrange
        var path = Path.Combine(Path.GetTempPath(), "Orchid-log-test-" + Guid.NewGuid());
        File.WriteAllText(path, "This is a file, not a log directory.");
        var log = new CrashLog(path);

        try
        {
            // Act
            var saved = log.TryWrite("Main", "Original failure");

            // Assert
            Assert.IsFalse(saved);
            Assert.AreEqual("This is a file, not a log directory.", File.ReadAllText(path));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
