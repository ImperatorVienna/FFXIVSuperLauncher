'use strict';
const {test}=require('node:test');const assert=require('node:assert/strict');const vm=require('node:vm');const fs=require('node:fs');const {EventEmitter}=require('node:events');
async function start(packaged=true,verification=null,release=false){
 const calls=[],handlers=new Map();let current;const ready=[];
 const app=new EventEmitter();Object.assign(app,{setPath(){},enableSandbox(){},requestSingleInstanceLock(){return true},whenReady(){const p=Promise.resolve();const original=p.then.bind(p);p.then=fn=>{const result=original(fn);ready.push(result);return result;};return p;},quit(){calls.push('quit')},exit(){calls.push('exit')}});
 class Window extends EventEmitter{constructor(options){super();this.options=options;this.webContents=new EventEmitter();this.webContents.mainFrame={url:''};this.webContents.setWindowOpenHandler=fn=>this.navigation=fn;current=this;calls.push('window');}loadFile(file,{query}){this.webContents.mainFrame.url=require('node:url').pathToFileURL(file).href+'?integration='+query.integration;return Promise.resolve();}maximize(){}isMinimized(){return false}focus(){}}
 class Backend{constructor(executable,args){calls.push({executable,args});}async request(method){calls.push(method);if(method==='system.initialize')return {windowPlacement:null,setupComplete:false};if(method==='operations.poll')return {state:'completed',launched:release,result:{exitForUpdate:true}};if(method==='session.release')return {ready:release};return {saved:true};}async close(){calls.push('backend.close')}}
 const electron={app,BrowserWindow:Window,ipcMain:{handle:(name,fn)=>handlers.set(name,fn)},Menu:{setApplicationMenu(){}},session:{defaultSession:{setPermissionRequestHandler(){},setPermissionCheckHandler(){},on(){}}},dialog:{},screen:{getPrimaryDisplay:()=>({workArea:{x:0,y:0,width:1280,height:800}}),getAllDisplays:()=>[]}};
 const mocks={'./session-handoff.cjs':{handoff:async()=>calls.push('handoff')},'electron':electron,'node:fs':{mkdirSync(){},existsSync:()=>packaged},'./captcha-view.cjs':{createCaptchaView:()=>verification||({update:async()=>{},close:async()=>{}})},'./backend-client.cjs':{BackendClient:Backend},'./launcher-entry.cjs':{writeDevelopmentEntry:async()=>calls.push('entry')},'./steam-session.cjs':{publishSession:async()=>{calls.push('session');return async()=>calls.push('remove-session')}},'./window-state.cjs':{...require('../window-state.cjs'),observePlacement:()=>({flush:async()=>calls.push('flush'),stop(){},minimize:()=>calls.push('minimize')})}};
 vm.runInNewContext(fs.readFileSync(require.resolve('../main.cjs'),'utf8'),{require:name=>mocks[name]||require(name.startsWith('./')?'../'+name.slice(2):name),__dirname:'/fixture/frontend',console,process:{once(){},argv:['electron','--super-launcher'],env:{APPIMAGE:'/test/renamed.AppImage',FFXIV_BACKEND:'/stale/backend',FFXIV_ELECTRON:'/runtime/electron',FFXIV_FRONTEND_NODE:'/runtime/node',FFXIV_FRONTEND_DATA_DIR:'/isolated',...(release?{FFXIV_SESSION_DIRECTORY:'/private-session',FFXIV_SESSION_SUPERVISOR:'123'}:{})}}});
 await Promise.all(ready);
 return {calls,handlers,current,app,event:{sender:current.webContents,senderFrame:current.webContents.mainFrame}};
}
test('packaged startup creates configuration before windows and ignores stale inherited backend',async()=>{
 const {calls,handlers,current,event}=await start();
 assert.equal(calls[0].executable,'/fixture/xivlauncher-backend');assert.ok(calls[0].args.includes('/isolated/appimage-launcher'));
 assert.ok(calls.indexOf('system.initialize')<calls.indexOf('session'));assert.ok(calls.indexOf('session')<calls.indexOf('window'));
 assert.equal(current.options.webPreferences.nodeIntegration,false);assert.equal(current.options.webPreferences.sandbox,true);
 assert.equal(handlers.has('launcher:system.initialize'),false);assert.equal(handlers.has('launcher:window.save'),false);
 await assert.rejects(handlers.get('launcher:snapshot')({...event,senderFrame:{}},{}),/Untrusted/);
 const response=await handlers.get('launcher:preferences.update')(event,{command:'arbitrary'});assert.equal(response.code,'invalid-request');
 await handlers.get('launcher:quit-for-update')(event,'trusted-id');assert.equal(calls.at(-1),'quit');
});
test('development startup creates its stable entry only after defaults exist',async()=>{
 const {calls}=await start(false);assert.equal(calls[0].executable,'/stale/backend');
 assert.ok(calls.indexOf('system.initialize')<calls.indexOf('entry'));assert.ok(calls.indexOf('entry')<calls.indexOf('window'));
});

// Closing the window must reach backend/session cleanup even when CDP never answers.
test('closing after a stalled verification releases the view, backend and owning session',async()=>{
 const {createCaptchaView}=require('../captcha-view.cjs');
 for(const cancelFirst of [false,true]){
  let view,commandStarted=false;
  class View{
   constructor(){view=this;const c=this.webContents=new EventEmitter();c.debugger=new EventEmitter();
    c.debugger.attach=()=>{};c.debugger.sendCommand=async()=>{commandStarted=true;await new Promise(()=>{});};
    c.setWindowOpenHandler=()=>{};c.loadURL=async()=>{};c.isDestroyed=()=>this.closed;c.close=()=>{this.closed=true};}
   setBounds(){}
  }
  const verification=createCaptchaView({window:{contentView:{addChildView(){},removeChildView(){}},getContentSize:()=>[800,700]},
   backend:{async request(method){if(method==='operations.poll')return {state:'running',webVerificationId:'test-id',challenge:{kind:'web-captcha'}};
    if(method==='captcha.page')return {url:'https://launcher.ffxiv.com.tw/index.html',binding:'synthetic-binding',html:'synthetic page'};return {};}},
   WebContentsView:View,session:{fromPartition:()=>({setPermissionRequestHandler(){},setPermissionCheckHandler(){},on(){}})},commandTimeout:5000});
  const h=await start(true,verification);
  const pending=h.handlers.get('launcher:captcha-layout')(h.event,{operationId:'operation',bounds:{x:0,y:0,width:300,height:150}});
  await new Promise(resolve=>setImmediate(resolve));assert.equal(commandStarted,true);
  if(cancelFirst)await h.handlers.get('launcher:captcha-layout')(h.event,null);
  h.current.emit('closed');h.app.emit('window-all-closed');assert.equal(h.calls.at(-1),'quit');
  let prevented=false;h.app.emit('before-quit',{preventDefault(){prevented=true}});
  await new Promise(resolve=>setImmediate(resolve));
  assert.equal(prevented,true);assert.equal(view.closed,true);
  assert.ok(h.calls.indexOf('backend.close')>=0);assert.ok(h.calls.indexOf('remove-session')>h.calls.indexOf('backend.close'));
  assert.equal(h.calls.at(-1),'exit');await pending;
 }
});

test('a failed view cleanup does not skip backend or session cleanup',async()=>{
 const h=await start(true,{close:async()=>{throw new Error('synthetic cleanup failure')}});
 h.app.emit('before-quit',{preventDefault(){}});await new Promise(resolve=>setImmediate(resolve));
 assert.ok(h.calls.includes('backend.close'));assert.ok(h.calls.includes('remove-session'));assert.equal(h.calls.at(-1),'exit');
});
test('crashed main renderer takes the same complete shutdown path',async()=>{
 const h=await start();h.current.webContents.emit('render-process-gone',{}, {reason:'crashed'});
 await new Promise(resolve=>setImmediate(resolve));assert.ok(h.calls.includes('backend.close'));assert.ok(h.calls.includes('remove-session'));assert.equal(h.calls.at(-1),'exit');
});
test('all backend contracts and exposed IPC methods have matching handlers',async()=>{
 const h=await start();const {contracts}=require('../ipc-contract.cjs');
 const host=fs.readFileSync(require.resolve('../../src/XIVLauncher.Linux/Frontend/FrontendHost.cs'),'utf8');
 const backendMethods=[...host.matchAll(/case "([a-z.-]+)":/g)].map(m=>m[1]).sort();
 assert.deepEqual(backendMethods,Object.keys(contracts).filter(method=>method!=='maintenance.browser-cache').sort());
 let bridge;vm.runInNewContext(fs.readFileSync(require.resolve('../preload.cjs'),'utf8'),{require:()=>({contextBridge:{exposeInMainWorld:(_name,value)=>bridge=value},ipcRenderer:{invoke:name=>assert.ok(h.handlers.has(name),name)}})});
 for(const call of Object.values(bridge))await call();
 const internal=new Set(['system.initialize','window.save','steam.attach','captcha.page','captcha.reply','captcha.fail','session.release']);
 for(const method of Object.keys(contracts))assert.equal(h.handlers.has('launcher:'+method),!internal.has(method));
});

test('confirmed launch hands off lifetime then exits Chromium without waiting for the game',async()=>{
 const h=await start(true,null,true);
 await h.handlers.get('launcher:finish-launch')(h.event,'launch-id');
 assert.ok(h.calls.indexOf('flush')<h.calls.indexOf('session.release'));
 assert.ok(h.calls.indexOf('session.release')<h.calls.indexOf('handoff'));
 assert.equal(h.calls.at(-1),'exit');assert.ok(!h.calls.includes('backend.close'));assert.ok(!h.calls.includes('remove-session'));
});
test('nonlaunch operations never hand off or exit',async()=>{
 const h=await start();await h.handlers.get('launcher:finish-launch')(h.event,'update-id');
 assert.ok(!h.calls.includes('handoff'));assert.ok(!h.calls.includes('exit'));
});
