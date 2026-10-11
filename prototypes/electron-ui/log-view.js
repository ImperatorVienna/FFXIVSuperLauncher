'use strict';
// Preserve text nodes/selection. Pending updates are bounded and resume after copying.
window.createLogView = (host,{selected=()=>{
  const selection=host.ownerDocument.getSelection();
  return selection&&!selection.isCollapsed&&(host.contains(selection.anchorNode)||host.contains(selection.focusNode));
},hidden=()=>host.ownerDocument.hidden}={}) => {
  let pending=null,dragging=false;
  function flush(){
    if(!pending||hidden()||dragging||selected())return;
    const lines=pending;pending=null;
    for(const child of [...host.children])if(child.tagName!=='DIV')child.remove();
    for(let i=0;i<lines.length;i++){
      let child=host.children[i];
      if(!child){child=host.ownerDocument.createElement('div');child.textContent=lines[i];host.append(child);}
      else if(child.textContent!==lines[i])child.textContent=lines[i];
    }
    while(host.children.length>lines.length)host.lastElementChild.remove();
  }
  host.addEventListener('pointerdown',()=>{dragging=true;});
  const release=()=>{dragging=false;flush();};
  host.ownerDocument.addEventListener('pointerup',release);
  host.ownerDocument.addEventListener('pointercancel',release);
  host.ownerDocument.defaultView?.addEventListener('blur',release);
  host.ownerDocument.addEventListener('selectionchange',flush);
  host.ownerDocument.addEventListener('visibilitychange',flush);
  return {update(lines){pending=lines.slice(-1000);flush();},
    lines:()=>pending||[...host.children].filter(n=>n.tagName==='DIV').map(n=>n.textContent),
    clear(){pending=null;host.replaceChildren();}};
};
