"""Sign the exact JSON bytes; keep the private key outside the repository and artifacts."""
import argparse,hashlib,json,pathlib,subprocess
p=argparse.ArgumentParser();p.add_argument('image',type=pathlib.Path);p.add_argument('--version',required=True);p.add_argument('--url',required=True);p.add_argument('--key',required=True,type=pathlib.Path);p.add_argument('--channel',choices=['stable','preview'],default='stable');a=p.parse_args()
with a.image.open('rb') as file:digest=hashlib.file_digest(file,'sha256').hexdigest()
manifest=a.image.parent/'appimage-update.json';signature=a.image.parent/'appimage-update.sig'
manifest.write_text(json.dumps({'Version':a.version,'Channel':a.channel,'Url':a.url,'Sha256':digest,'Size':a.image.stat().st_size},indent=2)+'\n')
subprocess.run(['openssl','dgst','-sha256','-sign',str(a.key),'-out',str(signature),str(manifest)],check=True)
