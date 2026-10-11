namespace Orchid.UiPreview;

internal static class PreviewColors
{
    // Reads the Light/Dark pair of a token defined in Theme.xaml for the current app theme.
    public static Color Get(string token)
    {
        var app = Microsoft.Maui.Controls.Application.Current
            ?? throw new InvalidOperationException("Theme colors require a running application.");
        var key = token + (app.RequestedTheme == AppTheme.Dark ? "Dark" : "Light");
        return app.Resources.TryGetValue(key, out var color) && color is Color value
            ? value
            : throw new InvalidOperationException($"Theme color '{key}' is not defined.");
    }
}

internal class TimelineDrawing(PreviewSession session) : IDrawable
{
    public int GridDenominator { get; set; } = 8;
    public const float PixelsPerSecond = 160;
    private const float Top = 48;
    private const float LaneHeight = 24;
    private int Lowest => session.Notes.Count == 0 ? 60 : Math.Min(60, session.Notes.Min(note => note.Midi));
    private int Highest => session.Notes.Count == 0 ? 72 : Math.Max(72, session.Notes.Max(note => note.Midi));
    public float ContentHeight => Top + (Highest - Lowest + 1) * LaneHeight;
    public float ContentWidth => (float)Math.Max(8, Math.Ceiling(session.Duration) + 2) * PixelsPerSecond;
    public float ViewportOffset { get; set; }
    internal float TimeToX(double time) => (float)(time * PixelsPerSecond) - ViewportOffset;
    internal RectF Bounds(PreviewNote note) => new(TimeToX(note.Attack), Top + (Highest - note.Midi) * LaneHeight,
        Math.Max(2, (float)(note.Duration * PixelsPerSecond)), LaneHeight - 2);
    public PreviewNote? HitTest(PointF point) => session.Notes.LastOrDefault(note => Bounds(note).Contains(point));

    public void DrawLabels(ICanvas canvas, RectF dirtyRect)
    {
        canvas.FillColor = PreviewColors.Get("Surface");
        canvas.FillRectangle(dirtyRect);
        canvas.FontSize = 12;
        canvas.FontColor = PreviewColors.Get("TextSecondary");
        for (var midi = Lowest; midi <= Highest; midi++)
            canvas.DrawString(new PreviewNote(midi, 0, 0).Name, 4, Top + (Highest - midi) * LaneHeight,
                dirtyRect.Width - 8, LaneHeight, HorizontalAlignment.Left, VerticalAlignment.Center);
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        var secondary = PreviewColors.Get("TextSecondary");
        canvas.FillColor = PreviewColors.Get("Surface");
        canvas.FillRectangle(dirtyRect);
        canvas.FontSize = 12;
        canvas.FontColor = secondary;
        canvas.StrokeSize = 1;
        var start = ViewportOffset / PixelsPerSecond;
        var end = (ViewportOffset + dirtyRect.Width) / PixelsPerSecond;
        canvas.StrokeColor = PreviewColors.Get("Border");
        for (var midi = Lowest; midi <= Highest; midi++)
        {
            var y = Top + (Highest - midi + 1) * LaneHeight;
            canvas.DrawLine(0, y, dirtyRect.Width, y);
        }
        var step = session.QuarterSeconds * 4 / GridDenominator;
        canvas.StrokeColor = PreviewColors.Get("SubdivisionLine");
        for (var index = Math.Max(0, (int)Math.Floor(start / step)); index * step <= end; index++)
            canvas.DrawLine(TimeToX(index * step), Top, TimeToX(index * step), dirtyRect.Height);
        var measure = PreviewColors.Get("MeasureLine");
        var beat = secondary.WithAlpha(.55f);
        for (var click = Math.Max(0, (int)Math.Floor(start / session.PulseSeconds)); click * session.PulseSeconds <= end; click++)
        {
            var time = click * session.PulseSeconds;
            canvas.StrokeColor = click % session.PulseDenominator == 0 ? measure : beat;
            canvas.DrawLine(TimeToX(time), Top - 10, TimeToX(time), dirtyRect.Height);
        }
        var labelStride = Math.Max(1, (int)Math.Ceiling(24 / (session.QuarterSeconds * PixelsPerSecond)));
        var firstBeat = Math.Max(0, (int)Math.Floor(start / session.QuarterSeconds / labelStride) * labelStride);
        for (var quarter = firstBeat; quarter * session.QuarterSeconds <= end; quarter += labelStride)
            canvas.DrawString((quarter % 4 + 1).ToString(), TimeToX(quarter * session.QuarterSeconds) - 8, 24, 16, 16, HorizontalAlignment.Center, VerticalAlignment.Center);
        for (var second = Math.Max(0, (int)Math.Floor(start)); second <= end; second++)
            canvas.DrawString($"{second} s", TimeToX(second) - 16, 0, 36, 18, HorizontalAlignment.Center, VerticalAlignment.Center);
        DrawNotes(canvas, start, end);
        canvas.StrokeColor = PreviewColors.Get("Accent");
        canvas.StrokeSize = 2;
        canvas.DrawLine(TimeToX(session.Position), 0, TimeToX(session.Position), dirtyRect.Height);
    }

    private void DrawNotes(ICanvas canvas, double start, double end)
    {
        canvas.StrokeSize = 1;
        foreach (var note in session.Notes.Where(note => note.Release >= start && note.Attack <= end))
        {
            var bounds = Bounds(note);
            var held = note.Attack <= session.Position && session.Position < note.Release;
            var text = PreviewColors.Get(held ? "SelectedText" : "NoteText");
            canvas.FillColor = PreviewColors.Get(held ? "SelectedFill" : "NoteFill");
            canvas.FillRoundedRectangle(bounds, 3);
            // The pale fill alone is close to the surface; the outline keeps the note shape readable.
            canvas.StrokeColor = text.WithAlpha(.45f);
            canvas.DrawRoundedRectangle(bounds, 3);
            canvas.FontColor = text;
            if (bounds.Width >= 36) canvas.DrawString(note.Name, bounds, HorizontalAlignment.Center, VerticalAlignment.Center);
        }
    }
}

internal class PitchLabelsDrawing(TimelineDrawing timeline) : IDrawable
{
    public void Draw(ICanvas canvas, RectF dirtyRect) => timeline.DrawLabels(canvas, dirtyRect);
}

internal class KeyboardDrawing(PreviewSession session) : IDrawable
{
    private const float KeyCorner = 4;

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        var whiteNotes = Enumerable.Range(48, 36).Where(midi => !IsBlack(midi)).ToArray();
        var held = session.HeldNotes.Select(note => note.Midi).ToHashSet();
        var width = dirtyRect.Width / whiteNotes.Length;
        var selectedFill = PreviewColors.Get("SelectedFill");
        canvas.FontSize = 10;
        canvas.StrokeSize = 1;
        for (var i = 0; i < whiteNotes.Length; i++)
        {
            var midi = whiteNotes[i];
            var bounds = new RectF(i * width, 0, width, dirtyRect.Height);
            canvas.FillColor = held.Contains(midi) ? selectedFill : PreviewColors.Get("KeyWhite");
            canvas.FillRoundedRectangle(bounds.X, bounds.Y, bounds.Width, bounds.Height, 0, 0, KeyCorner, KeyCorner);
            canvas.StrokeColor = PreviewColors.Get("Border");
            canvas.DrawLine(bounds.Right, 0, bounds.Right, bounds.Bottom);
            canvas.FontColor = PreviewColors.Get(held.Contains(midi) ? "SelectedText" : "KeyLabel");
            if (midi % 12 == 0) canvas.DrawString(new PreviewNote(midi, 0, 0).Name, bounds.Left, bounds.Bottom - 22, width, 20, HorizontalAlignment.Center, VerticalAlignment.Center);
        }
        for (var i = 0; i < whiteNotes.Length - 1; i++)
        {
            var midi = whiteNotes[i] + 1;
            if (!IsBlack(midi)) continue;
            var keyHeight = dirtyRect.Height * .62f;
            var left = (i + 1) * width - width * .3f;
            canvas.FillColor = held.Contains(midi) ? selectedFill : PreviewColors.Get("KeyBlack");
            canvas.FillRoundedRectangle(left, 0, width * .6f, keyHeight, 0, 0, KeyCorner - 1, KeyCorner - 1);
        }
    }

    private static bool IsBlack(int midi) => midi % 12 is 1 or 3 or 6 or 8 or 10;
}
