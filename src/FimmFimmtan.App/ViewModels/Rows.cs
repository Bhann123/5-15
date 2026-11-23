using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
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

public sealed class AnswerRow(int item, string scaleName, Action<AnswerRow> changed) : ObservableObject
{
    private ItemAnswer _answer = AnswerParser.Manual(item, null);

    public ItemAnswer Answer => _answer;
    public int Item => item;
    public string Text => ItemTexts.Get(item);
    public string ScaleName => scaleName;
    public bool IsScored => !Scales.UnscoredItems.Contains(item);

    /// <summary>Shown under the question when the file held something other than one of the three answers.</summary>
    public string? FileNote => _answer.NotRelevant ? $"Í skjali: „{_answer.Raw}“ (telst ekki með)"
        : _answer.Unrecognized ? $"Í skjali: „{_answer.Raw}“ – ekki þekkt svar, telst ósvarað" : null;
    public bool HasFileNote => FileNote != null;
    public bool IsUnrecognized => _answer.Unrecognized;
    public string Cell => _answer.SourceCell;
    public bool IsAnswered => _answer.Score is not null;

    public bool Is0 { get => _answer.Score == 0; set => Set(value, 0); }
    public bool Is1 { get => _answer.Score == 1; set => Set(value, 1); }
    public bool Is2 { get => _answer.Score == 2; set => Set(value, 2); }

    public void Load(ItemAnswer answer)
    {
        _answer = answer;
        RaiseAll();
    }

    public void SetScore(int? score)
    {
        if (_answer.Score == score && !_answer.Unrecognized && !_answer.NotRelevant) return;
        _answer = AnswerParser.Manual(item, score);
        RaiseAll();
        changed(this);
    }

    private void Set(bool isChecked, int score)
    {
        if (isChecked) SetScore(score);
        else if (_answer.Score == score) SetScore(null);
    }

    private void RaiseAll()
    {
        foreach (var name in new[] { nameof(Is0), nameof(Is1), nameof(Is2), nameof(FileNote), nameof(HasFileNote),
                     nameof(IsUnrecognized), nameof(Cell), nameof(IsAnswered), nameof(Answer) })
            OnPropertyChanged(name);
    }
}
