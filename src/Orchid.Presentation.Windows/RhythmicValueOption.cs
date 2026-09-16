using Orchid.Core;

namespace Orchid.Presentation.Windows;

internal sealed record RhythmicValueOption(RhythmicValue Value, string DisplayName)
{
    public override string ToString()
    {
        return DisplayName;
    }
}
