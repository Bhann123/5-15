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
            var down = ReadLine(ws, StartRow, StartColumn, vertical: true);
            var across = ReadLine(ws, StartRow, StartColumn, vertical: false);
            int CountAnswers(List<(string Raw, string Cell)> c) => c.Count(x => AnswerParser.Parse(x.Raw).Kind is CellKind.Item or CellKind.Impact);
            var downCount = CountAnswers(down);
            var acrossCount = CountAnswers(across);
            if (downCount == 0 && acrossCount == 0) continue;

            if (acrossCount > downCount * 4 && acrossCount >= 20)
                return AnswerParser.Map(across, $"{fileName} · blað „{ws.Name}“ · lína 2 frá E til hægri");
            var answers = AnswerParser.Map(down, $"{fileName} · blað „{ws.Name}“ · dálkur E frá E2 og niður");
            ReadChildInfo(ws, answers);
            return answers;
        }
        throw new InvalidDataException(
            $"Engin svör fundust í {fileName}. Svörin eiga að byrja í reit {StartCell} og halda áfram niður dálk E " +
            $"(„{AnswerParser.Option0}“, „{AnswerParser.Option1}“ eða „{AnswerParser.Option2}“).");
    }

    private static void ReadChildInfo(IXLWorksheet ws, QuestionnaireAnswers answers)
    {
        for (var row = 1; row <= 3; row++)
        {
            var label = AnswerParser.Normalize(ws.Cell(row, 7).GetFormattedString());
            var value = ws.Cell(row, 8).GetFormattedString().Trim();
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
