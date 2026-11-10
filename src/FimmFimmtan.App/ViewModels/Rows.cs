using System.Globalization;
using FimmFimmtan.Core;

namespace FimmFimmtan.App.ViewModels;

public sealed class ResultRow(ScaleResult result)
{
    private static readonly CultureInfo Is = CultureInfo.GetCultureInfo("is-IS");
    private static string F(double? v) => v?.ToString("0.00", Is) ?? "–";
    private Norm? N => result.Norm is { IsAvailable: true } n ? n : null;

    public ScaleResult Result => result;
    public string Name => result.Scale.NameIs;
    public string NameEn => result.Scale.NameEn;
    public bool IsDomain => result.Scale.IsDomain;
    public bool IsSubdomain => !result.Scale.IsDomain;
    public string ScoreText => F(result.Score);
    public string PercentText => N is null ? "–" : result.PercentAnswered.ToString("0", Is) + "%";
    public string MedianText => F(N?.Median);
    public string MeanText => F(N?.Mean);
    public string SdText => F(N?.SD);
    public string P90Text => F(N?.P90);
    public string P98Text => F(N?.P98);
    public string ZText => result.Z is { } z ? z.ToString("+0.0;−0.0;0.0", Is) : "–";
    public string LevelText => N is null ? "Engin viðmið fyrir aldur" : Scorer.Describe(result.Level);
    public string LevelShortText => result.Level switch
    {
        Level.Above98 => "Yfir 98. hundraðsm.",
        Level.Above90 => "Yfir 90. hundraðsm.",
        Level.Typical => "Innan marka",
        _ => N is null ? "Ekki metið (aldur)" : "Ekki svarað",
    };
    public bool FewAnswers => result.FewAnswers;
    public Level Level => result.Level;
    public bool IsAbove98 => result.Level == Level.Above98;
    public bool IsAbove90 => result.Level == Level.Above90;
    public bool IsTypical => result.Level == Level.Typical;
    public bool IsNotScored => result.Level == Level.NotScored;
    public string Tooltip => $"{LevelText}\n{result.Scale.NameEn}\nSpurningar {result.Scale.Items[0]}–{result.Scale.Items[^1]} · {result.Answered} af {result.Total} svarað" +
        (result.Z is { } z ? $"\nz = {z.ToString("0.00", Is)} (frávik frá meðaltali í staðalfrávikum)" : "");
}

public sealed class AnswerRow(ItemAnswer answer, string scaleName)
{
    public int Item => answer.Item;
    public string Text => ItemTexts.Get(answer.Item);
    public string ScaleName => scaleName;
    public string Raw => answer.NotRelevant ? "Óviðkomandi" : string.IsNullOrWhiteSpace(answer.Raw) ? "Ekki svarað" : answer.Raw;
    public string ScoreText => answer.Score?.ToString() ?? "–";
    public string Cell => answer.SourceCell;
    public bool IsMissing => answer.Score is null && !answer.Unrecognized;
    public bool IsUnrecognized => answer.Unrecognized;
    public bool IsAnswered => answer.Score is not null;
}
