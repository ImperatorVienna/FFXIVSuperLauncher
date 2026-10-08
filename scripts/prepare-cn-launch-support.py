#!/usr/bin/env python3
"""Package a fixed CN argument-fix helper, never a runtime updater. Excludes Dalamud.dll and plugin assets."""
import hashlib, json, os, pathlib, shutil, subprocess, sys, tempfile, urllib.request, zipfile
VERSION = '26-10-05-02'
RUNTIME = '10.0.1'
BASE = 'https://dalamud-dis.atmoomen.top/' + VERSION
output = pathlib.Path(sys.argv[1]) / 'Tools' / 'cn-launch-support'
if output.exists():
    raise SystemExit('Support output already exists; use a fresh package directory.')
output.mkdir(parents=True)
def fetch(url, path):
    with urllib.request.urlopen(urllib.request.Request(url, headers={'User-Agent':'XIVLauncherCN'}), timeout=120) as stream, open(path, 'wb') as dest:
        shutil.copyfileobj(stream, dest)
with tempfile.TemporaryDirectory(prefix='super-cn-support-') as scratch:
    scratch = pathlib.Path(scratch)
    addon = pathlib.Path(os.environ['CN_SUPPORT_ADDON']) if 'CN_SUPPORT_ADDON' in os.environ else scratch/'addon'
    if not addon.exists():
        addon.mkdir(); fetch(BASE+'/latest.7z', scratch/'latest.7z')
        subprocess.run(['7z','x','-y','-bso0','-bsp0','-o'+str(addon),str(scratch/'latest.7z')],check=True)
    shutil.copy2(pathlib.Path(__file__).resolve().parent.parent/'packaging/linux/cn-launch-hashes.json', scratch/'hashes.json')
    hashes = {k.replace('\\','/'):v for k,v in json.loads((scratch/'hashes.json').read_text()).items()}
    deps = json.loads((addon/'Dalamud.Injector.deps.json').read_text())
    files = {'Dalamud.Injector.exe','Dalamud.Injector.deps.json','Dalamud.Injector.runtimeconfig.json'}
    for target in deps['targets'].values():
        for library in target.values():
            files.update(pathlib.PurePosixPath(p).name for p in library.get('runtime', {}))
    target = output/'injector';target.mkdir()
    for name in sorted(files):
        src=addon/name
        digest=hashlib.md5(src.read_bytes()).hexdigest()
        if name not in hashes or digest.lower()!=hashes[name].lower():raise RuntimeError('Pinned helper hash mismatch: '+name)
        shutil.copy2(src,target/name)
    assert not (target/'Dalamud.dll').exists()
    if (addon/'licenses.txt').exists():shutil.copy2(addon/'licenses.txt',output/'licenses.txt')
    runtime = output/'runtime'; core=runtime/'shared'/'Microsoft.NETCore.App'/RUNTIME;core.mkdir(parents=True)
    host=runtime/'host'/'fxr'/RUNTIME;host.mkdir(parents=True)
    if 'CN_SUPPORT_RUNTIME' in os.environ:
        cached=pathlib.Path(os.environ['CN_SUPPORT_RUNTIME'])
        shutil.copytree(cached/'shared'/'Microsoft.NETCore.App'/RUNTIME,core,dirs_exist_ok=True)
        shutil.copy2(cached/'host'/'fxr'/RUNTIME/'hostfxr.dll',host/'hostfxr.dll')
    else:
        package='microsoft.netcore.app.runtime.win-x64'
        file=package+'.'+RUNTIME+'.nupkg'
        try:fetch('https://api.nuget.org/v3-flatcontainer/'+package+'/'+RUNTIME+'/'+file,scratch/file)
        except Exception:fetch('https://repo.huaweicloud.com/artifactory/api/nuget/v3/nuget-remote/'+package+'/'+RUNTIME+'/'+file,scratch/file)
        with zipfile.ZipFile(scratch/file) as archive:
            for entry in archive.infolist():
                if pathlib.PurePosixPath(entry.filename).name.lower() in ['license.txt','thirdpartynotices.txt']:
                    (output/('dotnet-'+pathlib.PurePosixPath(entry.filename).name)).write_bytes(archive.read(entry))
            for entry in archive.infolist():
                if not entry.is_dir() and entry.filename.startswith(('runtimes/win-x64/native/','runtimes/win-x64/lib/net10.0/')):
                    (core/pathlib.PurePosixPath(entry.filename).name).write_bytes(archive.read(entry))
        shutil.copy2(core/'hostfxr.dll',host/'hostfxr.dll')
    # Cached and downloaded runtime paths must carry identical legal material.
    legal = pathlib.Path(__file__).resolve().parent.parent/'compliance/notices'
    for name in ('LICENSE.TXT', 'THIRD-PARTY-NOTICES.TXT'):
        source = legal/('dotnet-runtime-win-10.0.1-'+name)
        if not source.is_file(): raise RuntimeError('Missing pinned Windows runtime notice: '+name)
        shutil.copy2(source, output/('dotnet-'+name))
    shutil.copy2(legal/'soil-Dalamud-12c1e860fc85-LICENSE', output/'LICENSE.Dalamud')
    shutil.copy2(legal/'FfxivArgLauncher-e2e32129d77e-LICENSE', output/'LICENSE.FfxivArgLauncher')
    shutil.copy2(legal.parent/'provenance/injectors.json', output/'SOURCE-PROVENANCE.json')
    (output/'NOTICE.txt').write_text('Fixed CN login argument-fix helper from Soil Dalamud '+VERSION+' (distribution: https://dalamud-dis.atmoomen.top/26-10-05-02/latest.7z).\nCorresponding source: https://github.com/Dalamud-DailyRoutines/Dalamud/tree/12c1e860fc85cf129470e2d1a58f8e3db883f140 . AGPL license retained in LICENSE.Dalamud. Recursive source/build delivery remains tracked in compliance/STATUS.json.\nUsed only with --without-dalamud; no Dalamud.dll or plugins included.\n.NET Windows runtime '+RUNTIME+' from Microsoft NuGet.\nFfxivArgLauncher: https://github.com/ottercorp/FfxivArgLauncher\nDependencies and versions: injector/Dalamud.Injector.deps.json\nUpdated only by replacing the launcher package, never automatically at launch.\n')
    sums=[]
    for path in sorted(output.rglob('*')):
        if path.is_file():sums.append(hashlib.sha256(path.read_bytes()).hexdigest()+'  '+str(path.relative_to(output)))
    (output/'SHA256SUMS').write_text('\n'.join(sums)+'\n')
print('Packaged fixed CN launch helper:',output)
