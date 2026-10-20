#!/bin/bash
set -euo pipefail

Arch="${1:?architecture is required}"
OutputPath="${2:?publish directory is required}"
Version="${3:?version is required}"

[ -d "$OutputPath" ] && [ -f "$OutputPath/v2rayNP" ] || { echo "Published application is missing" >&2; exit 1; }
# Standard packages use in-app core downloads, matching the ZIP release.
PackagePath="v2rayNP-Package-${Arch}"
[ ! -e "$PackagePath" ] || { echo "Package staging directory already exists" >&2; exit 1; }
mkdir -p "$PackagePath/v2rayNP.app/Contents/Resources"
cp -rf "$OutputPath" "$PackagePath/v2rayNP.app/Contents/MacOS"
cp -f "$PackagePath/v2rayNP.app/Contents/MacOS/v2rayNP.icns" "$PackagePath/v2rayNP.app/Contents/Resources/AppIcon.icns"
echo "When this file exists, app will not store configs under this folder" > "$PackagePath/v2rayNP.app/Contents/MacOS/NotStoreConfigHere.txt"
chmod +x "$PackagePath/v2rayNP.app/Contents/MacOS/v2rayNP"

cat >"$PackagePath/v2rayNP.app/Contents/Info.plist" <<-EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleDevelopmentRegion</key>
  <string>en</string>
  <key>CFBundleLocalizations</key>
  <array>
    <string>zh-Hans</string>
    <string>zh-Hant</string>
    <string>en</string>
    <string>fa</string>
    <string>fr</string>
    <string>ru</string>
    <string>hu</string>
  </array>
  <key>CFBundleDisplayName</key>
  <string>v2rayNP</string>
  <key>CFBundleExecutable</key>
  <string>v2rayNP</string>
  <key>CFBundleIconFile</key>
  <string>AppIcon</string>
  <key>CFBundleIconName</key>
  <string>AppIcon</string>
  <key>CFBundleIdentifier</key>
  <string>com.wenxig.v2raynp</string>
  <key>CFBundleName</key>
  <string>v2rayNP</string>
  <key>CFBundlePackageType</key>
  <string>APPL</string>
  <key>CFBundleShortVersionString</key>
  <string>${Version}</string>
  <key>CSResourcesFileMapped</key>
  <true/>
  <key>NSHighResolutionCapable</key>
  <true/>
  <key>LSMinimumSystemVersion</key>
  <string>13.6</string>
</dict>
</plist>
EOF

create-dmg \
    --volname "v2rayNP Installer" \
    --window-size 700 420 \
    --icon-size 100 \
    --icon "v2rayNP.app" 160 185 \
    --hide-extension "v2rayNP.app" \
    --app-drop-link 500 185 \
    "v2rayNP-${Arch}.dmg" \
    "$PackagePath/v2rayNP.app"
