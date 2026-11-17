using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using FimmFimmtan.Core;

namespace FimmFimmtan.App.Controls;

/// <summary>
/// One row of the profile chart: the scale's own percentile axis (0–90%, 90–98%, 98–100% zones, see
/// <see cref="Profile"/>) with the child's result drawn as a bar.
/// </summary>
public sealed class NormBar : Control
{
    public static readonly StyledProperty<ScaleResult?> ResultProperty =
        AvaloniaProperty.Register<NormBar, ScaleResult?>(nameof(Result));

    static NormBar() => AffectsRender<NormBar>(ResultProperty);

    public ScaleResult? Result
    {
        get => GetValue(ResultProperty);
        set => SetValue(ResultProperty, value);
    }

    public static readonly IBrush ZoneTypical = new SolidColorBrush(Color.Parse("#EEF1F5"));
    public static readonly IBrush Zone90 = new SolidColorBrush(Color.Parse("#FBEBC4"));
    public static readonly IBrush Zone98 = new SolidColorBrush(Color.Parse("#F6D2CD"));
    private static readonly IBrush BarTypical = new SolidColorBrush(Color.Parse("#3B7CB8"));
    private static readonly IBrush Bar90 = new SolidColorBrush(Color.Parse("#D89512"));
    private static readonly IBrush Bar98 = new SolidColorBrush(Color.Parse("#C2410C"));

    public override void Render(DrawingContext ctx)
    {
        var w = Bounds.Width;
        var h = Bounds.Height;
        var x90 = w * Profile.Band90;
        var x98 = w * Profile.Band98;

        ctx.DrawRectangle(ZoneTypical, null, new Rect(0, 0, x90, h));
        ctx.DrawRectangle(Zone90, null, new Rect(x90, 0, x98 - x90, h));
        ctx.DrawRectangle(Zone98, null, new Rect(x98, 0, w - x98, h));

        if (Result is not { ProfilePosition: { } pos } r) return;
        var brush = r.Level switch { Level.Above98 => Bar98, Level.Above90 => Bar90, _ => BarTypical };
        var barH = h * 0.5;
        ctx.DrawRectangle(brush, null, new RoundedRect(new Rect(0, (h - barH) / 2, Math.Max(3, pos * w), barH), 0, 2, 2, 0));
    }
}
