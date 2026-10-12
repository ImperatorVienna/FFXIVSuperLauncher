'use strict';
// Shared DOM continuity only: no device polling or directional navigation.
window.uiState=Object.freeze({
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
