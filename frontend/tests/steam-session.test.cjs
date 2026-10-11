'use strict';
const {test}=require('node:test');const assert=require('node:assert/strict');const fs=require('node:fs/promises');const path=require('node:path');const {tmpdir}=require('node:os');
const {publishSession,waitForPrimary}=require('../steam-session.cjs');
test('a secondary Steam invocation waits for the owning primary session, then releases',async t=>{
  const root=await fs.mkdtemp(path.join(tmpdir(),'steam-session-test-'));t.after(()=>fs.rm(root,{recursive:true,force:true}));
  const remove=await publishSession(root);let polls=0;
  await waitForPrimary(root,{alive:()=>true,delay:async()=>{if(++polls===3)await remove();}});
  assert.equal(polls,3);await assert.rejects(fs.access(path.join(root,'electron-session.json')));
});

test('secondary packaged entry transfers waiting to its shell without keeping Electron alive',async t=>{
 const root=await fs.mkdtemp(path.join(tmpdir(),'steam-transfer-test-'));t.after(()=>fs.rm(root,{recursive:true,force:true}));
 await publishSession(root,12345);let transferred;
 await waitForPrimary(root,{alive:()=>true,transfer:async pid=>{transferred=pid},delay:async()=>{throw new Error('must not poll after transfer')}});
 assert.equal(transferred,12345);
});
