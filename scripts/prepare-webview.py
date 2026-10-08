"""Package the pinned, unmodified Electron runtime with a minimal CAPTCHA-only shell."""
import hashlib, os, pathlib, shutil, sys, tempfile, urllib.request, zipfile
VERSION = '44.5.1'
SHA256 = '5bcd217611d6843ececd6c9e9c1fcd1da3ab066c43d8b1a9e4b44689a1fba6f5'
repo = pathlib.Path(__file__).resolve().parent.parent
bundle = pathlib.Path(sys.argv[1]) / 'Tools' / 'webview'
with tempfile.TemporaryDirectory(prefix='super-webview-') as temporary:
    archive = pathlib.Path(os.environ.get('WEBVIEW_ARCHIVE', str(pathlib.Path(temporary) / 'electron.zip')))
    if not archive.exists():
        urllib.request.urlretrieve(f'https://github.com/electron/electron/releases/download/v{VERSION}/electron-v{VERSION}-linux-x64.zip', archive)
    with archive.open('rb') as stream:
        if hashlib.file_digest(stream, 'sha256').hexdigest() != SHA256:
            raise RuntimeError('Web runtime SHA256 mismatch')
    bundle.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(archive) as data:
        data.extractall(bundle)
    (bundle / 'electron').chmod(0o755)
    # Use the user-namespace sandbox; never ship a writable setuid executable.
    (bundle / 'chrome-sandbox').chmod(0o755)
    (bundle / 'chrome_crashpad_handler').chmod(0o755)
    default_app = bundle / 'resources/default_app.asar'
    if default_app.exists(): default_app.unlink()
    shutil.copytree(repo / 'packaging/webview', bundle / 'resources/app', dirs_exist_ok=True)
    shutil.copy2(repo / 'compliance/notices/Electron-44.5.1-FFmpeg-COPYING.LGPLv2.1', bundle / 'COPYING.FFmpeg.LGPLv2.1')
    shutil.copy2(repo / 'compliance/provenance/webview.json', bundle / 'SOURCE-PROVENANCE.json')
    (bundle / 'COMPONENT.txt').write_text(f'Electron {VERSION}\nhttps://github.com/electron/electron/releases/tag/v{VERSION}\nArchive SHA256: {SHA256}\nRetain LICENSE, LICENSES.chromium.html and COPYING.FFmpeg.LGPLv2.1. FFmpeg source revision/archive: SOURCE-PROVENANCE.json. Updates ship only with launcher releases.\n')
print(f'Packaged verification web component: {bundle}')
