#!/bin/bash
set -euo pipefail

# Configuration
APP_NAME="Folder2Pdf"
BUNDLE_NAME="${APP_NAME}.app"
PROJECT_DIR="Folder2Pdf"
OUTPUT_DIR="publish"
ICON_SOURCE="assets/images/folder2pdf-icon.png"

# Detect architecture
ARCH=$(uname -m)
if [ "$ARCH" = "arm64" ]; then
    RID="osx-arm64"
else
    RID="osx-x64"
fi

echo "Building ${APP_NAME} for ${RID}..."

# Clean previous builds
rm -rf "${OUTPUT_DIR}"

# Publish as self-contained single-file app
dotnet publish "${PROJECT_DIR}/${APP_NAME}.csproj" \
    -c Release \
    -r "${RID}" \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -o "${OUTPUT_DIR}/bin"

echo "Creating macOS app bundle..."

# Create .app bundle structure
BUNDLE_PATH="${OUTPUT_DIR}/${BUNDLE_NAME}"
mkdir -p "${BUNDLE_PATH}/Contents/MacOS"
mkdir -p "${BUNDLE_PATH}/Contents/Resources"

# Copy the published executable and all files
cp -a "${OUTPUT_DIR}/bin/"* "${BUNDLE_PATH}/Contents/MacOS/"

# Copy Info.plist
cp "${PROJECT_DIR}/Info.plist" "${BUNDLE_PATH}/Contents/Info.plist"

# Generate .icns icon from PNG
if [ -f "${ICON_SOURCE}" ]; then
    echo "Generating app icon..."
    ICONSET_DIR="${OUTPUT_DIR}/AppIcon.iconset"
    mkdir -p "${ICONSET_DIR}"

    sips -z 16 16     "${ICON_SOURCE}" --out "${ICONSET_DIR}/icon_16x16.png"      > /dev/null 2>&1
    sips -z 32 32     "${ICON_SOURCE}" --out "${ICONSET_DIR}/icon_16x16@2x.png"   > /dev/null 2>&1
    sips -z 32 32     "${ICON_SOURCE}" --out "${ICONSET_DIR}/icon_32x32.png"      > /dev/null 2>&1
    sips -z 64 64     "${ICON_SOURCE}" --out "${ICONSET_DIR}/icon_32x32@2x.png"   > /dev/null 2>&1
    sips -z 128 128   "${ICON_SOURCE}" --out "${ICONSET_DIR}/icon_128x128.png"    > /dev/null 2>&1
    sips -z 256 256   "${ICON_SOURCE}" --out "${ICONSET_DIR}/icon_128x128@2x.png" > /dev/null 2>&1
    sips -z 256 256   "${ICON_SOURCE}" --out "${ICONSET_DIR}/icon_256x256.png"    > /dev/null 2>&1
    sips -z 512 512   "${ICON_SOURCE}" --out "${ICONSET_DIR}/icon_256x256@2x.png" > /dev/null 2>&1
    sips -z 512 512   "${ICON_SOURCE}" --out "${ICONSET_DIR}/icon_512x512.png"    > /dev/null 2>&1
    sips -z 1024 1024 "${ICON_SOURCE}" --out "${ICONSET_DIR}/icon_512x512@2x.png" > /dev/null 2>&1

    iconutil -c icns "${ICONSET_DIR}" -o "${BUNDLE_PATH}/Contents/Resources/AppIcon.icns"
    rm -rf "${ICONSET_DIR}"
    echo "Icon generated."
else
    echo "Warning: Icon source not found at ${ICON_SOURCE}, skipping icon generation."
fi

# Ad-hoc code sign (required for macOS to run the app)
echo "Code signing..."
codesign --force --deep --sign - "${BUNDLE_PATH}"

# Clean up the flat bin directory
rm -rf "${OUTPUT_DIR}/bin"

echo ""
echo "Build complete! App bundle is at: ${OUTPUT_DIR}/${BUNDLE_NAME}"
echo ""
echo "To install, drag ${BUNDLE_NAME} to /Applications, or run:"
echo "  cp -r \"${OUTPUT_DIR}/${BUNDLE_NAME}\" /Applications/"
echo ""
echo "To create a DMG for distribution:"
echo "  hdiutil create -volname ${APP_NAME} -srcfolder \"${OUTPUT_DIR}/${BUNDLE_NAME}\" -ov -format UDZO \"${OUTPUT_DIR}/${APP_NAME}.dmg\""
