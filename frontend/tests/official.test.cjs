'use strict';
const {test}=require('node:test');
const assert=require('node:assert/strict');
const {readFileSync}=require('node:fs');
const path=require('node:path');
const vm=require('node:vm');
const {contracts}=require('../ipc-contract.cjs');
const code=readFileSync(path.join(__dirname,'../../prototypes/electron-ui/official.js'),'utf8');
const tick=()=>new Promise(resolve=>setImmediate(resolve));
const snapshot=(region,language='en-gb')=>({selectedRegion:region,regions:[{id:'ffxiv',cdKeyVersion:'EU',euShopLanguage:language}]});
const response=(key,state='ready')=>({key,state,activities:[],notices:[],links:['home']});
function harness(call) {
  const window={},updates=[],errors=[],scheduled=[];
  vm.runInNewContext(code,{window,setTimeout,clearTimeout});
  const controller=window.createOfficialContentController({call,changed:v=>updates.push(v),report:e=>errors.push(e),schedule:fn=>{scheduled.push(fn);return fn;},cancel:id=>{const at=scheduled.indexOf(id);if(at>=0)scheduled.splice(at,1);}});
  return {controller,updates,errors,scheduled};
}
test('late previous-region response cannot replace current news',async()=>{
  const pending=[];const {controller}=harness(()=>new Promise(resolve=>pending.push(resolve)));
  controller.sync(snapshot('ffxiv_cn'));controller.sync(snapshot('ffxiv_tc'));
  pending[1](response('ffxiv_tc'));await tick();pending[0](response('ffxiv_cn'));await tick();
  assert.equal(controller.view().key,'ffxiv_tc');
});
test('EU language changes reload content, interface-only redraws do not',async()=>{
  let reads=0;const {controller}=harness(async()=>{reads++;return response('ffxiv:fr');});
  controller.sync(snapshot('ffxiv','fr-fr'));await tick();
  controller.sync({...snapshot('ffxiv','fr-fr'),language:'ja'});await tick();assert.equal(reads,1);
  controller.sync(snapshot('ffxiv','de-de'));await tick();assert.equal(reads,2);
});
test('loading polls stop when ready and refresh/open failures remain local',async()=>{
  let count=0;const {controller,errors,scheduled}=harness(async name=>{
    if(name==='openOfficial')throw new Error('browser unavailable');
    return response('ffxiv:en-us',count++===0?'loading':'ready');
  });
  controller.sync(snapshot('ffxiv'));await tick();assert.equal(scheduled.length,1);
  scheduled.shift()();await tick();assert.equal(scheduled.length,0);
  await controller.open('old-region','home');assert.equal(errors.length,0);
  await controller.open('ffxiv:en-us','home');assert.equal(errors.length,1);assert.equal(controller.view().state,'ready');
  await controller.refresh();assert.equal(controller.view().state,'ready');
});
test('failed reads can be retried without reinitialising unrelated state',async()=>{
  let broken=true;const {controller,errors}=harness(async()=>{if(broken)throw new Error('timeout');return response('ffxiv_tc');});
  controller.sync(snapshot('ffxiv_tc'));await tick();assert.equal(controller.view().state,'failed');assert.equal(errors.length,1);
  broken=false;await controller.refresh();assert.equal(controller.view().state,'ready');
});
test('official IPC accepts identifiers only and uses canonical EU preferences',()=>{
  assert.equal(contracts['official.open']({key:'ffxiv:fr',item:'home',url:'https://evil.example'}),false);
  assert.equal(contracts['official.read']({region:'ffxiv'}),false);
  assert.equal(contracts['official.open']({key:'ffxiv:fr',item:'home'}),true);
  assert.equal(contracts['preferences.update']({euShopLanguage:'fr'}),false);
  assert.equal(contracts['preferences.update']({euShopLanguage:'fr-fr'}),true);
});
