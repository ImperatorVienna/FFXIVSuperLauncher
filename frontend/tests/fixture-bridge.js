'use strict';
// Synthetic browser fixture only, loaded by create-preview-fixture.cjs. No disk/network/game access.
(() => {
  const ok=result=>Promise.resolve({ok:true,result:structuredClone(result)});
  const snapshot={protocolVersion:5,autoCheckUpdates:true,setupComplete:new URL(location.href).searchParams.get("setup")!=="new",language:'en',selectedRegion:'ffxiv',protonScript:'/fixture/proton',regions:['ffxiv_cn','ffxiv_tc','ffxiv'].map(id=>({id,gamePath:'/fixture/'+id,clientVersion:'test',dalamudVersion:'test',areaName:'陆行鸟',cdKeyVersion:'NA',euShopLanguage:'en-gb',clientLanguage:1}))};
  const records={ffxiv_cn:{ChinaFixture:{password:'',secret:''}},ffxiv_tc:{TaiwanFixture:{password:'fixture-password',secret:''}},ffxiv:{FixtureAccount:{password:'fixture-password',secret:'JBSWY3DPEHPK3PXP'}}};
  const list=region=>({region,selectedAccount:Object.keys(records[region])[0]||'',enableDalamud:false,accounts:Object.entries(records[region]).map(([name,r])=>({name,hasPassword:!!r.password,hasOtp:!!r.secret,steamAccount:false,freeTrial:false}))});
  const repositories=Object.fromEntries(['ffxiv_cn','ffxiv_tc','ffxiv'].map(region=>[region,{revision:'fixture-repositories',main:'https://main.invalid/catalog.json',presetsEnabled:region==='ffxiv_cn',presets:region==='ffxiv_cn'?['https://preset.invalid/catalog.json']:[],entries:[]}]));
  let operation,sequence=0,newsRevision=0,newsKey,newsPolls=0;
  function official({refresh=false}={}) {
    const global=snapshot.regions.find(x=>x.id==='ffxiv');
    const key=snapshot.selectedRegion==='ffxiv'?`ffxiv:${global.cdKeyVersion}:${global.euShopLanguage}`:snapshot.selectedRegion;
    if(newsKey!==key||refresh){newsKey=key;newsPolls=0;newsRevision++;}
    const mode=new URL(location.href).searchParams.get('news');
    const state=mode==='failed'?'failed':newsPolls++===0?'loading':'ready';
    return {key,region:snapshot.selectedRegion,state,links:['home','status','shop',snapshot.selectedRegion==='ffxiv'?'mog':'subscription'],
      activities:state==='ready'?Array.from({length:6},(_,i)=>({id:'event-'+newsRevision+'-'+i,title:key+' · Activity fixture '+i,date:'2026-10-08'})):[],
      notices:state==='ready'?Array.from({length:6},(_,i)=>({id:'notice-'+newsRevision+'-'+i,title:key+' · <img src=x onerror=alert(1)> & Notice fixture '+i,date:'2026-10-09'})):[]};
  }
  const pluginLists=Object.fromEntries(Object.keys(records).map(region=>[region,[
    {id:'Sample',name:'Fixture plugin <img src=x>',version:'1.0.0',enabled:true,scheduledForDeletion:false},
    {id:'Conditional',name:'Conditional fixture',version:'1.0.0',enabled:null,scheduledForDeletion:false},
    {id:'Retired',name:'Scheduled deletion fixture',version:'1.0.0',enabled:false,scheduledForDeletion:true}
  ]]));
  function finishPluginOperation() {
    if(!operation?.pluginRequest||operation.state!=='running')return;
    const mode=new URL(location.href).searchParams.get('plugins');
    if(mode==='slow')return; // Remain pending until the visible cancel control is used.
    if(++operation.polls<2)return;
    const value=operation.pluginRequest;
    if(mode==='write-failed'){operation.state='failed';operation.error='Synthetic plugin write failure.';delete operation.pluginRequest;return;}
    if(value.kind==='apply')for(const item of pluginLists[value.region])if(Object.hasOwn(value.states,item.id))item.enabled=value.states[item.id];
    if(value.kind==='check-repository')operation.result={kind:'repository-checked',count:3};
    if(value.kind==='sync')operation.result={kind:'plugins-synced',targets:value.targets,backupDirectory:'/fixture/sync-backups/test'};
    if(value.kind==='check-updates')operation.result={kind:'plugin-updates',plan:{region:value.region,planId:'fixture-plan',updates:[{id:'Sample',name:'Fixture plugin <img src=x>',fromVersion:'1.0.0',toVersion:'2.0.0',repository:'https://fixture.invalid/master.json'}],problems:[]}};
    if(value.kind==='install-updates')for(const item of pluginLists[value.region])if(value.plugins.includes(item.id))item.version='2.0.0';
    operation.state='completed';operation.log+='\n[TEST] Plugin operation completed: '+value.kind+(value.targets?' -> '+value.targets.join(','):'');
    delete operation.pluginRequest;
  }
  const compatibility={protonScript:'/fixture/proton',steamRoot:'/fixture/Steam',dataDirectory:'/fixture/shared-pfx',registered:false,registeredRoot:'',canRegister:true,useSteamPrefix:false,steamPrefix:'',runtimes:[{name:'Synthetic Proton',script:'/fixture/proton',ready:true}],prefixes:[{name:'Global (Steam)',path:'/fixture/Steam/steamapps/compatdata/39210'}]};
  const travel={session:'fixture-travel-session',revision:'fixture-travel-revision',account:'ChinaFixture',source:'1',character:'fixture-role',sources:[{id:'1',name:'Source',groups:[{id:'1:10',name:'Source world'}]},{id:'2',name:'Destination',groups:[{id:'2:20',name:'Destination world'}]}],characters:[{id:'fixture-role',name:'Fixture Character'}],targets:[{id:'2',name:'Destination',groups:[{id:'2:20',name:'Destination world'}]}],orders:[{id:'fixture-order',name:'Fixture Character',from:'Source / Source world',to:'Destination / Destination world',created:'2026-10-09'}],page:1,pages:1,completedArea:null};
  window.launcherBridge={
    snapshot:()=>ok(snapshot),
    applyPreferences:change=>{
      if(change.autoCheckUpdates!==undefined)snapshot.autoCheckUpdates=change.autoCheckUpdates;
      if(change.language)snapshot.language=change.language;
      if(change.region)snapshot.selectedRegion=change.region;
      const global=snapshot.regions.find(x=>x.id==='ffxiv');
      for(const key of ['cdKeyVersion','euShopLanguage','clientLanguage'])if(change[key]!==undefined)global[key]=change[key];
      return ok(snapshot);
    },
    completeSetup:()=>{snapshot.setupComplete=true;return ok(snapshot);},
    readAbout:()=>ok({version:'1.1.0',author:'ImperatorVienna',isAppImage:false,notice:'',diagnosticPath:'/fixture/logs/launcher.log',links:['project','issues','author','XIVLauncher','XIVLauncherCN','XIVLauncherCN (Soil)','XIVTCLauncher','Dalamud (goatcorp)','sources','icon']}),
    openAbout:()=>ok({opened:true}),readNotice:()=>ok({text:'Synthetic GPL notice\nSynthetic third-party notice'}),
    installDesktop:()=>ok({installed:true}),
    readLauncherUpdate:()=>ok(new URL(location.href).searchParams.get('updates')==='new'?{state:'ready',release:'fixture-release',version:'2.0.0',available:true,canInstall:true}:{state:'ready',release:'fixture-current',version:'1.0.0',available:false,canInstall:false}),
    openLauncherRelease:()=>ok({opened:true}),
    installLauncherUpdate:()=>{operation={id:'fixture-operation-'+(++sequence),region:snapshot.selectedRegion,state:'running',challenge:null,log:'[TEST] Downloading update.',launched:false};return ok({operationId:operation.id});},
    quitForUpdate:()=>Promise.resolve(),
    readOfficial:value=>ok(official(value)),
    openOfficial:value=>{
      // Visible synthetic receipt, without opening a real browser or a remote URL.
      const receipt=document.createElement('div');receipt.textContent='[TEST] Official entry: '+value.key+' / '+value.item;
      document.querySelector('#log').append(receipt);return ok({opened:true});
    },
    listAccounts:({region})=>ok(list(region)),
    readAccount:({region,name})=>ok({region,name,...records[region][name],revision:'fixture-revision'}),
    saveAccount:value=>{
      if(value.secret&&!value.password)return Promise.resolve({ok:false,code:'validation-failed',message:'A new 2FA secret requires a password.'});
      if(value.delete)delete records[value.region][value.name];else records[value.region][value.name]={password:value.password,secret:value.secret};
      return ok({saved:true});
    },
    listAreas:()=>ok([{name:'陆行鸟'},{name:'莫古力'}]),
    readPluginRepositories:({region})=>ok(repositories[region]),
    savePluginRepositories:value=>ok(repositories[value.region]={...repositories[value.region],...structuredClone(value)}),
    readPlugins:({region})=>new URL(location.href).searchParams.get('plugins')==='failed'?Promise.resolve({ok:false,code:'operation-failed',message:'Synthetic plugin read failure.'}):ok({region,revision:'fixture-plugin-list',dalamudVersion:'17.0.0',plugins:pluginLists[region],problemCount:0}),
    pluginSources:()=>ok(Object.keys(records).map(region=>({region,hasSource:true}))),
    startPluginOperation:value=>{
      operation={id:'fixture-operation-'+(++sequence),region:value.region,state:'running',challenge:null,log:'[TEST] Plugin operation started: '+value.kind,launched:false,pluginRequest:value,polls:0};
      return ok({operationId:operation.id});
    },
    startTravel:value=>{
      travel.completedArea=value.kind==='transfer'?'Destination':value.kind==='return'?'Source':null;
      if(value.kind==='source')travel.source=value.source;
      if(value.kind==='character')travel.character=value.character;
      if(travel.completedArea){snapshot.regions[0].areaName=travel.completedArea;travel.characters=[];travel.targets=[];}
      operation={id:'fixture-operation-'+(++sequence),region:'ffxiv_cn',state:new URL(location.href).searchParams.get('travel')==='slow'?'running':'completed',challenge:null,log:'[TEST] Travel: '+value.kind,launched:false,result:structuredClone(travel)};
      return ok({operationId:operation.id});
    },
    closeTravel:()=>ok({closed:true}),
    readCompatibility:()=>ok(compatibility),
    applyCompatibility:value=>{
      if(value.useSteamPrefix&&!compatibility.steamPrefix || new URL(location.href).searchParams.get('compatibility')==='failed')return Promise.resolve({ok:false,code:'operation-failed',message:'Synthetic compatibility write failure.'});
      Object.assign(compatibility,value);compatibility.registeredRoot=compatibility.registered?compatibility.steamRoot:'';return ok(compatibility);
    },
    diagnoseCompatibility:()=>{operation={id:'fixture-operation-'+(++sequence),region:snapshot.selectedRegion,state:'running',challenge:null,log:'[TEST] Checking environment.',launched:false};return ok({operationId:operation.id});},
    chooseProtonScript:()=>Promise.resolve('/fixture/proton'),
    startOperation:value=>{
      const kind=new URL(location.href).searchParams.get('challenge')||(value.region==='ffxiv_cn'?'mobile-confirmation':'otp');
      operation={id:'fixture-operation-'+(++sequence),region:value.region,state:'running',challenge:{id:'fixture-challenge-'+sequence,kind,message:kind==='mobile-confirmation'?'请在手机上打开叨鱼 App，完成滑动验证。':kind==='otp'?'2FA 验证码（可选）':kind==='qr'?'请使用叨鱼扫描二维码登录。':'Dalamud 更新连接超时。是否跳过本次更新？使用本地版本不能保证与当前游戏兼容。',code:kind==='mobile-confirmation'?'12345':null,optional:true,allowLocal:false},log:'[TEST] Synthetic operation started.',launched:false};
      return ok({operationId:operation.id});
    },
    pollOperation:()=>{
      finishPluginOperation();
      if(new URL(location.href).searchParams.get('logs')==='growing'&&operation?.state==='running')operation.log+='\n[TEST] Synthetic log line '+(operation.logSequence=(operation.logSequence||0)+1);
      return ok(operation);
    },
    clearOperationLog:()=>{operation.log='';return ok({accepted:true});},
    cancelOperation:()=>{operation.state='cancelled';operation.challenge=null;return ok({accepted:true});},
    replyOperation:value=>{
      if(operation.challenge?.id!==value.challengeId)return Promise.resolve({ok:false,code:'invalid-request',message:'Stale challenge.'});
      operation.state='completed';operation.challenge=null;operation.log+='\n[TEST] Synthetic operation completed.';return ok({accepted:true});
    },
    chooseClientDirectory:()=>Promise.resolve(null),finishLaunch:()=>Promise.resolve({released:false})
  };
})();
