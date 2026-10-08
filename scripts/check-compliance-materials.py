#!/usr/bin/env python3
"""Offline material-integrity checks; never infers legal permission from hashes."""
import argparse
import hashlib
import json
from pathlib import Path


def verify(root, source_dir=None, preserved_dir=None):
    errors = []
    checked = 0

    def check(path, expected):
        nonlocal checked
        try:
            with path.open('rb') as stream:
                actual = hashlib.file_digest(stream, 'sha256').hexdigest()
            if actual != expected:
                errors.append(f'Hash mismatch: {path}')
            else:
                checked += 1
        except OSError as exc:
            errors.append(f'Cannot read {path}: {exc}')

    notices = root / 'compliance/notices'
    for entry in json.loads((notices / 'sources.json').read_text()):
        check(notices / entry['file'], entry['sha256'])

    steam = json.loads((root / 'compliance/provenance/steam-native.json').read_text())
    check(root / steam['local_file'], steam['local_sha256'])

    if source_dir is not None:
        # Only final verified inputs, not the superseded draft source archive.
        check(source_dir / 'injector-sources-2026-10-08-verified.tar.gz',
              '5d2e51fc5e36a911708fff3b8ef49ea60f0a9baadb82781eb0403397ace52d3b')
        webview = json.loads((root / 'compliance/provenance/webview.json').read_text())
        check(source_dir / 'chromium-ffmpeg-2b68d2babae7-source.tar.gz',
              webview['ffmpeg_archive_sha256'])
        for item in json.loads((root / 'compliance/helper-dependency-source-inputs.json').read_text()):
            check(source_dir / item['archive'], item['sha256'])

    if preserved_dir is not None:
        check(preserved_dir / 'xivlauncher-super-0.12.1-source.tar.gz',
              '11d255882541c0d22dc0d2bbb2faaeaaf3e498baeb54d53fa939d859ef18bf94')
        check(preserved_dir / 'xivlauncher-super-0.12.1.tar.gz',
              '66d8a4a5aa0af541fc277b9abe8856e5b006e0ca4f5b58ac20e705a8fe138973')

    status = json.loads((root / 'compliance/STATUS.json').read_text())
    return {
        'verified_files': checked,
        'integrity_errors': errors,
        'public_release_cleared': status.get('public_release_cleared') is True,
        'review_items': [{'id': item['id'], 'status': item['status']}
                         for item in status['open_issues']],
        'scope': 'Checks recorded hashes only; does not grant rights or audit final release contents.'
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, default=Path(__file__).resolve().parent.parent)
    parser.add_argument('--source-materials', type=Path)
    parser.add_argument('--preserved', type=Path)
    parser.add_argument('--require-release-clearance', action='store_true',
                        help='Also fail when recorded public-release clearance is absent.')
    args = parser.parse_args()
    try:
        result = verify(args.root, args.source_materials, args.preserved)
    except (OSError, ValueError, KeyError, TypeError) as exc:
        print(json.dumps({'integrity_errors': [str(exc)], 'public_release_cleared': False}, indent=2))
        return 1
    print(json.dumps(result, indent=2))
    if result['integrity_errors']:
        return 1
    if args.require_release_clearance and not result['public_release_cleared']:
        return 2
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
