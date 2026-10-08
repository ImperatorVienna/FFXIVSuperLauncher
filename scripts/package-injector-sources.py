#!/usr/bin/env python3
"""Collect pinned sources for bundled injector targets; does not build or replace runtime binaries.
Usage: package-injector-sources.py OUTPUT.tar.gz [CACHE_DIRECTORY ...]
Cache entries are optional; missing inputs are fetched from recorded upstream URLs.
"""
import hashlib
import io
import json
import pathlib
import shutil
import sys
import tarfile
import tempfile
import urllib.request


def main():
    repo = pathlib.Path(__file__).resolve().parent.parent
    output = pathlib.Path(sys.argv[1]).resolve()
    if output.exists():
        raise FileExistsError('Refusing to overwrite an existing source archive')
    caches = [pathlib.Path(p) for p in sys.argv[2:]]
    manifest = repo / 'compliance/injector-source-inputs.json'
    with tempfile.TemporaryDirectory(prefix='super-injector-source-') as temporary:
        root = pathlib.Path(temporary) / 'xivlauncher-super-injector-sources'
        root.mkdir()
        for item in json.loads(manifest.read_text()):
            cached = next((p / item['archive'] for p in caches if (p / item['archive']).is_file()), None)
            if cached:
                data = cached.read_bytes()
            else:
                request = urllib.request.Request(item['url'], headers={'User-Agent': 'FFXIV-Super-Launcher-source-package'})
                with urllib.request.urlopen(request, timeout=60) as response:
                    data = response.read()
            if hashlib.sha256(data).hexdigest() != item['sha256']:
                raise ValueError('Source archive hash differs; review upstream before repinning: ' + item['name'])
            with tarfile.open(fileobj=io.BytesIO(data), mode='r:gz') as archive:
                for member in archive:
                    original = pathlib.PurePosixPath(member.name)
                    if original.is_absolute() or '..' in original.parts or member.issym() or member.islnk():
                        raise ValueError('Unsafe source archive entry')
                    relative = original.parts[1:]
                    if not member.isfile() or not relative:
                        continue
                    destination = root / item['destination'] / pathlib.Path(*relative)
                    destination.parent.mkdir(parents=True, exist_ok=True)
                    with archive.extractfile(member) as stream, destination.open('xb') as target:
                        shutil.copyfileobj(stream, target)
        shutil.copy2(manifest, root / manifest.name)
        shutil.copy2(repo / 'compliance/INJECTOR-SOURCE-BUILD.txt', root / 'BUILD.txt')
        shutil.copytree(repo / 'tools/tc-entrypoint', root / 'local-patch', ignore=shutil.ignore_patterns('bin', 'obj'))
        shutil.copy2(repo / 'LICENSE', root / 'local-patch/LICENSE')
        shutil.copy2(repo / 'compliance/provenance/injectors.json', root / 'PROVENANCE.json')
        output.parent.mkdir(parents=True, exist_ok=True)
        # Exclusive creation protects historical archives even when a command is retried.
        with tarfile.open(output, 'x:gz') as archive:
            archive.add(root, arcname=root.name)
    print(hashlib.sha256(output.read_bytes()).hexdigest() + '  ' + output.name)


if __name__ == '__main__':
    main()
