'use strict';
const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs/promises');
const path=require('node:path');
const {tmpdir}=require('node:os');
const {prepareRuntime}=require('../run.cjs');

test('development bootstrap keeps the packaged browser intact and resolves frontend resources independently',async t=>{
  const source=await fs.mkdtemp(path.join(tmpdir(),'ffxiv-runtime-test-'));
  t.after(()=>fs.rm(source,{recursive:true,force:true}));
  const executable=path.join(source,'electron');await fs.writeFile(executable,'synthetic executable',{mode:0o700});
  const original=path.join(source,'resources','app');await fs.mkdir(original,{recursive:true});
  await fs.writeFile(path.join(original,'main.cjs'),'original browser');
  await fs.writeFile(path.join(source,'runtime.pak'),'synthetic runtime');
  const entry=path.join(source,'front end','main.cjs');
  const runtime=await prepareRuntime(executable,entry);t.after(runtime.cleanup);
  assert.equal((await fs.lstat(runtime.executable)).isSymbolicLink(),false);
  assert.equal(await fs.readFile(path.join(original,'main.cjs'),'utf8'),'original browser');
  assert.equal(await fs.realpath(path.join(path.dirname(runtime.executable),'runtime.pak')),path.join(source,'runtime.pak'));
  assert.equal(await fs.readFile(path.join(path.dirname(runtime.executable),'resources','app','main.cjs'),'utf8'),`require(${JSON.stringify(entry)});\n`);
  await runtime.cleanup();await assert.rejects(fs.access(runtime.executable));
});
test('development bootstrap rejects an ambiguous runtime location',async()=>{
  await assert.rejects(prepareRuntime('electron'),/absolute path/);
});
