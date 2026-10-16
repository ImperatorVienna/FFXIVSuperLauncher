'use strict';
const {test}=require('node:test');
const assert=require('node:assert/strict');
const {readFile}=require('node:fs/promises');
const path=require('node:path');
const vm=require('node:vm');
const {contracts}=require('../ipc-contract.cjs');

test('IPC rejects arbitrary commands, cross-purpose fields and malformed verification responses',()=>{
  assert.equal(Object.hasOwn(contracts,'execute'),false);
  assert.equal(contracts['preferences.update']({password:'secret'}),false);
  assert.equal(contracts['accounts.read']({region:'../../wrong',name:'test'}),false);
  assert.equal(contracts['accounts.save']({region:'ffxiv',name:'test',existing:true,secret:'x',command:'run'}),false);
  assert.equal(contracts['operations.reply']({operationId:'1',challengeId:'2',value:'x'.repeat(33)}),false);
  assert.equal(contracts['operations.reply']({operationId:'1',challengeId:'2',value:'123456'}),true);
});

async function noBackend(query,bridge) {
  const errors=[];const buttons=['login','save-account'].map(action=>({dataset:{action},closest:()=>null}));
  const host={children:[],addEventListener(){}};
  const document={createElement:()=>({setAttribute(){}}),body:{dataset:{}},querySelector:selector=>selector==='#log'?host:null,querySelectorAll:selector=>selector==='[data-demo],[data-action]'?buttons:[],addEventListener(){}};
  const window={launcherBridge:bridge,previewI18n:{t:value=>value,apply(){},languages:{English:'en'}}};
  host.ownerDocument=document;
  vm.runInNewContext(await readFile(path.join(__dirname,'../../prototypes/electron-ui/log-view.js'),'utf8'),{window,document});
  vm.runInNewContext(await readFile(path.join(__dirname,'../../prototypes/electron-ui/ui-state.js'),'utf8'),{window,document});
 vm.runInNewContext(await readFile(path.join(__dirname,'../../prototypes/electron-ui/connection.js'),'utf8'),
    {window,document,URL,location:{href:'file:///index.html'+query}});
  await window.frontendConnection.start(()=>assert.fail('Unexpected snapshot'),error=>errors.push(error));
  return {document,buttons,errors};
}
test('ordinary browser preview needs no backend and has no runtime side effects',async()=>{
  const result=await noBackend('');assert.equal(result.document.body.dataset.backend,undefined);assert.equal(result.errors.length,0);
});
test('missing Electron preload cannot enable mock login in integration mode',async()=>{
  const result=await noBackend('?integration=1');assert.equal(result.errors.length,1);assert.equal(result.buttons[0].disabled,true);assert.equal(result.buttons[1].disabled,true);
});
test('an incompatible backend snapshot cannot enable preference writes',async()=>{
  const result=await noBackend('?integration=1',{snapshot:async()=>({ok:true,result:{protocolVersion:999}})});
  assert.equal(result.errors.length,1);assert.equal(result.buttons[0].disabled,true);assert.equal(result.buttons[1].disabled,true);
});

test('plugin IPC only accepts bounded identities and named data categories',()=>{
  assert.equal(contracts['plugins.read']({region:'ffxiv'}),true);
  assert.equal(contracts['plugins.start']({region:'ffxiv',kind:'install-updates',planId:'signed-by-backend',plugins:['Example']}),true);
  assert.equal(contracts['plugins.start']({region:'ffxiv',kind:'install-updates',download:'https://wrong.test/payload.zip'}),false);
  assert.equal(contracts['plugins.start']({region:'ffxiv',kind:'apply',states:{Example:'enabled'}}),false);
  assert.equal(contracts['plugins.start']({region:'ffxiv',kind:'sync',content:'passwords',source:'ffxiv',targets:['ffxiv_tc']}),false);
  assert.equal(contracts['plugins.start']({region:'ffxiv',kind:'sync',content:'plugin-settings',source:'ffxiv',targets:['../../outside']}),false);
  assert.equal(contracts['plugins.start']({region:'ffxiv',kind:'install-updates',planId:'x',plugins:Array(1001).fill('x')}),false);
});

async function operationHarness(recovery=false){
  const handlers={},timers=new Map(),calls=[],updates=[];let nextTimer=0,startTask,renders=0,polls=0;
  const logs={open:false},count={textContent:''};
  const document={hidden:false,body:{dataset:{}},querySelector:s=>s==='.logs'?logs:s==='#log-count'?count:s.startsWith('nav ')?{click(){}}:null,querySelectorAll:()=>[],addEventListener:(name,fn)=>{handlers[name]=fn;}};
  const snapshot={protocolVersion:5,selectedRegion:'ffxiv',setupComplete:true,regions:[{id:'ffxiv'}]};
  let value={id:'operation',region:'ffxiv',state:'running',log:'line',challenge:null};
  const bridge={snapshot:async()=>({ok:true,result:snapshot}),listAccounts:async()=>({ok:true,result:{accounts:[]}}),diagnoseCompatibility:async()=>({ok:true,result:{operationId:'operation'}}),
    pollOperation:async()=>{polls++;return {ok:true,result:{...value}};},captchaLayout:async v=>calls.push(v)};
  if(recovery){bridge.checkGameRecovery=async()=>({ok:true,result:{required:true}});bridge.startGameRecovery=async()=>{calls.push('recover');return {ok:true,result:{operationId:'operation'}};};}
  const window={launcherBridge:bridge,previewI18n:{t:v=>v,apply(){},languages:{English:'en'}},
    createLogView:()=>({lines:()=>[],update:lines=>updates.push(lines),clear(){}}),
    createCompatibilityPage:({start})=>{startTask=start;return {sync(){renders++;},completed(){}};},addEventListener(){}};
  vm.runInNewContext(await readFile(path.join(__dirname,'../../prototypes/electron-ui/ui-state.js'),'utf8'),{window,document});
 vm.runInNewContext(await readFile(path.join(__dirname,'../../prototypes/electron-ui/connection.js'),'utf8'),{window,document,URL,location:{href:'file:///index.html?integration=1'},
    setTimeout:(fn,delay)=>{const id=++nextTimer;timers.set(id,{fn,delay});return id;},clearTimeout:id=>timers.delete(id)});
  await window.frontendConnection.start(()=>{},()=>{});
  return {start:()=>startTask({}),timers,calls,updates,renders:()=>renders,polls:()=>polls,state:state=>{value={...value,...state};},
    visibility(hidden){document.hidden=hidden;handlers.visibilitychange();},
    async tick(){const [id,timer]=[...timers][0];timers.delete(id);timer.fn();await new Promise(resolve=>setImmediate(resolve));}};
}
test('unchanged polls avoid redraw and non-web operations never send verification layout IPC',async()=>{
 const h=await operationHarness();await h.start();const rendered=h.renders();assert.equal(h.updates.length,1);
 for(let i=0;i<10;i++)await h.tick();
 assert.equal(h.renders(),rendered);assert.equal(h.updates.length,1);assert.equal(h.calls.length,0);
 h.state({log:'line\nnew'});await h.tick();assert.equal(h.updates.length,2);assert.ok(h.renders()>rendered);
 h.state({state:'completed'});await h.tick();assert.equal(h.timers.size,0);
});
test('minimizing slows active polling, waking refreshes promptly, and completion stops polling',async()=>{
 const h=await operationHarness();await h.start();assert.equal([...h.timers.values()][0].delay,350);
 h.visibility(true);assert.equal([...h.timers.values()][0].delay,2000);const rendered=h.renders();
 h.state({log:'background progress'});await h.tick();assert.equal([...h.timers.values()][0].delay,2000);assert.equal(h.renders(),rendered);
 const before=h.polls();h.visibility(false);await new Promise(resolve=>setImmediate(resolve));assert.equal(h.polls(),before+1);assert.equal([...h.timers.values()][0].delay,350);
 h.state({state:'completed'});await h.tick();assert.equal(h.timers.size,0);
});

test('plugin rendering and account read failures cannot prevent setup, About or compatibility startup',async()=>{
 const calls=[],errors=[];
 const document={body:{dataset:{}},querySelector:()=>null,querySelectorAll:()=>[],addEventListener(){},createElement:()=>({setAttribute(){}})};
 const window={launcherBridge:{snapshot:async()=>({ok:true,result:{protocolVersion:5,selectedRegion:'ffxiv',regions:[]}}),listAccounts:async()=>({ok:false,message:'Synthetic credential read failed'})},
  previewI18n:{t:v=>v,apply(){},languages:{}},createLogView:()=>({lines:()=>[]}),
  createPluginPage:()=>({sync(){throw new Error('synthetic UI error')}}),
  createCompatibilityPage:()=>({sync(){calls.push('compatibility')}}),
  createAboutPage:()=>({sync(){calls.push('about')},start(){calls.push('about.start')}}),
  createSetupWizard:()=>({start(){calls.push('setup')}})};
 vm.runInNewContext(await readFile(path.join(__dirname,'../../prototypes/electron-ui/ui-state.js'),'utf8'),{window,document});
 vm.runInNewContext(await readFile(path.join(__dirname,'../../prototypes/electron-ui/connection.js'),'utf8'),{window,document,URL,location:{href:'file:///index.html?integration=1'}});
 await window.frontendConnection.start(()=>{},error=>errors.push(error));
 assert.ok(calls.includes('compatibility'));assert.ok(calls.includes('about.start'));assert.ok(calls.includes('setup'));
 assert.equal(errors.filter(e=>e==='Frontend feature failed: plugins.').length,1);assert.ok(errors.includes('Synthetic credential read failed'));
});


test('repository management IPC limits regions, entry shapes and repository request payloads',()=>{
  assert.equal(contracts['plugins.repositories.read']({region:'ffxiv_cn'}),true);
  assert.equal(contracts['plugins.repositories.save']({region:'ffxiv_cn',revision:'r',presetsEnabled:true,entries:[{url:'https://repo.test/catalog',isEnabled:true}]}),true);
  assert.equal(contracts['plugins.repositories.save']({region:'ffxiv_cn',revision:'r',presetsEnabled:true,entries:[{url:'https://repo.test/catalog',isEnabled:true,command:'run'}]}),false);
  assert.equal(contracts['plugins.repositories.save']({region:'ffxiv_tc',revision:'r',presetsEnabled:false,entries:Array(101).fill({url:'https://repo.test/catalog',isEnabled:true})}),false);
  assert.equal(contracts['plugins.repositories.read']({region:'../../elsewhere'}),false);
  assert.equal(contracts['plugins.start']({region:'ffxiv_cn',kind:'check-repository',url:'https://repo.test/catalog'}),true);
});

test('repository sync replaces the removed whole-Dalamud sync category',()=>{
 assert.equal(contracts['plugins.sources']({content:'dalamud-settings'}),false);
 assert.equal(contracts['plugins.start']({region:'ffxiv_cn',kind:'sync',content:'dalamud-settings',source:'ffxiv_cn',targets:['ffxiv_tc']}),false);
 assert.equal(contracts['plugins.start']({region:'ffxiv_cn',kind:'sync',content:'third-party-repositories',source:'ffxiv_cn',targets:['ffxiv_tc']}),true);
});

 test('reopened UI starts pending game authentication without launching another game',async()=>{
 const h=await operationHarness(true);assert.deepEqual(h.calls,['recover']);assert.equal(h.polls(),1);
 h.state({state:'completed',launched:false});await h.tick();assert.equal(h.timers.size,0);assert.deepEqual(h.calls,['recover']);
});
test('game recovery IPC never accepts caller supplied endpoint or credentials',()=>{
 assert.equal(contracts['game-auth.check']({}),true);assert.equal(contracts['game-auth.start']({}),true);
 assert.equal(contracts['game-auth.start']({port:123,token:'secret'}),false);
});

test('cleanup IPC only accepts the two named cleanup operations',()=>{
  assert.equal(contracts['maintenance.clean']({kind:'downloads'}),true);
  assert.equal(contracts['maintenance.clean']({kind:'logs'}),true);
  assert.equal(contracts['maintenance.clean']({kind:'all'}),false);
  assert.equal(contracts['maintenance.clean']({kind:'downloads',path:'/tmp'}),false);
});
