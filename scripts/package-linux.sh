#!/usr/bin/env bash
set -euo pipefail
repo_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
version="${VERSION:-1.0.0}"
release_root="${1:-${repo_root}/artifacts/linux}"
bundle_name="xivlauncher-super"
archive_name="xivlauncher-super-${version}"
bundle="${release_root}/${bundle_name}"
dotnet_command="${DOTNET:-dotnet}"
mkdir -p -- "$release_root"
if [[ -e "$bundle" ]]; then
    echo "Output already exists: $bundle. Choose a fresh output directory." >&2
    exit 1
fi
"$dotnet_command" publish "$repo_root/src/XIVLauncher.Linux/XIVLauncher.Linux.csproj" \
    -c Release -r linux-x64 --self-contained true -o "$bundle" \
    -p:Version="$version" -p:PublishSingleFile=false -p:PublishTrimmed=false -p:NuGetAudit=false -m:1
for readme in README.md README.zh-CN.md README.zh-TW.md README.ja.md; do
    install -m644 "$repo_root/$readme" "$bundle/$readme"
done
install -m644 "$repo_root/LICENSE" "$bundle/LICENSE"
install -m644 "$repo_root/LINUX.en.md" "$bundle/LINUX.en.md"
install -m644 "$repo_root/LINUX.zh-CN.md" "$bundle/LINUX.zh-CN.md"
install -m644 "$repo_root/LINUX.zh-TW.md" "$bundle/LINUX.zh-TW.md"
install -m644 "$repo_root/RELEASE-1.0.0.md" "$bundle/RELEASE-1.0.0.md"
install -m644 "$repo_root/src/XIVLauncher.Linux/Resources/icon.png" "$bundle/icon.png"
install -m644 "$repo_root/packaging/linux/xivlauncher-super.desktop" "$bundle/"
install -m644 "$repo_root/src/native/xdelta3/LICENSE" "$bundle/Tools/LICENSE.xdelta3"
python3 "$repo_root/scripts/prepare-cn-launch-support.py" "$bundle"
python3 "$repo_root/scripts/prepare-tc-entrypoint.py" "$bundle"
python3 "$repo_root/scripts/prepare-webview.py" "$bundle"
chmod +x "$bundle/xivlauncher-super" "$bundle/Tools/xdelta3"
python3 "$repo_root/scripts/package-linux-source.py" "$repo_root" "$release_root" "$version"
python3 "$repo_root/scripts/package-corresponding-sources.py" "$release_root" "$version"
tar -C "$release_root" -czf "$release_root/$archive_name.tar.gz" "$bundle_name"
python3 - "$release_root" "$archive_name" <<'PY'
import hashlib, pathlib, sys
root, name = pathlib.Path(sys.argv[1]), sys.argv[2]
with (root / 'SHA256SUMS').open('w') as output:
    for archive in sorted(root.glob('*.tar.gz')):
        with archive.open('rb') as stream:
            output.write(hashlib.file_digest(stream, 'sha256').hexdigest() + '  ' + archive.name + '\n')
PY
echo "Release and corresponding source: $release_root"
