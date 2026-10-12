'use strict';
window.createTravelPage=({call,start,cancel,ready,operation,report})=>{
  const {t}=window.previewI18n;
  let state=null,dialog=null,error='',autoLaunch=false,selectedTarget='',selectedOrder='',selectedCurrent='';
  const node=(tag,text,cls)=>{const n=document.createElement(tag);if(text)n.textContent=t(text);if(cls)n.className=cls;return n;};
  function close(){if(operation())return;dialog?.close();dialog?.remove();dialog=null;state=null;void call('closeTravel').catch(ex=>report(ex.message));}
  async function action(request){if(!ready())return;error='';try{await start({...request,session:state.session});}catch(ex){error=ex.message;present();}}
  function render(){
    if(!state)return;
    if(!dialog){dialog=node('dialog',null,'travel-dialog');dialog.dataset.travelUi='';document.body.append(dialog);
      dialog.addEventListener('cancel',event=>{event.preventDefault();if(operation())void cancel();else close();});}
    dialog.replaceChildren();dialog.append(node('h2','超域传送'),node('p',state.account,'muted'));
    function select(id,label,choices,value,change){const field=node('label',label,'field'),input=node('select');input.id=id;
      input.replaceChildren(...choices.map(([key,text])=>new Option(text,key)));input.value=value;
      input.addEventListener('change',()=>change(input.value));field.append(input);dialog.append(field);return input;}
    function buttons(){const row=node('div',null,'row compatibility-actions');dialog.append(row);return row;}
    function button(row,id,label,fn){const b=node('button',label);b.id=id;b.addEventListener('click',()=>void fn());row.append(b);return b;}
    select('travel-source','出发大区',state.sources.map(x=>[x.id,x.name]),state.source,value=>void action({kind:'source',source:value}));
    select('travel-character','角色',state.characters.map(x=>[x.id,x.name]),state.character,value=>void action({kind:'character',character:value}));
    const targetGroups=state.targets.flatMap(a=>a.groups.map(g=>[g.id,a.name+' / '+g.name]));
    if(!targetGroups.some(([id])=>id===selectedTarget))selectedTarget=targetGroups[0]?.[0]||'';
    select('travel-target','目标大区／服务器',targetGroups,selectedTarget,value=>{selectedTarget=value;});
    const transfer=button(buttons(),'travel-transfer','开始传送',async()=>{
      const role=state.characters.find(x=>x.id===state.character)?.name||'',target=targetGroups.find(([id])=>id===selectedTarget)?.[1]||'';
      if(await confirm(t('确认将 {role} 传送到 {target}？',{role,target})))await action({kind:'transfer',revision:state.revision,target:selectedTarget});
    });
    transfer.dataset.unavailable=String(!state.character||!selectedTarget);
    dialog.append(node('h3','传送记录','divider'));
    if(!state.orders.some(x=>x.id===selectedOrder))selectedOrder=state.orders[0]?.id||'';
    const allGroups=state.sources.flatMap(a=>a.groups.map(g=>[g.id,a.name+' / '+g.name]));
    if(!allGroups.some(([id])=>id===selectedCurrent))selectedCurrent=allGroups.find(([,text])=>text===state.orders.find(x=>x.id===selectedOrder)?.to)?.[0]||allGroups[0]?.[0]||'';
    select('travel-order','可返回的传送记录',state.orders.map(x=>[x.id,x.name+' · '+x.from+' → '+x.to+' · '+x.created]),selectedOrder,value=>{selectedOrder=value;selectedCurrent='';render();});
    select('travel-current','角色当前大区／服务器',allGroups,selectedCurrent,value=>{selectedCurrent=value;});
    const history=buttons();
    const back=button(history,'travel-return','返回原服务器',async()=>{
      const order=state.orders.find(x=>x.id===selectedOrder),current=allGroups.find(([id])=>id===selectedCurrent)?.[1]||'';
      if(order&&await confirm(t('确认将 {role} 从 {current} 返回 {target}？',{role:order.name,current,target:order.from})))await action({kind:'return',revision:state.revision,order:selectedOrder,current:selectedCurrent});
    });back.dataset.unavailable=String(!selectedOrder||!selectedCurrent);
    button(history,'travel-refresh','刷新记录',()=>action({kind:'history',page:state.page}));
    const pages=buttons();
    button(pages,'travel-previous','上一页',()=>action({kind:'history',page:state.page-1})).dataset.unavailable=String(state.page<=1);
    pages.append(node('span',t('第 {page} / {pages} 页',{page:state.page,pages:state.pages}),'muted'));
    button(pages,'travel-next','下一页',()=>action({kind:'history',page:state.page+1})).dataset.unavailable=String(state.page>=state.pages);
    const check=node('label',null,'check'),input=node('input');input.type='checkbox';input.id='travel-auto-launch';input.checked=autoLaunch;
    input.addEventListener('change',()=>{autoLaunch=input.checked;});check.append(input,document.createTextNode(t('完成后启动游戏')));dialog.append(check);
    dialog.append(node('p','停止等待或关闭窗口不会撤销已提交的服务端传送。可重新打开并刷新记录确认结果。','muted'));
    const feedback=node('p',null,'muted');feedback.id='travel-feedback';feedback.setAttribute('role','status');dialog.append(feedback);
    const end=buttons();button(end,'travel-stop','停止等待',cancel);button(end,'travel-close','关闭',close);
    present();if(!dialog.open)window.uiState.showDialog(dialog);
  }
  async function confirm(message){
    const modal=node('dialog');modal.dataset.travelUi='';modal.append(node('p',message));
    const result=new Promise(resolve=>{
      const actions=node('div',null,'row dialog-actions');modal.append(actions);
      for(const [label,value] of [['确认',true],['取消',false]]){const b=node('button',label,value?'primary':'');b.addEventListener('click',()=>{modal.close();resolve(value);});actions.append(b);}
      modal.addEventListener('cancel',()=>resolve(false),{once:true});
    });document.body.append(modal);window.uiState.showDialog(modal);try{return await result;}finally{modal.remove();}
  }
  function present(){
    if(!dialog)return;
    for(const input of dialog.querySelectorAll('input,select,button'))input.disabled=!ready()||input.dataset.unavailable==='true';
    const stop=dialog.querySelector('#travel-stop');stop.hidden=!operation();stop.disabled=operation()?.state!=='running';
    const feedback=dialog.querySelector('#travel-feedback');feedback.className=error?'inline-error':'muted';
    feedback.textContent=t(error||(operation()?'正在处理，请稍候…':state.completedArea?'超域操作已完成。':''));
  }
  return {present,reset:close,completed(result){
    if(result.state==='completed'&&result.result?.session){state=result.result;error='';render();if(state.completedArea&&autoLaunch){close();return true;}}
    else {error=result.error||(result.state==='cancelled'?'已取消。':'操作失败，请查看日志。');present();}
    return false;
  }};
};
