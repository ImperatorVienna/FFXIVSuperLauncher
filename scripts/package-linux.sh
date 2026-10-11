#!/usr/bin/env bash
set -euo pipefail
repo_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
version="${VERSION:-1.1.1}"
release_root="${1:-${repo_root}/artifacts/linux}"
python3 "$repo_root/scripts/package-electron.py" "$release_root" --version "$version"
python3 "$repo_root/scripts/package-corresponding-sources.py" "$release_root" "$version"
tar -C "$release_root" -czf "$release_root/xivlauncher-super-$version.tar.gz" xivlauncher-super
python3 - "$release_root" <<'CHECKSUMS'
import hashlib, pathlib, sys
root = pathlib.Path(sys.argv[1])
with (root / 'SHA256SUMS').open('w') as output:
    for archive in sorted(root.glob('*.tar.gz')):
        with archive.open('rb') as stream:
            output.write(hashlib.file_digest(stream, 'sha256').hexdigest() + '  ' + archive.name + '\n')
CHECKSUMS
echo "Electron release and corresponding source: $release_root"
