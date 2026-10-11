'use strict';
function restoreBounds(saved,displays) {
  const fallback={x:0,y:0,width:1320,height:860,maximized:false};
  const valid=saved&&Number.isFinite(saved.width)&&Number.isFinite(saved.height)&&saved.width>=640&&saved.height>=650;
  const value=valid?saved:fallback;
  const area=displays.find(d=>value.x+40>=d.x&&value.x+40<d.x+d.width&&value.y+20>=d.y&&value.y+20<d.y+d.height)||displays[0]||fallback;
  const width=Math.min(Math.max(860,value.width),area.width),height=Math.min(Math.max(680,value.height),area.height);
  return {x:Math.round(Math.max(area.x,Math.min(value.x,area.x+area.width-width))),y:Math.round(Math.max(area.y,Math.min(value.y,area.y+area.height-height))),
    width:Math.round(width),height:Math.round(height),maximized:Boolean(value.maximized)};
}
function observePlacement(window,save) {
  let last=null,timer=null,pending=Promise.resolve(),serialized='';
  const capture=()=>{if(window.isDestroyed()||window.isMinimized())return;last={...window.getNormalBounds(),maximized:window.isMaximized()};};
  const flush=()=>{clearTimeout(timer);capture();if(!last)return pending;const value={...last},text=JSON.stringify(value);
    if(text===serialized)return pending;serialized=text;
    pending=pending.catch(()=>{}).then(()=>save(value)).catch(error=>{serialized='';throw error;});return pending;};
  const changed=()=>{capture();clearTimeout(timer);timer=setTimeout(()=>void flush().catch(()=>{}),250);};
  for(const name of ['move','resize','maximize','unmaximize','restore'])window.on(name,changed);
  window.on('close',capture);capture();
  return {flush,minimize(){capture();window.minimize();},stop(){clearTimeout(timer);}};
}
module.exports={restoreBounds,observePlacement};
