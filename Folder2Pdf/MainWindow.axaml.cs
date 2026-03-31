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
    private bool IncludeTimestamp => TimestampCheck.IsChecked == true;

    private string TimestampSuffix => IncludeTimestamp ? $"_{DateTime.Now:yyyyMMdd_HHmmss}" : "";

    private string OutputExtension
    {
        get
        {
            if (FormatPdf.IsChecked == true) return ".pdf";
            if (FormatTxt.IsChecked == true) return ".txt";
            if (FormatMd.IsChecked == true) return ".md";
            // "Other" — parse from custom box
            var custom = CustomFormatBox?.Text?.Trim() ?? "";
            if (!string.IsNullOrEmpty(custom))
                return custom.StartsWith('.') ? custom : "." + custom;
            return ".txt"; // fallback
        }
    }

    public MainWindow()
    {
        InitializeComponent();
        FolderList.ItemsSource = _folders;
    }

    // ── Format radio button changed ──────────────────────────────────────────

    private void Format_Changed(object? sender, RoutedEventArgs e)
    {
        if (ConvertButton is null) return; // guard during init

        var ext = OutputExtension;
        var isOther = FormatOther.IsChecked == true;
        CustomFormatBox.IsVisible = isOther;

        ConvertButton.Content = $"Export to {ext.TrimStart('.').ToUpperInvariant()}";

        // Update extension in merged mode output path
        if (!SeparateBySource && !string.IsNullOrWhiteSpace(OutputPathBox.Text))
        {
            var currentExt = Path.GetExtension(OutputPathBox.Text);
            if (!string.IsNullOrEmpty(currentExt))
                OutputPathBox.Text = Path.ChangeExtension(OutputPathBox.Text, ext);
        }
    }

    // ── Separate-by-source toggle ────────────────────────────────────────────

    private void SeparateBySource_Changed(object? sender, RoutedEventArgs e)
    {
        if (OutputLabel is null) return; // guard during init

        OutputLabel.Text = SeparateBySource ? "Output Folder" : "Output File";

        if (_folders.Count > 0)
            OutputPathBox.Text = SeparateBySource
                ? BuildDefaultOutputDir()
                : BuildDefaultOutputPath(_folders, OutputExtension);
        else
            OutputPathBox.Text = "";
    }

    // ── Extensions combo changed ─────────────────────────────────────────────

    private void Extensions_Changed(object? sender, SelectionChangedEventArgs e)
    {
        if (CustomExtensionsBox is null) return; // guard during init
        // Show custom text box only when "Other…" is selected (last item)
        CustomExtensionsBox.IsVisible = ExtensionsCombo.SelectedIndex == ExtensionsCombo.ItemCount - 1;
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
                : BuildDefaultOutputPath(_folders, OutputExtension);
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

        var ext = OutputExtension;
        var extNoDot = ext.TrimStart('.');
        var label = extNoDot.ToUpperInvariant();
        var fileType = new FilePickerFileType($"{label} Files") { Patterns = new[] { $"*{ext}" } };

        var result = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = $"Save {label} As",
            DefaultExtension = extNoDot,
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
        var selectedIndex = ExtensionsCombo.SelectedIndex;
        HashSet<string> extensions;
        if (selectedIndex == 0)
        {
            // "All supported (default)"
            extensions = PdfConverter.DefaultTextExtensions;
            AppendLog($"Using default extensions: {string.Join(", ", extensions)}");
        }
        else if (selectedIndex == ExtensionsCombo.ItemCount - 1)
        {
            // "Other…" — parse custom input
            var customInput = CustomExtensionsBox.Text?.Trim();
            if (string.IsNullOrEmpty(customInput))
            {
                extensions = PdfConverter.DefaultTextExtensions;
                AppendLog("No custom extensions specified, using defaults.");
            }
            else
            {
                extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var raw in customInput.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    var ext = raw.Trim();
                    if (!ext.StartsWith('.')) ext = "." + ext;
                    extensions.Add(ext);
                }
                AppendLog($"Using extensions: {string.Join(", ", extensions)}");
            }
        }
        else
        {
            // Specific extension selected from dropdown
            var selected = ((ComboBoxItem)ExtensionsCombo.SelectedItem!).Content!.ToString()!;
            extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { selected };
            AppendLog($"Using extension: {selected}");
        }

        var expectedExt    = OutputExtension;
        var includeHeaders = IncludeHeadersCheck.IsChecked == true;
        var isPdf          = IsPdf;
        var separateMode   = SeparateBySource;
        var folderSnapshot = _folders.ToList();
        var timestampSuffix = TimestampSuffix;

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
                    var outFile    = Path.Combine(outDir, $"{folderName}{timestampSuffix}{expectedExt}");

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
                    outputPath = BuildDefaultOutputPath(folderSnapshot, expectedExt, timestampSuffix);
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
                AppendLog($"✓  {expectedExt.TrimStart('.').ToUpperInvariant()} created: {outputPath}");
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
            ProgressBar.IsVisible = false;
            ProgressLabel.IsVisible = false;
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

    private string BuildDefaultOutputPath(IList<string> folders, string ext, string? timestamp = null)
    {
        var baseName = folders.Count == 1
            ? new DirectoryInfo(folders[0]).Name
            : "Export";
        return Path.Combine(BuildDefaultOutputDir(), $"{baseName}{timestamp ?? TimestampSuffix}{ext}");
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
