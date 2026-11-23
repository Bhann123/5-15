using System.Globalization;
using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace FimmFimmtan.Core;

public static class PdfReportWriter
{
    private static readonly CultureInfo Is = CultureInfo.GetCultureInfo("is-IS");

    static PdfReportWriter()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var asm = typeof(PdfReportWriter).Assembly;
        foreach (var name in asm.GetManifestResourceNames().Where(n => n.StartsWith("Fonts.") && n.EndsWith(".ttf")))
        {
            using var stream = asm.GetManifestResourceStream(name)!;
            FontManager.RegisterFont(stream);
        }
    }

    private static string F(double? v) => v?.ToString("0.00", Is) ?? "–";

    public static void Write(Report report, string path) => Build(report).GeneratePdf(path);

    public static byte[] Generate(Report report) => Build(report).GeneratePdf();

    private static void ProfileChart(IContainer container, string title, IReadOnlyList<ScaleResult> rows)
    {
        const float labelWidth = 150, valueWidth = 34, rowHeight = 15;
        container.Border(0.75f).BorderColor("#E2E7EE").Padding(10).Column(col =>
        {
            col.Item().PaddingBottom(6).Text(title).FontSize(11).SemiBold();
            foreach (var r in rows)
            {
                col.Item().Height(rowHeight).Row(row =>
                {
                    row.ConstantItem(labelWidth).AlignMiddle().Text(r.Scale.NameIs).FontSize(8)
                        .FontColor(r.Level == Level.NotScored ? "#94A3B8" : "#1F2937");
                    row.RelativeItem().Layers(layers =>
                    {
                        layers.PrimaryLayer().Row(zones =>
                        {
                            zones.RelativeItem((float)Profile.Band90).Background("#EEF1F5");
                            zones.RelativeItem((float)(Profile.Band98 - Profile.Band90)).Background("#FBEBC4");
                            zones.RelativeItem((float)(1 - Profile.Band98)).Background("#F6D2CD");
                        });
                        if (r.ProfilePosition is { } pos)
                        {
                            var color = r.Level switch { Level.Above98 => "#C2410C", Level.Above90 => "#D89512", _ => "#3B7CB8" };
                            var filled = (float)Math.Max(pos, 0.004);
                            layers.Layer().PaddingVertical(rowHeight / 4).Row(bar =>
                            {
                                bar.RelativeItem(filled).Background(color);
                                if (filled < 1) bar.RelativeItem(1 - filled);
                            });
                        }
                    });
                    row.ConstantItem(valueWidth).AlignMiddle().AlignRight().Text(t =>
                    {
                        var span = t.Span(F(r.Score)).FontSize(8).SemiBold();
                        if (r.Level == Level.Above98) span.FontColor("#B91C1C");
                        else if (r.Level == Level.Above90) span.FontColor("#B45309");
                    });
                });
            }
            col.Item().PaddingTop(4).Row(row =>
            {
                row.ConstantItem(labelWidth);
                row.RelativeItem((float)Profile.Band90).AlignCenter().Text("0–90%").FontSize(7).FontColor("#6B7280");
                row.RelativeItem((float)(Profile.Band98 - Profile.Band90)).AlignCenter().Text("90–98%").FontSize(7).FontColor("#6B7280");
                row.RelativeItem((float)(1 - Profile.Band98)).AlignCenter().Text("98–100%").FontSize(7).FontColor("#6B7280");
                row.ConstantItem(valueWidth);
            });
        });
    }

    private static Document Build(Report report) => Document.Create(doc => doc.Page(page =>
    {
        page.Size(PageSizes.A4);
        page.Margin(36);
        page.DefaultTextStyle(t => t.FontFamily("Lato").FontSize(9).FontColor("#1F2937"));

        page.Header().Column(col =>
        {
            col.Item().Text("5-15 – Norrænn spurningalisti fyrir foreldra").FontSize(16).SemiBold().FontColor("#0F3D5E");
            col.Item().PaddingTop(2).Text("Niðurstöður skorunar miðað við viðmið").FontColor("#6B7280");
            col.Item().PaddingVertical(10).Row(row =>
            {
                void Field(string label, string value) => row.RelativeItem().Column(c =>
                {
                    c.Item().Text(label).FontSize(8).FontColor("#6B7280");
                    c.Item().Text(value).SemiBold();
                });
                Field("Nafn", string.IsNullOrWhiteSpace(report.Child.Name) ? "–" : report.Child.Name);
                Field("Aldur", $"{report.Child.Age} ára");
                Field("Kyn", report.Child.Sex == Sex.Male ? "Drengur" : "Stúlka");
                Field("Viðmiðunarhópur", $"{Norms.Label(report.Group)}, {Norms.Label(report.Child.Sex)}");
                Field("Dagsetning", DateTime.Today.ToString("d.M.yyyy", Is));
            });
        });

        page.Content().Column(col =>
        {
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(3.4f);
                    for (var i = 0; i < 7; i++) c.RelativeColumn(1);
                    c.RelativeColumn(2.2f);
                });

                table.Header(h =>
                {
                    foreach (var title in new[] { "Svið", "Niðurst.", "% svarað", "Miðgildi", "Meðaltal", "Staðalfr.", "90%", "98%", "Mat" })
                        h.Cell().Background("#E8EEF5").PaddingVertical(4).PaddingHorizontal(3).Text(title).SemiBold().FontSize(8);
                });

                foreach (var r in report.Results)
                {
                    var bg = r.Level switch { Level.Above98 => "#F8D0CC", Level.Above90 => "#FCE8B8", _ => r.Scale.IsDomain ? "#F7F9FB" : "#FFFFFF" };
                    var n = r.Norm is { IsAvailable: true } norm ? norm : null;
                    IContainer Cell() => table.Cell().Background(bg).BorderBottom(0.5f).BorderColor("#E5E7EB").PaddingVertical(3).PaddingHorizontal(3);
                    void Num(double? v) => Cell().AlignRight().Text(F(v));

                    Cell().PaddingLeft(r.Scale.IsDomain ? 3 : 12).Text(t =>
                    {
                        var s = t.Span(r.Scale.NameIs);
                        if (r.Scale.IsDomain) s.SemiBold();
                    });
                    Cell().AlignRight().Text(t =>
                    {
                        var s = t.Span(F(r.Score));
                        if (r.Level is Level.Above90 or Level.Above98) s.SemiBold();
                    });
                    Cell().AlignRight().Text(r.Score is null && n is null ? "–" : r.PercentAnswered.ToString("0", Is));
                    Num(n?.Median); Num(n?.Mean); Num(n?.SD); Num(n?.P90); Num(n?.P98);
                    Cell().PaddingLeft(8).Text(Scorer.Describe(r.Level) + (r.FewAnswers ? " (fá svör)" : "")).FontSize(8);
                }
            });

            col.Item().PaddingTop(12).Text(t =>
            {
                t.Span("Skorun: ").SemiBold();
                t.Span("Á ekki við = 0, Á stundum við = 1, Á við = 2. Niðurstaða hvers sviðs er meðaltal svaraðra spurninga á sviðinu. ");
                t.Span("Niðurstaða við eða yfir 90. hundraðsmarki viðmiðunarhóps er merkt gul, við eða yfir 98. hundraðsmarki rauð.");
                t.Span(" Spurningar 110 og 111 (styrkleikar) eru ekki skoraðar.");
            });
            if (report.Answers.Warnings.Count > 0)
                col.Item().PaddingTop(6).Text("Athugasemdir við innlestur: " + string.Join(" ", report.Answers.Warnings)).FontColor("#9A3412");
            col.Item().PaddingTop(6).Text("Listann má ekki nota einan og sér til að komast að greiningarniðurstöðu.").Italic().FontColor("#6B7280");

            col.Item().PageBreak();
            col.Item().Text("Hvert svið er sýnt á eigin hundraðsmarkakvarða viðmiðunarhópsins. Súlan sýnir hvar niðurstaða barnsins lendir.").FontColor("#6B7280");
            col.Item().PaddingTop(10).Element(c => ProfileChart(c, "Aðalþættir", Profile.Domains(report.Results).ToList()));
            col.Item().PaddingTop(16).Element(c => ProfileChart(c, "Undirþættir", Profile.Subdomains(report.Results).ToList()));
        });

        page.Footer().AlignRight().Text(t =>
        {
            t.Span("Bls. ").FontSize(8).FontColor("#9CA3AF");
            t.CurrentPageNumber().FontSize(8).FontColor("#9CA3AF");
        });
    }));
}
