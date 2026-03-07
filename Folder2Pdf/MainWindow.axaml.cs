using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace Folder2Pdf;

public partial class MainWindow : Window
{
    private bool _isConverting;
    private readonly ObservableCollection<string> _folders = new();

    private bool IsPdf => FormatPdf.IsChecked == true;

    public MainWindow()
    {
        InitializeComponent();
        FolderList.ItemsSource = _folders;
    }

    // ── Format radio button changed ──────────────────────────────────────────

    private void Format_Changed(object sender, RoutedEventArgs e)
    {
        if (ConvertButton is null) return; // guard during init

        ConvertButton.Content = IsPdf ? "Export to PDF" : "Export to TXT";

        // Flip the extension of an auto-generated output path
        if (!string.IsNullOrWhiteSpace(OutputPathBox.Text))
        {
            var ext   = IsPdf ? ".pdf" : ".txt";
            var other = IsPdf ? ".txt" : ".pdf";
            if (OutputPathBox.Text!.EndsWith(other, StringComparison.OrdinalIgnoreCase))
                OutputPathBox.Text = Path.ChangeExtension(OutputPathBox.Text, ext);
        }
    }

    // ── Folder list management ───────────────────────────────────────────────

    private async void AddFolder_Click(object sender, RoutedEventArgs e)
    {
        var results = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Add Source Folder(s)",
            AllowMultiple = true
        });

        foreach (var item in results)
        {
            var path = item.Path.LocalPath;
            if (!_folders.Contains(path))
                _folders.Add(path);
        }

        // Auto-fill output path the first time a folder is added
        if (_folders.Count > 0 && string.IsNullOrWhiteSpace(OutputPathBox.Text))
            OutputPathBox.Text = BuildDefaultOutputPath(_folders, IsPdf ? ".pdf" : ".txt");
    }

    private void RemoveFolder_Click(object sender, RoutedEventArgs e)
    {
        // Collect selected items first to avoid modifying the collection while iterating
        var toRemove = FolderList.SelectedItems?.Cast<string>().ToList() ?? new List<string>();
        foreach (var path in toRemove)
            _folders.Remove(path);
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

        if (_folders.Count == 0)
        {
            AppendLog("⚠  Add at least one source folder first.");
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
            outputPath = BuildDefaultOutputPath(_folders, expectedExt);
        else if (!outputPath.EndsWith(expectedExt, StringComparison.OrdinalIgnoreCase))
            outputPath = Path.ChangeExtension(outputPath, expectedExt);

        var outDir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outDir))
            Directory.CreateDirectory(outDir);

        var includeHeaders = IncludeHeadersCheck.IsChecked == true;
        var isPdf = IsPdf;
        var folderSnapshot = _folders.ToList();

        // Lock UI
        _isConverting = true;
        ConvertButton.IsEnabled = false;
        ProgressBar.IsVisible = true;
        ProgressBar.Value = 0;
        ProgressLabel.IsVisible = true;
        ProgressLabel.Text = "";

        try
        {
            // Scan all folders and merge file lists
            var allFiles = new List<string>();
            foreach (var folder in folderSnapshot)
            {
                AppendLog($"Scanning: {folder}");
                var found = await Task.Run(() => PdfConverter.GetFilesRecursive(folder, extensions));
                AppendLog($"  → {found.Count} file(s)");
                allFiles.AddRange(found);
            }

            if (allFiles.Count == 0)
            {
                AppendLog("⚠  No matching files found in any of the selected folders.");
                return;
            }

            AppendLog($"Total: {allFiles.Count} file(s) to process.");

            var progress = new Progress<(int current, int total, string fileName)>(p =>
            {
                ProgressBar.Value = (double)p.current / p.total * 100;
                ProgressLabel.Text = $"{p.current} / {p.total}  –  {p.fileName}";
                AppendLog($"  [{p.current}/{p.total}] {p.fileName}");
            });

            if (isPdf)
                await Task.Run(() => PdfConverter.CreatePdfFromFiles(allFiles, outputPath, includeHeaders, progress));
            else
                await Task.Run(() => PdfConverter.CreateTxtFromFiles(allFiles, outputPath, includeHeaders, progress));

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

    private static string BuildDefaultOutputPath(IList<string> folders, string ext)
    {
        var baseName = folders.Count == 1
            ? new DirectoryInfo(folders[0]).Name
            : "Export";
        var outputDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Folder2PDF");
        return Path.Combine(outputDir, $"{baseName}_{DateTime.Now:yyyyMMdd_HHmmss}{ext}");
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
