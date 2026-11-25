using ClosedXML.Excel;

namespace FimmFimmtan.Core;

public static class ExcelAnswerReader
{
    public const string StartCell = "E2";
    private const int StartRow = 2;
    private const int StartColumn = 5;

    /// <summary>
    /// Reads the parent's answers starting at E2 and going down column E. If column E is (almost) empty but
    /// row 2 has answers from E to the right (e.g. a Microsoft/Google Forms export with one row per
    /// respondent), the row is read instead.
    /// </summary>
    public static QuestionnaireAnswers Read(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var wb = new XLWorkbook(stream);
        return Read(wb, Path.GetFileName(path));
    }

    public static QuestionnaireAnswers Read(XLWorkbook wb, string fileName)
    {
        foreach (var ws in wb.Worksheets)
        {
            var keyed = ReadKeyedRows(ws);
            if (keyed.Count(r => int.TryParse(r.Key.Trim().TrimEnd('.'), out var n) && n is >= 1 and <= Scales.ItemCount) >= 100
                && keyed.Any(r => AnswerParser.Parse(r.Raw).Kind == CellKind.Item))
            {
                var keyedAnswers = AnswerParser.MapKeyed(keyed, $"{fileName} · blað „{ws.Name}“ · dálkur E, spurninganúmer í dálki A");
                ReadChildInfo(ws, keyedAnswers, fileName);
                return keyedAnswers;
            }

            if (ReadLabeledRows(ws) is { } labeled && labeled.Rows.Count(r => AnswerParser.Parse(r.Raw).Kind == CellKind.Item) >= 20)
            {
                var labeledAnswers = AnswerParser.MapLabeled(labeled.Rows,
                    $"{fileName} · blað „{ws.Name}“ · dálkur {labeled.AnswerColumn}, spurningar þekktar á texta");
                ReadChildInfo(ws, labeledAnswers, fileName);
                return labeledAnswers;
            }

            var down = ReadLine(ws, StartRow, StartColumn, vertical: true);
            var across = ReadLine(ws, StartRow, StartColumn, vertical: false);
            int CountAnswers(List<(string Raw, string Cell)> c) => c.Count(x => AnswerParser.Parse(x.Raw).Kind is CellKind.Item or CellKind.Impact);
            var downCount = CountAnswers(down);
            var acrossCount = CountAnswers(across);
            if (downCount == 0 && acrossCount == 0) continue;

            if (acrossCount > downCount * 4 && acrossCount >= 20)
                return AnswerParser.Map(across, $"{fileName} · blað „{ws.Name}“ · lína 2 frá E til hægri");
            var answers = AnswerParser.Map(down, $"{fileName} · blað „{ws.Name}“ · dálkur E frá E2 og niður");
            ReadChildInfo(ws, answers, fileName);
            return answers;
        }
        throw new InvalidDataException(
            $"Engin svör fundust í {fileName}. Svörin eiga að byrja í reit {StartCell} og halda áfram niður dálk E " +
            $"(„{AnswerParser.Option0}“, „{AnswerParser.Option1}“ eða „{AnswerParser.Option2}“).");
    }

    private static void ReadChildInfo(IXLWorksheet ws, QuestionnaireAnswers answers, string fileName)
    {
        // 5-15.org exports are named "<name> - 5-15 Norrænn spurningalisti ....xlsx".
        var dash = fileName.IndexOf(" - 5-15", StringComparison.Ordinal);
        if (dash > 0) answers.ChildName = fileName[..dash].Trim();

        for (var row = 1; row <= 6; row++)
        for (var col = 7; col <= 9; col++)
        {
            var label = AnswerParser.Normalize(ws.Cell(row, col).GetFormattedString());
            var value = ws.Cell(row, col + 1).GetFormattedString().Trim();
            if (value.Length == 0) continue;
            switch (label)
            {
                case "nafn": answers.ChildName = value; break;
                case "aldur" when int.TryParse(value, out var age): answers.ChildAge = age; break;
                case "kyn":
                    var sex = AnswerParser.Normalize(value);
                    if (sex.StartsWith("dreng") || sex.StartsWith("strak") || sex is "kk" or "karl") answers.ChildSex = Sex.Male;
                    else if (sex.StartsWith("stulk") || sex is "kvk" or "kona") answers.ChildSex = Sex.Female;
                    break;
            }
        }
    }

    private static readonly string[] AnswerHeaders = ["answer", "svar", "svor"];
    private static readonly string[] LabelHeaders = ["label", "spurning", "question", "texti", "text"];
    private static readonly string[] TypeHeaders = ["type", "tegund"];

    /// <summary>Reads rows below a header row that names an answer column and a question-text column, or null if there is none.</summary>
    private static (List<(string Type, string Label, string Raw, string Cell)> Rows, string AnswerColumn)? ReadLabeledRows(IXLWorksheet ws)
    {
        var lastColumn = ws.Row(1).LastCellUsed()?.Address.ColumnNumber ?? 0;
        var headers = Enumerable.Range(1, lastColumn).Select(c => (Col: c, Name: AnswerParser.Normalize(ws.Cell(1, c).GetFormattedString()))).ToList();
        int? Find(string[] names) => headers.FirstOrDefault(h => names.Contains(h.Name)).Col is var c and > 0 ? c : null;
        if (Find(AnswerHeaders) is not { } answerCol || Find(LabelHeaders) is not { } labelCol) return null;
        var typeCol = Find(TypeHeaders);

        var last = ws.LastRowUsed()?.RowNumber() ?? 0;
        var rows = new List<(string, string, string, string)>();
        for (var r = StartRow; r <= last; r++)
        {
            var answer = ws.Cell(r, answerCol);
            rows.Add((typeCol is { } t ? ws.Cell(r, t).GetFormattedString() : "", ws.Cell(r, labelCol).GetFormattedString(),
                answer.GetFormattedString(), answer.Address.ToString()!));
        }
        return (rows, ws.Cell(1, answerCol).Address.ColumnLetter);
    }

    private static List<(string Key, string Text, string Raw, string Cell)> ReadKeyedRows(IXLWorksheet ws)
    {
        var last = Math.Max(ws.Column(1).LastCellUsed()?.Address.RowNumber ?? 0, ws.Column(StartColumn).LastCellUsed()?.Address.RowNumber ?? 0);
        var rows = new List<(string, string, string, string)>();
        for (var r = StartRow; r <= last; r++)
        {
            var e = ws.Cell(r, StartColumn);
            rows.Add((ws.Cell(r, 1).GetFormattedString(), ws.Cell(r, 2).GetFormattedString(), e.GetFormattedString(), e.Address.ToString()!));
        }
        return rows;
    }

    private static List<(string Raw, string Cell)> ReadLine(IXLWorksheet ws, int row, int col, bool vertical)
    {
        var last = vertical
            ? ws.Column(col).LastCellUsed()?.Address.RowNumber ?? 0
            : ws.Row(row).LastCellUsed()?.Address.ColumnNumber ?? 0;
        var list = new List<(string, string)>();
        for (var i = vertical ? row : col; i <= last; i++)
        {
            var cell = vertical ? ws.Cell(i, col) : ws.Cell(row, i);
            list.Add((cell.GetFormattedString(), cell.Address.ToString()!));
        }
        return list;
    }
}
