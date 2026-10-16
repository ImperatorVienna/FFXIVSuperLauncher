#!/usr/bin/env python3
"""Build a portable Windows x64 EXE on Windows. No installer or registry writes.
Requires .NET 10 SDK, the MSVC x64 Native Tools environment, a verified Electron
ZIP, and the existing pinned support bundle. Never downloads replacement helpers.
"""
import argparse, hashlib, importlib.util, json, os, pathlib, shutil, subprocess, zipfile

ELECTRON_VERSION = '44.5.1'
ELECTRON_SHA256 = '9b382492dcfee91f8f9e92c91f7972550a1b95d2299cac72279dab33a600d7db'

def digest(file):
    with file.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()

def extract_verified_electron(archive, destination):
    if digest(archive) != ELECTRON_SHA256:
        raise ValueError('Windows Electron ZIP does not match the pinned official release.')
    with zipfile.ZipFile(archive) as source:
        for item in source.infolist():
            relative = pathlib.PurePosixPath(item.filename.replace('\\', '/'))
            if relative.is_absolute() or '..' in relative.parts or ':' in str(relative):
                raise ValueError('Unsafe runtime archive path.')
        source.extractall(destination)
    (destination / 'resources/default_app.asar').unlink(missing_ok=True)

def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('output', type=pathlib.Path)
    p.add_argument('--version', required=True)
    p.add_argument('--electron-zip', type=pathlib.Path, required=True)
    p.add_argument('--support-bundle', type=pathlib.Path, required=True)
    a = p.parse_args()
    if os.name != 'nt':
        p.error('Run on Windows in an x64 Native Tools terminal. Native AOT cannot be cross-published from Linux.')
    if not shutil.which('cl'):
        p.error('The MSVC x64 Native Tools environment is required.')
    repo = pathlib.Path(__file__).resolve().parent.parent
    output = a.output.resolve()
    if output.exists():
        p.error('Choose a fresh output directory; existing builds are never overwritten.')
    output.mkdir(parents=True)
    bundle = output / 'payload'; bundle.mkdir()
    extract_verified_electron(a.electron_zip, bundle)
    dotnet = os.environ.get('DOTNET', 'dotnet')
    build = output / 'build'
    subprocess.run([dotnet, 'publish', str(repo/'src/XIVLauncher.Windows/XIVLauncher.Windows.csproj'),
        '-c', 'Release', '-r', 'win-x64', '--self-contained', 'true', '--artifacts-path', str(build),
        '-o', str(bundle), '-p:Version='+a.version, '-p:PublishSingleFile=false', '-p:NuGetAudit=false', '-m:1'], check=True)
    # CN argument-fix helper is already a Windows binary; preserve its exact hashes.
    for relative, expected in json.loads((repo/'packaging/electron/support-hashes.json').read_text()).items():
        if not relative.startswith('cn-launch-support/'):
            continue
        source = a.support_bundle/'Tools'/relative
        if digest(source) != expected:
            raise ValueError('Pinned support mismatch: '+relative)
        target = bundle/'Tools'/relative; target.parent.mkdir(parents=True, exist_ok=True); shutil.copy2(source, target)
    xdelta = bundle/'Tools/xdelta3.exe'
    definitions = ['XD3_MAIN=1','XD3_POSIX=0','XD3_WIN32=1','XD3_STDIO=0','SIZEOF_SIZE_T=8',
        'SIZEOF_UNSIGNED_LONG=4','SIZEOF_UNSIGNED_LONG_LONG=8','SIZEOF_UNSIGNED_INT=4',
        'EXTERNAL_COMPRESSION=0','HAVE_CONFIG_H=0','_CRT_SECURE_NO_WARNINGS']
    subprocess.run(['cl','/nologo','/O2',*[('/D'+v) for v in definitions],
        str(repo/'src/native/xdelta3/xdelta3.c'),'/Fe:'+str(xdelta),'/Fo:'+str(output/'xdelta3.obj')],check=True)
    # Validate the actual built differential worker, not just PE headers.
    sample = output/'xdelta-smoke'; sample.mkdir()
    old, new, patch, decoded = [sample/n for n in ['old','new','delta','decoded']]
    old.write_bytes(b'launcher base\n'*100); new.write_bytes(b'launcher updated\n'*100)
    subprocess.run([str(xdelta),'-e','-f','-s',str(old),str(new),str(patch)],check=True)
    subprocess.run([str(xdelta),'-d','-D','-f','-s',str(old),str(patch),str(decoded)],check=True)
    if decoded.read_bytes() != new.read_bytes(): raise ValueError('Windows xdelta roundtrip failed.')
    frontend = bundle/'frontend'; frontend.mkdir()
    for name in ['portable-electron-paths.cjs','main.cjs','preload.cjs','backend-client.cjs','ipc-contract.cjs',
        'window-state.cjs','window-ui.cjs','steam-integration.cjs','steam-session.cjs','launcher-entry.cjs',
        'captcha-view.cjs','browser-cache.cjs','shutdown.cjs','session-handoff.cjs']:
        shutil.copy2(repo/'frontend'/name, frontend/name)
    shutil.copytree(repo/'frontend/platforms', frontend/'platforms')
    ui=bundle/'prototypes/electron-ui'
    shutil.copytree(repo/'prototypes/electron-ui',ui,ignore=shutil.ignore_patterns('README.md'))
    html=(ui/'index.html').read_text(encoding='utf-8').replace('../../src/XIVLauncher.Linux/Resources/icon.png','../../icon.png').replace('FFXIV Super Launcher · 交互预览','FFXIV Super Launcher')
    (ui/'index.html').write_text(html,encoding='utf-8')
    app=bundle/'resources/app'; app.mkdir(parents=True,exist_ok=True)
    (app/'main.cjs').write_text("'use strict';\nif(process.argv.includes('--super-launcher'))require('../../frontend/main.cjs');\nelse require('./official-main.cjs');\n",encoding='utf-8')
    for source,target in [(repo/'packaging/webview/main.cjs','official-main.cjs'),(repo/'frontend/portable-electron-paths.cjs','portable-electron-paths.cjs')]:shutil.copy2(source,app/target)
    (app/'package.json').write_text(json.dumps({'name':'ffxiv-super-launcher','version':a.version,'main':'main.cjs','private':True}),encoding='utf-8')
    (bundle/'frontend-package.json').write_text(json.dumps({'version':a.version,'frontend':'electron','protocol':5}),encoding='utf-8')
    (bundle/'distribution.json').write_text(json.dumps({'Kind':'windows-portable','Channel':'stable','LocalTest':False,
        'PublicKey':(repo/'packaging/linux/appimage-public.pem').read_text()}),encoding='utf-8')
    for name in ['README.md','README.zh-CN.md','README.zh-TW.md','README.ja.md','LICENSE']:
        shutil.copy2(repo/name,bundle/name)
    spec=importlib.util.spec_from_file_location('source_package',repo/'scripts/package-linux-source.py')
    source=importlib.util.module_from_spec(spec);spec.loader.exec_module(source)
    source.package_notices(repo,bundle,build/'obj/XIVLauncher.Windows/project.assets.json',deps_name='xivlauncher-backend.deps.json')
    for name in ['Cinzel-OFL.txt','GenRyu-OFL.txt','font-sources.json']:shutil.copy2(ui/'assets'/name,bundle/'licenses'/name)
    shutil.copy2(repo/'src/native/xdelta3/LICENSE',bundle/'Tools/LICENSE.xdelta3')
    shutil.copy2(repo/'compliance/notices/Electron-44.5.1-FFmpeg-COPYING.LGPLv2.1',bundle/'COPYING.FFmpeg.LGPLv2.1')
    (bundle/'WINDOWS-RUNTIME.json').write_text(json.dumps({'electronVersion':ELECTRON_VERSION,'electronArchiveSha256':ELECTRON_SHA256,
        'electronArchiveUrl':f'https://github.com/electron/electron/releases/download/v{ELECTRON_VERSION}/electron-v{ELECTRON_VERSION}-win32-x64.zip',
        'xdeltaSource':'src/native/xdelta3','xdeltaSha256':digest(xdelta),'steamApiSha256':digest(bundle/'steam_api64.dll')},indent=2),encoding='utf-8')
    # Native AOT avoids .NET bundle extraction into the user's global temp folder.
    # The embedded payload is extracted only beside the outer launcher EXE.
    payload=output/'payload.zip'
    with zipfile.ZipFile(payload,'x',compression=zipfile.ZIP_DEFLATED,compresslevel=6) as archive:
        for file in sorted(bundle.rglob('*')):
            if file.is_file():archive.write(file,file.relative_to(bundle).as_posix())
    host=output/'host'
    subprocess.run([dotnet,'publish',str(repo/'src/XIVLauncher.Windows.Portable/XIVLauncher.Windows.Portable.csproj'),
        '-c','Release','-r','win-x64','--self-contained','true','-o',str(host),'-p:PublishAot=true',
        '-p:Version='+a.version,'-p:PortablePayload='+str(payload),'-p:NuGetAudit=false','-m:1'],check=True)
    executable=output/f'FFXIVSuperLauncher-{a.version}-win-x64.exe';shutil.copy2(host/'FFXIVSuperLauncher.exe',executable)
    source.source_archive(repo,output,a.version)
    (output/'SHA256SUMS').write_text(''.join(f'{digest(f)}  {f.name}\n' for f in [executable,output/f'xivlauncher-super-{a.version}-source.tar.gz']),encoding='utf-8')
    print(executable)

if __name__=='__main__':main()
