using System.Globalization;
using Orchid.Application;

namespace Orchid.Presentation.Windows;

internal static class PerformanceLogWriter
{
    private const string Header = "ORCHID-RHYTHM-LOG 2";

    public static string Write(RhythmSession session, string directoryPath)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);

        Directory.CreateDirectory(directoryPath);

        var filePath = Path.Combine(
            directoryPath,
            $"rhythm-session-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.orchid");

        using var writer = new StreamWriter(filePath, append: false);
        writer.WriteLine(Header);
        writer.WriteLine(
            $"TEMPO {session.Tempo.BeatsPerMinute.ToString(CultureInfo.InvariantCulture)} " +
            $"{(int)session.Tempo.BeatUnit}");
        writer.WriteLine("TIME-SIGNATURE 4/4");
        writer.WriteLine($"DURATION {FormatMilliseconds(session.Duration)}");

        foreach (var note in session.Notes)
        {
            writer.WriteLine(
                $"NOTE {note.MidiNoteNumber} {note.Channel} {note.Velocity} " +
                $"{FormatMilliseconds(note.StartOffset)} {FormatMilliseconds(note.Duration)}");
        }

        return filePath;
    }

    private static string FormatMilliseconds(TimeSpan value)
    {
        return value.TotalMilliseconds.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
