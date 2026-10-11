'use strict';
const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs/promises');
const path=require('node:path');
const {tmpdir}=require('node:os');
const {execFile}=require('node:child_process');
const {promisify}=require('node:util');
const {writeDevelopmentEntry}=require('../launcher-entry.cjs');
const {contracts}=require('../ipc-contract.cjs');

test('persistent development entry waits for configuration and preserves paths and Steam arguments',async t=>{
  const root=await fs.mkdtemp(path.join(tmpdir(),"launcher entry '$()-"));t.after(()=>fs.rm(root,{recursive:true,force:true}));
  const runner=path.join(root,'capture.cjs');
  await fs.writeFile(runner,'process.stdout.write(JSON.stringify({args:process.argv.slice(2),backend:process.env.FFXIV_BACKEND,root:process.env.FFXIV_FRONTEND_DATA_DIR}));');
  const config={root,node:process.execPath,runner,backend:path.join(root,'backend'),electron:path.join(root,'electron')};
  await assert.rejects(writeDevelopmentEntry(config),/ENOENT/);
  await fs.writeFile(path.join(root,'linux-settings.json'),'{}');
  const entry=await writeDevelopmentEntry(config);
  const env={...process.env};delete env.NODE_TEST_CONTEXT;
  const {stdout}=await promisify(execFile)(entry,['--steam-entry'],{env});
  assert.deepEqual(JSON.parse(stdout),{args:['--steam-entry'],backend:config.backend,root});
  assert.equal((await fs.stat(entry)).mode&0o777,0o700);
});
test('compatibility IPC exposes choices without a renderer-controlled executable entry',()=>{
  assert.equal(Object.hasOwn(contracts,'runtime.list'),false);
  assert.equal(contracts['preferences.update']({protonScript:'/tmp/proton'}),false);
  assert.equal(contracts['compatibility.update']({protonScript:'/tmp/proton'}),true);
  assert.equal(contracts['compatibility.update']({registered:true,launcherEntry:'/tmp/untrusted'}),false);
  assert.equal(contracts['compatibility.update']({useSteamPrefix:'yes'}),false);
});
