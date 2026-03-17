@echo off
setlocal

set APP_NAME=Folder2Pdf
set PROJECT_DIR=Folder2Pdf
set OUTPUT_DIR=publish

echo Building %APP_NAME% for Windows...

:: Clean previous builds
if exist "%OUTPUT_DIR%" rmdir /s /q "%OUTPUT_DIR%"

:: Publish as self-contained single-file app
dotnet publish "%PROJECT_DIR%\%APP_NAME%.csproj" ^
    -c Release ^
    -r win-x64 ^
    --self-contained true ^
    -p:PublishSingleFile=true ^
    -p:IncludeNativeLibrariesForSelfExtract=true ^
    -o "%OUTPUT_DIR%"

if %ERRORLEVEL% neq 0 (
    echo Build failed.
    exit /b 1
)

echo.
echo Build complete! Executable is at: %OUTPUT_DIR%\%APP_NAME%.exe
echo.
echo You can run it directly or distribute the exe file.
