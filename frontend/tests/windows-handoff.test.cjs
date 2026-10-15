'use strict';
const test=require('node:test'),assert=require('node:assert/strict');
const fs=require('node:fs/promises'),path=require('node:path'),os=require('node:os');
const {handoff}=require('../platforms/windows-handoff.cjs');
test('Windows does not release backend until supervisor acknowledges its exact PID',async()=>{
 const root=await fs.mkdtemp(path.join(os.tmpdir(),'windows-handoff-'));let released=false;
 try{
  const pending=handoff({child:{pid:12345},release(){released=true;}},root);
  for(let i=0;i<100;i++){try{await fs.access(path.join(root,'backend'));break;}catch{await new Promise(r=>setTimeout(r,10));}}
  assert.equal(await fs.readFile(path.join(root,'backend'),'utf8'),'12345');assert.equal(released,false);
  await fs.writeFile(path.join(root,'accepted'),'11111');await new Promise(r=>setTimeout(r,150));assert.equal(released,false);
  await fs.writeFile(path.join(root,'accepted'),'12345');await pending;assert.equal(released,true);
 }finally{await fs.rm(root,{recursive:true,force:true});}
});
test('invalid Windows handoff cannot release a backend',async()=>{
 let released=false;await assert.rejects(handoff({child:{pid:5},release(){released=true;}},'relative'),/supervisor/);assert.equal(released,false);
});
