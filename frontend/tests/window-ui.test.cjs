'use strict';
const {test}=require('node:test'),assert=require('node:assert/strict'),{EventEmitter}=require('node:events');
const {contentZoom,observeContentSize,bindExitKeys}=require('../window-ui.cjs');
test('content scale respects both dimensions and never shrinks Deck or small screens',()=>{
 for(const [w,h,z] of [[1280,800,1],[1024,600,1],[1320,860,1],[1920,1080,1080/860],[2560,1440,1440/860],[3840,2160,2160/860],[3440,1440,1440/860]])assert.equal(contentZoom(w,h),z);
});
test('resize/load recompute from native content size without feedback or repeated writes',()=>{
 const window=new EventEmitter(),c=window.webContents=new EventEmitter();let size=[3840,2160],zoom=1,writes=0;
 window.isDestroyed=c.isDestroyed=()=>false;window.getContentSize=()=>size;c.getZoomFactor=()=>zoom;c.setZoomFactor=value=>{zoom=value;writes++;};
 observeContentSize(window);assert.equal(zoom,2160/860);window.emit('resize');assert.equal(writes,1);
 size=[1280,800];window.emit('resize');assert.equal(zoom,1);zoom=2;c.emit('did-finish-load');assert.equal(zoom,1);
 window.emit('closed');assert.equal(window.listenerCount('resize'),0);assert.equal(c.listenerCount('did-finish-load'),0);
});
test('exit accelerators do not consume Escape, typing, overlays or key repeats',()=>{
 const c=new EventEmitter();let quits=0,prevented=0;const remove=bindExitKeys(c,()=>quits++);
 const press=key=>c.emit('before-input-event',{preventDefault(){prevented++;}},{type:'keyDown',...key});
 for(const key of [{key:'q'},{key:'Escape'},{key:'Tab',shift:true},{key:'Q',control:true,shift:true},{key:'q',control:true,isAutoRepeat:true},{key:'q',control:true,type:'keyUp'}])press(key);
 assert.equal(quits,0);press({key:'q',control:true});press({key:'F4',alt:true});assert.equal(quits,2);assert.equal(prevented,2);remove();assert.equal(c.listenerCount('before-input-event'),0);
});
