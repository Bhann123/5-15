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

    /// <summary>Case report from 5-15.org (8-11 years, female) that includes the Learning domain.</summary>
    [Fact]
    public void Reproduces_5_15_org_case_report_including_learning()
    {
        var sums = new Dictionary<string, int>
        {
            ["exec.attention"] = 12, ["exec.hyper"] = 13, ["exec.passivity"] = 6, ["exec.planning"] = 5,
            ["perc.space"] = 1, ["perc.time"] = 1, ["perc.body"] = 1, ["memory"] = 6,
            ["lang.comprehension"] = 3, ["lang.communication"] = 3,
            ["learn.arithmetic"] = 1, ["learn.general"] = 1, ["learn.coping"] = 10,
            ["emo.internal"] = 6, ["emo.acting"] = 11,
        };
        var expected = new Dictionary<string, double>
        {
            ["motor"] = 0.00, ["exec"] = 1.44, ["exec.attention"] = 1.33, ["exec.hyper"] = 1.44, ["exec.passivity"] = 1.50,
            ["exec.planning"] = 1.67, ["perc"] = 0.17, ["perc.space"] = 0.20, ["perc.time"] = 0.25, ["perc.body"] = 0.20,
            ["memory"] = 0.55, ["lang"] = 0.29, ["lang.comprehension"] = 0.60, ["lang.communication"] = 1.00,
            ["learn"] = 0.44, ["learn.reading"] = 0.00, ["learn.arithmetic"] = 0.20, ["learn.general"] = 0.25,
            ["learn.coping"] = 1.00, ["social"] = 0.00, ["emo"] = 0.52, ["emo.internal"] = 0.50, ["emo.acting"] = 0.85,
            ["emo.obsessive"] = 0.00,
        };
        var scores = new int?[Scales.ItemCount + 1];
        for (var i = 1; i <= Scales.ItemCount; i++) scores[i] = 0;
        foreach (var scale in Scales.All.Where(s => sums.ContainsKey(s.Key)))
        {
            var remaining = sums[scale.Key];
            foreach (var item in scale.Items)
            {
                var v = Math.Min(2, remaining);
                scores[item] = v;
                remaining -= v;
            }
        }
        scores[110] = 2;
        scores[111] = 2;

        var report = Scorer.Score(new ChildInfo("Case", 9, Sex.Female), ToAnswers(scores));
        foreach (var (key, value) in expected)
            Assert.Equal(value, Math.Round(report.Results.Single(r => r.Scale.Key == key).Score!.Value, 2));
    }

    /// <summary>Second case report from 5-15.org (8-11 years, male), table cut to Median and Mid. v. columns.</summary>
    [Fact]
    public void Reproduces_second_5_15_org_case_report()
    {
        var sums = new Dictionary<string, int>
        {
            ["motor.gross"] = 2, ["motor.fine"] = 2, ["exec.attention"] = 16, ["exec.hyper"] = 14, ["exec.passivity"] = 5,
            ["exec.planning"] = 6, ["perc.body"] = 2, ["memory"] = 7, ["lang.comprehension"] = 3, ["lang.expressive"] = 2,
            ["lang.communication"] = 1, ["learn.reading"] = 6, ["learn.arithmetic"] = 2, ["learn.general"] = 2,
            ["learn.coping"] = 15, ["social"] = 26, ["emo.internal"] = 12, ["emo.acting"] = 11, ["emo.obsessive"] = 2,
        };
        var expected = new Dictionary<string, double>
        {
            ["motor"] = 0.24, ["motor.gross"] = 0.29, ["motor.fine"] = 0.20, ["exec"] = 1.64, ["exec.attention"] = 1.78,
            ["exec.hyper"] = 1.56, ["exec.passivity"] = 1.25, ["exec.planning"] = 2.00, ["perc"] = 0.11, ["perc.body"] = 0.40,
            ["memory"] = 0.64, ["lang"] = 0.29, ["lang.comprehension"] = 0.60, ["lang.expressive"] = 0.15,
            ["lang.communication"] = 0.33, ["learn"] = 0.93, ["learn.reading"] = 0.75, ["learn.arithmetic"] = 0.40,
            ["learn.general"] = 0.50, ["learn.coping"] = 1.50, ["social"] = 0.96, ["emo"] = 0.76, ["emo.internal"] = 1.00,
            ["emo.acting"] = 0.85, ["emo.obsessive"] = 0.25,
        };
        var scores = new int?[Scales.ItemCount + 1];
        for (var i = 1; i <= Scales.ItemCount; i++) scores[i] = 0;
        foreach (var scale in Scales.All.Where(s => sums.ContainsKey(s.Key)))
        {
            var remaining = sums[scale.Key];
            foreach (var item in scale.Items)
            {
                var v = Math.Min(2, remaining);
                scores[item] = v;
                remaining -= v;
            }
        }

        var report = Scorer.Score(new ChildInfo("Case", 10, Sex.Male), ToAnswers(scores));
        foreach (var (key, value) in expected)
            Assert.Equal(value, Math.Round(report.Results.Single(r => r.Scale.Key == key).Score!.Value, 2));
        Assert.Equal((0.07, 0.16), (report.Results.Single(r => r.Scale.Key == "motor").Norm!.Median, report.Results.Single(r => r.Scale.Key == "motor").Norm!.Mean));
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

    [Theory]
    // Points read off the profile chart in the example report (8-11 years, female; axis 0..1).
    [InlineData("motor", 0.00, 0.00)]
    [InlineData("perc", 0.17, 0.30)]
    [InlineData("learn", 0.44, 0.44)]
    [InlineData("memory", 0.55, 0.56)]
    [InlineData("emo", 0.52, 0.68)]
    [InlineData("exec", 1.44, 0.82)]
    [InlineData("emo.acting", 0.85, 0.77)]
    public void Profile_position_matches_example_chart(string key, double score, double expected)
    {
        var norm = Norms.Get(AgeGroup.Age8_11, Sex.Female, key)!;
        Assert.Equal(expected, Profile.Position(score, norm), 2);
    }

    [Fact]
    public void Profile_charts_split_into_domains_and_subdomains()
    {
        var report = Scorer.Score(new ChildInfo("Dæmi", 9, Sex.Male), ToAnswers(ExampleScores()));
        Assert.Equal(8, Profile.Domains(report.Results).Count());
        var subs = Profile.Subdomains(report.Results).Select(r => r.Scale.Key).ToList();
        Assert.Equal(22, subs.Count);
        Assert.Equal(["motor.gross", "motor.fine"], subs.Take(2));
        Assert.Contains("memory", subs);
        Assert.Contains("social", subs);
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
    public void Exports_excel_and_pdf_reports()
    {
        var report = Scorer.Score(new ChildInfo("Jón Jónsson", 9, Sex.Male), ToAnswers(ExampleScores()));
        var dir = Directory.CreateTempSubdirectory();
        var xlsx = Path.Combine(dir.FullName, "r.xlsx");
        var pdf = Path.Combine(dir.FullName, "r.pdf");

        ExcelReportWriter.Write(report, xlsx);
        PdfReportWriter.Write(report, pdf);

        using var wb = new XLWorkbook(xlsx);
        Assert.Equal("Jón Jónsson", wb.Worksheet("Niðurstöður").Cell("B3").GetString());
        Assert.Equal(Scales.ItemCount + 1, wb.Worksheet("Svör").LastRowUsed()!.RowNumber());
        Assert.True(new FileInfo(pdf).Length > 10_000);
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
