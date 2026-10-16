namespace Orchid.Ui.Tests;

/// <summary>A clock that moves only when a test sets it.</summary>
internal sealed class ManualClock : TimeProvider
{
    public long Milliseconds { get; set; }
    public override long TimestampFrequency => 1000;
    public override long GetTimestamp() => Milliseconds;
}
