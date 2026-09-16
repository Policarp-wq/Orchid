using Orchid.Core;

namespace Orchid.Presentation.Windows;

internal sealed class RhythmTimelineControl : ScrollableControl
{
    private const int LeftMargin = 72;
    private const int RightMargin = 120;
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

        AutoScrollPosition = Point.Empty;
        AutoScrollMinSize = new Size(contentWidth, 250);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs eventArgs)
    {
        base.OnPaint(eventArgs);
        eventArgs.Graphics.TranslateTransform(AutoScrollPosition.X, 0);
        eventArgs.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        DrawLegend(eventArgs.Graphics);
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
        graphics.DrawString("Grid", Font, labelBrush, 12, 92);
        graphics.DrawString("Notes", Font, labelBrush, 12, 174);
    }

    private void DrawLegend(Graphics graphics)
    {
        using var textBrush = new SolidBrush(Color.Gainsboro);
        using var measureBrush = new SolidBrush(Color.Gold);
        using var gridBrush = new SolidBrush(Color.SteelBlue);
        using var noteBrush = new SolidBrush(Color.MediumSeaGreen);

        graphics.FillRectangle(measureBrush, LeftMargin, 8, 16, 8);
        graphics.DrawString("Measure start", Font, textBrush, LeftMargin + 22, 3);
        graphics.FillRectangle(gridBrush, LeftMargin + 126, 8, 16, 8);
        graphics.DrawString($"Selected grid ({FormatRhythmicValue(subdivision)})", Font, textBrush, LeftMargin + 148, 3);
        graphics.FillRectangle(noteBrush, LeftMargin + 310, 8, 16, 8);
        graphics.DrawString("Played note", Font, textBrush, LeftMargin + 332, 3);
    }

    private void DrawEmptyState(Graphics graphics)
    {
        using var brush = new SolidBrush(Color.DarkGray);
        graphics.DrawString("Record or open a performance to display its timeline.", Font, brush, LeftMargin, 142);
    }

    private void DrawTimeAxis(Graphics graphics, TimeSpan duration)
    {
        using var axisPen = new Pen(Color.Gray);
        using var textBrush = new SolidBrush(Color.Silver);
        var visibleSeconds = (int)Math.Ceiling(duration.TotalSeconds);

        graphics.DrawLine(axisPen, LeftMargin, 55, LeftMargin + ToPixels(duration), 55);

        for (var second = 0; second <= visibleSeconds; second++)
        {
            var x = LeftMargin + (second * PixelsPerSecond);
            graphics.DrawLine(axisPen, x, 50, x, 60);
            graphics.DrawString($"{second}s", Font, textBrush, x + 3, 33);
        }
    }

    private void DrawGrid(Graphics graphics, PerformanceSession performanceSession)
    {
        if (performanceSession.Tempo is null)
        {
            using var brush = new SolidBrush(Color.DarkGray);
            graphics.DrawString("This legacy log has no tempo information.", Font, brush, LeftMargin, 92);
            return;
        }

        var measureTicks = performanceSession.Tempo.GetMeasureDuration().Ticks;
        var offsets = RhythmGrid.CreateOffsets(performanceSession.Tempo, subdivision, performanceSession.Duration);

        foreach (var offset in offsets)
        {
            var isMeasureStart = offset.Ticks % measureTicks == 0;
            var x = LeftMargin + ToPixels(offset);
            using var pen = new Pen(isMeasureStart ? Color.Gold : Color.SteelBlue, isMeasureStart ? 2f : 1f);
            graphics.DrawLine(pen, x, 72, x, 132);
        }
    }

    private void DrawNotes(Graphics graphics, PerformanceSession performanceSession)
    {
        if (performanceSession.Notes.Count == 0)
        {
            using var emptyBrush = new SolidBrush(Color.DarkGray);
            graphics.DrawString("No MIDI notes were recorded in this session.", Font, emptyBrush, LeftMargin, 174);
            return;
        }

        using var noteBrush = new SolidBrush(Color.MediumSeaGreen);
        using var noteLabelBrush = new SolidBrush(Color.FromArgb(34, 92, 66));
        using var textBrush = new SolidBrush(Color.WhiteSmoke);
        using var markerPen = new Pen(Color.MediumSeaGreen, 2f);

        for (var noteIndex = 0; noteIndex < performanceSession.Notes.Count; noteIndex++)
        {
            var note = performanceSession.Notes[noteIndex];
            var x = LeftMargin + ToPixels(note.StartOffset);
            var durationWidth = Math.Max(3, ToPixels(note.Duration));
            var y = 154 + ((noteIndex % 3) * 30);
            var noteName = MidiNoteName.Get(note.MidiNoteNumber);
            var textSize = graphics.MeasureString(noteName, Font);
            var labelBounds = new RectangleF(x + 6, y, Math.Max(38, textSize.Width + 12), 24);

            graphics.DrawLine(markerPen, x, 144, x, 238);
            graphics.FillEllipse(noteBrush, x - 4, y + 8, 9, 9);
            graphics.FillRectangle(noteLabelBrush, labelBounds);
            graphics.DrawRectangle(markerPen, labelBounds.X, labelBounds.Y, labelBounds.Width, labelBounds.Height);
            graphics.DrawString(noteName, Font, textBrush, labelBounds.X + 6, labelBounds.Y + 4);
            graphics.FillRectangle(noteBrush, x, y + 25, durationWidth, 4);
        }
    }

    private static string FormatRhythmicValue(RhythmicValue value)
    {
        return $"1/{(int)value}";
    }

    private static int ToPixels(TimeSpan time)
    {
        return (int)Math.Round(time.TotalSeconds * PixelsPerSecond);
    }
}
