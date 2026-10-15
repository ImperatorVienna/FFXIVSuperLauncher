'use strict';
const test=require('node:test');const assert=require('node:assert/strict');
const {runtime}=require('../platforms/runtime.cjs');
test('Windows does not inherit Linux Steam controls or storage',()=>{
 const value=runtime('win32',{LOCALAPPDATA:'/profile/local',XDG_DATA_HOME:'/linux',FFXIV_PORTABLE_EXECUTABLE:'/portable/launcher.exe'},'/home','/bundle');
 assert.equal(value.steamControls,false);assert.equal(value.defaultData,'/portable');
 assert.equal(value.backend,'/bundle/xivlauncher-backend.exe');assert.equal(value.launcher,'/portable/launcher.exe');
});
test('Linux uses the external AppImage directory, not its temporary mount',()=>{
 const value=runtime('linux',{XDG_DATA_HOME:'/data',APPIMAGE:'/portable/Launcher.AppImage'},'/home','/bundle');
 assert.equal(value.steamControls,true);assert.equal(value.defaultData,'/portable');
 assert.equal(value.backend,'/bundle/xivlauncher-backend');assert.equal(value.launcher,'/bundle/xivlauncher-super');
});
test('unsupported platforms fail explicitly',()=>assert.throws(()=>runtime('darwin',{},'/home','/bundle'),/Unsupported/));
