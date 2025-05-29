using System.Text;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using iText.Kernel.Font;
using iText.Layout.Properties;
using iText.IO.Font.Constants;

namespace Folder2Pdf
{
    class Program
    {
        // Common text-based file extensions
        private static readonly HashSet<string> DefaultTextExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".txt", ".cs", ".java", ".py", ".js", ".html", ".css", ".xml", ".json", ".md",
            ".csv", ".log", ".sql", ".sh", ".bat", ".ps1", ".yaml", ".yml", ".ini", ".config",
            ".c", ".cpp", ".h", ".hpp", ".go", ".ts", ".rb", ".php", ".swift", ".kt"
        };

        static void Main(string?[] args)
        {
            Console.WriteLine("Folder2PDF - Convert text files to PDF");
            
            // Get folder path
            var folderPath = GetFolderPath(args);
            
            if (string.IsNullOrEmpty(folderPath))
            {
                return;
            }

            // Ask if headers should be included
            var includeHeaders = AskForHeaderInclusion();
            
            // Get extensions to process
            var extensionsToProcess = GetFileExtensions();
            
            // Get output file path
            var outputPath = GetOutputFilePath(folderPath);
            
            // Process files and create PDF file
            try
            {
                var files = GetFilesRecursive(folderPath, extensionsToProcess);
                
                if (files.Count == 0)
                {
                    Console.WriteLine("No matching files found in the specified folder.");
                    return;
                }
                
                Console.WriteLine($"Found {files.Count} files to process.");
                CreatePdfFromFiles(files, outputPath, includeHeaders);
                
                Console.WriteLine($"PDF successfully created at: {outputPath}");
                Console.WriteLine("Press any key to exit.");
                Console.ReadKey();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
                Console.WriteLine("Press any key to exit.");
                Console.ReadKey();
            }
        }
        
        private static string? GetFolderPath(string?[] args)
        {
            string? folderPath;
            
            if (args.Length > 0 && Directory.Exists(args[0]))
            {
                folderPath = args[0];
            }
            else
            {
                Console.WriteLine("Please enter the path to the folder containing text files:");
                folderPath = Console.ReadLine()?.Trim('"', ' ');
            }
            
            if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
            {
                Console.WriteLine("Invalid folder path or folder does not exist.");
                return null;
            }
            
            return folderPath;
        }
        
        private static bool AskForHeaderInclusion()
        {
            Console.WriteLine("Include file paths as headers in the PDF? (y/n) [Default: y]");
            
            var response = Console.ReadLine()?.Trim().ToLowerInvariant() ?? "y";
            
            return response != "n";
        }
        
        private static HashSet<string> GetFileExtensions()
        {
            Console.WriteLine("Enter file extensions to process (comma separated, e.g., .txt,.cs,.md)\nOr press Enter to use default list of text-based file extensions:");
            var input = Console.ReadLine()?.Trim();
            
            if (string.IsNullOrEmpty(input))
            {
                Console.WriteLine($"Using default extensions: {string.Join(", ", DefaultTextExtensions)}");
                return DefaultTextExtensions;
            }
            
            var extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            
            foreach (var ext in input.Split(','))
            {
                var trimmedExt = ext.Trim();
                
                if (!trimmedExt.StartsWith('.'))
                {
                    trimmedExt = "." + trimmedExt;
                }
                
                extensions.Add(trimmedExt);
            }
            
            return extensions;
        }
        
        private static string GetOutputFilePath(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath))
            {
                Console.WriteLine("Invalid folder path.");
                return string.Empty;
            }
            
            // Create output directory under the application folder
            var outputDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "output");
            
            // Ensure the output directory exists
            if (!Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }
            
            // Get just the last folder name from the path
            var lastFolderName = new DirectoryInfo(folderPath).Name;
            
            // Create a filename based on the last folder name
            var outputFileName = $"{lastFolderName}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
            
            var defaultPath = Path.Combine(outputDir, outputFileName);
            
            Console.WriteLine($"Enter output PDF path [Default: {defaultPath}]:");
            var outputPath = Console.ReadLine()?.Trim();
            
            if (string.IsNullOrEmpty(outputPath))
            {
                return defaultPath;
            }
            
            // Add a .pdf extension if not present
            if (!outputPath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                outputPath += ".pdf";
            }
            
            return outputPath;
        }
        
        private static List<string> GetFilesRecursive(string folderPath, HashSet<string> extensions)
        {
            var files = new List<string>();
            
            try
            {
                // Process all files in the current directory
                foreach (var file in Directory.GetFiles(folderPath))
                {
                    var ext = Path.GetExtension(file);
                    if (extensions.Contains(ext))
                    {
                        files.Add(file);
                    }
                }
                
                // Recursively process all subdirectories
                foreach (var directory in Directory.GetDirectories(folderPath))
                {
                    files.AddRange(GetFilesRecursive(directory, extensions));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error accessing {folderPath}: {ex.Message}");
            }
            
            return files;
        }
        
        private static void CreatePdfFromFiles(List<string> files, string outputPath, bool includeHeaders)
        {
            // Create PDF writer
            var writer = new PdfWriter(outputPath);
            
            // Create PDF document
            using var pdfDoc = new PdfDocument(writer);
            
            // Create Document (iText representation of the PDF)
            using var document = new Document(pdfDoc);
            
            // Define fonts
            var regularFont = PdfFontFactory.CreateFont(StandardFonts.COURIER);
            var boldFont = PdfFontFactory.CreateFont(StandardFonts.COURIER_BOLD);
            
            var fileCount = 0;
            foreach (var file in files)
            {
                fileCount++;
                Console.WriteLine($"Processing {fileCount}/{files.Count}: {Path.GetFileName(file)}");
                
                // Add a new page for each file (except the first one)
                if (fileCount > 1)
                {
                    document.Add(new AreaBreak(AreaBreakType.NEXT_PAGE));
                }
                
                try
                {
                    // Add file path as a header if requested
                    if (includeHeaders)
                    {
                        var header = new Paragraph(file)
                            .SetFont(boldFont)
                            .SetFontSize(12)
                            .SetMarginBottom(20);
                        document.Add(header);
                    }
                    
                    // Read file content
                    var content = File.ReadAllText(file, GetEncoding(file));
                    
                    // Add content to the document with proper line breaks preserved
                    var text = new Text(content)
                        .SetFont(regularFont)
                        .SetFontSize(10);
                    
                    document.Add(new Paragraph(text));
                }
                catch (Exception ex)
                {
                    var error = new Paragraph($"Error reading file: {ex.Message}")
                        .SetFont(regularFont)
                        .SetFontSize(10)
                        .SetFontColor(new iText.Kernel.Colors.DeviceRgb(255, 0, 0));
                        
                    document.Add(error);
                }
            }
            
            // Close the document
            document.Close();
            pdfDoc.Close();
        }
        
        private static Encoding GetEncoding(string filePath)
        {
            // Try to detect encoding - simple version
            // For a more robust solution, consider using a library like Ude
            try
            {
                using var reader = new StreamReader(filePath, Encoding.Default, true);
                reader.Peek(); // This will detect encoding
                return reader.CurrentEncoding;
            }
            catch
            {
                return Encoding.UTF8; // Default to UTF8 if detection fails
            }
        }
    }
}
