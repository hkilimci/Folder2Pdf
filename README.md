# Folder2Pdf

Folder2Pdf is a cross-platform desktop app (Avalonia + .NET) that scans one or more folders and exports matched text files as:

- A single merged `PDF` or `TXT`, or
- Separate output files per source folder

## Features

- Multi-folder source selection
- Recursive file discovery in all subfolders
- Output format options: `PDF` and `TXT`
- Optional file path headers in output
- Optional "separate by source folder" mode
- Progress bar and live conversion log
- Encoding detection with BOM support and UTF-8 fallback

## Requirements

- .NET SDK 9.0+

NuGet dependencies are restored automatically:

- `Avalonia` / `Avalonia.Desktop` / `Avalonia.Themes.Fluent`
- `itext7`
- `itext7.bouncy-castle-adapter`

## Run

```bash
dotnet run --project Folder2Pdf/Folder2Pdf.csproj
```

## Build

```bash
dotnet build Folder2Pdf.sln
```

## Usage

1. Click `Add Folder...` and select one or more source folders.
2. (Optional) Toggle:
   - `Include file paths as headers`
   - `Separate output by source folder`
3. Choose output format (`PDF` or `TXT`).
4. Set file extensions (comma-separated), or leave blank for defaults.
5. Set output file/folder (or leave blank to auto-generate).
6. Click `Export`.

After export, use `Open Folder` to open the generated output directory.

## Default Behavior

- Default output directory: `~/Folder2PDF`
- Merged output filename:
  - Single source folder: `<FolderName>_yyyyMMdd_HHmmss.<ext>`
  - Multiple source folders: `Export_yyyyMMdd_HHmmss.<ext>`
- Separate mode output filename (per source folder):
  - `<FolderName>_yyyyMMdd_HHmmss.<ext>`

Default extensions (if extension box is empty):

`.txt, .cs, .java, .py, .js, .html, .css, .xml, .json, .md, .csv, .log, .sql, .sh, .bat, .ps1, .yaml, .yml, .ini, .config, .c, .cpp, .h, .hpp, .go, .ts, .rb, .php, .swift, .kt`

## Notes

- In `PDF` mode, each source file is placed on a separate page.
- Read errors are logged and do not stop the whole conversion.

## License

MIT. See [LICENSE](LICENSE).
