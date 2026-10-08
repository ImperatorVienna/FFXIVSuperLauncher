#!/usr/bin/env python3
"""Place corresponding-source downloads beside a release; never publishes anything."""
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import urllib.request


def digest(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def main():
    repo = Path(__file__).resolve().parent.parent
    release = Path(sys.argv[1]).resolve()
    version = sys.argv[2]
    bundle = release / 'xivlauncher-super'
    if not bundle.is_dir():
        raise ValueError('Build the release bundle first')
    caches = [Path(p) for p in os.environ.get('SOURCE_MATERIAL_CACHE', '').split(os.pathsep) if p]
    records = []
    injector = release / f'xivlauncher-super-{version}-injector-source.tar.gz'
    subprocess.run([sys.executable, str(repo / 'scripts/package-injector-sources.py'),
                    str(injector), *map(str, caches)], check=True)
    records.append({'component': 'bundled injectors and local patch', 'file': injector.name,
                    'sha256': digest(injector), 'inputs': 'compliance/injector-source-inputs.json'})
    web = json.loads((repo / 'compliance/provenance/webview.json').read_text())
    inputs = [{'component': 'Chromium FFmpeg',
               'archive': f"chromium-ffmpeg-{web['ffmpeg_commit'][:12]}-source.tar.gz",
               'url': web['ffmpeg_archive_url'], 'sha256': web['ffmpeg_archive_sha256']}]
    inputs += json.loads((repo / 'compliance/helper-dependency-source-inputs.json').read_text())
    for item in inputs:
        target = release / item['archive']
        cached = next((p / item['archive'] for p in caches if (p / item['archive']).is_file()), None)
        try:
            # Exclusive creation: never overwrite a previous release attachment.
            with target.open('xb') as output:
                if cached is not None:
                    with cached.open('rb') as stream:
                        shutil.copyfileobj(stream, output)
                else:
                    request = urllib.request.Request(item['url'], headers={'User-Agent': 'FFXIV-Super-Launcher-source-package'})
                    with urllib.request.urlopen(request, timeout=60) as stream:
                        shutil.copyfileobj(stream, output)
        except FileExistsError:
            raise
        except Exception:
            target.unlink(missing_ok=True)
            raise
        if digest(target) != item['sha256']:
            target.unlink()
            raise ValueError('Corresponding source hash mismatch: ' + item['component'])
        records.append({'component': item['component'], 'file': target.name,
                        'sha256': item['sha256'], 'upstream': item['url']})
    launcher = release / f'xivlauncher-super-{version}-source.tar.gz'
    records.insert(0, {'component': 'launcher', 'file': launcher.name, 'sha256': digest(launcher)})
    manifest = {'version': version, 'delivery': 'Publish all listed files alongside the binary, at no additional cost.',
                'scope': 'Source delivery manifest; not a license clearance or byte-identical rebuild claim.',
                'files': records}
    (bundle / 'SOURCE-DELIVERY.json').write_text(json.dumps(manifest, indent=2) + '\n')
    shutil.copy2(repo / 'compliance/SOURCE-DELIVERY.txt', bundle / 'SOURCE-DELIVERY.txt')
    shutil.copy2(bundle / 'SOURCE-DELIVERY.json', release / 'SOURCE-DELIVERY.json')


if __name__ == '__main__':
    main()
