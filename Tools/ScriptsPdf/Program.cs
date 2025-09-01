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

        var now = DateTimeOffset.UtcNow;
        var sha = Environment.GetEnvironmentVariable("GITHUB_SHA") ?? "";
        var shortSha = sha.Length >= 7 ? sha[..7] : sha;
        var repo = Environment.GetEnvironmentVariable("GITHUB_REPOSITORY") ?? "";
        var branch = Environment.GetEnvironmentVariable("GITHUB_REF_NAME") ?? "";
        var fileCount = files.Count;
        var scanned = string.Join("; ", inputDirs);

        var doc = Document.Create(container =>
        {
            // Kansisivu
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(40);
                page.Content().Column(col =>
                {
                    col.Item().Text("RogueShooter – All Scripts").SemiBold().FontSize(24);

                    // Rivikohtainen fonttikoko:
                    col.Item().Text(txt =>
                    {
                        txt.Line($"Generated: {now:yyyy-MM-dd HH:mm} UTC").FontSize(12);
                        if (!string.IsNullOrWhiteSpace(repo))     txt.Line($"Repo: {repo}").FontSize(12);
                        if (!string.IsNullOrWhiteSpace(branch))   txt.Line($"Branch: {branch}").FontSize(12);
                        if (!string.IsNullOrWhiteSpace(shortSha)) txt.Line($"Commit: {shortSha}").FontSize(12);
                        txt.Line($"Files: {fileCount}").FontSize(12);
                        txt.Line($"Scanned: {scanned}").FontSize(12);
                    });
                });
            });

            // Varsinaiset sivut
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(30);
                page.DefaultTextStyle(ts => ts.FontSize(9));
                page.Header().Text("RogueShooter – All Scripts").SemiBold().FontSize(12);
                page.Footer().AlignCenter().Text(x =>
                {
                    x.CurrentPageNumber();
                    x.Span(" / ");
                    x.TotalPages();
                });

                page.Content().PaddingTop(8).Column(col =>
                {
                    foreach (var path in files)
                    {
                        var rel = path.Replace('\\', '/');
                        string code;
                        try { code = File.ReadAllText(path, new UTF8Encoding(false, false)); }
                        catch { code = "// [READ ERROR]"; }

                        col.Item().PaddingBottom(4).Text(rel).SemiBold().FontSize(11);

                        col.Item()
                           .Border(1).Padding(6).Background(Colors.Grey.Lighten4)
                           .DefaultTextStyle(ts => ts.FontFamily("Consolas").FontSize(8))
                           // 2024.3+: WrapAnywhere on poistettu -> annetaan layoutin hoitaa rivinvaihdot
                           .Text(t => t.Span(code));

                        col.Item().PageBreak();
                    }
                });
            });
        });

        doc.GeneratePdf(output);

        var full = Path.GetFullPath(output);
        Console.WriteLine(File.Exists(output)
            ? $"OK: {full}"
            : $"FAILED: File not found after generation: {full}");

        return 0;
    }
}
