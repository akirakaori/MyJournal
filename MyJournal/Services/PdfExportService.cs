using JournalMaui.Models;
using JournalMaui.Services;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Text.RegularExpressions;

namespace MyJournal.Services;

public class PdfExportService
{
    private readonly JournalDatabases _db;

    public PdfExportService(JournalDatabases db)
    {
        _db = db;
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public async Task<string> ExportEntriesToPdfAsync(DateTime startDate, DateTime endDate)
    {
        try
        {
            // Fetch entries in date range
            var entries = await _db.GetEntriesByDateRangeAsync(startDate, endDate);
            
            if (entries == null || entries.Count == 0)
            {
                throw new InvalidOperationException($"No journal entries found between {startDate:yyyy-MM-dd} and {endDate:yyyy-MM-dd}");
            }

            // Sort by DateKey ascending
            var sortedEntries = entries.OrderBy(e => e.DateKey).ToList();

            // Determine export folder
            string exportFolder = GetExportFolder();
            Directory.CreateDirectory(exportFolder);

            // Generate filename
            string fileName = $"Journal_Export_{startDate:yyyy-MM-dd}_to_{endDate:yyyy-MM-dd}.pdf";
            string filePath = Path.Combine(exportFolder, fileName);

            // Delete existing file if present
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            // Generate PDF
            GeneratePdf(sortedEntries, filePath, startDate, endDate);

            // Verify file was created
            if (!File.Exists(filePath))
            {
                throw new IOException($"PDF file was not created at {filePath}");
            }

            return filePath;
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to export PDF: {ex.Message}", ex);
        }
    }

    private string GetExportFolder()
    {
        // Try to save to Documents folder on Windows for visibility
        if (DeviceInfo.Platform == DevicePlatform.WinUI)
        {
            try
            {
                string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                string documentsExportPath = Path.Combine(documentsPath, "MyJournalExports");
                
                // Test write access
                Directory.CreateDirectory(documentsExportPath);
                string testFile = Path.Combine(documentsExportPath, ".test");
                File.WriteAllText(testFile, "test");
                File.Delete(testFile);
                
                return documentsExportPath;
            }
            catch
            {
                // Fallback to AppDataDirectory
            }
        }

        // Default: use AppDataDirectory (always works)
        string appDataExportPath = Path.Combine(FileSystem.AppDataDirectory, "MyJournalExports");
        return appDataExportPath;
    }

    private void GeneratePdf(List<JournalEntries> entries, string filePath, DateTime startDate, DateTime endDate)
    {
        try
        {
            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.PageColor(QuestPDF.Helpers.Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(11).FontColor(QuestPDF.Helpers.Colors.Black));

                    page.Header()
                        .Text($"Journal Export: {startDate:MMMM d, yyyy} - {endDate:MMMM d, yyyy}")
                        .SemiBold().FontSize(18).FontColor(QuestPDF.Helpers.Colors.Blue.Darken2);

                    page.Content()
                        .PaddingVertical(1, Unit.Centimetre)
                        .Column(column =>
                        {
                            column.Spacing(20);

                            foreach (var entry in entries)
                            {
                                column.Item().Element(c => ComposeEntryBlock(c, entry));
                            }
                        });

                    page.Footer()
                        .AlignCenter()
                        .Text(text =>
                        {
                            text.Span("Page ");
                            text.CurrentPageNumber();
                            text.Span(" of ");
                            text.TotalPages();
                        });
                });
            })
            .GeneratePdf(filePath);
        }
        catch (Exception ex)
        {
            throw new Exception($"QuestPDF generation failed: {ex.Message}", ex);
        }
    }

    private void ComposeEntryBlock(QuestPDF.Infrastructure.IContainer container, JournalEntries entry)
    {
        container.Border(1).BorderColor(QuestPDF.Helpers.Colors.Grey.Lighten2).Padding(15).Column(column =>
        {
            // Date header
            column.Item().Text(entry.DateKey).SemiBold().FontSize(14).FontColor(QuestPDF.Helpers.Colors.Blue.Medium);

            column.Item().PaddingVertical(5);

            // Title
            if (!string.IsNullOrWhiteSpace(entry.Title))
            {
                column.Item().Text(entry.Title).SemiBold().FontSize(12);
                column.Item().PaddingVertical(3);
            }

            // Mood
            if (!string.IsNullOrWhiteSpace(entry.PrimaryMood))
            {
                column.Item().Row(row =>
                {
                    row.AutoItem().Text("Mood: ").FontSize(9).FontColor(QuestPDF.Helpers.Colors.Grey.Darken1);
                    row.AutoItem().Text(entry.PrimaryMood).FontSize(9).SemiBold().FontColor(QuestPDF.Helpers.Colors.Blue.Medium);
                    
                    if (!string.IsNullOrWhiteSpace(entry.SecondaryMoodsCsv))
                    {
                        var secondaryMoods = entry.SecondaryMoodsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries);
                        if (secondaryMoods.Length > 0)
                        {
                            row.AutoItem().Text(" + ").FontSize(9).FontColor(QuestPDF.Helpers.Colors.Grey.Medium);
                            row.AutoItem().Text(string.Join(", ", secondaryMoods.Select(m => m.Trim())))
                                .FontSize(9).FontColor(QuestPDF.Helpers.Colors.Grey.Darken1);
                        }
                    }
                });
                column.Item().PaddingVertical(3);
            }

            // Content
            if (entry.HasPin)
            {
                column.Item().Text("Locked (content not exported)").Italic().FontColor(QuestPDF.Helpers.Colors.Grey.Darken1);
            }
            else if (string.IsNullOrWhiteSpace(entry.Content))
            {
                column.Item().Text("(No content)").Italic().FontColor(QuestPDF.Helpers.Colors.Grey.Medium);
            }
            else
            {
                string cleanContent = StripHtmlAndMarkdown(entry.Content);
                column.Item().Text(cleanContent).FontSize(10).LineHeight(1.4f);
            }

            // Timestamp
            column.Item().PaddingTop(8).Text($"Created: {entry.CreatedAt:MMM d, yyyy h:mm tt}")
                .FontSize(8).FontColor(QuestPDF.Helpers.Colors.Grey.Darken1);
        });
    }

    private string StripHtmlAndMarkdown(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        // Remove HTML tags
        string text = Regex.Replace(input, @"<[^>]+>", string.Empty);
        
        // Remove markdown bold/italic
        text = Regex.Replace(text, @"\*\*(.+?)\*\*", "$1");
        text = Regex.Replace(text, @"\*(.+?)\*", "$1");
        text = Regex.Replace(text, @"__(.+?)__", "$1");
        text = Regex.Replace(text, @"_(.+?)_", "$1");
        
        // Remove markdown headers
        text = Regex.Replace(text, @"^#+\s+", string.Empty, RegexOptions.Multiline);
        
        // Remove markdown links [text](url)
        text = Regex.Replace(text, @"\[([^\]]+)\]\([^\)]+\)", "$1");
        
        // Decode HTML entities
        text = System.Net.WebUtility.HtmlDecode(text);
        
        // Normalize whitespace
        text = Regex.Replace(text, @"\s+", " ");
        text = text.Trim();

        return text;
    }
}
