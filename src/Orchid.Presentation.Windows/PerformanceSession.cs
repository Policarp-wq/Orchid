using Orchid.Application;
using Orchid.Core;

namespace Orchid.Presentation.Windows;

internal sealed record PerformanceSession(
    Tempo? Tempo,
    TimeSpan Duration,
    IReadOnlyList<RecordedNote> Notes)
{
    public static PerformanceSession FromRhythmSession(RhythmSession session)
    {
        return new PerformanceSession(session.Tempo, session.Duration, session.Notes);
    }
}
