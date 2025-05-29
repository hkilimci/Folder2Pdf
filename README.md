# Folder2Pdf

A C# console application that converts text-based files in a folder (including subfolders) into a single PDF document.

## Features

- Converts all text files in a specified folder and its subfolders to a single PDF
- Each file appears on a separate page in the PDF
- Includes file paths as optional headers at the top of each page
- Preserves line breaks and formatting from the original files
- Handles files with different text encodings
- Automatically creates an output folder for the generated PDF
- Names the output file based on the source folder name

## Requirements

- .NET 9.0 or later
- Dependencies (installed via NuGet):
  - itext7 (v8.0.4)
  - itext7.bouncy-castle-adapter (v8.0.4)

## Installation

1. Clone the repository
2. Open the solution in Visual Studio, JetBrains Rider, or your preferred IDE
3. Restore NuGet packages
4. Build the solution

## Usage

Run the application from the command line:

```
dotnet run --project Folder2Pdf/Folder2Pdf.csproj [path-to-folder]
```

Or run the built executable directly:

```
Folder2Pdf.exe [path-to-folder]
```

### Interactive Mode

If you run the application without a command-line argument, it will:

1. Prompt you to enter the path to the folder containing text files
2. Ask if you want to include file paths as headers in the PDF
3. Allow you to specify which file extensions to process
4. Give you the option to customize the output PDF path

### Default Settings

- The PDF is saved to an `output` folder in the application directory
- The output filename is based on the last folder name of the input path
- Common text-based file extensions are included by default (.txt, .cs, .java, etc.)

## How It Works

The application:

1. Recursively scans the specified folder for text files with matching extensions
2. Creates a PDF document using iText7
3. Processes each file, adding its contents to a new page in the PDF
4. Detects file encoding to ensure text is properly displayed
5. Saves the PDF to the output location

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## Author

hhklmc