namespace Orchid.Windows;

public sealed class CrashLog(string directory)
{
    private readonly object _gate = new();
    public string FilePath { get; } = Path.Combine(directory, $"orchid-{DateTime.UtcNow:yyyy-MM-dd}-{Environment.ProcessId}.log");

    public bool TryWrite(string source, string details)
    {
        try
        {
            lock (_gate)
            {
                Directory.CreateDirectory(directory);
                File.AppendAllText(FilePath,
                    $"{DateTimeOffset.UtcNow:O} | PID {Environment.ProcessId} | Thread {Environment.CurrentManagedThreadId} | {source}\n{details}\n\n");
            }
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            System.Diagnostics.Debug.WriteLine($"Could not write crash log: {exception}\n{source}: {details}");
            return false;
        }
    }
}
