'use strict';
const {test}=require('node:test');const assert=require('node:assert/strict');
const fs=require('node:fs/promises');const path=require('node:path');const {tmpdir}=require('node:os');const {spawn}=require('node:child_process');
const {handoff}=require('../session-handoff.cjs');
test('handoff records process identity before releasing the transport',async t=>{
 const dir=await fs.mkdtemp(path.join(tmpdir(),'handoff-test-'));t.after(()=>fs.rm(dir,{recursive:true,force:true}));let released=false;
 await handoff({child:{pid:process.pid},release(){released=true}},dir);
 assert.equal(released,true);assert.match(await fs.readFile(path.join(dir,'backend'),'utf8'),new RegExp(`^${process.pid} [0-9]+\\n$`));
 await assert.rejects(handoff({child:{pid:99999999},release(){throw new Error('must not release')}},dir));
});
test('supervisor outlives Electron and exits when the handed-off session exits',async t=>{
 const dir=await fs.mkdtemp(path.join(tmpdir(),'supervisor-test-'));t.after(()=>fs.rm(dir,{recursive:true,force:true}));
 await fs.mkdir(path.join(dir,'Tools/webview'),{recursive:true});
 await fs.copyFile(path.join(__dirname,'../../packaging/electron/xivlauncher-super'),path.join(dir,'xivlauncher-super'));
 const fake=path.join(dir,'Tools/webview/electron');
 await fs.writeFile(fake,`#!/bin/sh\nsleep 2 &\npid=$!\nIFS= read -r stat < /proc/$pid/stat\nfields=\${stat##*) }\nset -- $fields\nshift 19\nprintf '%s %s\\n' "$pid" "$1" > "$FFXIV_SESSION_DIRECTORY/backend"\nprintf 'electron-exited\\n'\n`,{mode:0o700});
 const before=Date.now(),child=spawn('/bin/sh',[path.join(dir,'xivlauncher-super')]);let text='';child.stdout.on('data',x=>text+=x);child.stderr.resume();
 const code=await new Promise(resolve=>child.on('exit',resolve));
 assert.equal(code,0);assert.match(text,/electron-exited/);assert.ok(Date.now()-before>=1800);
});
