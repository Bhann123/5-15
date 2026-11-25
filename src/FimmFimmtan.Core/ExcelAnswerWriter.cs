using ClosedXML.Excel;

namespace FimmFimmtan.Core;

/// <summary>
/// Writes answers in the layout of the 5-15.org export (id, type, number, label, answer), which
/// <see cref="ExcelAnswerReader"/> reads: answers 0–2 in column E, the daily-function questions (0–3) on their own
/// rows after the section they follow, and the "younger than 8" note as a text row before question 93.
/// </summary>
public static class ExcelAnswerWriter
{
    /// <summary>"131" is the number column's value for question 122 in this layout, as in the 5-15.org export.</summary>
    public const string YoungerThan8Note = "Ef barnið er yngra en 8 ára vinsamlegast svarið spurningu 131 næst:";

    public static void Write(QuestionnaireAnswers answers, ChildInfo? child, string path)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("5-15 Norrænn spurningalisti");
        string[] headers = ["id", "type", "number", "label", "answer", "svar"];
        for (var c = 0; c < headers.Length; c++) ws.Cell(1, c + 1).Value = headers[c];
        ws.Row(1).Style.Font.Bold = true;

        var row = 1;
        var itemRows = new List<int>();
        var impactRows = new List<int>();
        void Add(string id, string type, string label)
        {
            row++;
            ws.Cell(row, 1).Value = id;
            ws.Cell(row, 2).Value = type;
            ws.Cell(row, 3).Value = row - 1;
            ws.Cell(row, 4).Value = label;
        }

        foreach (var a in answers.Items)
        {
            if (a.Item == 93)
            {
                Add("", "textbox", YoungerThan8Note);
                ws.Range(row, 1, row, 6).Style.Font.FontColor = XLColor.FromHtml("#64748B");
            }

            Add($"q{a.Item}", "checkbox", ItemTexts.Get(a.Item));
            itemRows.Add(row);
            if (a.Score is { } score)
            {
                ws.Cell(row, 5).Value = score;
                ws.Cell(row, 6).Value = AnswerParser.Label(score);
            }
            else if (a.NotRelevant)
            {
                ws.Cell(row, 5).Value = a.Raw;
            }

            if (ImpactQuestions.AfterItem(a.Item) is not { } q) continue;
            Add(q.Key.ToLowerInvariant(), "checkbox", q.TextIs);
            impactRows.Add(row);
            if (answers.ImpactLevelOf(q.Number) is { } level)
            {
                ws.Cell(row, 5).Value = level;
                ws.Cell(row, 6).Value = ImpactQuestions.Label(level);
            }
            ws.Range(row, 1, row, 6).Style.Fill.BackgroundColor = XLColor.FromHtml("#EEF4FA");
            ws.Range(row, 1, row, 6).Style.Font.Italic = true;
        }

        foreach (var r in itemRows) ws.Cell(r, 5).CreateDataValidation().List("\"0,1,2\"");
        foreach (var r in impactRows) ws.Cell(r, 5).CreateDataValidation().List("\"0,1,2,3\"");

        ws.Cell(1, 8).Value = "Svör: 0 = Á ekki við, 1 = Á stundum við, 2 = Á við. " +
                              "Hömlun í daglegu lífi: 0 = Alls ekki, 1 = Lítið, 2 = Mikið, 3 = Mjög mikið.";
        ws.Cell(1, 8).Style.Font.Italic = true;
        ws.Cell(1, 8).Style.Font.FontColor = XLColor.FromHtml("#64748B");
        if (child != null)
        {
            ws.Cell(3, 8).Value = "Nafn"; ws.Cell(3, 9).Value = child.Name;
            ws.Cell(4, 8).Value = "Aldur"; ws.Cell(4, 9).Value = child.Age;
            ws.Cell(5, 8).Value = "Kyn"; ws.Cell(5, 9).Value = child.Sex == Sex.Male ? "Drengur" : "Stúlka";
            ws.Range(3, 8, 5, 8).Style.Font.Bold = true;
        }

        ws.Column(1).Width = 6;
        ws.Column(2).Width = 10;
        ws.Column(3).Width = 8;
        ws.Column(4).Width = 80;
        ws.Column(5).Width = 8;
        ws.Column(6).Width = 18;
        ws.Column(8).Width = 8;
        ws.Column(9).Width = 24;
        ws.SheetView.FreezeRows(1);
        wb.SaveAs(path);
    }
}
