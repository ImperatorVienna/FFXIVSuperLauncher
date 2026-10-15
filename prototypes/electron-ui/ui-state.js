'use strict';
// Shared DOM continuity only: no device polling or directional navigation.
window.uiState=Object.freeze({
  clippedBounds(element){
    const r=element.getBoundingClientRect();
    let left=Math.max(0,r.left),top=Math.max(0,r.top),right=Math.min(innerWidth,r.right),bottom=Math.min(innerHeight,r.bottom);
    for(let parent=element.parentElement;parent;parent=parent.parentElement){
      const style=getComputedStyle(parent),box=parent.getBoundingClientRect();
      if(/auto|scroll|hidden|clip/.test(style.overflowX)){left=Math.max(left,box.left);right=Math.min(right,box.right);}
      if(/auto|scroll|hidden|clip/.test(style.overflowY)){top=Math.max(top,box.top);bottom=Math.min(bottom,box.bottom);}
    }
    if(right<=left||bottom<=top)return null;
    return {bounds:{x:r.x,y:r.y,width:r.width,height:r.height},clip:{x:left,y:top,width:right-left,height:bottom-top}};
  },
  supports(snapshot,feature){return !Array.isArray(snapshot?.capabilities)||snapshot.capabilities.includes(feature);},
  mergeCompatibility(previous,saved){return {...saved,runtimes:previous?.runtimes||[],prefixes:previous?.prefixes||[]};},
  capture(root){
    const active=document.activeElement,scroll=root?.scrollTop;
    if(!root?.contains(active))return ()=>{};
    const id=active.id,data=Array.from(active.attributes).filter(a=>a.name.startsWith('data-'));
    const tag=active.tagName,text=active.textContent,owner=active.closest('dialog');
    const page=document.querySelector('nav [aria-current]')?.dataset.page;
    return ()=>{
      if(page!==document.querySelector('nav [aria-current]')?.dataset.page)return;
      if(document.querySelector('dialog[open]')&&!owner?.open)return;
      if(root.isConnected&&scroll!==undefined)root.scrollTop=scroll;
      if(document.activeElement!==document.body&&document.activeElement!==active&&document.activeElement?.isConnected)return;
      const scope=owner?.open?owner:root;
      const target=active.isConnected?active:Array.from(scope.querySelectorAll('button,input,select,textarea,a,summary')).find(el=>
        id?el.id===id:el.tagName===tag&&(data.length?data.every(a=>el.getAttribute(a.name)===a.value):el.textContent===text));
      if(target&&!target.disabled&&target.getClientRects().length)target.focus({preventScroll:true});
    };
  },
  showDialog(dialog){
    if(dialog.open)return;
    const restore=window.uiState.capture(document);
    dialog.addEventListener('close',()=>queueMicrotask(restore),{once:true});
    dialog.showModal();
  }
});
