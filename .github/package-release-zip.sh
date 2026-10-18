#!/bin/bash
set -euo pipefail

Arch="${1:?architecture is required}"
OutputPath="${2:?publish directory is required}"
case "$Arch" in
  windows-64|windows-arm64|linux-64|linux-arm64|linux-riscv64|linux-loong64|macos-64|macos-arm64) ;;
  *) echo "Unsupported architecture: $Arch" >&2; exit 1 ;;
esac
[ -d "$OutputPath" ] || { echo "Publish directory does not exist" >&2; exit 1; }
if [[ "$Arch" == windows-* ]]; then AppName=v2rayN.exe; else AppName=v2rayN; fi
[ -f "$OutputPath/$AppName" ] || { echo "Published application is missing" >&2; exit 1; }

# Standard releases download cores in the application. Never merge an unrelated core-bin archive.
FileName="$PWD/v2rayN-${Arch}.zip"
[ ! -e "$FileName" ] || { echo "Archive already exists: $FileName" >&2; exit 1; }
Stage=$(mktemp -d)
trap 'rm -rf "$Stage"' EXIT
mkdir "$Stage/v2rayN-${Arch}"
cp -R "$OutputPath/." "$Stage/v2rayN-${Arch}/"
if command -v 7z >/dev/null 2>&1; then
  (cd "$Stage" && 7z a -tZip "$FileName" "v2rayN-${Arch}" -mx1)
else
  (cd "$Stage" && zip -q -r -1 "$FileName" "v2rayN-${Arch}")
fi
