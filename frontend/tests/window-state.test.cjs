'use strict';
const {test}=require('node:test');const assert=require('node:assert/strict');const {EventEmitter}=require('node:events');
const {restoreBounds,observePlacement}=require('../window-state.cjs');
test('restore keeps normal geometry visible after a monitor disappears',()=>{
  const b=restoreBounds({x:3000,y:100,width:1400,height:900,maximized:true},[{x:0,y:0,width:1280,height:800}]);
  assert.deepEqual(b,{x:0,y:0,width:1280,height:800,maximized:true});
});
test('automatic minimize preserves normal bounds and maximized preference',async()=>{
  const window=new EventEmitter();let minimized=false,destroyed=false;const saved=[];
  window.isDestroyed=()=>destroyed;window.isMinimized=()=>minimized;window.isMaximized=()=>true;
  window.getNormalBounds=()=>({x:20,y:30,width:1320,height:860});window.minimize=()=>{minimized=true;window.emit('resize');};
  const observer=observePlacement(window,x=>{saved.push(x);return Promise.resolve();});observer.minimize();destroyed=true;await observer.flush();observer.stop();
  assert.deepEqual(saved,[{x:20,y:30,width:1320,height:860,maximized:true}]);
});
