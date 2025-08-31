using System.Text;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

class Program
{
    static int Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("Usage: ScriptsPdf <inputDir1>[;<inputDir2>...] <outputPdfPath>");
            Console.WriteLine("Example: ScriptsPdf Assets/scripts;Assets/Other Docs/AllScripts.pdf");
            return 1;
        }

        var inputDirs = args[0].Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var output = args[1];

        var files = inputDirs
            .Where(Directory.Exists)
            .SelectMany(dir => Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories))
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (files.Count == 0)
        {
            Console.WriteLine("No .cs files found.");
            return 2;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        QuestPDF.Settings.License = LicenseType.Community;

        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(30);
                page.DefaultTextStyle(ts => ts.FontSize(9));
                page.Header().Text("RogueShooter – All Scripts").SemiBold().FontSize(14);
                page.Footer().AlignCenter().Text(x =>
                {
                    x.CurrentPageNumber();
                    x.Span(" / ");
                    x.TotalPages();
                });

                page.Content().PaddingTop(10).Column(col =>
                {
                    foreach (var path in files)
                    {
                        var rel = path.Replace('\\', '/');
                        string code;
                        try
                        {
                            code = File.ReadAllText(path, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false));
                        }
                        catch
                        {
                            code = "// [READ ERROR]";
                        }

                        // Otsikko
                        col.Item()
                           .PaddingBottom(4)
                           .Text(rel)
                           .SemiBold()
                           .FontSize(11);

                        // Koodilohko
                        col.Item()
                           .Border(1)
                           .Padding(6)
                           .Background(Colors.Grey.Lighten4)
                           .DefaultTextStyle(ts => ts.FontFamily("Consolas").FontSize(8))
                           .Text(t =>
                           {
                               // Pitkät rivit eivät katkea luonnostaan -> WrapAnywhere auttaa
                               t.Span(code).WrapAnywhere();
                           });

                        // Sivunvaihto seuraavaa tiedostoa kohti
                        col.Item().PageBreak();
                    }
                });
            });
        });

        doc.GeneratePdf(output);
        Console.WriteLine($"OK: {output}");
        return 0;
    }
}
