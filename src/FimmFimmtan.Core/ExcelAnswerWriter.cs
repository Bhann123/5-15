using ClosedXML.Excel;

namespace FimmFimmtan.Core;

/// <summary>
/// Writes answers in the layout <see cref="ExcelAnswerReader"/> reads: one answer per row in column E with the question
/// number in column A, and the daily-function questions (H1–H10) on their own rows after the section they follow.
/// </summary>
public static class ExcelAnswerWriter
{
    public static void Write(QuestionnaireAnswers answers, ChildInfo? child, string path)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Svör");
        ws.Cell(1, 1).Value = "Nr.";
        ws.Cell(1, 2).Value = "Spurning";
        ws.Cell(1, 3).Value = "Svið";
        ws.Cell(1, 4).Value = "Stig";
        ws.Cell(1, 5).Value = "Svar";
        ws.Row(1).Style.Font.Bold = true;

        var row = 1;
        var itemRows = new List<int>();
        var impactRows = new List<int>();
        foreach (var a in answers.Items)
        {
            row++;
            itemRows.Add(row);
            ws.Cell(row, 1).Value = a.Item;
            ws.Cell(row, 2).Value = ItemTexts.Get(a.Item);
            ws.Cell(row, 3).Value = Scales.All.Where(s => s.Items.Contains(a.Item)).OrderBy(s => s.IsDomain)
                .Select(s => s.NameIs).FirstOrDefault() ?? "Ekki skorað";
            if (a.Score is { } score)
            {
                ws.Cell(row, 4).Value = score;
                ws.Cell(row, 5).Value = AnswerParser.Label(score);
            }
            else if (a.NotRelevant)
            {
                ws.Cell(row, 5).Value = a.Raw;
            }

            if (ImpactQuestions.AfterItem(a.Item) is not { } q) continue;
            row++;
            impactRows.Add(row);
            ws.Cell(row, 1).Value = q.Key;
            ws.Cell(row, 2).Value = q.TextIs;
            ws.Cell(row, 3).Value = "Hömlun í daglegu lífi";
            if (answers.ImpactLevelOf(q.Number) is { } level)
            {
                ws.Cell(row, 4).Value = level;
                ws.Cell(row, 5).Value = ImpactQuestions.Label(level);
            }
            var range = ws.Range(row, 1, row, 5);
            range.Style.Fill.BackgroundColor = XLColor.FromHtml("#EEF4FA");
            range.Style.Font.Italic = true;
        }

        if (child != null)
        {
            ws.Cell(1, 7).Value = "Nafn"; ws.Cell(1, 8).Value = child.Name;
            ws.Cell(2, 7).Value = "Aldur"; ws.Cell(2, 8).Value = child.Age;
            ws.Cell(3, 7).Value = "Kyn"; ws.Cell(3, 8).Value = child.Sex == Sex.Male ? "Drengur" : "Stúlka";
            ws.Range(1, 7, 3, 7).Style.Font.Bold = true;
        }

        var itemOptions = $"\"{AnswerParser.Option0},{AnswerParser.Option1},{AnswerParser.Option2}\"";
        var impactOptions = $"\"{string.Join(",", ImpactQuestions.Options)}\"";
        foreach (var r in itemRows) ws.Cell(r, 5).CreateDataValidation().List(itemOptions);
        foreach (var r in impactRows) ws.Cell(r, 5).CreateDataValidation().List(impactOptions);

        ws.Column(1).Width = 6;
        ws.Column(2).Width = 70;
        ws.Column(3).Width = 28;
        ws.Column(4).Width = 6;
        ws.Column(5).Width = 32;
        ws.Column(7).Width = 8;
        ws.Column(8).Width = 24;
        ws.SheetView.FreezeRows(1);
        wb.SaveAs(path);
    }
}
