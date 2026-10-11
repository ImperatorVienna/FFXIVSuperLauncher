'use strict';
const { app, BrowserWindow, WebContentsView, ipcMain, Menu, session, dialog, screen } = require('electron');
const path = require('node:path');
const {mkdirSync,existsSync} = require('node:fs');
const {restoreBounds,observePlacement} = require('./window-state.cjs');
const {publishSession,waitForPrimary} = require('./steam-session.cjs');
const {createCaptchaView}=require('./captcha-view.cjs');
const {shutdown}=require('./shutdown.cjs');
const {handoff}=require('./session-handoff.cjs');
const {writeDevelopmentEntry} = require('./launcher-entry.cjs');
const steamEntry=process.argv.includes('--steam-entry');
const { pathToFileURL } = require('node:url');
const { BackendClient } = require('./backend-client.cjs');
const {contracts} = require('./ipc-contract.cjs');
const {createBrowserCacheCleaner}=require('./browser-cache.cjs');
const repository = path.resolve(__dirname, '..');
const packaged=existsSync(path.join(repository,'frontend-package.json'));
const defaultData=path.join(process.env.XDG_DATA_HOME&&path.isAbsolute(process.env.XDG_DATA_HOME)?process.env.XDG_DATA_HOME:path.join(require('node:os').homedir(),'.local/share'),'xivlauncher-super');
const dataRoot = path.resolve(process.env.FFXIV_FRONTEND_DATA_DIR || (packaged?defaultData:path.join(repository, 'artifacts/electron-development-data')));
const executable = packaged?path.join(repository,'xivlauncher-backend'):process.env.FFXIV_BACKEND;
if (!executable || !path.isAbsolute(executable)) throw new Error('Set FFXIV_BACKEND to the built launcher executable (absolute path).');
const userData = path.join(dataRoot, 'electron');
mkdirSync(userData, {recursive:true,mode:0o700});
app.setPath('userData', userData);
app.enableSandbox();
if (!app.requestSingleInstanceLock({steamEntry})) {
  if(steamEntry)void waitForPrimary(dataRoot,packaged&&process.env.FFXIV_SESSION_DIRECTORY
    ? {transfer:pid=>handoff({child:{pid},release(){}},process.env.FFXIV_SESSION_DIRECTORY)} : {}).catch(()=>{}).finally(()=>app.quit());else app.quit();
}
else {
  let backend;
  let window;
  let closing,placement,verification,removeSession,operationId,attachSteam=steamEntry;
  function quit(code=0){
    return closing??=shutdown({placement,verification,backend,removeSession,
      report:stage=>console.error('[frontend] cleanup-failed: '+stage)}).finally(()=>app.exit(code));
  }
  function rendererFailed(stage){console.error('[frontend] '+stage);void quit(1);}
  const page = path.join(repository, 'prototypes/electron-ui/index.html');
  const localUrl = pathToFileURL(page).href + '?integration=1';
  function checkSender(event) {
    if (!window || event.sender !== window.webContents || event.senderFrame !== window.webContents.mainFrame || event.senderFrame.url !== localUrl)
      throw new Error('Untrusted frontend request.');
  }
  app.whenReady().then(async () => {
    Menu.setApplicationMenu(null);
    session.defaultSession.setPermissionRequestHandler((_contents,_permission,callback) => callback(false));
    session.defaultSession.setPermissionCheckHandler(() => false);
    session.defaultSession.on('will-download', event => event.preventDefault());
    const launcherEntry=packaged?(process.env.APPIMAGE?path.join(dataRoot,'appimage-launcher'):path.join(repository,'xivlauncher-super')):path.join(dataRoot,'electron-launcher');
    backend = new BackendClient(executable, ['--frontend-host','--data-root',dataRoot,'--launcher-entry',launcherEntry,...(attachSteam?['--steam-entry']:[])], {waitForGame:attachSteam});
    backend.onFailure=()=>{void verification?.close();};
    const startup=await backend.request('system.initialize');
    if(!packaged)await writeDevelopmentEntry({root:dataRoot,node:process.env.FFXIV_FRONTEND_NODE,runner:path.join(__dirname,'run.cjs'),
      backend:executable,electron:process.env.FFXIV_ELECTRON,dotnetRoot:process.env.DOTNET_ROOT});
    removeSession=await publishSession(dataRoot,packaged&&/^\d+$/.test(process.env.FFXIV_SESSION_SUPERVISOR||'')?Number(process.env.FFXIV_SESSION_SUPERVISOR):process.pid);
    const clearBrowserCache=createBrowserCacheCleaner({session,busy:async()=>{
      if(verification?.isActive())return true;
      if(!operationId)return false;
      const state=await backend.request('operations.poll',{operationId});
      return ['running','cancelling'].includes(state.state);
    }});
    for (const [method,validate] of Object.entries(contracts)) {
      if(['system.initialize','window.save','steam.attach','captcha.page','captcha.reply','captcha.fail','session.release'].includes(method))continue;
      ipcMain.handle('launcher:'+method, async (event, params) => {
        checkSender(event);
        if(!validate(params))return {ok:false,code:'invalid-request',message:'Invalid request.'};
        try {const result=method==='maintenance.browser-cache'?await clearBrowserCache():await backend.request(method,params);if(result?.operationId)operationId=result.operationId;return {ok:true,result};}
        catch(error){return {ok:false,code:error.message,message:error.userMessage || (method==='maintenance.browser-cache'?error.message:'The backend request failed.'),logMessage:error.logMessage || 'The backend request failed.'};}
      });
    }
    ipcMain.handle('launcher:captcha-layout',async(event,value)=>{
      checkSender(event);
      if(value!==null&&(!value||typeof value.operationId!=='string'||value.operationId.length>64||
        value.bounds!==null&&(!value.bounds||Object.keys(value.bounds).sort().join(',')!=='height,width,x,y'||!Object.values(value.bounds).every(v=>Number.isFinite(v)&&v>=0&&v<30000))))throw new Error('Invalid verification bounds.');
      await verification?.update(value);
    });
    ipcMain.handle('launcher:choose-client',async event=>{
      checkSender(event);
      const result=await dialog.showOpenDialog(window,{properties:['openDirectory']});
      return result.canceled ? null : result.filePaths[0];
    });
    ipcMain.handle('launcher:choose-proton',async event=>{
      checkSender(event);
      const result=await dialog.showOpenDialog(window,{properties:['openFile'],filters:[{name:'Proton',extensions:['*']}]});
      return result.canceled ? null : result.filePaths[0];
    });
    ipcMain.handle('launcher:finish-launch',async (event, operationId)=>{
      checkSender(event);
      if(typeof operationId!=='string'||operationId.length>64)throw new Error('Invalid operation.');
      const result=await backend.request('operations.poll',{operationId});
      if(result.state!=='completed'||!result.launched)return {released:false};
      if(!packaged||!process.env.FFXIV_SESSION_DIRECTORY){placement?.minimize();return {released:false};}
      // Save placement before EOF; never accept a renderer-supplied PID or file path.
      await placement?.flush();
      let decision;
      for(let attempt=0;attempt<20;attempt++){
        decision=await backend.request('session.release',{operationId});
        if(decision.ready)break;
        await new Promise(resolve=>setTimeout(resolve,500));
      }
      if(!decision.ready){placement?.minimize();return {released:false};}
      await verification?.close();
      await handoff(backend,process.env.FFXIV_SESSION_DIRECTORY);
      placement?.stop();removeSession=null;
      // Leave the supervisor-owned session marker until its PID exits. A later
      // invocation replaces the marker; secondary Steam entries wait for that PID.
      app.exit(0);
      return {released:true};
    });
    ipcMain.handle('launcher:quit-for-update',async(event,operationId)=>{
      checkSender(event);
      if(typeof operationId!=='string'||operationId.length>64)throw new Error('Invalid operation.');
      const result=await backend.request('operations.poll',{operationId});
      if(result.state!=='completed'||result.result?.exitForUpdate!==true)throw new Error('No prepared update.');
      app.quit();
    });
    const bounds=restoreBounds(startup.windowPlacement,[screen.getPrimaryDisplay().workArea,...screen.getAllDisplays().map(d=>d.workArea)]);
    window = new BrowserWindow({...bounds,minWidth:Math.min(860,bounds.width),minHeight:Math.min(680,bounds.height),
      title:'FFXIV Super Launcher',
      icon:path.join(repository,packaged?'icon.png':'src/XIVLauncher.Linux/Resources/icon.png'),
      webPreferences:{preload:path.join(__dirname,'preload.cjs'),contextIsolation:true,nodeIntegration:false,sandbox:true,webSecurity:true,backgroundThrottling:true}});
    verification=createCaptchaView({window,backend,WebContentsView,session});
    window.webContents.on('will-navigate', event => event.preventDefault());
    window.webContents.setWindowOpenHandler(() => ({action:'deny'}));
    window.webContents.on('preload-error', () => rendererFailed('preload-failed'));
    window.webContents.on('render-process-gone', () => rendererFailed('renderer-exited'));
    window.webContents.on('did-fail-load', (_event, code, _description, _url, main) => {if(main&&code!==-3)rendererFailed('page-load-failed');});
    // A reload loses the renderer's operation identity. Cancel its pending work first.
    window.webContents.on('did-start-navigation', (_event,_url,inPlace,main) => {
      if(main&&!inPlace){void verification?.update(null);if(operationId)void backend.request('operations.cancel',{operationId}).catch(()=>{});}
    });
    window.webContents.once('did-finish-load', () => console.info('[frontend] local-page-loaded'));
    if(bounds.maximized)window.maximize();
    placement=observePlacement(window,value=>backend.request('window.save',value));
    window.on('closed', () => { window=null; });
    return window.loadFile(page, {query:{integration:'1'}});
  }).catch(() => { console.error('[frontend] startup-failed');void quit(1); });
  app.on('second-instance', (_event,args,_directory,data) => {
    if(data?.steamEntry||args.includes('--steam-entry')){attachSteam=true;if(backend){backend.waitForGame=true;void backend.request('steam.attach').catch(()=>{});}}
    if (window) { if(window.isMinimized())window.restore();window.focus(); }
  });
  app.on('before-quit', event => {
    if(!backend)return;event.preventDefault();void quit();
  });
  process.once('SIGTERM',()=>app.quit());process.once('SIGINT',()=>app.quit());
  app.on('window-all-closed', () => app.quit());
}
