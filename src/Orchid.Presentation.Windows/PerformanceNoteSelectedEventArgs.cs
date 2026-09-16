using Orchid.Application;
using Orchid.Core;

namespace Orchid.Presentation.Windows;

internal sealed class PerformanceNoteSelectedEventArgs(
    RecordedNote note,
    IReadOnlyList<RhythmDeviation> deviations) : EventArgs
{
    public RecordedNote Note { get; } = note;

    public IReadOnlyList<RhythmDeviation> Deviations { get; } = deviations;
}
