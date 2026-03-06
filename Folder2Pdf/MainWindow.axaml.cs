using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace Folder2Pdf;

public partial class MainWindow : Window
{
    private bool _isConverting;

    public MainWindow() => InitializeComponent();

    // ── Folder picker ────────────────────────────────────────────────────────

    private async void BrowseFolder_Click(object sender, RoutedEventArgs e)
    {
        var results = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select Source Folder",
            AllowMultiple = false
        });

        if (results.Count == 0) return;

        var path = results[0].Path.LocalPath;
        FolderPathBox.Text = path;

        // Auto-fill output path if the field is still empty
        if (string.IsNullOrWhiteSpace(OutputPathBox.Text))
            OutputPathBox.Text = BuildDefaultOutputPath(path);
    }

    // ── Output file picker ───────────────────────────────────────────────────

    private async void BrowseOutput_Click(object sender, RoutedEventArgs e)
    {
        var result = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save PDF As",
            DefaultExtension = "pdf",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("PDF Files") { Patterns = new[] { "*.pdf" } }
            }
        });

        if (result is not null)
            OutputPathBox.Text = result.Path.LocalPath;
    }

    // ── Convert ──────────────────────────────────────────────────────────────

    private async void Convert_Click(object sender, RoutedEventArgs e)
    {
        if (_isConverting) return;

        var folderPath = FolderPathBox.Text?.Trim();
        if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
        {
            AppendLog("⚠  Please select a valid source folder first.");
            return;
        }

        // Parse extensions
        var extInput = ExtensionsBox.Text?.Trim();
        HashSet<string> extensions;
        if (string.IsNullOrEmpty(extInput))
        {
            extensions = PdfConverter.DefaultTextExtensions;
            AppendLog($"Using default extensions: {string.Join(", ", extensions)}");
        }
        else
        {
            extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var raw in extInput.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                var ext = raw.Trim();
                if (!ext.StartsWith('.')) ext = "." + ext;
                extensions.Add(ext);
            }
            AppendLog($"Using extensions: {string.Join(", ", extensions)}");
        }

        // Resolve output path
        var outputPath = OutputPathBox.Text?.Trim();
        if (string.IsNullOrEmpty(outputPath))
            outputPath = BuildDefaultOutputPath(folderPath);

        if (!outputPath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            outputPath += ".pdf";

        var outDir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outDir))
            Directory.CreateDirectory(outDir);

        var includeHeaders = IncludeHeadersCheck.IsChecked == true;

        // Lock UI
        _isConverting = true;
        ConvertButton.IsEnabled = false;
        ProgressBar.IsVisible = true;
        ProgressBar.Value = 0;
        ProgressLabel.IsVisible = true;
        ProgressLabel.Text = "";

        AppendLog($"Scanning: {folderPath}");

        try
        {
            var files = await Task.Run(() => PdfConverter.GetFilesRecursive(folderPath, extensions));

            if (files.Count == 0)
            {
                AppendLog("⚠  No matching files found in the selected folder.");
                return;
            }

            AppendLog($"Found {files.Count} file(s) to process.");

            // Progress<T> captures the UI SynchronizationContext, so callbacks run on the UI thread.
            var progress = new Progress<(int current, int total, string fileName)>(p =>
            {
                var pct = (double)p.current / p.total * 100;
                ProgressBar.Value = pct;
                ProgressLabel.Text = $"{p.current} / {p.total}  –  {p.fileName}";
                AppendLog($"  [{p.current}/{p.total}] {p.fileName}");
            });

            await Task.Run(() => PdfConverter.CreatePdfFromFiles(files, outputPath, includeHeaders, progress));

            ProgressBar.Value = 100;
            ProgressLabel.Text = "Done!";
            AppendLog($"✓  PDF created: {outputPath}");
        }
        catch (Exception ex)
        {
            AppendLog($"✗  Error: {ex.Message}");
        }
        finally
        {
            _isConverting = false;
            ConvertButton.IsEnabled = true;
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string BuildDefaultOutputPath(string folderPath)
    {
        var folderName = new DirectoryInfo(folderPath).Name;
        var outputDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Folder2PDF");
        return Path.Combine(outputDir, $"{folderName}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");
    }

    private void AppendLog(string message)
    {
        void Append()
        {
            StatusLog.Text = (StatusLog.Text ?? "") + message + "\n";
            // Move caret to end so the TextBox scrolls to show the latest line
            StatusLog.CaretIndex = StatusLog.Text.Length;
        }

        if (Dispatcher.UIThread.CheckAccess())
            Append();
        else
            Dispatcher.UIThread.Post(Append);
    }
}
