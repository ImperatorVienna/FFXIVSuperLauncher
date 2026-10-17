'use strict';
const {test} = require('node:test');
const assert = require('node:assert/strict');
const {EventEmitter} = require('node:events');
const {PassThrough} = require('node:stream');
const {mkdtemp,readFile,readdir,rm} = require('node:fs/promises');
const {tmpdir} = require('node:os');
const path = require('node:path');
const vm = require('node:vm');
const {BackendClient} = require('../backend-client.cjs');

function harness(t,timeout=1000) {
  const child = new EventEmitter();
  child.stdin=new PassThrough(); child.stdout=new PassThrough(); child.stderr=new PassThrough();
  child.kill=()=>{child.killed=true;child.emit('exit',0);};
  child.stdin.on('finish',()=>child.emit('exit',0));
  const client = new BackendClient('/backend',['--frontend-host'],{timeout,spawnProcess:(file,args,options)=>{
    assert.equal(file,'/backend');assert.deepEqual(args,['--frontend-host']);assert.equal(options.shell,false);
    return child;
  }});
  t.after(()=>client.close());
  const take=()=>JSON.parse(child.stdin.read().toString());
  const respond=value=>child.stdout.write(JSON.stringify(value)+'\n');
  return {child,client,take,respond};
}

test('frames partial/coalesced responses and matches request IDs independently of order',async t=>{
  const {client,child,take}=harness(t);
  const a=client.request('snapshot'); const first=take();
  const b=client.request('preferences.update',{language:'ja'});const second=take();
  const frames=JSON.stringify({id:second.id,result:{language:'ja'}})+'\n'+JSON.stringify({id:first.id,result:{language:'en'}})+'\n';
  child.stdout.write(frames.slice(0,17));child.stdout.write(frames.slice(17));
  assert.deepEqual(await b,{language:'ja'});assert.deepEqual(await a,{language:'en'});
  assert.equal(client.pending.size,0);
});

test('request rejection does not prevent a later request',async t=>{
  const {client,take,respond}=harness(t);
  const bad=client.request('preferences.update',{language:'invalid'});const {id}=take();
  respond({id,error:{code:'invalid-request',message:'Do not forward this detail'}});
  await assert.rejects(bad,{message:'invalid-request'});
  const next=client.request('snapshot');respond({id:take().id,result:{protocolVersion:1}});
  assert.equal((await next).protocolVersion,1);
});

for (const [name,data] of [['malformed JSON','not-json\n'],['missing ID','{}\n'],['unbounded response','x'.repeat(1048577)]]) {
  test(name+' terminates the faulty host',async t=>{
    const {client,child}=harness(t);const waiting=client.request('snapshot');
    child.stdout.write(data);
    await assert.rejects(waiting,{message:'invalid-response'});assert.equal(child.killed,true);
    await assert.rejects(client.request('snapshot'),{message:'backend-unavailable'});
  });
}

test('host exit rejects all pending calls promptly',async t=>{
  const {client,child}=harness(t);
  const waiting=[client.request('snapshot'),client.request('snapshot')];
  child.emit('exit',1);
  assert.deepEqual((await Promise.allSettled(waiting)).map(x=>x.reason.message),['backend-exited','backend-exited']);
});

test('deadline terminates a stalled host and releases every request',async t=>{
  const {client,child}=harness(t,30);
  const results=await Promise.allSettled([client.request('snapshot'),client.request('snapshot')]);
  assert.ok(results.every(x=>x.status==='rejected'&&x.reason.message==='backend-timeout'));
  assert.equal(child.killed,true);assert.equal(client.pending.size,0);
});

test('methods, payload size and outstanding calls are bounded',async t=>{
  const {client}=harness(t);
  await assert.rejects(client.request('execute'),{message:'unknown-method'});
  await assert.rejects(client.request('preferences.update',{language:'x'.repeat(65536)}),{message:'invalid-request'});
  const all=Promise.allSettled(Array.from({length:16},()=>client.request('snapshot')));
  await assert.rejects(client.request('snapshot'),{message:'backend-busy'});
  client.close();assert.ok((await all).every(x=>x.reason.message==='backend-closed'));
});

test('broken input pipe does not leak a pending timer or keep the host alive',async t=>{
  const {client,child}=harness(t);
  child.stdin.write=()=>{throw new Error('broken pipe');};
  await assert.rejects(client.request('snapshot'),{message:'backend-unavailable'});
  assert.equal(client.pending.size,0);assert.equal(child.killed,true);
});

test('preload exposes only named operations and never raw IPC',async()=>{
  let exposed;const calls=[];
  const electron={contextBridge:{exposeInMainWorld:(name,value)=>{assert.equal(name,'launcherBridge');exposed=value;}},
    ipcRenderer:{invoke:(...args)=>{calls.push(args);return Promise.resolve('ok');}}};
  const code=await readFile(path.join(__dirname,'../preload.cjs'),'utf8');
  vm.runInNewContext(code,{require:name=>{assert.equal(name,'electron');return electron;}});
  assert.equal(Object.keys(exposed).length,41);assert.equal(Object.isFrozen(exposed),true);assert.equal(exposed.invoke,undefined);assert.equal(exposed.send,undefined);
  await exposed.snapshot();await exposed.applyPreferences({language:'ja'});
  assert.deepEqual(JSON.parse(JSON.stringify(calls)),[['launcher:snapshot',{}],['launcher:preferences.update',{language:'ja'}]]);
});

test('real .NET host persists preferences across process restart in an isolated directory',
  {skip:!process.env.FFXIV_BACKEND},async t=>{
    const root=await mkdtemp(path.join(tmpdir(),'ffxiv-frontend-test-'));
    let client;
    t.after(async()=>{await client?.close();await rm(root,{recursive:true,force:true});});
    const open=()=>new BackendClient(process.env.FFXIV_BACKEND,['--frontend-host','--data-root',root]);
    client=open();
    const initial=await client.request('snapshot');assert.equal(initial.protocolVersion,5);
    assert.deepEqual(await readdir(root),[]);
    const changed=await client.request('preferences.update',{language:'ja',region:'ffxiv_tc'});
    assert.equal(changed.language,'ja');assert.equal(changed.selectedRegion,'ffxiv_tc');
    await assert.rejects(client.request('preferences.update',{password:'synthetic-test-value'}),{message:'invalid-request'});
    assert.equal((await client.request('snapshot')).language,'ja');
    assert.deepEqual(await readdir(root),['linux-settings.json']);
    const config=await readFile(path.join(root,'linux-settings.json'),'utf8');
    assert.ok(!config.includes('synthetic-test-value'));
    await client.close();client=open();
    const restored=await client.request('snapshot');assert.equal(restored.language,'ja');assert.equal(restored.selectedRegion,'ffxiv_tc');
  });

test('real host account APIs and failed launch leave the command channel responsive',
  {skip:!process.env.FFXIV_BACKEND},async t=>{
    const root=await mkdtemp(path.join(tmpdir(),'ffxiv-frontend-account-test-'));
    const client=new BackendClient(process.env.FFXIV_BACKEND,['--frontend-host','--data-root',root]);
    t.after(async()=>{await client.close();await rm(root,{recursive:true,force:true});});
    await client.request('preferences.update',{region:'ffxiv'});
    await client.request('accounts.save',{region:'ffxiv',name:'测试账户-é',existing:false,password:'测试密码-é'});
    const list=await client.request('accounts.list',{region:'ffxiv'});
    assert.equal(list.accounts[0].name,'测试账户-é');assert.equal(list.accounts[0].hasPassword,true);
    assert.ok(!JSON.stringify(list).includes('测试密码-é'));
    const editor=await client.request('accounts.read',{region:'ffxiv',name:'测试账户-é'});
    assert.equal(editor.password,'测试密码-é');
    // No valid client or Proton configured: fail before any network authentication.
    const {operationId}=await client.request('operations.start',{region:'ffxiv',kind:'launch',account:'测试账户-é'});
    let state;const deadline=Date.now()+2000;
    do {state=await client.request('operations.poll',{operationId});if(state.state!=='running')break;await new Promise(resolve=>setTimeout(resolve,10));}while(Date.now()<deadline);
    assert.equal(state.state,'failed');assert.ok(!JSON.stringify(state).includes('测试密码-é'));
    const plugins=await client.request('plugins.read',{region:'ffxiv'});
    assert.deepEqual(plugins.plugins,[]);assert.ok(plugins.revision);
    const sources=await client.request('plugins.sources',{content:'installed-plugins'});
    assert.equal(sources.length,3);assert.ok(sources.every(source=>!source.hasSource));
    await client.request('operations.clear-log',{operationId});assert.equal((await client.request('operations.poll',{operationId})).log,'');
    assert.equal((await client.request('snapshot')).selectedRegion,'ffxiv');
    await client.request('accounts.save',{region:'ffxiv',name:'测试账户-é',existing:true,delete:true,revision:editor.revision});
    assert.equal((await client.request('accounts.list',{region:'ffxiv'})).accounts.length,0);
  });

test('backend failure notifies its owner once while normal shutdown does not',async t=>{
 const {client,child}=harness(t);let failures=0;client.onFailure=()=>failures++;
 child.emit('error',new Error('synthetic'));child.emit('exit',1);assert.equal(failures,1);
 const other=harness(t);other.client.onFailure=()=>assert.fail('Normal shutdown reported as failure');await other.client.close();
});


test('isolated read timeout preserves other requests, ignores late replies and permits retry',async t=>{
 const {client,child,take,respond}=harness(t,50);
 const read=client.request('compatibility.read');const first=take();
 await assert.rejects(read,{message:'read-timeout'});
 assert.equal(client.closed,false);assert.notEqual(child.killed,true);
 const next=client.request('snapshot');const second=take();
 respond({id:first.id,result:{stale:true}});respond({id:second.id,result:{live:true}});
 assert.equal((await next).live,true);
 const retry=client.request('compatibility.read');respond({id:take().id,result:{runtimes:[]}});
 assert.deepEqual(await retry,{runtimes:[]});
});

test('uncertain mutation timeout still terminates the host without replaying the write',async t=>{
 const {client,child,take}=harness(t,30);
 const request=client.request('preferences.update',{language:'ja'});take();
 await assert.rejects(request,{message:'backend-timeout'});
 assert.equal(child.killed,true);assert.equal(client.sequence,1);
});
