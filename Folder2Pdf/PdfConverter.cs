using System.Text;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using iText.Kernel.Font;
using iText.Layout.Properties;
using iText.IO.Font.Constants;

namespace Folder2Pdf;

public static class PdfConverter
{
    public static readonly HashSet<string> DefaultTextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".cs", ".java", ".py", ".js", ".html", ".css", ".xml", ".json", ".md",
        ".csv", ".log", ".sql", ".sh", ".bat", ".ps1", ".yaml", ".yml", ".ini", ".config",
        ".c", ".cpp", ".h", ".hpp", ".go", ".ts", ".rb", ".php", ".swift", ".kt"
    };

    public static List<string> GetFilesRecursive(string folderPath, HashSet<string> extensions)
    {
        var files = new List<string>();
        try
        {
            foreach (var file in Directory.GetFiles(folderPath))
            {
                if (extensions.Contains(Path.GetExtension(file)))
                    files.Add(file);
            }
            foreach (var directory in Directory.GetDirectories(folderPath))
                files.AddRange(GetFilesRecursive(directory, extensions));
        }
        catch (Exception ex)
        {
            // Surface error via progress so the UI can display it
            Console.Error.WriteLine($"Error accessing {folderPath}: {ex.Message}");
        }
        return files;
    }

    public static void CreatePdfFromFiles(
        List<string> files,
        string outputPath,
        bool includeHeaders,
        IProgress<(int current, int total, string fileName)>? progress = null)
    {
        var writer = new PdfWriter(outputPath);
        using var pdfDoc = new PdfDocument(writer);
        using var document = new Document(pdfDoc);

        var regularFont = PdfFontFactory.CreateFont(StandardFonts.COURIER);
        var boldFont = PdfFontFactory.CreateFont(StandardFonts.COURIER_BOLD);

        for (var i = 0; i < files.Count; i++)
        {
            var file = files[i];
            progress?.Report((i + 1, files.Count, Path.GetFileName(file)));

            if (i > 0)
                document.Add(new AreaBreak(AreaBreakType.NEXT_PAGE));

            try
            {
                if (includeHeaders)
                {
                    document.Add(new Paragraph(file)
                        .SetFont(boldFont)
                        .SetFontSize(12)
                        .SetMarginBottom(20));
                }

                var content = File.ReadAllText(file, GetEncoding(file));
                document.Add(new Paragraph(new Text(content)
                    .SetFont(regularFont)
                    .SetFontSize(10)));
            }
            catch (Exception ex)
            {
                document.Add(new Paragraph($"Error reading file: {ex.Message}")
                    .SetFont(regularFont)
                    .SetFontSize(10)
                    .SetFontColor(new iText.Kernel.Colors.DeviceRgb(255, 0, 0)));
            }
        }

        document.Close();
        pdfDoc.Close();
    }

    private static Encoding GetEncoding(string filePath)
    {
        try
        {
            using var reader = new StreamReader(filePath, Encoding.Default, detectEncodingFromByteOrderMarks: true);
            reader.Peek();
            return reader.CurrentEncoding;
        }
        catch
        {
            return Encoding.UTF8;
        }
    }
}
