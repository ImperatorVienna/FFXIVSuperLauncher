'use strict';
// Real Electron UI tests with a synthetic bridge. Uses an isolated profile, never game/account files.
// NODE_PATH=<playwright modules> FFXIV_ELECTRON=<bundled electron> node frontend/tests/ui-regressions.cjs
const assert=require('node:assert/strict'),fs=require('node:fs/promises'),path=require('node:path'),http=require('node:http');
const {tmpdir}=require('node:os'),{_electron}=require('playwright'),{prepareRuntime}=require('../run.cjs');
const root=path.resolve(__dirname,'../..');
(async()=>{
 const temp=await fs.mkdtemp(path.join(tmpdir(),'launcher-ui-regressions-'));
 let runtime,app,server,page,extra='';const errors=[];
 try{
  const fixture=await fs.readFile(path.join(__dirname,'fixture-bridge.js'),'utf8');
  server=http.createServer(async(req,res)=>{try{
   const pathname=new URL(req.url,'http://localhost').pathname,file=path.resolve(root,'.'+(pathname.endsWith('/')?pathname+'index.html':pathname));
   if(!file.startsWith(root+path.sep))throw Error('Invalid path');
   res.setHeader('Content-Type',({'.js':'text/javascript','.html':'text/html','.css':'text/css','.svg':'image/svg+xml'})[path.extname(file)]||'application/octet-stream');res.setHeader('Cache-Control','no-store');
   res.end(file.endsWith('/frontend/tests/fixture-bridge.js')?fixture+'\n'+extra:await fs.readFile(file));
  }catch{res.statusCode=404;res.end();}});
  await new Promise(resolve=>server.listen(0,'127.0.0.1',resolve));
  const base=`http://127.0.0.1:${server.address().port}/artifacts/frontend-ui-tests/`;
  const main=path.join(temp,'main.cjs');await fs.writeFile(main,`globalThis.testRequire=require;const {app,BrowserWindow}=require('electron');app.setPath('userData',${JSON.stringify(path.join(temp,'profile'))});app.whenReady().then(()=>{const window=new BrowserWindow({width:1280,height:800,webPreferences:{contextIsolation:true,sandbox:true}});require(${JSON.stringify(path.join(root,'frontend/window-ui.cjs'))}).observeContentSize(window);window.loadURL('about:blank');});app.on('window-all-closed',()=>app.quit());`);
  runtime=await prepareRuntime(process.env.FFXIV_ELECTRON,main);
  const env={...process.env};delete env.ELECTRON_RUN_AS_NODE;delete env.NODE_OPTIONS;delete env.NODE_TEST_CONTEXT;
  app=await _electron.launch({executablePath:runtime.executable,args:['--no-sandbox'],env});page=await app.firstWindow();page.setDefaultTimeout(8000);page.on('pageerror',e=>errors.push(e.message));
  async function open(query='',code=''){extra=code;await page.goto(base+query);await page.waitForFunction(()=>document.body.dataset.backend==='connected');}
  async function select(id,value){await page.locator(id).selectOption(value);await page.waitForFunction(()=>document.querySelector('.setup-dialog')?.getAttribute('aria-busy')!=='true');}
  async function next(){await page.locator('#setup-next').click();await page.waitForFunction(()=>document.querySelector('.setup-dialog')?.getAttribute('aria-busy')!=='true');}
  async function tab(n){await page.locator(`nav [data-page="${n}"]`).click();}
  async function step3(){await next();await next();await page.waitForSelector('#setup-proton');}
  function pass(name){assert.deepEqual(errors,[],name);console.log('PASS: '+name);}

  await open('?setup=new',`window.updateReads=0;const update=launcherBridge.readLauncherUpdate;launcherBridge.readLauncherUpdate=x=>{window.updateReads++;return update(x);};`);
  assert.equal(await page.locator('#setup-language').inputValue(),'en');await page.keyboard.press('Escape');await page.mouse.click(2,2);assert.equal(await page.locator('.setup-dialog').evaluate(e=>e.open),true);assert.equal(await page.evaluate(()=>window.updateReads),0);
  await select('#setup-language','zh-CN');await next();assert.equal(await page.locator('#setup-cdkey').inputValue(),'NA');assert.equal(await page.locator('#setup-clientLanguage').inputValue(),'1');
  await select('#setup-cdkey','EU');assert.equal(await page.locator('#setup-euShopLanguage').inputValue(),'en-gb');
  const pair=await page.locator('.settings-pair select').evaluateAll(nodes=>nodes.map(n=>n.getBoundingClientRect().top));assert.equal(pair[0],pair[1]);
  assert.equal(await page.locator('.setup-dialog').evaluate(e=>getComputedStyle(e).backgroundColor),'rgba(0, 0, 0, 0)');assert.equal(await page.locator('.setup-dialog').evaluate(e=>getComputedStyle(e).backdropFilter),'blur(3px)');
  await next();await page.locator('#setup-enableDalamud').check();await page.waitForFunction(()=>document.querySelector('.setup-dialog').getAttribute('aria-busy')==='false');
  assert.equal(await page.locator('#setup-proton option').count(),1);await page.locator('#setup-complete').click();await page.waitForFunction(()=>!document.querySelector('.setup-dialog'));assert.equal(await page.evaluate(async()=>(await launcherBridge.snapshot()).result.regions.find(r=>r.id==='ffxiv').enableDalamud),true);await page.waitForFunction(()=>window.updateReads===1);
  pass('wizard: English defaults, nondismissible, separated steps, EU defaults, transparent surface, immediate regional save and update only after completion');

  await open('?setup=new',`const save=launcherBridge.applyPreferences;launcherBridge.applyPreferences=x=>x.language?new Promise(resolve=>{window.rejectSave=()=>resolve({ok:false,message:'Synthetic preference failure.'});}):save(x);`);
  await page.locator('#setup-language').selectOption('ja');await page.locator('#setup-next').click();await page.evaluate(()=>window.rejectSave());await page.waitForFunction(()=>document.querySelector('.setup-dialog').getAttribute('aria-busy')==='false');assert.equal(await page.locator('.setup-dialog').getAttribute('data-step'),'1');assert.match(await page.locator('.setup-dialog [role=status]').textContent(),/Synthetic preference failure/);assert.equal(await page.locator('#setup-language').inputValue(),'en');
  pass('a queued Next cannot bypass a failed preference save');

  await open('?setup=new',`const apply=launcherBridge.applyCompatibility;launcherBridge.applyCompatibility=x=>x.dataDirectory==='/bad'?Promise.resolve({ok:false,message:'Synthetic path error.'}):apply(x);`);await step3();
  await page.locator('#setup-data').fill('/bad');await page.locator('#setup-data').dispatchEvent('change');await page.waitForFunction(()=>document.querySelector('#setup-data').getAttribute('aria-invalid')==='true');await page.locator('#setup-enableDalamud').check();await page.waitForFunction(()=>document.querySelector('.setup-dialog').getAttribute('aria-busy')==='false');
  assert.equal(await page.locator('#setup-data').inputValue(),'/bad');assert.match(await page.locator('.setup-dialog [role=status]').textContent(),/Synthetic path error/);await page.locator('#setup-complete').click();assert.equal(await page.locator('.setup-dialog').count(),1);
  await page.locator('#setup-data').fill('/fixed');await page.locator('#setup-data').dispatchEvent('change');await page.waitForFunction(()=>document.querySelector('#setup-data').getAttribute('aria-invalid')==='false');assert.equal(await page.locator('#setup-proton option').count(),1);await page.locator('#setup-complete').click();await page.waitForFunction(()=>!document.querySelector('.setup-dialog'));
  pass('failed path draft survives other saves; completion is blocked until corrected; committed save retains discovered Proton choices');

  await open('',`const apply=launcherBridge.applyCompatibility;launcherBridge.applyCompatibility=x=>x.dataDirectory==='/bad'?Promise.resolve({ok:false,message:'Synthetic path error.'}):apply(x);`);await tab(2);await page.waitForSelector('#compatibility-data');await page.evaluate(()=>window.originalInput=document.querySelector('#compatibility-data'));
  await page.locator('#compatibility-data').fill('/bad');await page.locator('#compatibility-data').dispatchEvent('change');await page.waitForFunction(()=>document.querySelector('#compatibility-feedback').textContent.includes('Synthetic path error'));assert.equal(await page.locator('#compatibility-data').inputValue(),'/bad');
  await page.locator('#compatibility-data').fill('/fixed');await page.locator('#compatibility-data').dispatchEvent('change');await page.waitForFunction(()=>document.querySelector('#compatibility-feedback').textContent.includes('saved'));assert.equal(await page.evaluate(()=>window.originalInput===document.querySelector('#compatibility-data')),true);assert.equal(await page.locator('#proton-choice option').count(),1);assert.equal(await page.evaluate(()=>document.activeElement.id),'compatibility-data');
  pass('compatibility errors retain drafts; ordinary saves keep controls and runtime list');

  await tab(4);await page.waitForSelector('[data-plugin-id="Sample"]');await page.waitForSelector('[data-repository-body]>.row>button');
  await page.evaluate(()=>window.repositoryPanel=document.querySelector('[data-repository-body]').firstElementChild);await page.locator('[data-plugin-body] .plugin-actions button').first().click();await page.waitForFunction(()=>document.querySelector('[data-plugin-body] .plugin-actions button').disabled===false);assert.equal(await page.evaluate(()=>window.repositoryPanel===document.querySelector('[data-repository-body]').firstElementChild),true);assert.equal(await page.locator('[data-plugin-body] .plugin-actions button').first().evaluate(e=>e===document.activeElement),true);
  await page.evaluate(()=>window.pluginCheckbox=document.querySelector('[data-plugin-id="Sample"]'));await page.locator('[data-repository-body]>.row>button').nth(1).click();await page.waitForFunction(()=>document.querySelector('[data-repository-body]>.row>button').disabled===false);assert.equal(await page.evaluate(()=>window.pluginCheckbox===document.querySelector('[data-plugin-id="Sample"]')),true);
  pass('refreshing each plugin/repository panel leaves the other panel intact');

  await open('',`launcherBridge.startPluginOperation=()=>Promise.resolve({ok:false,message:'Synthetic start failure.',logMessage:'Synthetic start failure.'});`);await tab(4);await page.locator('[data-plugin-id="Sample"]').click();await page.waitForSelector('[data-plugin-body] .inline-error');assert.equal(await page.locator('#backend-feedback').count(),0);assert.equal((await page.locator('#log').textContent()).split('Synthetic start failure.').length-1,1);assert.equal(await page.locator('[data-repository-body] .inline-error').count(),0);
  pass('plugin start error stays local and is logged once');

  await open('',`let saved=false;const apply=launcherBridge.applyCompatibility,read=launcherBridge.readCompatibility;launcherBridge.applyCompatibility=async x=>{const result=await apply(x);saved=true;return result;};launcherBridge.readCompatibility=()=>saved?Promise.resolve({ok:false,message:'Synthetic read failure.'}):read();`);await tab(2);await page.locator('#compatibility-data').fill('/committed');await page.locator('#compatibility-data').dispatchEvent('change');await page.waitForFunction(()=>document.querySelector('#compatibility-feedback').textContent.includes('saved, but'));assert.equal(await page.locator('#compatibility-data').inputValue(),'/committed');assert.equal(await page.locator('#proton-choice option').count(),1);
  pass('a failed post-save rescan cannot discard the committed value or discovered runtimes');

  await open('?setup=new',`let completed=false;const done=launcherBridge.completeSetup,list=launcherBridge.listAccounts;launcherBridge.completeSetup=async x=>{const result=await done(x);completed=true;return result;};launcherBridge.listAccounts=x=>completed?Promise.resolve({ok:false,message:'Synthetic account failure.'}):list(x);`);await step3();await page.locator('#setup-complete').click();await page.waitForFunction(()=>!document.querySelector('.setup-dialog'));await page.waitForSelector('#backend-feedback');assert.equal(await page.locator('nav [aria-current]').getAttribute('data-page'),'0');assert.equal(await page.locator('.logs').evaluate(e=>e.open),false);
  pass('setup completion survives an independent account refresh failure');

  await open('?updates=new',`window.installs=0;launcherBridge.installLauncherUpdate=()=>{window.installs++;return new Promise(resolve=>window.failInstall=()=>resolve({ok:false,message:'Synthetic update failure.'}));};`);
  await page.waitForSelector('#launcher-update-title');const install=page.locator('dialog .primary');await install.click();await install.dispatchEvent('click');assert.equal(await page.evaluate(()=>window.installs),1);await page.keyboard.press('Escape');assert.equal(await page.locator('dialog[open]').count(),1);await page.evaluate(()=>window.failInstall());await page.waitForFunction(()=>document.querySelector('dialog [role=status]').textContent.includes('Synthetic update failure'));assert.equal(await install.isEnabled(),true);await page.locator('dialog button').filter({hasText:'Later'}).click();await page.waitForFunction(()=>!document.querySelector('dialog[open]'));assert.equal(await page.locator('.logs').evaluate(e=>e.open),false);
  pass('update dialog blocks duplicate installs and premature Escape, keeps failure visible and permits retry');

  await open();await tab(5);await page.waitForSelector('#art-sources');await page.locator('#art-sources').click();await page.locator('.art-sources-dialog button').filter({hasText:'Close'}).click();await page.waitForFunction(()=>document.activeElement?.id==='art-sources');
  await page.evaluate(()=>window.aboutButton=document.querySelector('#art-sources'));await page.locator('#auto-check-updates').uncheck();await page.waitForFunction(()=>!document.querySelector('#auto-check-updates').disabled);assert.equal(await page.evaluate(()=>window.aboutButton===document.querySelector('#art-sources')),true);
  pass('dialog close restores its trigger; preference saves do not rebuild About controls');

  await open('?challenge=otp');await app.evaluate(({BrowserWindow})=>BrowserWindow.getAllWindows()[0].setContentSize(1024,600));await page.locator('[data-action="login"]').click();await page.waitForSelector('#otp');
  const scroll=await page.evaluate(()=>({page:document.querySelector('#page').scrollHeight>document.querySelector('#page').clientHeight,nested:[...document.querySelectorAll('.news,.news-column,.login-column,.verification,.verification-body')].filter(n=>['auto','scroll'].includes(getComputedStyle(n).overflowY)&&n.scrollHeight>n.clientHeight).length}));assert.equal(scroll.page,true);assert.equal(scroll.nested,0);
  for(const n of [1,2,3,4,5]){await tab(n);assert.equal(await page.locator('#page').evaluate(e=>getComputedStyle(e).overflowY),'auto');}
  pass('1024×600 home and verification use page scrolling; other pages share the same scroll container');
  if(process.env.UI_TEST_ARTIFACTS){const out=path.resolve(process.env.UI_TEST_ARTIFACTS);await fs.mkdir(out,{recursive:true});await page.screenshot({path:path.join(out,'small-window-about.png')});await open('?setup=new');await select('#setup-language','zh-CN');await next();await select('#setup-cdkey','EU');await page.screenshot({path:path.join(out,'setup-region-zh-CN.png')});}

  await open('',`window.keyboardRequests=[];launcherBridge.showSteamKeyboard=async value=>{window.keyboardRequests.push(value);return {status:'unavailable'};};`);
  await page.locator('#login-name').click();await page.waitForSelector('.steam-keyboard-hint');
  assert.equal(await page.locator('#login-name').evaluate(e=>document.activeElement===e),true);
  await page.keyboard.type('Synthetic');assert.match(await page.locator('#login-name').inputValue(),/Synthetic/);
  const keyboardRequest=await page.evaluate(()=>window.keyboardRequests[0]);assert.deepEqual(Object.keys(keyboardRequest).sort(),['height','mode','width','x','y']);
  await page.locator('[data-action="accounts"]').click();assert.equal(await page.locator('.steam-keyboard-hint').count(),0);
  pass('Steam keyboard failure stays inline, sends only geometry, and does not trap focus or block typing');

  await open('',`window.keyboardRequests=[];launcherBridge.showSteamKeyboard=async value=>{window.keyboardRequests.push(value);return {status:'requested'};};`);
  await page.locator('#login-name').click();await page.waitForFunction(()=>window.keyboardRequests.length===1);assert.equal(await page.locator('.steam-keyboard-hint').count(),0);
  await tab(2);await page.waitForSelector('#register-steam');assert.match(await page.locator('nav [data-page="2"]').textContent(),/Steam and Proton/);
  await page.locator('#register-steam').check();await page.waitForFunction(()=>!document.querySelector('#register-steam').disabled);assert.equal(await page.evaluate(async()=>(await launcherBridge.readCompatibility()).result.registered),true);
  for(const [language,label] of [['简体中文','Steam 和 Proton 设置'],['繁體中文','Steam 與 Proton 設定'],['日本語','Steam と Proton の設定'],['English','Steam and Proton settings']]){
    await tab(3);await page.locator('#ui-language').selectOption({label:language});await page.waitForFunction(()=>!document.querySelector('#ui-language').disabled);await tab(2);
    assert.equal((await page.locator('nav [data-page="2"]').textContent()).trim(),label);
  }
  pass('Steam integration is immediate and the renamed tab is translated in all four languages');

  await app.evaluate(async({BrowserWindow},root)=>{
    const require=globalThis.testRequire;
    const {createSteamIntegration}=require(root+'/frontend/steam-integration.cjs');
    const {observePlacement}=require(root+'/frontend/window-state.cjs');
    const assert=require('node:assert/strict'),window=BrowserWindow.getAllWindows()[0];
    const original=window.getNormalBounds(),saved=[];let integration;
    const placement=observePlacement(window,value=>{saved.push(value);return Promise.resolve();},()=>!integration?.active);
    integration=createSteamIntegration({window,placement,fromSteam:true});
    const entered=new Promise(resolve=>window.once('enter-full-screen',resolve));integration.update(true);await entered;
    assert.equal(window.isFullScreen(),true);await placement.flush();assert.equal(saved.length,1);assert.equal(saved[0].width,original.width);
    const left=new Promise(resolve=>window.once('leave-full-screen',resolve));integration.update(false);await left;
    assert.equal(window.isFullScreen(),false);assert.deepEqual(window.getNormalBounds(),original);await placement.flush();assert.equal(saved.length,1);placement.stop();
  },root);
  pass('native Electron fullscreen restores exact desktop bounds without writing fullscreen geometry');

  // Native window dimensions drive zoom; inspect all page layouts at each available size.
  const originalPage=page,newPage=app.waitForEvent('window');
  await app.evaluate(({BrowserWindow},root)=>{globalThis.sizeTestWindow=new BrowserWindow({show:false,webPreferences:{offscreen:true,contextIsolation:true,sandbox:true}});globalThis.testRequire(root+'/frontend/window-ui.cjs').observeContentSize(globalThis.sizeTestWindow);globalThis.sizeTestWindow.loadURL('about:blank');},root);
  page=await newPage;
  for(const [width,height] of [[1280,800],[1920,1080],[2560,1440],[3840,2160]]){
    const actual=await app.evaluate(({BrowserWindow},size)=>{const w=globalThis.sizeTestWindow;w.setContentSize(...size);return w.getContentSize();},[width,height]);
    await open();
    await page.waitForFunction(()=>document.fonts.status==='loaded');
    const zoom=await app.evaluate(({BrowserWindow})=>globalThis.sizeTestWindow.webContents.getZoomFactor());
    assert.ok(Math.abs(zoom-Math.max(1,Math.min(actual[0]/1320,actual[1]/860)))<.002);
    await page.waitForFunction(([size,z])=>Math.abs(innerWidth-size[0]/z)<2&&Math.abs(innerHeight-size[1]/z)<2,[actual,zoom]);
    const metrics=await page.evaluate(()=>({width:innerWidth,height:innerHeight,ratio:devicePixelRatio}));
    for(const n of [0,1,2,3,4,5]){await tab(n);assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1),true,`horizontal overflow: ${actual} page ${n}`);}
    if(process.env.UI_TEST_ARTIFACTS){await tab(0);const png=await app.evaluate(async()=> (await globalThis.sizeTestWindow.webContents.capturePage()).toPNG().toString('base64'));await fs.writeFile(path.join(path.resolve(process.env.UI_TEST_ARTIFACTS),`home-${width}x${height}.png`),Buffer.from(png,'base64'));}
    console.log(JSON.stringify({requested:[width,height],actual,zoom,viewport:metrics}));
  }
  await app.evaluate(()=>globalThis.sizeTestWindow.destroy());page=originalPage;
  pass('native content scaling and six page widths at Deck, Full HD, QHD and 4K sizes');
  // Send actual Chromium key events, using a harmless counter instead of closing the harness.
  await app.evaluate(({BrowserWindow},root)=>{globalThis.exitRequests=0;globalThis.testRequire(root+'/frontend/window-ui.cjs').bindExitKeys(BrowserWindow.getAllWindows()[0].webContents,()=>globalThis.exitRequests++);},root);
  await page.keyboard.press('Escape');assert.equal(await app.evaluate(()=>globalThis.exitRequests),0);
  await app.evaluate(async({BrowserWindow})=>{const w=BrowserWindow.getAllWindows()[0];w.focus();w.webContents.sendInputEvent({type:'keyDown',keyCode:'Q',modifiers:['control']});w.webContents.sendInputEvent({type:'keyUp',keyCode:'Q',modifiers:['control']});await new Promise(resolve=>setTimeout(resolve,100));});assert.equal(await app.evaluate(()=>globalThis.exitRequests),1);
  pass('actual Electron keyboard input reaches the exit handler while Escape stays separate');

  console.log('All Electron UI regressions passed.');
 }finally{if(app)await app.close();if(runtime)await runtime.cleanup();if(server)await new Promise(resolve=>server.close(resolve));await fs.rm(temp,{recursive:true,force:true});}
})().catch(error=>{console.error(error);process.exitCode=1;});
