namespace FimmFimmtan.Core;

public enum Level
{
    /// <summary>No answers, or no norms exist for this scale at this age (Learning for 5–7 years).</summary>
    NotScored,
    Typical,
    Above90,
    Above98,
}

public sealed record ScaleResult(Scale Scale, double? Score, int Answered, int Total, Norm? Norm)
{
    public double PercentAnswered => Total == 0 ? 0 : 100.0 * Answered / Total;

    public double? Z => Score is { } s && Norm is { IsAvailable: true } n ? (s - n.Mean) / n.SD : null;

    public Level Level => Score is not { } s || Norm is not { IsAvailable: true } n
        ? Level.NotScored
        : s >= n.P98 ? Level.Above98 : s >= n.P90 ? Level.Above90 : Level.Typical;

    public bool FewAnswers => Answered > 0 && PercentAnswered < Scorer.MinPercentAnswered;

    public double? ProfilePosition => Score is { } s && Norm is { IsAvailable: true } n ? Profile.Position(s, n) : null;
}

/// <summary>
/// Profile chart scale used in the 5-15 reports: each scale is drawn on its own percentile axis, where
/// 0..P90 fills the first half, P90..P98 the next quarter and P98..2 the last quarter.
/// </summary>
public static class Profile
{
    public const double Band90 = 0.5;
    public const double Band98 = 0.75;
    private const double MaxScore = 2.0;

    public static double Position(double score, Norm n)
    {
        if (score <= n.P90) return n.P90 > 0 ? score / n.P90 * Band90 : 0;
        if (score <= n.P98) return Band90 + (n.P98 > n.P90 ? (score - n.P90) / (n.P98 - n.P90) : 1) * (Band98 - Band90);
        return Math.Min(1, Band98 + (MaxScore > n.P98 ? (score - n.P98) / (MaxScore - n.P98) : 1) * (1 - Band98));
    }

    public static IEnumerable<ScaleResult> Domains(IEnumerable<ScaleResult> results) => results.Where(r => r.Scale.IsDomain);

    /// <summary>All subdomains in questionnaire order; Memory and Social skills have none and appear as themselves.</summary>
    public static IEnumerable<ScaleResult> Subdomains(IEnumerable<ScaleResult> results) =>
        results.Where(r => !r.Scale.IsDomain || !Scales.SubdomainsOf(r.Scale.Key).Any());
}

public sealed record ChildInfo(string Name, int Age, Sex Sex);

public sealed record Report(ChildInfo Child, AgeGroup Group, IReadOnlyList<ScaleResult> Results, QuestionnaireAnswers Answers);

public static class Scorer
{
    public const double MinPercentAnswered = 80;

    public static Report Score(ChildInfo child, QuestionnaireAnswers answers)
    {
        var group = Norms.GroupForAge(child.Age)
            ?? throw new ArgumentOutOfRangeException(nameof(child), $"Viðmið eru aðeins til fyrir {Norms.MinAge}–{Norms.MaxAge} ára.");

        var results = Scales.All.Select(scale =>
        {
            var norm = Norms.Get(group, child.Sex, scale.Key);
            var items = scale.Items;
            if (norm is not { IsAvailable: true })
                return new ScaleResult(scale, null, 0, items.Count, norm);

            var scores = items.Select(answers.ScoreOf).Where(s => s.HasValue).Select(s => s!.Value).ToList();
            double? mean = scores.Count == 0 ? null : scores.Average();
            return new ScaleResult(scale, mean, scores.Count, items.Count, norm);
        }).ToList();

        return new Report(child, group, results, answers);
    }

    public static string Describe(Level level) => level switch
    {
        Level.Above98 => "Yfir 98. hundraðsmarki",
        Level.Above90 => "Yfir 90. hundraðsmarki",
        Level.Typical => "Innan eðlilegra marka",
        _ => "Ekki metið",
    };
}
