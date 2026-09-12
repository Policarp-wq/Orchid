using System.Globalization;

namespace Orchid.Presentation.Windows;

internal static class PerformanceLogReader
{
    private const string Header = "ORCHID-PIANO-LOG 1";

    public static IReadOnlyList<PerformanceNote> Read(string filePath)
    {
        var lines = File.ReadAllLines(filePath);

        if (lines.Length == 0 || !string.Equals(lines[0], Header, StringComparison.Ordinal))
        {
            throw new InvalidDataException("The selected file is not an Orchid piano performance log.");
        }

        var notes = new List<PerformanceNote>();

        for (var lineIndex = 1; lineIndex < lines.Length; lineIndex++)
        {
            var line = lines[lineIndex].Trim();

            if (line.Length == 0)
            {
                continue;
            }

            notes.Add(ParseNote(line, lineIndex + 1));
        }

        return notes.OrderBy(note => note.StartOffset).ToArray();
    }

    private static PerformanceNote ParseNote(string line, int lineNumber)
    {
        var fields = line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (fields.Length != 4 || !string.Equals(fields[0], "NOTE", StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Invalid performance log entry at line {lineNumber}.");
        }

        if (!int.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture, out var midiNoteNumber) ||
            midiNoteNumber is < 0 or > 127 ||
            !double.TryParse(fields[2], NumberStyles.None, CultureInfo.InvariantCulture, out var startMilliseconds) ||
            startMilliseconds < 0 ||
            !double.TryParse(fields[3], NumberStyles.None, CultureInfo.InvariantCulture, out var durationMilliseconds) ||
            durationMilliseconds < 0)
        {
            throw new InvalidDataException($"Invalid note values at line {lineNumber}.");
        }

        return new PerformanceNote(
            midiNoteNumber,
            TimeSpan.FromMilliseconds(startMilliseconds),
            TimeSpan.FromMilliseconds(durationMilliseconds));
    }
}
