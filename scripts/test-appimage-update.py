"""Local end-to-end update. Starts only a loopback server; never publishes or logs in."""
import select
import argparse,functools,hashlib,http.server,json,os,pathlib,shutil,subprocess,tempfile,threading,time
p=argparse.ArgumentParser();p.add_argument('old',type=pathlib.Path);p.add_argument('new',type=pathlib.Path);p.add_argument('--key',type=pathlib.Path,required=True);a=p.parse_args()
def request(process, method, params=None):
 process.stdin.write(json.dumps({'id':'test','method':method,'params':params or {}})+'\n');process.stdin.flush()
 if not select.select([process.stdout],[],[],12)[0]:raise TimeoutError('Backend did not respond: '+method)
 response=json.loads(process.stdout.readline())
 if 'error' in response:raise RuntimeError(response['error']['code'])
 return response['result']
with tempfile.TemporaryDirectory(prefix='super-update-e2e-') as temp:
 root=pathlib.Path(temp);image=root/'installed launcher.AppImage';shutil.copy2(a.old,image)
 class Handler(http.server.SimpleHTTPRequestHandler):
  def log_message(self,*args):pass
 server=http.server.ThreadingHTTPServer(('127.0.0.1',0),functools.partial(Handler,directory=str(root)))
 base=f'http://127.0.0.1:{server.server_port}'
 env=dict(os.environ,APPIMAGE_EXTRACT_AND_RUN='1',SUPER_UPDATE_TEST_API=base+'/releases',XDG_DATA_HOME=str(root/'data'),XDG_CONFIG_HOME=str(root/'config'),XDG_CACHE_HOME=str(root/'cache'))
 def version(path):return subprocess.check_output([str(path),'--launcher-version'],env=env,text=True,timeout=90).strip()
 before=version(image);after=version(a.new.resolve());assert before!=after
 destination=root/f'xivlauncher-super-{after}-x86_64.AppImage'
 manifest={'Version':after,'Channel':'preview','Url':base+'/new.AppImage','Sha256':hashlib.file_digest(a.new.open('rb'),'sha256').hexdigest(),'Size':a.new.stat().st_size}
 (root/'appimage-update.json').write_text(json.dumps(manifest));shutil.copy2(a.new,root/'new.AppImage')
 subprocess.run(['openssl','dgst','-sha256','-sign',str(a.key),'-out',str(root/'appimage-update.sig'),str(root/'appimage-update.json')],check=True)
 (root/'releases').write_text(json.dumps([{'tag_name':'v'+after,'draft':False,'prerelease':True,'html_url':base+'/notes','assets':[{'name':n,'browser_download_url':base+'/'+n} for n in ['appimage-update.json','appimage-update.sig']]}]))
 thread=threading.Thread(target=server.serve_forever,daemon=True);thread.start()
 try:
  extracted=root/'old-runtime';extracted.mkdir()
  subprocess.run([str(image),'--appimage-extract'],cwd=extracted,stdout=subprocess.DEVNULL,check=True,timeout=90)
  bundle=extracted/'squashfs-root/usr/lib/xivlauncher-super'
  distribution=json.loads((bundle/'distribution.json').read_text())
  if not distribution.get('LocalTest') or distribution.get('Channel')!='preview':
   raise RuntimeError('Use an isolated AppImage fixture packaged with --test, never a production image.')
  backend_env=dict(env,APPIMAGE=str(image),DISPLAY='',WAYLAND_DISPLAY='')
  # Headless test: use the exact protocol behind the Electron update controls.
  # The installer still attempts its normal direct relaunch; GUI acceptance is separate.
  process=subprocess.Popen([str(bundle/'xivlauncher-backend'),'--frontend-host','--data-root',str(root/'data/xivlauncher-super')],
   env=backend_env,stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=subprocess.DEVNULL,text=True)
  try:
   request(process,'system.initialize')
   # Fresh isolated profiles have not completed setup, so automatic checks are disabled.
   request(process,'updates.read',{'refresh':True})
   deadline=time.monotonic()+180
   while True:
    update=request(process,'updates.read')
    if update['state']!='checking':break
    if time.monotonic()>deadline:raise TimeoutError('Update check timed out')
    time.sleep(.1)
   if update['state']!='ready' or not update['available'] or not update['canInstall']:raise RuntimeError('No verified update available')
   operation=request(process,'updates.install',{'release':update['release']})
   while True:
    result=request(process,'operations.poll',operation)
    if result['state'] not in ['running','cancelling']:break
    if time.monotonic()>deadline:raise TimeoutError('Update installation timed out')
    time.sleep(.1)
   if result['state']!='completed' or not result['result']['exitForUpdate']:raise RuntimeError('Update preparation failed')
   process.stdin.close();process.wait(timeout=15)
  finally:
   if process.poll() is None:process.kill();process.wait()
  for _ in range(60):
   if destination.exists() and not image.exists() and hashlib.file_digest(destination.open('rb'),'sha256').hexdigest()==manifest['Sha256']:break
   time.sleep(1)
  else:raise RuntimeError('Install did not finish; inspect the isolated diagnostic logs.')
  assert version(destination)==after
  assert hashlib.file_digest(pathlib.Path(str(image)+'.previous').open('rb'),'sha256').hexdigest()==hashlib.file_digest(a.old.open('rb'),'sha256').hexdigest()
  print(f'PASS: {before} -> {after}; full signed AppImage installed with new filename; backup matches original.')
 finally:server.shutdown();server.server_close()
