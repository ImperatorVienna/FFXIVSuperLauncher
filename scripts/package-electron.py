#!/usr/bin/env python3
"""Build an Electron bundle; no old GUI assemblies, configuration, or test fixtures."""
import argparse, hashlib, importlib.util, json, os, pathlib, shutil, subprocess
p=argparse.ArgumentParser();p.add_argument('output',type=pathlib.Path);p.add_argument('--version',default='1.1.1');p.add_argument('--support-bundle',type=pathlib.Path)
a=p.parse_args();repo=pathlib.Path(__file__).resolve().parent.parent;output=a.output.resolve();bundle=output/'xivlauncher-super'
if bundle.exists():raise SystemExit('Choose a fresh output directory; existing bundles are never replaced.')
output.mkdir(parents=True,exist_ok=True)
dotnet=os.environ.get('DOTNET','dotnet')
subprocess.run([dotnet,'publish',str(repo/'src/XIVLauncher.Linux/XIVLauncher.Linux.csproj'),'-c','Release','-r','linux-x64','--self-contained','true','--artifacts-path',str(output/'build'),'-o',str(bundle),'-p:Version='+a.version,'-p:PublishSingleFile=false','-p:PublishTrimmed=false','-p:NuGetAudit=false','-m:1'],check=True)
(bundle/'xivlauncher-super').rename(bundle/'xivlauncher-backend')
shutil.copy2(repo/'packaging/electron/xivlauncher-super',bundle/'xivlauncher-super');(bundle/'xivlauncher-super').chmod(0o755)
if a.support_bundle:
    # Reuse only explicitly pinned helper files, not the old launcher or any GUI library.
    for relative,expected in json.loads((repo/'packaging/electron/support-hashes.json').read_text()).items():
        # These first-party wrappers are always generated below, never reused.
        if relative in {'webview/resources/app/main.cjs','webview/resources/app/package.json'}:continue
        source=a.support_bundle/'Tools'/relative
        with source.open('rb') as stream:actual=hashlib.file_digest(stream,'sha256').hexdigest()
        if actual!=expected:raise ValueError('Support component hash mismatch: '+relative)
        destination=bundle/'Tools'/relative;destination.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(source,destination)
else:
    for script in ['prepare-cn-launch-support.py','prepare-tc-entrypoint.py','prepare-webview.py']:
        subprocess.run(['python3',str(repo/'scripts'/script),str(bundle)],check=True)
# Official webpages keep their existing isolated shell. The dispatcher supplies the new launcher entry.
web=bundle/'Tools/webview/resources/app';shutil.copy2(repo/'packaging/webview/main.cjs',web/'official-main.cjs');shutil.copy2(repo/'packaging/electron/main.cjs',web/'main.cjs')
metadata=json.loads((repo/'packaging/webview/package.json').read_text());metadata['version']=a.version
(web/'package.json').write_text(json.dumps(metadata)+'\n')
(bundle/'frontend').mkdir()
for name in ['README.md','CLEANUP-AUDIT.md']:
    shutil.copy2(repo/'frontend'/name,bundle/'frontend'/name)
for name in ['main.cjs','preload.cjs','backend-client.cjs','ipc-contract.cjs','window-state.cjs','steam-session.cjs','launcher-entry.cjs','captcha-view.cjs','browser-cache.cjs','shutdown.cjs','session-handoff.cjs']:
    shutil.copy2(repo/'frontend'/name,bundle/'frontend'/name)
ui=bundle/'prototypes/electron-ui';shutil.copytree(repo/'prototypes/electron-ui',ui,ignore=shutil.ignore_patterns('README.md'))
html=(ui/'index.html').read_text().replace('../../src/XIVLauncher.Linux/Resources/icon.png','../../icon.png').replace('FFXIV Super Launcher · 交互预览','FFXIV Super Launcher');(ui/'index.html').write_text(html)
(bundle/'frontend-package.json').write_text(json.dumps({'version':a.version,'frontend':'electron','protocol':5})+'\n')
for name in ['README.md','README.zh-CN.md','README.zh-TW.md','README.ja.md','LINUX.en.md','LINUX.zh-CN.md','LINUX.zh-TW.md','LICENSE']:
    shutil.copy2(repo/name,bundle/name)
shutil.copy2(repo/'src/native/xdelta3/LICENSE',bundle/'Tools/LICENSE.xdelta3')
shutil.copy2(repo/'packaging/linux/xivlauncher-super.desktop',bundle/'xivlauncher-super.desktop')
(bundle/'Tools/xdelta3').chmod(0o755)
spec=importlib.util.spec_from_file_location('source_package',repo/'scripts/package-linux-source.py');source=importlib.util.module_from_spec(spec);spec.loader.exec_module(source)
assets=output/'build/obj/XIVLauncher.Linux/project.assets.json'
source.package_notices(repo,bundle,assets)
for name in ['Cinzel-OFL.txt','GenRyu-OFL.txt','font-sources.json']:shutil.copy2(ui/'assets'/name,bundle/'licenses'/name)
with (bundle/'THIRD-PARTY-NOTICES.txt').open('a') as f:f.write('\nThis build excludes the Avalonia frontend; historical source notices are retained.\n')
source.source_archive(repo,output,a.version)
if any(bundle.glob('Avalonia*')) or any(bundle.glob('*Skia*')):raise RuntimeError('Legacy GUI dependency found')
assert not (bundle/'frontend/tests').exists()
print('Electron-only bundle:',bundle)
