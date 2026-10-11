'use strict';
// Optional adapter: the same templates remain a static mock when opened in an ordinary browser.
window.frontendConnection = (() => {
  const bridge=window.launcherBridge;
  const integration=Boolean(bridge)||new URL(location.href).searchParams.get('integration')==='1';
  const {t,apply:translate}=window.previewI18n;
  const $=selector=>document.querySelector(selector);
  const languageIds=window.previewI18n.languages;
  let snapshot=null, applying=false, failed=false, accountList=null, areas=[],areaLoading=false;
  const pendingReports=[];
  let receive=()=>{}, report=message=>pendingReports.push(message), generation=0, operation=null, pollTimer=null, view=null, replying=false;
  let login={name:'',autoOtp:false,steamAccount:false,freeTrial:false,enableDalamud:false};
  let previousLogs=[],localLogs=[],logRevision=0;
  const logView=window.createLogView($('#log'));
  let pollBusy=false,lastViewKey='',pendingRedraw=false,pendingPresent=false;
  let operationKind='game';
  const operationTraits=Object.freeze({
    game:{verification:true,accounts:true,home:true},
    recovery:{verification:true,accounts:true,home:true},
    travel:{verification:true,accounts:true},
    'launcher-update':{about:true},cleanup:{about:true},plugins:{},compatibility:{}
  });
  const traits=(kind=operationKind)=>operationTraits[kind]||{};
  const editors=new Map(), selections=new Map(); let editor=null;
  const editorKey=(r,name,existing)=>r+'\n'+(existing?'saved:':'new:')+name;
  const active=()=>Boolean(operation);
  const region=()=>snapshot?.selectedRegion;
  const ready=()=>Boolean(snapshot)&&!applying&&!failed;
  const featureFailures=new Set();
  function feature(name,create){
    if(!integration)return null;
    const failed=()=>{if(!featureFailures.has(name)){featureFailures.add(name);report('Frontend feature failed: '+name+'.');}};
    try{const value=create();if(!value)return null;
      return Object.fromEntries(Object.entries(value).map(([key,fn])=>[key,(...args)=>{
        try{const result=fn(...args);return result?.then?result.catch(failed):result;}catch{failed();}
      }]));
    }catch{failed();return null;}
  }
  const plugins=feature('plugins',()=>window.createPluginPage?.({call,ready:()=>ready()&&!active(),region,start:request=>startManagedOperation('startPluginOperation',request,'plugins'),cancel:cancelOperation,
    operation:()=>operationKind==='plugins'&&active()?view:null,report:(message,options)=>report(message,options)}));
  const compatibility=feature('compatibility',()=>window.createCompatibilityPage?.({call,ready:()=>ready()&&!active(),start:request=>startManagedOperation('diagnoseCompatibility',request,'compatibility'),cancel:cancelOperation,
    operation:()=>operationKind==='compatibility'&&active()?view:null,chooseDirectory:()=>bridge.chooseClientDirectory(),chooseProton:()=>bridge.chooseProtonScript(),report:(message,options)=>report(message,options)}));
  const travel=feature('travel',()=>window.createTravelPage?.({call,ready:()=>ready()&&!active(),start:request=>startManagedOperation('startTravel',request,'travel'),cancel:cancelOperation,
    operation:()=>operationKind==='travel'&&active()?view:null,report:message=>report(message)}));
  const about=feature('about',()=>window.createAboutPage?.({call,apply,ready:()=>ready()&&!active(),snapshot:()=>snapshot,clean:request=>startManagedOperation('cleanLauncher',request,'cleanup'),start:request=>startManagedOperation('installLauncherUpdate',request,'launcher-update'),cancel:cancelOperation,
    operation:()=>traits().about&&active()?view:null,report:(message,options)=>report(message,options)}));
  const setup=feature('setup',()=>window.createSetupWizard?.({call,snapshot:()=>snapshot,apply,chooseDirectory:()=>bridge.chooseClientDirectory(),chooseProton:()=>bridge.chooseProtonScript(),report:message=>report(message),
    completed:async result=>{snapshot=result;redraw();navigate('0');try{await reloadAccounts(true);}catch(error){fail(error);}}}));
  const official=feature('official',()=>window.createOfficialContentPage?.({call,ready,report:message=>report(t(message),{expand:false})}));
  const currentPage=()=>document.querySelector('nav [aria-current="page"]')?.dataset.page;
  const account=name=>accountList?.accounts.find(item=>item.name===name);
  const feedback=message=>{let node=$('#backend-feedback');if(!node){node=document.createElement('p');node.id='backend-feedback';node.className='inline-error';node.setAttribute('role','status');const host=$('#page .settings-panel')||$('#verification');if(host){host.hidden=false;host.append(node);}}if(node)node.textContent=t(message);};
  function options(select,items,value) {if(!select)return;select.replaceChildren(...items.map(([id,label])=>new Option(label,id)));select.value=value;}
  async function call(name,value) {
    const response=await bridge[name](value);
    if(!response?.ok){
      const uncertain=['saveAccount','savePluginRepositories','applyPreferences','applyCompatibility','completeSetup','installDesktop'].includes(name)&&['backend-timeout','backend-exited','backend-unavailable'].includes(response?.code);
      const error=new Error(uncertain?t('写入结果未确认，请重新启动启动器后核对设置。'):response?.code==='read-timeout'?t('读取超时，请刷新后重试。'):response?.message||t('后端连接失败，界面仍可浏览。'));
      error.code=response?.code;error.logMessage=uncertain?'The write result could not be confirmed. Restart the launcher and check the settings.':response?.logMessage||(/^[\x00-\x7F]*$/.test(error.message)?error.message:undefined);markUnavailable(error);throw error;
    }
    const activity={
      saveAccount:value?.delete?'Saved account removed.':'Account credentials saved.',
      savePluginRepositories:'Repository settings saved.',
      applyCompatibility:'Compatibility settings saved.',
      completeSetup:'Initial setup completed.',
      installDesktop:'Application menu entry installed.',
      closeTravel:'Travel session closed.'
    }[name];
    if(activity)report(activity,{error:false});
    if(name==='applyPreferences'){
      if(value.autoCheckUpdates!==undefined)report(value.autoCheckUpdates?'Automatic update checks enabled.':'Automatic update checks disabled.',{error:false});
      if(Object.keys(value).some(key=>key!=='autoCheckUpdates'))report('Launcher preferences saved.',{error:false});
    }
    return response.result;
  }
  function markUnavailable(error){if(['backend-unavailable','backend-exited','backend-timeout','invalid-response'].includes(error.code)||!bridge)failed=true;}
  function fail(error) {
    markUnavailable(error);
    report(error.logMessage||'The backend request failed.');feedback(error.message||'后端连接失败，界面仍可浏览。');present();
  }
  function selectLogin(name) {
    login.name=name;const saved=account(name);
    login.areaName=saved?.areaName||'';
    login.autoOtp=Boolean(saved?.hasOtp);login.steamAccount=Boolean(saved?.steamAccount);login.freeTrial=Boolean(saved?.freeTrial);
  }
  function present() {
    if(!integration)return;
    if(document.hidden){pendingPresent=true;return;}pendingPresent=false;
    document.body.dataset.backend='connected';document.title='FFXIV Super Launcher';
    const editable=ready()&&!active();
    const actions=new Set(['region-settings','clear-log','local-otp']);
    if(ready())for(const name of ['open-official','status','refresh-official'])actions.add(name);
    if(editable)for(const name of ['recover-game-auth','browse-client','accounts','login','travel','download','add-account','delete-account','save-account','discard-account'])actions.add(name);
    if(active()&&view?.state==='running')actions.add('cancel-operation');
    if(active()&&view?.state==='running'&&!replying)for(const name of ['submit-otp','skip-otp','dalamud-disable'])actions.add(name);
    if(active()&&view?.state==='running'&&!replying&&view?.challenge?.allowLocal)actions.add('dalamud-local');
    if(editor?.loading||!editor?.existing)actions.delete('delete-account');
    if(!editor||editor.loading||editor.readFailed)actions.delete('save-account');
    if(!accountList||snapshot?.setupComplete===false){actions.delete('login');actions.delete('download');actions.delete('travel');}
    for(const button of document.querySelectorAll('[data-demo],[data-action]')) {
      if(button.closest('#account-dialog'))continue;
      button.disabled=!actions.has(button.dataset.action);
      if(button.disabled)button.title=t(active()?'正在处理，请稍候…':'当前操作不可用。');else button.removeAttribute('title');
    }
    const fields=new Set(['ui-language','region','client-language','cdkey','eu-language','login-name','auto-otp','steam-account','free-trial','enable-dalamud','no-plugins','china-area','credential-account','credential-name','credential-password','credential-secret']);
    for(const field of document.querySelectorAll('#page input,#page select')) {
      if(field.closest('[data-compatibility-ui],[data-about-ui]'))continue;
      field.disabled=!(editable&&(fields.has(field.id)||field.dataset.clientPath));
      if(field.id==='otp')field.disabled=!active()||replying||view?.state!=='running';
      if(field.id.startsWith('credential-')&&field.id!=='credential-account')field.disabled ||= !editor||editor.loading||editor.readFailed;
    }
    for(const element of document.querySelectorAll('.integration-only'))element.hidden=false;

    for(const item of snapshot?.regions??[]) {
      const field=document.querySelector(`[data-client-path="${item.id}"]`);if(field){field.disabled ||= item.available===false;if(document.activeElement!==field)field.value=item.gamePath;}
      const version=document.querySelector(`[data-client-version="${item.id}"]`);if(version)version.textContent=t('客户端版本：{version}',{version:item.available===false?t('操作失败，请查看日志。'):item.clientVersion||t('未安装或未读取')});
    }
    if($('#login-name')&&document.activeElement!==$('#login-name'))$('#login-name').value=login.name;
    for(const [id,key] of [['auto-otp','autoOtp'],['steam-account','steamAccount'],['free-trial','freeTrial'],['enable-dalamud','enableDalamud'],['no-plugins','noPlugins']])if($('#'+id))$('#'+id).checked=login[key];
    if($('#china-area'))options($('#china-area'),areas.map(x=>[x.name,x.name]),login.areaName||'');
    const status=$('.log-toolbar > span');if(status)status.textContent=t(failed?'后端连接失败，界面仍可浏览。':'操作日志');
    const hint=$('.setting-hint');if(hint)hint.hidden=true;
    const demos=$('.preview-tools');if(demos)demos.hidden=true;
    if($('#credential-name'))renderEditor();
    if($('#verification')&&view&&(traits().verification))renderOperation();
    const count=logView.lines().length;if($('#log-count'))$('#log-count').textContent=t(count===1?'1 条':'{count} 条',{count});
    official?.sync(snapshot);official?.present();plugins?.sync();compatibility?.sync();travel?.present();about?.sync();
  }
  function redraw() {if(document.hidden){pendingRedraw=true;return;}pendingRedraw=false;if(snapshot)receive(snapshot);else present();}
  async function reloadAccounts(reset=false) {
    const selected=region();const data=await call('listAccounts',{region:selected});
    if(region()!==selected)return;
    accountList=data;
    if(reset)selectLogin(data.selectedAccount||'');
    login.enableDalamud=data.enableDalamud;
    present();
  }
  async function loadAreas() {
    if(region()!=='ffxiv_cn'||active()||areaLoading)return;
    areaLoading=true;
    const selected=region();
    try {const values=await call('listAreas',{region:selected});if(region()===selected){areas=values;present();}}
    catch(error){if(region()===selected){present();report(error.message);}}
    finally{areaLoading=false;}
  }
  async function apply(change,rethrow=false) {
    if(!ready()||active())return;
    applying=true;present();
    try {
      const previous=region();snapshot=await call('applyPreferences',change);
      if(previous!==region()) {
        travel?.reset();generation++;view=null;editor=editors.get(selections.get(region()))||null;accountList=null;areas=[];
        login={name:'',autoOtp:false,steamAccount:false,freeTrial:false,enableDalamud:false,areaName:snapshot.regions.find(x=>x.id===region())?.areaName||''};
        await reloadAccounts(true);
      }
      redraw();
    }catch(error){redraw();if(rethrow)throw error;fail(error);}
    finally {applying=false;present();if(region()==='ffxiv_cn'&&areas.length===0)void loadAreas();}
  }
  function renderEditor() {
    const items=(accountList?.accounts??[]).map(item=>[item.name,item.name]);
    if(!editor?.existing)items.push(['',t('新账号')]);
    options($('#credential-account'),items,editor?.existing?editor.name:'');
    if(!editor){for(const id of ['credential-name','credential-password','credential-secret'])if($('#'+id))$('#'+id).value='';return;}
    $('#credential-name').value=editor.name;$('#credential-name').readOnly=editor.existing;
    if($('#credential-password')){$('#credential-password').value=editor.password;$('#credential-password').placeholder='';}
    if($('#credential-secret')){$('#credential-secret').value=editor.secret;$('#credential-secret').placeholder=t('可选的 Base32 密钥；清空后保存即删除');}
    if(editor.delete)feedback('已标记删除；保存后删除账号及关联凭据。');
  }
  async function openEditor(name,discard=false,rethrow=false) {
    const selected=region(),revision=++generation;
    const key=editorKey(selected,name,Boolean(account(name)));
    if(!discard&&editors.has(key)&&!editors.get(key).loading&&!editors.get(key).readFailed){editor=editors.get(key);selections.set(selected,key);present();return;}
    editor={name:name||'',existing:Boolean(account(name)),password:'',secret:'',dirty:false,loading:false,region:selected,key};
    editors.set(key,editor);selections.set(selected,key);
    if(!editor.existing){present();return;}
    editor.loading=true;present();
    try {
      const data=await call('readAccount',{region:selected,name});
      if(revision!==generation||region()!==selected)return;
      Object.assign(editor,data,{loading:false});present();
    }catch(error){if(revision===generation){editor.loading=false;editor.readFailed=true;present();if(rethrow)throw error;fail(error);}}
  }
  async function enterEditor(name) {
    if(!accountList)await reloadAccounts();
    if(name!==undefined||!editor||editor.region!==region()||editor.loading||editor.readFailed)await openEditor(name??editor?.name??(accountList.selectedAccount||accountList.accounts[0]?.name||''));
    present();
  }
  function navigate(page) {document.querySelector(`nav [data-page="${page}"]`).click();}
  async function guide(name) {navigate('1');await enterEditor(name);feedback('这是首次登录此账户，请填写并保存凭据，然后手动回到游戏页选择自己的登录名并登录。');}
  async function saveEditor() {
    if(!editor||editor.loading||editor.readFailed)return;
    const draft=editor;applying=true;present();
    try {
      await call('saveAccount',{region:region(),name:draft.name,existing:draft.existing,password:draft.password,secret:draft.secret,delete:Boolean(draft.delete),...(draft.revision?{revision:draft.revision}:{})});
      draft.password='';draft.secret='';editors.delete(draft.key);editor=null;
      try {
        await reloadAccounts();await openEditor(draft.delete?'':draft.name,false,true);
        feedback('修改已保存。');
      }catch(error){accountList=null;feedback('修改已保存，但刷新失败，请重新载入。');report('Credentials saved, but refreshing the account view failed.');}
      selectLogin(login.name); // A newly added/removed secret changes the default for the selected login.
    }catch(error){fail(error);}
    finally{applying=false;present();}
  }
  function chooseAccount() {
    const dialog=$('#account-dialog'),list=dialog.querySelector('.account-options');list.replaceChildren();
    dialog.querySelector('.muted').hidden=true;
    for(const item of accountList?.accounts??[]){const button=document.createElement('button');button.className='account-option';button.dataset.noTranslate='';button.textContent=item.name;button.addEventListener('click',()=>{selectLogin(item.name);dialog.close();present();});list.append(button);}
    dialog.showModal();
  }
  async function startOperation(kind) {
    if(!ready()||active())return;
    const needsLogin=kind==='launch'||region()==='ffxiv';
    if($('#login-name'))login.name=$('#login-name').value.trim();
    if(needsLogin&&region()!=='ffxiv_cn'&&(!account(login.name)||!account(login.name).hasPassword)){await guide(login.name);return;}
    const request={region:region(),kind,account:login.name,areaName:$('#china-area')?.value||login.areaName||'',autoOtp:login.autoOtp,steamAccount:login.steamAccount,freeTrial:login.freeTrial,enableDalamud:login.enableDalamud,noPlugins:Boolean(login.noPlugins)};
    applying=true;present();
    try {
      await beginOperation('startOperation',request,'game');
    }catch(error){fail(error);}
    finally{applying=false;present();}
  }
  async function startManagedOperation(method,request,kind) {
    if(!ready()||active())return;
    applying=true;present();
    try{await beginOperation(method,request,kind);}
    finally{applying=false;present();}
  }
  async function beginOperation(method,request,kind) {
    const result=await call(method,request);
    previousLogs=logView.lines().slice(-500);localLogs=[];lastViewKey='';
    operation=result.operationId;operationKind=kind;
    view={id:operation,region:request.region,state:'running',challenge:null,log:''};
    if(traits(kind).home||kind==='travel'&&request.kind==='open')navigate('0');else present();
    await poll();
  }
  async function poll() {
    const id=operation;if(!id||pollBusy)return;clearTimeout(pollTimer);pollBusy=true;
    const revision=logRevision;
    try {
      const result=await call('pollOperation',{operationId:id});if(operation!==id)return;
      if(result.challenge?.id!==view?.challenge?.id||result.state!=='running')replying=false;
      const key=JSON.stringify(result),changed=key!==lastViewKey;lastViewKey=key;
      view=result;void syncCaptchaLayout();
      if(changed&&revision===logRevision){
        const lines=[...previousLogs,...result.log.split('\n').filter(Boolean),...localLogs].slice(-1000);logView.update(lines);
        const count=t(lines.length===1?'1 条':'{count} 条',{count:lines.length});if($('#log-count').textContent!==count)$('#log-count').textContent=count;
      }
      if(result.state==='running'||result.state==='cancelling'){
        if(changed){if(document.hidden)pendingPresent=true;else if(traits().verification)renderOperation();else present();}
      }
      else {
        operation=null;
        if(result.state==='completed'&&result.result?.exitForUpdate){applying=true;present();await bridge.quitForUpdate(id);return;}
        if(operationKind==='cleanup'&&result.state==='completed'&&result.result?.kind==='logs'){logRevision++;previousLogs=[];localLogs=[];logView.clear();lastViewKey='';}
        if(traits().about)about?.completed(result);
        const travelLaunch=operationKind==='travel'&&travel?.completed(result);if(operationKind==='plugins')await plugins?.completed(result);if(operationKind==='compatibility')compatibility?.completed(result);present();if((traits().verification))renderOperation();
        // Window management and account refresh must not turn a successful game launch into a failure.
        if(result.launched&&result.state==='completed')try{if((await bridge.finishLaunch(id))?.released)return;}catch{report(t('界面退出准备失败；游戏启动结果不受影响。'));}
        try{snapshot=await call('snapshot');redraw();if(traits().accounts)await reloadAccounts(result.state==='completed');}catch(error){fail(error);}
        if(travelLaunch)setTimeout(()=>void startOperation('launch'),0);
      }
    }catch(error){operation=null;view={...view,state:'failed',error:error.message,challenge:null};void syncCaptchaLayout();fail(error);if(!document.hidden&&(traits().verification))renderOperation();}
    finally{pollBusy=false;if(operation===id)pollTimer=setTimeout(()=>void poll(),document.hidden?2000:350);}
  }
  let captchaLayoutBusy=false,captchaLayoutPending=false,captchaLayoutKey='null';
  async function syncCaptchaLayout(){
    if(!bridge?.captchaLayout)return;
    const live=operation&&view?.state==='running'&&view.webVerificationId;
    // Cancellation must interrupt initialization rather than queue behind a CDP wait.
    if(!live){if(captchaLayoutKey!=='null'){captchaLayoutKey='null';try{await bridge.captchaLayout(null);}catch{report(t('网页验证显示失败，请取消后重试。'));}}return;}
    if(captchaLayoutBusy){captchaLayoutPending=true;return;}
    const slot=$('#captcha-slot');let bounds=null;
    if(live&&!document.hidden&&slot&&currentPage()==='0'&&!document.querySelector('dialog[open],.logs[open],select:open,[popover]:popover-open')){
      const r=slot.getBoundingClientRect();if(r.width>0&&r.height>0)bounds={x:Math.max(0,r.x),y:Math.max(0,r.y),width:r.width,height:r.height};
    }
    const request=live?{operationId:operation,bounds}:null;
    const key=live?JSON.stringify([view.webVerificationId,view.challenge?.kind,request]):'null';
    if(key===captchaLayoutKey)return;captchaLayoutBusy=true;captchaLayoutKey=key;
    try{await bridge.captchaLayout(request);}catch{report(t('网页验证显示失败，请取消后重试。'));}
    finally{captchaLayoutBusy=false;if(captchaLayoutPending){captchaLayoutPending=false;void syncCaptchaLayout();}}
  }
  function renderOperation() {
    const panel=$('#verification');if(!panel||!view)return;
    const challenge=view.challenge,key=challenge?.id||view.state;
    if(panel.dataset.challenge===key)return;
    panel.dataset.challenge=key;panel.hidden=false;panel.replaceChildren();
    const heading=document.createElement('div');heading.className='section-heading';
    const title=document.createElement('h2');title.id='verification-title';title.textContent=t('登录验证');heading.append(title);panel.append(heading);
    const body=document.createElement('div');body.className='verification-body';panel.append(body);
    const text=document.createElement('p');text.className='muted';text.textContent=t(challenge?.message||'')||t(view.state==='running'?'正在处理，请稍候…':view.state==='cancelling'?'正在取消…':view.state==='completed'?(operationKind==='recovery'?'跨区授权已恢复，请回到游戏重新点击跨区。':'操作已完成。'):view.state==='cancelled'?'已取消。':view.error||'操作失败，请查看日志。');body.append(text);
    const buttons=document.createElement('div');buttons.className='row';
    const add=(action,label,disabled=false)=>{const button=document.createElement('button');button.dataset.action=action;button.textContent=t(label);button.disabled=disabled;buttons.append(button);};
    if(challenge?.kind==='qr'&&challenge.image?.startsWith('data:image/png;base64,')){const image=document.createElement('img');image.src=challenge.image;image.alt=t('请使用叨鱼扫描二维码登录。');image.className='backend-qr';body.append(image);}
    if(challenge?.kind==='mobile-confirmation'&&challenge.code){
      const line=document.createElement('div');line.className='mobile-code';
      const label=document.createElement('span');label.textContent=t('确认码');
      const code=document.createElement('output');code.textContent=challenge.code;code.setAttribute('data-no-translate','');
      line.append(label,code);body.append(line);
    }
    if(challenge?.kind==='web-captcha'){
      body.classList.add('web-verification');const slot=document.createElement('div');slot.id='captcha-slot';slot.setAttribute('aria-label',t('繁中区登录验证'));body.append(slot);
      requestAnimationFrame(()=>void syncCaptchaLayout());
    }
    if(challenge?.kind==='otp'){
      const input=document.createElement('input');input.id='otp';input.maxLength=6;input.inputMode='numeric';input.autocomplete='off';input.type='password';input.setAttribute('aria-label',t('验证码'));body.append(input);
      add('submit-otp','确认并登录');if(challenge.optional)add('skip-otp','账号未启用 2FA，继续');
    }
    if(challenge?.kind==='dalamud-timeout'){
      add('dalamud-local','跳过更新，使用本地 Dalamud',!challenge.allowLocal);add('dalamud-disable','禁用 Dalamud 并继续启动');
      const note=document.createElement('p');note.textContent=t('选择禁用后将保存设置；以后需要自行重新勾选“启用 Dalamud”。');note.className='muted';body.append(note);
    }
    if(active())add('cancel-operation','取消登录',view.state==='cancelling');
    body.append(buttons);translate(panel);void syncCaptchaLayout();
  }
  async function reply(value) {
    if(!operation||!view?.challenge||replying||view.state!=='running')return;
    if(view.challenge.kind==='otp'&&value!==''&&!/^\d{6}$/.test(value)){feedback('请输入六位数字。');return;}
    replying=true;present();
    try {await call('replyOperation',{operationId:operation,challengeId:view.challenge.id,value});if($('#otp'))$('#otp').value='';}
    catch(error){replying=false;fail(error);}
  }
  async function cancelOperation() {
    if(!operation||view?.state!=='running')return;
    view={...view,state:'cancelling',challenge:null};replying=false;void syncCaptchaLayout();present();
    try{await call('cancelOperation',{operationId:operation});}catch(error){fail(error);}
  }
  if(integration) {
    document.addEventListener('visibilitychange',()=>{
      void syncCaptchaLayout();
      if(!document.hidden){if(pendingRedraw)redraw();else if(pendingPresent)present();if(operation)void poll();}
      else if(operation&&!pollBusy){clearTimeout(pollTimer);pollTimer=setTimeout(()=>void poll(),2000);}
    });
    if(bridge?.captchaLayout){window.addEventListener('resize',()=>void syncCaptchaLayout());document.addEventListener('scroll',()=>void syncCaptchaLayout(),true);document.addEventListener('toggle',()=>void syncCaptchaLayout(),true);}
    document.addEventListener('click',event=>{
      if(event.target.closest('[data-plugin-ui],[data-compatibility-ui],[data-travel-ui],[data-about-ui],[data-setup-ui],[data-otp-ui]'))return;
      const button=event.target.closest('button');if(!button)return;
      if(button.dataset.page!==undefined){setTimeout(()=>{if(currentPage()==='1'&&!active())void enterEditor().catch(fail);if(currentPage()==='0'&&areas.length===0)void loadAreas();},0);return;}
      if(button.closest('#account-dialog'))return;
      if(button.dataset.action==='region-settings'||button.dataset.action==='clear-log'&&!view)return;
      event.stopImmediatePropagation();if(button.disabled)return;
      void (async()=>{
        switch(button.dataset.action){
          case 'open-official':case 'status':await official?.open(button.dataset.officialKey,button.dataset.officialItem);break;
          case 'refresh-official':await official?.refresh();break;
          case 'clear-log':logRevision++;previousLogs=[];localLogs=[];logView.clear();$('#log-count').textContent=t('{count} 条',{count:0});if(view)await call('clearOperationLog',{operationId:view.id});lastViewKey='';break;
          case 'browse-client':{const path=await bridge.chooseClientDirectory();if(path)await apply({clientPaths:{[button.dataset.region]:path}});break;}
          case 'accounts':await reloadAccounts();chooseAccount();break;
          case 'add-account':await openEditor('');break;
          case 'discard-account':await openEditor(editor?.existing?editor.name:'',true);feedback('已放弃修改。');break;
          case 'delete-account':editor.delete=!editor.delete;editor.dirty=true;present();break;
          case 'save-account':await saveEditor();break;
          case 'local-otp':window.openLocalOtp(editor?.secret||'');break;
          case 'travel':await startManagedOperation('startTravel',{kind:'open',account:$('#login-name')?.value.trim()||login.name},'travel');break;
          case 'recover-game-auth':await recoverGameAuthentication(true);break;
          case 'login':await startOperation('launch');break;
          case 'download':await startOperation('game-update');break;
          case 'submit-otp':if(!/^\d{6}$/.test($('#otp')?.value||'')){feedback('请输入六位数字。');break;}await reply($('#otp').value);break;
          case 'skip-otp':await reply('');break;
          case 'dalamud-local':await reply('local');break;
          case 'dalamud-disable':await reply('disable');break;
          case 'cancel-operation':await cancelOperation();break;
        }
      })().catch(fail);
    },true);
    document.addEventListener('change',event=>{
      if(event.target.closest('[data-plugin-ui],[data-compatibility-ui],[data-travel-ui],[data-about-ui],[data-setup-ui],[data-otp-ui]'))return;
      const field=event.target;
      event.stopImmediatePropagation();
      void (async()=>{
        if(field.id==='ui-language')await apply({language:languageIds[field.value]});
        else if(field.id==='client-language')await apply({clientLanguage:['日本語','English','Deutsch','Français'].indexOf(field.value)});
        else if(field.id==='region')await apply({region:['ffxiv_cn','ffxiv_tc','ffxiv'][Number(field.value)]});
        else if(field.dataset.clientPath)await apply({clientPaths:{[field.dataset.clientPath]:field.value.trim()}});
        else if(field.id==='cdkey')await apply({cdKeyVersion:field.value});
        else if(field.id==='eu-language')await apply({euShopLanguage:{'English(UK)':'en-gb','Français':'fr-fr','Deutsch':'de-de'}[field.value]});
        else if(field.id==='credential-account')await openEditor(field.value);
        else if(field.id==='china-area')login.areaName=field.value;
        else if(field.id==='login-name'){selectLogin(field.value.trim());present();if(login.name&&region()!=='ffxiv_cn'&&accountList&&!account(login.name))await guide(login.name);}
        else {const key=({'auto-otp':'autoOtp','steam-account':'steamAccount','free-trial':'freeTrial','enable-dalamud':'enableDalamud','no-plugins':'noPlugins'})[field.id];if(key)login[key]=field.checked;}
      })().catch(fail);
    },true);
    document.addEventListener('input',event=>{
      const key=({'credential-name':'name','credential-password':'password','credential-secret':'secret'})[event.target.id];
      if(key&&editor&&!editor.loading){editor[key]=event.target.value;editor.dirty=true;}
    },true);
    document.addEventListener('keydown',event=>{
      if(event.target.closest('[data-otp-ui],[data-about-ui],[data-setup-ui],[data-travel-ui],[data-plugin-ui]'))return;
      if(event.target.id==='otp'&&event.key==='Enter'){event.stopImmediatePropagation();event.preventDefault();if(/^\d{6}$/.test(event.target.value))void reply(event.target.value).catch(fail);else feedback('请输入六位数字。');}
      if(active()&&event.key==='Escape'){event.stopImmediatePropagation();void cancelOperation().catch(fail);}
    },true);
  }
  async function recoverGameAuthentication(manual) {
    if(!bridge?.checkGameRecovery||active())return;
    if((await call('checkGameRecovery')).required)await startManagedOperation('startGameRecovery',{},'recovery');
    else if(manual)feedback('当前没有需要恢复授权的游戏会话。');
  }
  async function start(onSnapshot,onError) {
    if(!integration)return;
    receive=onSnapshot;report=(message,options)=>{
      const line=onError(message,options);if(operation&&typeof line==='string'){localLogs.push(line);localLogs=localLogs.slice(-100);}
    };for(const message of pendingReports.splice(0))report(message);present();
    try {
      snapshot=await call('snapshot');if(snapshot.protocolVersion!==5){snapshot=null;throw new Error('Backend protocol version mismatch.');}
      login.areaName=snapshot.regions.find(x=>x.id===region())?.areaName||'';
      redraw();try{await reloadAccounts(true);}catch(error){fail(error);}if(region()==='ffxiv_cn')void loadAreas();setup?.start();about?.start();
      await recoverGameAuthentication(false);
    }catch(error){if(!snapshot)failed=true;fail(error);}
    present();
  }
  return Object.freeze({present,start});
})();
