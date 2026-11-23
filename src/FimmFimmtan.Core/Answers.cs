using System.Globalization;
using System.Text;

namespace FimmFimmtan.Core;

public enum CellKind { Empty, Item, Impact, Unknown }

public sealed record ParsedCell(CellKind Kind, int? Score, string Raw)
{
    public bool IsNotRelevant { get; init; }
}

public sealed record ItemAnswer(int Item, int? Score, string Raw, bool Unrecognized, bool NotRelevant, string SourceCell);

public sealed record ImpactAnswer(int AfterItem, string Raw, string SourceCell);

public sealed class QuestionnaireAnswers
{
    public required IReadOnlyList<ItemAnswer> Items { get; init; }
    public IReadOnlyList<ImpactAnswer> Impact { get; init; } = [];
    public List<string> Warnings { get; } = [];
    public string SourceDescription { get; init; } = "";

    /// <summary>Child details found next to the answers in a file saved by this program, if any.</summary>
    public string? ChildName { get; set; }
    public int? ChildAge { get; set; }
    public Sex? ChildSex { get; set; }

    public int? ScoreOf(int item) => item >= 1 && item <= Items.Count ? Items[item - 1].Score : null;
    public int AnsweredCount => Items.Count(i => i.Score.HasValue);
}

public static class AnswerParser
{
    public const string Option0 = "Á ekki við";
    public const string Option1 = "Á stundum við";
    public const string Option2 = "Á við";

    private static readonly string[] ImpactOptions = ["alls ekki", "litid", "mikid", "mjog mikid"];

    public static string Label(int score) => score switch
    {
        0 => Option0,
        1 => Option1,
        2 => Option2,
        _ => throw new ArgumentOutOfRangeException(nameof(score)),
    };

    public static ItemAnswer Manual(int item, int? score) =>
        new(item, score, score is { } s ? Label(s) : "", false, false, "");

    public static QuestionnaireAnswers Empty(string sourceDescription = "") => new()
    {
        Items = Enumerable.Range(1, Scales.ItemCount).Select(i => Manual(i, null)).ToList(),
        SourceDescription = sourceDescription,
    };

    /// <summary>Folds Icelandic letters to ASCII and normalises spacing so minor variations in the export still match.</summary>
    public static string Normalize(string s)
    {
        var lower = s.Trim().ToLowerInvariant()
            .Replace('ð', 'd').Replace('þ', 't').Replace("æ", "ae").Replace('ö', 'o');
        var decomposed = lower.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            sb.Append(char.IsLetterOrDigit(c) || c == '/' ? c : ' ');
        }
        return string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    public static ParsedCell Parse(string? raw)
    {
        raw ??= "";
        var n = Normalize(raw);
        if (n.Length == 0) return new(CellKind.Empty, null, raw);

        if (n is "0" or "1" or "2") return new(CellKind.Item, int.Parse(n), raw);
        if (ImpactOptions.Contains(n)) return new(CellKind.Impact, null, raw);
        if (n.Contains("stundum") || n.Contains("nokkru")) return new(CellKind.Item, 1, raw);
        if (n.StartsWith("ovidkomandi")) return new(CellKind.Item, null, raw) { IsNotRelevant = true };
        if (n.Contains("ekki vid")) return new(CellKind.Item, 0, raw);
        if (n == "a vid") return new(CellKind.Item, 2, raw);
        return new(CellKind.Unknown, null, raw);
    }

    /// <summary>
    /// Maps a sequence of cells to items 1..181. Cells holding an impact answer ("alls ekki", "lítið", "mikið",
    /// "mjög mikið") belong to the unnumbered "Hamla erfiðleikar ... í daglegu lífi" questions and are set aside
    /// so they do not shift the item numbering.
    /// </summary>
    public static QuestionnaireAnswers Map(IReadOnlyList<(string Raw, string Cell)> cells, string sourceDescription)
    {
        var items = new List<ItemAnswer>();
        var impact = new List<ImpactAnswer>();
        var unknown = 0;
        foreach (var (raw, cell) in cells)
        {
            var p = Parse(raw);
            if (p.Kind == CellKind.Impact)
            {
                impact.Add(new(items.Count, raw.Trim(), cell));
                continue;
            }
            if (items.Count >= Scales.ItemCount)
            {
                if (p.Kind != CellKind.Empty) items.Add(new(items.Count + 1, null, raw, true, false, cell));
                continue;
            }
            if (p.Kind == CellKind.Unknown) unknown++;
            items.Add(new(items.Count + 1, p.Score, raw.Trim(), p.Kind == CellKind.Unknown, p.IsNotRelevant, cell));
        }

        var extra = items.Count - Scales.ItemCount;
        var result = new QuestionnaireAnswers
        {
            Items = items.Take(Scales.ItemCount).ToList(),
            Impact = impact,
            SourceDescription = sourceDescription,
        };
        if (items.Count < Scales.ItemCount)
        {
            var padded = result.Items.ToList();
            for (var i = padded.Count + 1; i <= Scales.ItemCount; i++) padded.Add(new(i, null, "", false, false, ""));
            result = new QuestionnaireAnswers { Items = padded, Impact = impact, SourceDescription = sourceDescription };
            result.Warnings.Add($"Aðeins {items.Count} línur fundust en listinn hefur {Scales.ItemCount} spurningar. Spurningar {items.Count + 1}–{Scales.ItemCount} teljast ósvaraðar.");
        }
        if (extra > 0)
            result.Warnings.Add($"Fleiri svör fundust ({items.Count}) en spurningarnar eru ({Scales.ItemCount}). Aukasvörin voru hunsuð – athugaðu hvort röðin sé rétt.");
        if (unknown > 0)
            result.Warnings.Add($"{unknown} svör voru ekki þekkt og teljast ósvaruð. Leyfileg svör: „{Option0}“, „{Option1}“, „{Option2}“ (eða 0, 1, 2).");
        return result;
    }
}
