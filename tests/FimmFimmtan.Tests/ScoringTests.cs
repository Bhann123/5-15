using ClosedXML.Excel;
using FimmFimmtan.Core;

namespace FimmFimmtan.Tests;

public class ScoringTests
{
    /// <summary>Sum of item scores per subdomain in the worked example printed on every norm table.</summary>
    private static readonly Dictionary<string, int> ExampleSums = new()
    {
        ["motor.gross"] = 4, ["motor.fine"] = 0,
        ["exec.attention"] = 13, ["exec.hyper"] = 15, ["exec.passivity"] = 4, ["exec.planning"] = 3,
        ["perc.space"] = 2, ["perc.time"] = 6, ["perc.body"] = 2, ["perc.visual"] = 0,
        ["memory"] = 7,
        ["lang.comprehension"] = 3, ["lang.expressive"] = 0, ["lang.communication"] = 2,
        ["social"] = 9,
        ["emo.internal"] = 2, ["emo.acting"] = 4, ["emo.obsessive"] = 0,
    };

    /// <summary>"Result" column of the example, identical in all eight norm PDFs.</summary>
    private static readonly Dictionary<string, double> ExampleResults = new()
    {
        ["motor"] = 0.24, ["motor.gross"] = 0.57, ["motor.fine"] = 0.00,
        ["exec"] = 1.40, ["exec.attention"] = 1.44, ["exec.hyper"] = 1.67, ["exec.passivity"] = 1.00, ["exec.planning"] = 1.00,
        ["perc"] = 0.56, ["perc.space"] = 0.40, ["perc.time"] = 1.50, ["perc.body"] = 0.40, ["perc.visual"] = 0.00,
        ["memory"] = 0.64,
        ["lang"] = 0.24, ["lang.comprehension"] = 0.60, ["lang.expressive"] = 0.00, ["lang.communication"] = 0.67,
        ["social"] = 0.33,
        ["emo"] = 0.18, ["emo.internal"] = 0.17, ["emo.acting"] = 0.31, ["emo.obsessive"] = 0.00,
    };

    private static int?[] ExampleScores()
    {
        var scores = new int?[Scales.ItemCount + 1];
        foreach (var scale in Scales.All.Where(s => ExampleSums.ContainsKey(s.Key)))
        {
            var remaining = ExampleSums[scale.Key];
            foreach (var item in scale.Items)
            {
                var v = Math.Min(2, remaining);
                scores[item] = v;
                remaining -= v;
            }
        }
        return scores;
    }

    private static QuestionnaireAnswers ToAnswers(int?[] scores) => new()
    {
        Items = Enumerable.Range(1, Scales.ItemCount).Select(i => new ItemAnswer(i, scores[i], scores[i]?.ToString() ?? "", false, false, "")).ToList(),
    };

    [Theory]
    [InlineData(6, Sex.Male)]
    [InlineData(6, Sex.Female)]
    [InlineData(9, Sex.Male)]
    [InlineData(13, Sex.Female)]
    [InlineData(17, Sex.Male)]
    public void Reproduces_worked_example_from_norm_tables(int age, Sex sex)
    {
        var report = Scorer.Score(new ChildInfo("Dæmi", age, sex), ToAnswers(ExampleScores()));

        foreach (var (key, expected) in ExampleResults)
        {
            var r = report.Results.Single(x => x.Scale.Key == key);
            Assert.Equal(expected, Math.Round(r.Score!.Value, 2));
            Assert.Equal(100, r.PercentAnswered);
        }
        foreach (var r in report.Results.Where(x => x.Scale.Key.StartsWith("learn")))
        {
            Assert.Null(r.Score);
            Assert.Equal(Level.NotScored, r.Level);
        }
    }

    [Fact]
    public void Flags_against_90th_and_98th_percentile()
    {
        var report = Scorer.Score(new ChildInfo("Dæmi", 6, Sex.Male), ToAnswers(ExampleScores()));
        Level L(string key) => report.Results.Single(r => r.Scale.Key == key).Level;

        Assert.Equal(Level.Above98, L("exec"));         // 1.40 vs P98 1.40
        Assert.Equal(Level.Above98, L("exec.hyper"));   // 1.67 vs P98 1.55
        Assert.Equal(Level.Above90, L("exec.attention")); // 1.44 vs P90 0.88, P98 1.67
        Assert.Equal(Level.Typical, L("motor"));        // 0.24 vs P90 0.47
        var z = report.Results.Single(r => r.Scale.Key == "exec").Z!.Value;
        Assert.Equal((35.0 / 25 - 0.31) / 0.34, z, 6);
    }

    [Fact]
    public void Norm_tables_contain_every_scale_for_every_group()
    {
        foreach (var group in Enum.GetValues<AgeGroup>())
        foreach (var sex in Enum.GetValues<Sex>())
        foreach (var scale in Scales.All)
        {
            var norm = Norms.Get(group, sex, scale.Key);
            Assert.NotNull(norm);
            Assert.Equal(!(group == AgeGroup.Age5_7 && scale.Key.StartsWith("learn")), norm!.IsAvailable);
        }
        Assert.Equal(new Norm(0.20, 0.31, 0.34, 1.40, 0.76), Norms.Get(AgeGroup.Age5_7, Sex.Male, "exec"));
        Assert.Equal(new Norm(0.13, 0.33, 0.49, 1.80, 1.12), Norms.Get(AgeGroup.Age12_15, Sex.Male, "learn.reading"));
    }

    [Theory]
    [InlineData("Á ekki við", 0)]
    [InlineData("á ekki við ", 0)]
    [InlineData("A ekki vid", 0)]
    [InlineData("Á stundum/ að nokkru leyti við", 1)]
    [InlineData("Á stundum / að nokkru leyti við", 1)]
    [InlineData("Á við", 2)]
    [InlineData("á við.", 2)]
    [InlineData("2", 2)]
    public void Parses_answer_text(string raw, int expected)
    {
        var p = AnswerParser.Parse(raw);
        Assert.Equal(CellKind.Item, p.Kind);
        Assert.Equal(expected, p.Score);
    }

    [Fact]
    public void Reads_answers_from_E2_downwards_and_skips_impact_answers()
    {
        var scores = ExampleScores();
        string Text(int? s) => s switch { 0 => "Á ekki við", 1 => "Á stundum/ að nokkru leyti við", 2 => "Á við", _ => "" };

        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Svör");
        ws.Cell("A1").Value = "Nr"; ws.Cell("E1").Value = "Svar";
        var row = 2;
        for (var i = 1; i <= Scales.ItemCount; i++)
        {
            ws.Cell(row, 1).Value = i;
            ws.Cell(row++, 5).Value = Text(scores[i]);
            if (i == 17) ws.Cell(row++, 5).Value = "lítið";
        }

        var answers = ExcelAnswerReader.Read(wb, "test.xlsx");
        Assert.Empty(answers.Warnings);
        Assert.Single(answers.Impact);
        Assert.Equal(17, answers.Impact[0].AfterItem);
        Assert.Equal("E20", answers.Items[17].SourceCell);

        var report = Scorer.Score(new ChildInfo("Dæmi", 6, Sex.Female), answers);
        Assert.Equal(1.40, Math.Round(report.Results.Single(r => r.Scale.Key == "exec").Score!.Value, 2));
    }

    [Fact]
    public void Reads_a_single_row_export_horizontally()
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Form1");
        for (var i = 1; i <= Scales.ItemCount; i++) ws.Cell(2, 4 + i).Value = i <= 7 ? "Á við" : "Á ekki við";

        var answers = ExcelAnswerReader.Read(wb, "form.xlsx");
        Assert.Contains("lína 2", answers.SourceDescription);
        Assert.Equal(2, answers.ScoreOf(7));
        Assert.Equal(0, answers.ScoreOf(181));
    }

    [Fact]
    public void Warns_when_rows_are_missing()
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Svör");
        for (var i = 0; i < 50; i++) ws.Cell(2 + i, 5).Value = "Á við";

        var answers = ExcelAnswerReader.Read(wb, "short.xlsx");
        Assert.Equal(Scales.ItemCount, answers.Items.Count);
        Assert.Contains(answers.Warnings, w => w.Contains("Aðeins 50"));
    }
}
