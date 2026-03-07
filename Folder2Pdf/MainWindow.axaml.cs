using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace Folder2Pdf;

public partial class MainWindow : Window
{
    private bool _isConverting;
    private string? _lastOutputDir;
    private readonly ObservableCollection<string> _folders = new();

    private bool IsPdf => FormatPdf.IsChecked == true;
    private bool SeparateBySource => SeparateBySourceCheck.IsChecked == true;

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

        // Only flip extension in merged mode (separate mode has a directory path)
        if (!SeparateBySource && !string.IsNullOrWhiteSpace(OutputPathBox.Text))
        {
            var ext   = IsPdf ? ".pdf" : ".txt";
            var other = IsPdf ? ".txt" : ".pdf";
            if (OutputPathBox.Text!.EndsWith(other, StringComparison.OrdinalIgnoreCase))
                OutputPathBox.Text = Path.ChangeExtension(OutputPathBox.Text, ext);
        }
    }

    // ── Separate-by-source toggle ────────────────────────────────────────────

    private void SeparateBySource_Changed(object sender, RoutedEventArgs e)
    {
        if (OutputLabel is null) return; // guard during init

        OutputLabel.Text = SeparateBySource ? "Output Folder" : "Output File";

        if (_folders.Count > 0)
            OutputPathBox.Text = SeparateBySource
                ? BuildDefaultOutputDir()
                : BuildDefaultOutputPath(_folders, IsPdf ? ".pdf" : ".txt");
        else
            OutputPathBox.Text = "";
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
            OutputPathBox.Text = SeparateBySource
                ? BuildDefaultOutputDir()
                : BuildDefaultOutputPath(_folders, IsPdf ? ".pdf" : ".txt");
    }

    private void RemoveFolder_Click(object sender, RoutedEventArgs e)
    {
        var toRemove = FolderList.SelectedItems?.Cast<string>().ToList() ?? new List<string>();
        foreach (var path in toRemove)
            _folders.Remove(path);
    }

    // ── Output file/folder picker ────────────────────────────────────────────

    private async void BrowseOutput_Click(object sender, RoutedEventArgs e)
    {
        if (SeparateBySource)
        {
            var results = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select Output Folder"
            });
            if (results.Count > 0)
                OutputPathBox.Text = results[0].Path.LocalPath;
            return;
        }

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

        var expectedExt    = IsPdf ? ".pdf" : ".txt";
        var includeHeaders = IncludeHeadersCheck.IsChecked == true;
        var isPdf          = IsPdf;
        var separateMode   = SeparateBySource;
        var folderSnapshot = _folders.ToList();

        // Lock UI
        _isConverting = true;
        ConvertButton.IsEnabled    = false;
        OpenFolderButton.IsVisible = false;
        ProgressBar.IsVisible      = true;
        ProgressBar.Value          = 0;
        ProgressLabel.IsVisible    = true;
        ProgressLabel.Text         = "";

        try
        {
            if (separateMode)
            {
                var outDir = OutputPathBox.Text?.Trim();
                if (string.IsNullOrEmpty(outDir))
                    outDir = BuildDefaultOutputDir();
                Directory.CreateDirectory(outDir);

                int totalFolders = folderSnapshot.Count;
                for (int fi = 0; fi < totalFolders; fi++)
                {
                    var folder    = folderSnapshot[fi];
                    var folderNum = fi + 1; // capture for closure

                    AppendLog($"[{folderNum}/{totalFolders}] Scanning: {folder}");
                    var files = await Task.Run(() => PdfConverter.GetFilesRecursive(folder, extensions));
                    AppendLog($"  → {files.Count} file(s)");

                    if (files.Count == 0) continue;

                    var folderName = new DirectoryInfo(folder).Name;
                    var outFile    = Path.Combine(outDir, $"{folderName}_{DateTime.Now:yyyyMMdd_HHmmss}{expectedExt}");

                    var progress = new Progress<(int current, int total, string fileName)>(p =>
                    {
                        ProgressBar.Value  = (double)p.current / p.total * 100;
                        ProgressLabel.Text = $"[{folderNum}/{totalFolders}] {p.current}/{p.total}  –  {p.fileName}";
                        AppendLog($"    [{p.current}/{p.total}] {p.fileName}");
                    });

                    if (isPdf)
                        await Task.Run(() => PdfConverter.CreatePdfFromFiles(files, outFile, includeHeaders, progress));
                    else
                        await Task.Run(() => PdfConverter.CreateTxtFromFiles(files, outFile, includeHeaders, progress));

                    AppendLog($"✓  Created: {outFile}");
                }

                ProgressBar.Value  = 100;
                ProgressLabel.Text = "Done!";
                _lastOutputDir     = outDir;
            }
            else
            {
                // Merged mode
                var outputPath = OutputPathBox.Text?.Trim();
                if (string.IsNullOrEmpty(outputPath))
                    outputPath = BuildDefaultOutputPath(folderSnapshot, expectedExt);
                else if (!outputPath.EndsWith(expectedExt, StringComparison.OrdinalIgnoreCase))
                    outputPath = Path.ChangeExtension(outputPath, expectedExt);

                var outDir = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(outDir))
                    Directory.CreateDirectory(outDir);

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
                    ProgressBar.Value  = (double)p.current / p.total * 100;
                    ProgressLabel.Text = $"{p.current} / {p.total}  –  {p.fileName}";
                    AppendLog($"  [{p.current}/{p.total}] {p.fileName}");
                });

                if (isPdf)
                    await Task.Run(() => PdfConverter.CreatePdfFromFiles(allFiles, outputPath, includeHeaders, progress));
                else
                    await Task.Run(() => PdfConverter.CreateTxtFromFiles(allFiles, outputPath, includeHeaders, progress));

                ProgressBar.Value  = 100;
                ProgressLabel.Text = "Done!";
                AppendLog($"✓  {(isPdf ? "PDF" : "TXT")} created: {outputPath}");
                _lastOutputDir = outDir;
            }

            OpenFolderButton.IsVisible = true;
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

    // ── Open output folder ────────────────────────────────────────────────────

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_lastOutputDir) || !Directory.Exists(_lastOutputDir)) return;
        Process.Start(new ProcessStartInfo
        {
            FileName       = _lastOutputDir,
            UseShellExecute = true
        });
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string BuildDefaultOutputPath(IList<string> folders, string ext)
    {
        var baseName = folders.Count == 1
            ? new DirectoryInfo(folders[0]).Name
            : "Export";
        return Path.Combine(BuildDefaultOutputDir(), $"{baseName}_{DateTime.Now:yyyyMMdd_HHmmss}{ext}");
    }

    private static string BuildDefaultOutputDir() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Folder2PDF");

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
