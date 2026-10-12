'use strict';
// Text entry only. Steam owns the keyboard; no controller navigation, polling,
// input interception or focus changes are introduced here.
(()=>{
  const bridge=window.launcherBridge;if(!bridge?.showSteamKeyboard)return;
  let hint=null,sequence=0;
  const editable=node=>node instanceof HTMLTextAreaElement || node instanceof HTMLInputElement && ['text','password','email','url','search','tel','number'].includes(node.type);
  async function request(node){
    if(!editable(node)||node.disabled||node.readOnly||document.activeElement!==node)return;
    const id=++sequence;hint?.remove();hint=null;
    const rect=node.getBoundingClientRect();
    if(!rect.width||!rect.height)return;
    const scale=window.devicePixelRatio||1;
    const value={x:Math.max(0,Math.round(rect.x*scale)),y:Math.max(0,Math.round(rect.y*scale)),width:Math.max(1,Math.round(rect.width*scale)),height:Math.max(1,Math.round(rect.height*scale)),mode:node instanceof HTMLTextAreaElement?1:node.type==='number'||node.inputMode==='numeric'?3:node.type==='email'?2:0};
    let result;try{result=await bridge.showSteamKeyboard(value);}catch{result={status:'unavailable'};}
    if(id!==sequence||!node.isConnected||document.activeElement!==node||result?.status!=='unavailable')return;
    hint=document.createElement('span');hint.className='steam-keyboard-hint muted';hint.setAttribute('role','status');
    hint.textContent=window.previewI18n.t('无法调用 Steam 虚拟键盘，请使用键盘输入。');(node.closest('.field')||node).insertAdjacentElement('afterend',hint);
  }
  document.addEventListener('pointerup',event=>{if(event.isTrusted&&event.button===0)void request(event.target);});
  document.addEventListener('keyup',event=>{if(event.isTrusted&&event.key==='Tab')void request(document.activeElement);});
  document.addEventListener('focusout',()=>{++sequence;hint?.remove();hint=null;});
})();
