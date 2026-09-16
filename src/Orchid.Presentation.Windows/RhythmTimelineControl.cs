using Orchid.Core;

namespace Orchid.Presentation.Windows;

internal sealed class RhythmTimelineControl : ScrollableControl
{
    private const int LeftMargin = 72;
    private const int RightMargin = 40;
    private const int PixelsPerSecond = 180;
    private PerformanceSession? session;
    private RhythmicValue subdivision = RhythmicValue.Quarter;

    public RhythmTimelineControl()
    {
        AutoScroll = true;
        BackColor = Color.FromArgb(28, 30, 35);
        DoubleBuffered = true;
        MinimumSize = new Size(600, 220);
    }

    public void SetSession(PerformanceSession? performanceSession, RhythmicValue gridSubdivision)
    {
        session = performanceSession;
        subdivision = gridSubdivision;

        var contentWidth = performanceSession is null
            ? ClientSize.Width
            : LeftMargin + RightMargin + Math.Max(ClientSize.Width, ToPixels(performanceSession.Duration));

        AutoScrollMinSize = new Size(contentWidth, 0);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs eventArgs)
    {
        base.OnPaint(eventArgs);
        eventArgs.Graphics.TranslateTransform(AutoScrollPosition.X, 0);
        eventArgs.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        DrawLaneLabels(eventArgs.Graphics);

        if (session is null)
        {
            DrawEmptyState(eventArgs.Graphics);
            return;
        }

        DrawTimeAxis(eventArgs.Graphics, session.Duration);
        DrawGrid(eventArgs.Graphics, session);
        DrawNotes(eventArgs.Graphics, session);
    }

    private void DrawLaneLabels(Graphics graphics)
    {
        using var labelBrush = new SolidBrush(Color.Gainsboro);
        graphics.DrawString("Grid", Font, labelBrush, 12, 54);
        graphics.DrawString("Notes", Font, labelBrush, 12, 138);
    }

    private void DrawEmptyState(Graphics graphics)
    {
        using var brush = new SolidBrush(Color.DarkGray);
        graphics.DrawString("Record or open a performance to display its timeline.", Font, brush, LeftMargin, 95);
    }

    private void DrawTimeAxis(Graphics graphics, TimeSpan duration)
    {
        using var axisPen = new Pen(Color.Gray);
        using var textBrush = new SolidBrush(Color.Silver);
        var visibleSeconds = (int)Math.Ceiling(duration.TotalSeconds);

        graphics.DrawLine(axisPen, LeftMargin, 32, LeftMargin + ToPixels(duration), 32);

        for (var second = 0; second <= visibleSeconds; second++)
        {
            var x = LeftMargin + (second * PixelsPerSecond);
            graphics.DrawLine(axisPen, x, 27, x, 37);
            graphics.DrawString($"{second}s", Font, textBrush, x + 3, 10);
        }
    }

    private void DrawGrid(Graphics graphics, PerformanceSession performanceSession)
    {
        if (performanceSession.Tempo is null)
        {
            using var brush = new SolidBrush(Color.DarkGray);
            graphics.DrawString("This legacy log has no tempo information.", Font, brush, LeftMargin, 54);
            return;
        }

        var measureTicks = performanceSession.Tempo.GetMeasureDuration().Ticks;
        var offsets = RhythmGrid.CreateOffsets(performanceSession.Tempo, subdivision, performanceSession.Duration);

        foreach (var offset in offsets)
        {
            var isMeasureStart = offset.Ticks % measureTicks == 0;
            var x = LeftMargin + ToPixels(offset);
            using var pen = new Pen(isMeasureStart ? Color.Gold : Color.SteelBlue, isMeasureStart ? 2f : 1f);
            graphics.DrawLine(pen, x, 44, x, 102);
        }
    }

    private void DrawNotes(Graphics graphics, PerformanceSession performanceSession)
    {
        using var noteBrush = new SolidBrush(Color.DeepSkyBlue);
        using var textBrush = new SolidBrush(Color.WhiteSmoke);
        using var markerPen = new Pen(Color.DeepSkyBlue, 2f);

        for (var noteIndex = 0; noteIndex < performanceSession.Notes.Count; noteIndex++)
        {
            var note = performanceSession.Notes[noteIndex];
            var x = LeftMargin + ToPixels(note.StartOffset);
            var width = Math.Max(3, ToPixels(note.Duration));
            var y = 128 + ((noteIndex % 3) * 24);

            graphics.DrawLine(markerPen, x, 112, x, 198);
            graphics.FillRectangle(noteBrush, x, y, width, 6);
            graphics.DrawString(MidiNoteName.Get(note.MidiNoteNumber), Font, textBrush, x + 3, y + 7);
        }
    }

    private static int ToPixels(TimeSpan time)
    {
        return (int)Math.Round(time.TotalSeconds * PixelsPerSecond);
    }
}
