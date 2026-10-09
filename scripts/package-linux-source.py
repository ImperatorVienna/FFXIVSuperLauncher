"""Package reviewed source paths and notices for dependencies actually present in the release.
Missing legal material is reported, never silently treated as a completed license review.
"""
import hashlib
import json
import pathlib
import shutil
import subprocess
import sys
import tarfile
import xml.etree.ElementTree as ET

SOURCE_ROOTS = {'src', 'scripts', 'packaging', 'tools', 'compliance', '.github'}
ROOT_FILES = {'LICENSE', 'README.md', 'README.zh-CN.md', 'README.zh-TW.md', 'README.ja.md', 'LINUX.en.md', 'LINUX.zh-CN.md', 'LINUX.zh-TW.md', 'ARCHITECTURE.md', '.gitignore', 'NuGet.Config', 'global.json', 'SOURCES.txt', 'THIRD-PARTY-NOTICES.txt', 'RELEASE-COMPLIANCE.txt'}
EXCLUDED = {'bin', 'obj', '__pycache__', '.git', 'node_modules', 'artifacts', 'outputs', 'research', 'experiments', '.venv'}


def include_source(relative):
    path = pathlib.PurePosixPath(relative)
    if not relative or path.is_absolute() or '..' in path.parts or any(part in EXCLUDED for part in path.parts):
        return False
    if len(path.parts) == 1:
        return path.name in ROOT_FILES or (path.name.startswith('RELEASE-') and path.suffix == '.md')
    return path.parts[0] in SOURCE_ROOTS and path.suffix not in {'.pyc', '.xlsx', '.log', '.tspack', '.dmp'}


def source_archive(repo, output, version):
    paths = subprocess.check_output(['git', '-C', str(repo), 'ls-files', '--cached', '--others', '--exclude-standard', '-z']).decode().split('\0')
    target = output / f'xivlauncher-super-{version}-source.tar.gz'
    # Do not replace a historical source archive when packaging is retried.
    with tarfile.open(target, 'x:gz') as archive:
        for relative in sorted(set(filter(include_source, paths))):
            path = repo / relative
            if path.is_symlink():
                raise ValueError(f'Source symlink requires explicit review: {relative}')
            if path.is_file():
                archive.add(path, arcname=f'xivlauncher-super-{version}-source/{relative}', recursive=False)
    return target


def package_notices(repo, bundle):
    assets = json.loads((repo / 'src/XIVLauncher.Linux/obj/project.assets.json').read_text())
    destination = bundle / 'licenses'
    destination.mkdir(exist_ok=True)
    supplemental = json.loads((repo / 'compliance/notices/sources.json').read_text())
    helper_metadata = {item['package']: item['package_metadata'] for item in
                       json.loads((repo / 'compliance/provenance/helper-dependencies.json').read_text())['packages']}
    results = []
    contexts = [('launcher', bundle / 'xivlauncher-super.deps.json'),
                ('cn-launch-support', bundle / 'Tools/cn-launch-support/injector/Dalamud.Injector.deps.json')]
    libraries = []
    for scope, deps_path in contexts:
        deps = json.loads(deps_path.read_text())
        libraries.extend((scope, key, library) for key, library in sorted(deps['libraries'].items()))
    for scope, key, library in libraries:
        if library['type'] not in {'package', 'runtimepack'}:
            continue
        package, version = key.removeprefix('runtimepack.').split('/')
        entry = {'scope': scope, 'package': package, 'version': version, 'license': None, 'copyright': None, 'notices': []}
        if scope == 'cn-launch-support' and key in helper_metadata:
            # The fixed helper arrives as upstream binaries; a normal launcher
            # restore need not populate these package versions in the local cache.
            entry.update(helper_metadata[key])
        def copy_notice(source, name):
            data = source.read_bytes()
            (destination / name).write_bytes(data)
            entry['notices'].append({'file': 'licenses/' + name, 'sha256': hashlib.sha256(data).hexdigest()})
        for folder in assets['packageFolders']:
            root = pathlib.Path(folder) / package.lower() / version.lower()
            if not root.is_dir():
                continue
            for spec in root.glob('*.nuspec'):
                for element in ET.parse(spec).iter():
                    name = element.tag.split('}')[-1]
                    if name in {'license', 'copyright'}:
                        entry[name] = element.text
                    if name == 'license' and element.attrib.get('type') == 'file':
                        source = root / element.text
                        if root.resolve() not in source.resolve().parents:
                            raise ValueError('License path escaped package')
                        copy_notice(source, package + '-' + version + '-' + source.name)
            for source in root.iterdir():
                if source.is_file() and source.name.lower().startswith(('license', 'licence', 'third-party', 'thirdparty', 'notice', 'copying')):
                    copy_notice(source, package + '-' + version + '-' + source.name)
        component = ('Avalonia' if package.startswith('Avalonia') else package) + '-' + version
        for notice in supplemental:
            if notice['component'] != component:
                continue
            source = repo / 'compliance/notices' / notice['file']
            if hashlib.sha256(source.read_bytes()).hexdigest() != notice['sha256']:
                raise ValueError('Supplemental license hash mismatch: ' + notice['file'])
            copy_notice(source, notice['file'])
        entry['notices'] = list({x['file']: x for x in entry['notices']}.values())
        if not entry['notices']:
            raise ValueError(f'Missing dependency license material: [{scope}] {package}/{version}')
        entry['review'] = 'Notice files collected; see compliance/STATUS.json for unresolved rights'
        results.append(entry)
    (bundle / 'DEPENDENCY-NOTICES.json').write_text(json.dumps(results, indent=2) + '\n')
    lines = [(repo / 'THIRD-PARTY-NOTICES.txt').read_text().rstrip(), '', 'Published dependency inventory', '', 'Inventory follows the published .deps.json, not every restored platform/build package.',
             'This inventory does not resolve the outstanding rights listed in COMPLIANCE-STATUS.json.', '',
             'xdelta3: Apache-2.0; retain Tools/LICENSE.xdelta3 and vendored copyright notices.', '']
    for entry in results:
        lines.append(f"[{entry['scope']}] {entry['package']} {entry['version']}: {entry['license'] or 'see component license'}")
        lines += ['  ' + item['file'] for item in entry['notices']]
    (bundle / 'THIRD-PARTY-NOTICES.txt').write_text('\n'.join(lines) + '\n')
    shutil.copy2(repo / 'compliance/STATUS.json', bundle / 'COMPLIANCE-STATUS.json')
    shutil.copy2(repo / 'SOURCES.txt', bundle / 'SOURCES.txt')
    shutil.copytree(repo / 'compliance', bundle / 'compliance', dirs_exist_ok=True)


def main():
    repo, output, version = pathlib.Path(sys.argv[1]), pathlib.Path(sys.argv[2]), sys.argv[3]
    package_notices(repo, output / 'xivlauncher-super')
    source_archive(repo, output, version)


if __name__ == '__main__':
    main()
