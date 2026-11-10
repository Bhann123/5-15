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
                t.Span("Á ekki við = 0, Á stundum/að nokkru leyti við = 1, Á við = 2. Niðurstaða hvers sviðs er meðaltal svaraðra spurninga á sviðinu. ");
                t.Span("Niðurstaða við eða yfir 90. hundraðsmarki viðmiðunarhóps er merkt gul, við eða yfir 98. hundraðsmarki rauð.");
                if (!report.Options.IncludePositiveItems) t.Span(" Spurningar 110 og 111 eru ekki taldar með.");
            });
            if (report.Answers.Warnings.Count > 0)
                col.Item().PaddingTop(6).Text("Athugasemdir við innlestur: " + string.Join(" ", report.Answers.Warnings)).FontColor("#9A3412");
            col.Item().PaddingTop(6).Text("Listann má ekki nota einan og sér til að komast að greiningarniðurstöðu.").Italic().FontColor("#6B7280");
        });

        page.Footer().AlignRight().Text(t =>
        {
            t.Span("Bls. ").FontSize(8).FontColor("#9CA3AF");
            t.CurrentPageNumber().FontSize(8).FontColor("#9CA3AF");
        });
    }));
}
