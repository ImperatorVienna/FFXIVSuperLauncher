'use strict';
// Content dimensions are device-independent pixels: OS scaling is already applied.
function contentZoom(width,height){
  return Math.max(1,Math.min(width/1320,height/860));
}
function bindExitKeys(contents,quit){
  const input=(event,key)=>{
    if(key.type!=='keyDown'||key.isAutoRepeat||key.shift||key.meta)return;
    if((key.control&&!key.alt&&key.key.toLowerCase()==='q')||
       (key.alt&&!key.control&&key.key==='F4')){event.preventDefault();quit();}
  };
  contents.on('before-input-event',input);
  return ()=>contents.removeListener('before-input-event',input);
}
function observeContentSize(window){
  const contents=window.webContents;
  const update=()=>{
    if(window.isDestroyed()||contents.isDestroyed())return;
    const zoom=contentZoom(...window.getContentSize());
    if(Math.abs(contents.getZoomFactor()-zoom)>0.001)contents.setZoomFactor(zoom);
  };
  window.on('resize',update);
  contents.on('did-finish-load',update);
  window.once('closed',()=>{window.removeListener('resize',update);contents.removeListener('did-finish-load',update);});
  update();
}
module.exports={contentZoom,observeContentSize,bindExitKeys};
