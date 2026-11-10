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
}

public sealed record ChildInfo(string Name, int Age, Sex Sex);

public sealed record ScoringOptions(bool IncludePositiveItems = true);

public sealed record Report(ChildInfo Child, AgeGroup Group, IReadOnlyList<ScaleResult> Results, QuestionnaireAnswers Answers, ScoringOptions Options);

public static class Scorer
{
    public const double MinPercentAnswered = 80;

    public static Report Score(ChildInfo child, QuestionnaireAnswers answers, ScoringOptions? options = null)
    {
        options ??= new ScoringOptions();
        var group = Norms.GroupForAge(child.Age)
            ?? throw new ArgumentOutOfRangeException(nameof(child), $"Viðmið eru aðeins til fyrir {Norms.MinAge}–{Norms.MaxAge} ára.");

        var results = Scales.All.Select(scale =>
        {
            var norm = Norms.Get(group, child.Sex, scale.Key);
            var items = scale.Items.Where(i => options.IncludePositiveItems || !Scales.PositiveItems.Contains(i)).ToList();
            if (norm is not { IsAvailable: true })
                return new ScaleResult(scale, null, 0, items.Count, norm);

            var scores = items.Select(answers.ScoreOf).Where(s => s.HasValue).Select(s => s!.Value).ToList();
            double? mean = scores.Count == 0 ? null : scores.Average();
            return new ScaleResult(scale, mean, scores.Count, items.Count, norm);
        }).ToList();

        return new Report(child, group, results, answers, options);
    }

    public static string Describe(Level level) => level switch
    {
        Level.Above98 => "Yfir 98. hundraðsmarki",
        Level.Above90 => "Yfir 90. hundraðsmarki",
        Level.Typical => "Innan eðlilegra marka",
        _ => "Ekki metið",
    };
}
