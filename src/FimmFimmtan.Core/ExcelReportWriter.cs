using ClosedXML.Excel;

namespace FimmFimmtan.Core;

public static class ExcelReportWriter
{
    public static void Write(Report report, string path)
    {
        using var wb = new XLWorkbook();
        WriteResults(wb.AddWorksheet("Niðurstöður"), report);
        WriteAnswers(wb.AddWorksheet("Svör"), report.Answers);
        wb.SaveAs(path);
    }

    private static void WriteResults(IXLWorksheet ws, Report report)
    {
        var c = report.Child;
        ws.Cell("A1").Value = "5-15 – Norrænn spurningalisti fyrir foreldra";
        ws.Cell("A1").Style.Font.SetBold().Font.SetFontSize(14);
        ws.Cell("A3").Value = "Nafn"; ws.Cell("B3").Value = c.Name;
        ws.Cell("A4").Value = "Aldur"; ws.Cell("B4").Value = c.Age;
        ws.Cell("A5").Value = "Kyn"; ws.Cell("B5").Value = c.Sex == Sex.Male ? "Drengur" : "Stúlka";
        ws.Cell("A6").Value = "Viðmiðunarhópur"; ws.Cell("B6").Value = $"{Norms.Label(report.Group)}, {Norms.Label(c.Sex)}";
        ws.Cell("A7").Value = "Svör"; ws.Cell("B7").Value = report.Answers.SourceDescription;
        ws.Cell("A8").Value = "Dagsetning"; ws.Cell("B8").Value = DateTime.Today; ws.Cell("B8").Style.DateFormat.Format = "d.M.yyyy";
        ws.Range("A3:A8").Style.Font.SetBold();
        ws.Cell("B4").Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Left);

        var headers = new[] { "Svið", "Domain", "Niðurstaða", "% svarað", "Miðgildi", "Meðaltal", "Staðalfrávik", "90%", "98%", "z", "Mat" };
        const int top = 10;
        for (var i = 0; i < headers.Length; i++) ws.Cell(top, i + 1).Value = headers[i];
        ws.Range(top, 1, top, headers.Length).Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.FromHtml("#E8EEF5"));

        var row = top + 1;
        foreach (var r in report.Results)
        {
            ws.Cell(row, 1).Value = r.Scale.IsDomain ? r.Scale.NameIs : "   " + r.Scale.NameIs;
            ws.Cell(row, 2).Value = r.Scale.NameEn;
            if (r.Score is { } s) ws.Cell(row, 3).Value = Math.Round(s, 2);
            ws.Cell(row, 4).Value = Math.Round(r.PercentAnswered, 0);
            if (r.Norm is { IsAvailable: true } n)
            {
                ws.Cell(row, 5).Value = n.Median; ws.Cell(row, 6).Value = n.Mean; ws.Cell(row, 7).Value = n.SD;
                ws.Cell(row, 8).Value = n.P90; ws.Cell(row, 9).Value = n.P98;
            }
            if (r.Z is { } z) ws.Cell(row, 10).Value = Math.Round(z, 2);
            ws.Cell(row, 11).Value = Scorer.Describe(r.Level) + (r.FewAnswers ? " (fá svör)" : "");
            if (r.Scale.IsDomain) ws.Range(row, 1, row, 11).Style.Font.SetBold();
            var fill = r.Level switch { Level.Above98 => "#F8D0CC", Level.Above90 => "#FCE8B8", _ => null };
            if (fill != null) ws.Range(row, 3, row, 11).Style.Fill.SetBackgroundColor(XLColor.FromHtml(fill));
            row++;
        }
        ws.Range(top + 1, 3, row, 10).Style.NumberFormat.Format = "0.00";
        ws.Range(top + 1, 4, row, 4).Style.NumberFormat.Format = "0";

        row += 1;
        ws.Cell(row++, 1).Value = "Skorun: Á ekki við = 0, Á stundum/að nokkru leyti við = 1, Á við = 2. Niðurstaða sviðs er meðaltal svaraðra spurninga.";
        ws.Cell(row++, 1).Value = "Niðurstaða við eða yfir 90. hundraðsmarki viðmiðunarhóps er merkt gul, við eða yfir 98. hundraðsmarki rauð.";
        if (!report.Options.IncludePositiveItems) ws.Cell(row++, 1).Value = "Spurningar 110 og 111 eru ekki taldar með.";
        ws.Cell(row, 1).Value = "Listann má ekki nota einan og sér til að komast að greiningarniðurstöðu.";

        ws.Columns(1, 2).AdjustToContents();
        ws.Columns(3, 11).Width = 12;
        ws.Column(11).Width = 26;
    }

    private static void WriteAnswers(IXLWorksheet ws, QuestionnaireAnswers answers)
    {
        string[] headers = ["Nr.", "Spurning", "Svar", "Stig", "Reitur"];
        for (var i = 0; i < headers.Length; i++) ws.Cell(1, i + 1).Value = headers[i];
        ws.Range(1, 1, 1, headers.Length).Style.Font.SetBold();
        var row = 2;
        foreach (var a in answers.Items)
        {
            ws.Cell(row, 1).Value = a.Item;
            ws.Cell(row, 2).Value = ItemTexts.Get(a.Item);
            ws.Cell(row, 3).Value = a.Raw;
            if (a.Score is { } s) ws.Cell(row, 4).Value = s;
            ws.Cell(row, 5).Value = a.SourceCell;
            row++;
        }
        ws.Column(1).Width = 6;
        ws.Column(2).Width = 90;
        ws.Column(3).Width = 32;
        ws.Columns(4, 5).Width = 8;
    }
}
