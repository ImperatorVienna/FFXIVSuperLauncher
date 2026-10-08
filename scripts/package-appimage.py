"""Wrap a complete Linux bundle in a pinned type-2 runtime. No downloads or signing keys are implicit."""
import argparse, hashlib, json, pathlib, shutil, subprocess, tempfile
p=argparse.ArgumentParser();p.add_argument('bundle',type=pathlib.Path);p.add_argument('output',type=pathlib.Path);p.add_argument('--runtime',type=pathlib.Path,required=True);p.add_argument('--public-key',type=pathlib.Path,required=True);p.add_argument('--test',action='store_true');a=p.parse_args()
expected='156f4bdbde9c52d01814600013e0a273f0118dc2de98975f3c8c63427ec79074'
assert hashlib.sha256(a.runtime.read_bytes()).hexdigest()==expected,'Unreviewed AppImage runtime'
if a.output.exists():raise SystemExit('Refusing to overwrite existing AppImage')
repo=pathlib.Path(__file__).resolve().parent.parent
a.output.parent.mkdir(parents=True,exist_ok=True)
with tempfile.TemporaryDirectory(prefix='super-appdir-') as temp:
 appdir=pathlib.Path(temp)/'AppDir';appdir.mkdir();lib=appdir/'usr/lib/xivlauncher-super';shutil.copytree(a.bundle,lib)
 (lib/'distribution.json').write_text(json.dumps({'Kind':'appimage','Channel':'preview' if a.test else 'stable','LocalTest':a.test,'PublicKey':a.public_key.read_text()},indent=2)+'\n')
 shutil.copy2(lib/'icon.png',appdir/'xivlauncher-super.png')
 (appdir/'.DirIcon').symlink_to('xivlauncher-super.png')
 shutil.copy2(repo/'packaging/linux/xivlauncher-super.desktop',appdir/'xivlauncher-super.desktop')
 shutil.copytree(repo/'compliance/notices/appimage',lib/'licenses/appimage',dirs_exist_ok=True)
 (appdir/'AppRun').write_text('#!/bin/sh\nset -eu\nappdir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)\nexec "$appdir/usr/lib/xivlauncher-super/xivlauncher-super" "$@"\n');(appdir/'AppRun').chmod(0o755)
 squash=pathlib.Path(temp)/'payload.squashfs'
 subprocess.run(['mksquashfs',str(appdir),str(squash),'-noappend','-comp','zstd','-Xcompression-level','15','-all-root','-no-progress','-processors','2'],check=True)
 with a.output.open('xb') as out:
  out.write(a.runtime.read_bytes())
  with squash.open('rb') as source:shutil.copyfileobj(source,out)
 a.output.chmod(0o755)
print(a.output)
