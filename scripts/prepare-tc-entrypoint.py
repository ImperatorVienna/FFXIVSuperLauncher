#!/usr/bin/env python3
"""Build the pinned TC injector copy without modifying upstream files."""
import hashlib, os, pathlib, shutil, subprocess, sys, tempfile, urllib.request
repo = pathlib.Path(__file__).resolve().parent.parent
original = 'fea73493a4517d81057740a5812305729a09bc52e34b7e888a8e740a832bb820'
patched = 'e03ca94e7a0f94cdf1068f9b2bfc33fb9f9e23bc98099f806cdbd0d2b883a381'
version = '26-10-03-01'
output = pathlib.Path(sys.argv[1]).resolve() / 'Tools/tc-entrypoint'
cache = pathlib.Path(os.environ.get('NUGET_PACKAGES', str(pathlib.Path.home()/'.nuget/packages')))
cecil = os.environ.get('CECIL_PATH', str(cache/'microsoft.codecoverage/18.7.0/build/netstandard2.0/Mono.Cecil.dll'))
if not pathlib.Path(cecil).is_file(): raise SystemExit('Set CECIL_PATH to Mono.Cecil.dll to build the TC adapter.')
def digest(p):
    with p.open('rb') as f: return hashlib.file_digest(f, 'sha256').hexdigest()
with tempfile.TemporaryDirectory(prefix='super-tc-entrypoint-') as temp:
    temp = pathlib.Path(temp)
    addon = pathlib.Path(os.environ['TC_SUPPORT_ADDON']) if 'TC_SUPPORT_ADDON' in os.environ else temp/'upstream'
    if not addon.exists():
        archive = temp/'latest.7z'
        urllib.request.urlretrieve(f'https://github.com/yanmucorp/Dalamud/releases/download/{version}/latest.7z', archive)
        subprocess.run(['7z','x','-y','-bso0','-bsp0','-o'+str(addon),str(archive)],check=True)
    source = addon/'Dalamud.Injector.dll'
    if digest(source) != original: raise RuntimeError('Unsupported TC injector; refusing to patch')
    dotnet = os.environ.get('DOTNET', 'dotnet')
    subprocess.run([dotnet,'build',str(repo/'tools/tc-entrypoint/PatchInjector.csproj'),'-o',str(temp/'tool'),'-p:CecilPath='+cecil,'-p:NuGetAudit=false','-m:1'],check=True)
    target = temp/'Dalamud.Injector.dll'
    subprocess.run([dotnet,str(temp/'tool/PatchInjector.dll'),str(source),str(target)],check=True)
    if digest(target) != patched: raise RuntimeError('Patched TC injector differs from the tested build')
    output.mkdir(parents=True, exist_ok=False)
    shutil.copy2(target,output/target.name)
    shutil.copy2(repo/'compliance/notices/yanmu-Dalamud-eed420e2d117-LICENSE', output/'LICENSE.Dalamud')
    shutil.copy2(repo/'compliance/provenance/injectors.json', output/'SOURCE-PROVENANCE.json')
    (output/'COMPONENT.txt').write_text(f'Upstream: https://github.com/yanmucorp/Dalamud/releases/tag/{version}\nOriginal SHA256: {original}\nAdapted SHA256: {patched}\nModification: skip only ArgFixer construction/Fix before entrypoint rewrite.\nModifier: FFXIV Super Launcher; modification documented 2026-10-08 (first shipped in 0.10.9-entrypoint.1).\nUpstream source: https://github.com/yanmucorp/Dalamud/tree/eed420e2d1170a0804e5c6ae6f75f56cef31e0d9 .\nAGPL license: LICENSE.Dalamud.\nLocal patch source: tools/tc-entrypoint/PatchInjector.cs in launcher source archive. This patcher is not the complete upstream corresponding source; recursive source/build delivery remains tracked in compliance/STATUS.json.\n')
    for name in ['licenses.txt','LICENSE','LICENSE.txt']:
        if (addon/name).is_file(): shutil.copy2(addon/name,output/name)
print('Packaged verified TC entrypoint adapter:', output)
