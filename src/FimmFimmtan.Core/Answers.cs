using System.Globalization;
using System.Text;

namespace FimmFimmtan.Core;

public enum CellKind { Empty, Item, Impact, Unknown }

public sealed record ParsedCell(CellKind Kind, int? Score, string Raw)
{
    public bool IsNotRelevant { get; init; }
}

public sealed record ItemAnswer(int Item, int? Score, string Raw, bool Unrecognized, bool NotRelevant, string SourceCell);

/// <summary>Answer to one of the ten daily-function questions; <see cref="Level"/> is 0–3 (alls ekki … mjög mikið).</summary>
public sealed record ImpactAnswer(int Question, int? Level, string Raw, string SourceCell)
{
    public ImpactQuestion Info => ImpactQuestions.All[Question - 1];
}

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
    public int? ImpactLevelOf(int question) => Impact.FirstOrDefault(i => i.Question == question)?.Level;
}

public static class AnswerParser
{
    public const string Option0 = "Á ekki við";
    public const string Option1 = "Á stundum við";
    public const string Option2 = "Á við";

    private static readonly Dictionary<string, int> ImpactOptions = new()
    {
        ["alls ekki"] = 0, ["litid"] = 1, ["mikid"] = 2, ["mjog mikid"] = 3,
        ["not at all"] = 0, ["a little"] = 1, ["pretty much"] = 2, ["very much"] = 3,
    };

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
        if (ImpactOptions.TryGetValue(n, out var level)) return new(CellKind.Impact, level, raw);
        if (n is "no answer") return new(CellKind.Empty, null, raw);
        if (n.Contains("stundum") || n.Contains("nokkru") || n.Contains("sometimes")) return new(CellKind.Item, 1, raw);
        if (n.StartsWith("ovidkomandi")) return new(CellKind.Item, null, raw) { IsNotRelevant = true };
        if (n.Contains("ekki vid") || n is "does not apply") return new(CellKind.Item, 0, raw);
        if (n is "a vid" or "applies") return new(CellKind.Item, 2, raw);
        return new(CellKind.Unknown, null, raw);
    }

    /// <summary>Parses an answer to a daily-function question: the four options in Icelandic or English, or 0–3.</summary>
    public static ParsedCell ParseImpact(string? raw)
    {
        raw ??= "";
        var n = Normalize(raw);
        if (n.Length == 0 || n is "no answer") return new(CellKind.Empty, null, raw);
        if (n is "0" or "1" or "2" or "3") return new(CellKind.Impact, int.Parse(n), raw);
        return ImpactOptions.TryGetValue(n, out var level) ? new(CellKind.Impact, level, raw) : new(CellKind.Unknown, null, raw);
    }

    private static bool IsImpactText(string text)
    {
        var n = Normalize(text);
        return n.Contains("daglegu lifi") || n.Contains("daily function");
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
        var misplacedImpact = 0;
        foreach (var (raw, cell) in cells)
        {
            var p = Parse(raw);
            if (p.Kind == CellKind.Impact)
            {
                if (ImpactQuestions.AfterItem(items.Count) is { } q && impact.All(i => i.Question != q.Number))
                    impact.Add(new(q.Number, p.Score, raw.Trim(), cell));
                else
                    misplacedImpact++;
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
        var padded = items.Take(Scales.ItemCount).ToList();
        for (var i = padded.Count + 1; i <= Scales.ItemCount; i++) padded.Add(new(i, null, "", false, false, ""));
        var result = new QuestionnaireAnswers { Items = padded, Impact = impact, SourceDescription = sourceDescription };
        if (items.Count < Scales.ItemCount)
            result.Warnings.Add($"Aðeins {items.Count} línur fundust en listinn hefur {Scales.ItemCount} spurningar. Spurningar {items.Count + 1}–{Scales.ItemCount} teljast ósvaraðar.");
        if (extra > 0)
            result.Warnings.Add($"Fleiri svör fundust ({items.Count}) en spurningarnar eru ({Scales.ItemCount}). Aukasvörin voru hunsuð – athugaðu hvort röðin sé rétt.");
        if (misplacedImpact > 0)
            result.Warnings.Add($"{misplacedImpact} svör um hömlun í daglegu lífi voru ekki á réttum stað og voru hunsuð. Líklega vantar línu fyrir ofan – " +
                                "settu spurninganúmer í dálk A svo hver lína sé lesin eftir númeri.");
        AddUnknownWarning(result, unknown);
        return result;
    }

    /// <summary>
    /// Maps rows that carry their own question number in column A (1–181, or H1–H10 for the daily-function
    /// questions). Empty or extra rows therefore cannot shift the numbering.
    /// </summary>
    public static QuestionnaireAnswers MapKeyed(IReadOnlyList<(string Key, string Text, string Raw, string Cell)> rows, string sourceDescription)
    {
        var items = new ItemAnswer?[Scales.ItemCount + 1];
        var impact = new Dictionary<int, ImpactAnswer>();
        var unknown = 0;
        var duplicates = new List<string>();
        var lastItem = 0;
        foreach (var (key, text, raw, cell) in rows)
        {
            var k = key.Trim().TrimEnd('.');
            if (int.TryParse(k, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) && n is >= 1 and <= Scales.ItemCount)
            {
                lastItem = n;
                var p = Parse(raw);
                var bad = p.Kind is CellKind.Unknown or CellKind.Impact;
                if (bad) unknown++;
                if (items[n] != null) duplicates.Add(n.ToString(CultureInfo.InvariantCulture));
                items[n] = new(n, bad ? null : p.Score, raw.Trim(), bad, p.IsNotRelevant, cell);
                continue;
            }

            var q = ImpactQuestions.ByKey(k);
            if (q is null && (IsImpactText(text) || Parse(raw).Kind == CellKind.Impact)) q = ImpactQuestions.AfterItem(lastItem);
            if (q is null) continue;
            var level = ParseImpact(raw);
            if (level.Kind == CellKind.Empty) continue;
            if (level.Kind == CellKind.Unknown) unknown++;
            if (impact.ContainsKey(q.Number)) duplicates.Add(q.Key);
            impact[q.Number] = new(q.Number, level.Score, raw.Trim(), cell);
        }

        var list = Enumerable.Range(1, Scales.ItemCount).Select(i => items[i] ?? new(i, null, "", false, false, "")).ToList();
        var result = new QuestionnaireAnswers
        {
            Items = list,
            Impact = impact.Values.OrderBy(i => i.Question).ToList(),
            SourceDescription = sourceDescription,
        };
        var missing = Enumerable.Range(1, Scales.ItemCount).Where(i => items[i] is null).ToList();
        if (missing.Count > 0)
            result.Warnings.Add($"Línur fyrir {missing.Count} spurningar fundust ekki í dálki A (t.d. {string.Join(", ", missing.Take(5))}). Þær teljast ósvaraðar.");
        if (duplicates.Count > 0)
            result.Warnings.Add($"Sama spurningin kom oftar en einu sinni fyrir ({string.Join(", ", duplicates.Distinct().Take(5))}). Síðasta svarið gildir.");
        AddUnknownWarning(result, unknown);
        return result;
    }

    private static void AddUnknownWarning(QuestionnaireAnswers result, int unknown)
    {
        if (unknown > 0)
            result.Warnings.Add($"{unknown} svör voru ekki þekkt og teljast ósvaruð. Leyfileg svör: „{Option0}“, „{Option1}“, „{Option2}“ (eða 0, 1, 2) " +
                                $"og við spurningum um hömlun: {string.Join(", ", ImpactQuestions.Options.Select(o => $"„{o}“"))} (eða 0–3).");
    }
}
