using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace Folder2Pdf;

public partial class MainWindow : Window
{
    private bool _isConverting;

    private bool IsPdf => FormatPdf.IsChecked == true;

    public MainWindow() => InitializeComponent();

    // ── Format radio button changed ──────────────────────────────────────────

    private void Format_Changed(object sender, RoutedEventArgs e)
    {
        if (ConvertButton is null) return; // guard during init

        ConvertButton.Content = IsPdf ? "Export to PDF" : "Export to TXT";

        // Re-derive the output path when the format changes and the field
        // was auto-filled (i.e. it still uses the previous auto-generated name).
        var folder = FolderPathBox.Text?.Trim();
        if (!string.IsNullOrEmpty(folder) && !string.IsNullOrWhiteSpace(OutputPathBox.Text))
        {
            var ext = IsPdf ? ".pdf" : ".txt";
            var other = IsPdf ? ".txt" : ".pdf";
            if (OutputPathBox.Text!.EndsWith(other, StringComparison.OrdinalIgnoreCase))
                OutputPathBox.Text = Path.ChangeExtension(OutputPathBox.Text, ext);
        }
    }

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
            OutputPathBox.Text = BuildDefaultOutputPath(path, IsPdf ? ".pdf" : ".txt");
    }

    // ── Output file picker ───────────────────────────────────────────────────

    private async void BrowseOutput_Click(object sender, RoutedEventArgs e)
    {
        FilePickerFileType fileType = IsPdf
            ? new FilePickerFileType("PDF Files") { Patterns = new[] { "*.pdf" } }
            : new FilePickerFileType("Text Files") { Patterns = new[] { "*.txt" } };

        var result = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = IsPdf ? "Save PDF As" : "Save TXT As",
            DefaultExtension = IsPdf ? "pdf" : "txt",
            FileTypeChoices = new[] { fileType }
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
        var expectedExt = IsPdf ? ".pdf" : ".txt";
        var outputPath = OutputPathBox.Text?.Trim();
        if (string.IsNullOrEmpty(outputPath))
            outputPath = BuildDefaultOutputPath(folderPath, expectedExt);
        else if (!outputPath.EndsWith(expectedExt, StringComparison.OrdinalIgnoreCase))
            outputPath = Path.ChangeExtension(outputPath, expectedExt);

        var outDir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outDir))
            Directory.CreateDirectory(outDir);

        var includeHeaders = IncludeHeadersCheck.IsChecked == true;
        var isPdf = IsPdf;

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

            // Progress<T> captures the UI SynchronizationContext → callbacks run on the UI thread.
            var progress = new Progress<(int current, int total, string fileName)>(p =>
            {
                ProgressBar.Value = (double)p.current / p.total * 100;
                ProgressLabel.Text = $"{p.current} / {p.total}  –  {p.fileName}";
                AppendLog($"  [{p.current}/{p.total}] {p.fileName}");
            });

            if (isPdf)
                await Task.Run(() => PdfConverter.CreatePdfFromFiles(files, outputPath, includeHeaders, progress));
            else
                await Task.Run(() => PdfConverter.CreateTxtFromFiles(files, outputPath, includeHeaders, progress));

            ProgressBar.Value = 100;
            ProgressLabel.Text = "Done!";
            AppendLog($"✓  {(isPdf ? "PDF" : "TXT")} created: {outputPath}");
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

    private static string BuildDefaultOutputPath(string folderPath, string ext)
    {
        var folderName = new DirectoryInfo(folderPath).Name;
        var outputDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Folder2PDF");
        return Path.Combine(outputDir, $"{folderName}_{DateTime.Now:yyyyMMdd_HHmmss}{ext}");
    }

    private void AppendLog(string message)
    {
        void Append()
        {
            StatusLog.Text = (StatusLog.Text ?? "") + message + "\n";
            StatusLog.CaretIndex = StatusLog.Text.Length;
        }

        if (Dispatcher.UIThread.CheckAccess())
            Append();
        else
            Dispatcher.UIThread.Post(Append);
    }
}
