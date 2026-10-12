'use strict';
window.createSetupWizard=({call,snapshot,apply,completed,chooseProton,chooseDirectory,report})=>{
  const {t,languages}=window.previewI18n;
  let dialog,step=1,compat=null,draft=null,error='',queue=Promise.resolve(),queued=0,renderedShape='';
  const buttonTasks=new Set(),textDrafts=new Map(),fieldErrors=new Map();
  const shape=()=>JSON.stringify([step,draft.language,draft.region,draft.cdKeyVersion==='EU',compat?.runtimes]);
  const node=(tag,text,cls)=>{const n=document.createElement(tag);if(text)n.textContent=t(text);if(cls)n.className=cls;return n;};
  function present(){
    if(!dialog)return;dialog.setAttribute('aria-busy',String(queued>0));
    dialog.querySelector('[role=status]').textContent=t(error||fieldErrors.values().next().value||'');
    for(const [key,id] of [['steamRoot','setup-steam'],['dataDirectory','setup-data']])dialog.querySelector('#'+id)?.setAttribute('aria-invalid',String(fieldErrors.has(key)));
  }
  function reconcile(){
    if(!dialog)return;if(shape()!==renderedShape){render();return;}
    for(const key of ['language','region','cdkey','clientLanguage','euShopLanguage','proton','registered','enableDalamud']){
      const control=dialog.querySelector('#setup-'+key);if(!control)continue;
      const value=draft[key==='cdkey'?'cdKeyVersion':key];if(control.type==='checkbox')control.checked=Boolean(value);else control.value=value;
    }
  }
  function fail(ex){error=ex.message||'操作失败，请查看日志。';report(ex.logMessage||'Initial setup failed.');}
  function execute(fn,clear=true){
    queued++;present();
    const task=queue.then(async()=>{if(!dialog)return;if(clear)error='';await fn();}).catch(fail);
    queue=task.finally(()=>{queued--;if(dialog){if(!queued)reconcile();present();}}).catch(fail);
    return queue;
  }
  async function saveCompatibility(key,value,replacement){
    const property=key==='proton'?'protonScript':key;
    try{const saved=await call('applyCompatibility',{[property]:value});compat=window.uiState.mergeCompatibility(compat,saved);fieldErrors.delete(key);}
    catch(ex){if(textDrafts.has(key))fieldErrors.set(key,ex.message||'操作失败，请查看日志。');throw ex;}
    draft[key]=compat[property];
    const input=dialog?.querySelector('#setup-'+(key==='steamRoot'?'steam':key==='dataDirectory'?'data':key));
    if(input?.tagName==='INPUT'&&input.type!=='checkbox'&&(!textDrafts.has(key)||textDrafts.get(key).trim()===String(value)||(replacement&&textDrafts.get(key)===replacement.text))){textDrafts.delete(key);input.value=draft[key];}
  }
  function render(){
    if(!dialog){dialog=node('dialog',null,'setup-dialog');dialog.dataset.setupUi='';dialog.setAttribute('closedby','none');dialog.setAttribute('aria-labelledby','setup-title');dialog.addEventListener('cancel',event=>event.preventDefault());document.body.append(dialog);}
    const focusedId=dialog.contains(document.activeElement)?document.activeElement.id:null,oldStep=dialog.dataset.step;dialog.dataset.step=String(step);
    dialog.replaceChildren();const heading=node('h2','首次设置');heading.id='setup-title';dialog.append(heading);
    function select(id,label,choices,value,change,parent=dialog){const field=node('label',label,'field'),input=node('select');input.id=id;input.replaceChildren(...choices.map(([key,text])=>new Option(text,key)));input.value=value;input.addEventListener('change',()=>{const value=input.value;void execute(()=>change(value));});field.append(input);parent.append(field);}
    function field(id,label,value,key){
      const parent=node('label',label,'field'),row=node('div',null,'row'),input=node('input');input.id=id;input.value=textDrafts.get(key)??value;
      input.addEventListener('input',()=>{if(input.value.trim()===draft[key]){textDrafts.delete(key);fieldErrors.delete(key);}else textDrafts.set(key,input.value);present();});
      input.addEventListener('change',()=>{const value=input.value.trim();if(value===draft[key]){input.value=value;return;}void execute(()=>saveCompatibility(key,value));});row.append(input);
      button(id+'-browse','浏览…',async()=>{const previous=textDrafts.get(key),path=await chooseDirectory();if(path)await saveCompatibility(key,path,{text:previous});},row);
      parent.append(row);dialog.append(parent);
    }
    function button(id,label,fn,parent=dialog){const b=node('button',label);b.id=id;b.type='button';b.addEventListener('click',()=>{
      if(buttonTasks.has(id))return;const followsSave=queued>0,advances=['setup-next','setup-complete'].includes(id);buttonTasks.add(id);
      void execute(async()=>{if(advances&&followsSave&&error)return;if(advances&&(textDrafts.size||fieldErrors.size)){error=fieldErrors.values().next().value||'请先保存或修正未保存的设置。';return;}error='';await fn();},false).finally(()=>buttonTasks.delete(id));
    });parent.append(b);return b;}
    if(step===1){
      select('setup-language','启动器界面语言',Object.entries(languages).map(([name,id])=>[id,name]),draft.language,async value=>{await apply({language:value},true);draft.language=value;});
      const actions=node('div',null,'row dialog-actions');dialog.append(actions);button('setup-next','下一步：游戏区服',async()=>{step=2;},actions).classList.add('primary');
    }else if(step===2){
      select('setup-region','游戏区服',[['ffxiv_cn',t('中国区')],['ffxiv_tc',t('繁中区')],['ffxiv',t('国际区')]],draft.region,async value=>{await apply({region:value},true);draft.region=value;draft.enableDalamud=snapshot().regions.find(x=>x.id===value)?.enableDalamud||false;});
      if(draft.region==='ffxiv'){
        select('setup-clientLanguage','国际区客户端语言',[[0,'日本語'],[1,'English'],[2,'Deutsch'],[3,'Français']],draft.clientLanguage,async value=>{await apply({clientLanguage:Number(value)},true);draft.clientLanguage=Number(value);});
        const pair=node('div',null,'settings-pair');dialog.append(pair);
        select('setup-cdkey','账户 CDKey 版本',['NA','JP','EU'].map(v=>[v,v]),draft.cdKeyVersion,async value=>{await apply({cdKeyVersion:value},true);draft.cdKeyVersion=value;},pair);
        if(draft.cdKeyVersion==='EU')select('setup-euShopLanguage','官网及商城偏好语言',[['en-gb','English(UK)'],['fr-fr','Français'],['de-de','Deutsch']],draft.euShopLanguage,async value=>{await apply({euShopLanguage:value},true);draft.euShopLanguage=value;},pair);
      }
      const actions=node('div',null,'row dialog-actions');dialog.append(actions);button('setup-back','上一步',async()=>{step=1;},actions);
      button('setup-next','下一步：Steam 和 Proton 设置',async()=>{compat=await call('readCompatibility');Object.assign(draft,{proton:compat.protonScript||'',steamRoot:compat.steamRoot||'',dataDirectory:compat.dataDirectory,registered:compat.registered});step=3;},actions).classList.add('primary');
    }else{
      dialog.append(node('p','第三步：确认自动发现的 Steam / Proton。未发现时请手动选择；Steam 启动适配为可选项。','muted'));
      select('setup-proton','Proton 版本',compat.runtimes.map(x=>[x.script,x.name]),draft.proton,value=>saveCompatibility('proton',value));
      button('setup-proton-browse','手动添加 Proton…',async()=>{const script=await chooseProton();if(script){await saveCompatibility('proton',script);try{compat=await call('readCompatibility');}catch(ex){error='修改已保存，但刷新失败，请重新载入。';report('Proton selection saved, but refreshing installed environments failed.');}}});
      field('setup-steam','Steam 安装目录（用于手动添加工具）',draft.steamRoot,'steamRoot');field('setup-data','Proton 数据目录（自动在此创建 pfx，建议独立使用）',draft.dataDirectory,'dataDirectory');
      for(const [key,label] of [['registered','适配从 Steam 启动'],['enableDalamud','启用 Dalamud']]){
        const parent=node('label',null,'check'),input=node('input');input.type='checkbox';input.id='setup-'+key;input.checked=draft[key];
        input.addEventListener('change',()=>{const value=input.checked;void execute(async()=>{if(key==='enableDalamud'){await apply({enableDalamud:value},true);draft[key]=value;}else await saveCompatibility(key,value);});});parent.append(input,document.createTextNode(t(label)));dialog.append(parent);if(key==='registered')dialog.append(node('p','勾选后将启动器注册为 Steam 兼容性工具；通过 Steam 启动时自动全屏，点击输入框会调用 Steam 虚拟键盘。桌面窗口尺寸单独保留。更改后请重启 Steam。','muted'));
      }
      dialog.append(node('p','Dalamud 功能违反游戏用户协议，使用后果由用户自行承担。','muted'));
      const actions=node('div',null,'row dialog-actions');dialog.append(actions);button('setup-back','上一步',async()=>{step=2;},actions);
      button('setup-complete','完成配置',async()=>{const result=await call('completeSetup',{enableDalamud:draft.enableDalamud});dialog.close();dialog.remove();dialog=null;await completed(result);},actions).classList.add('primary');
    }
    if(snapshot()?.isAppImage)dialog.append(node('p','手动移动或重命名 AppImage 后，请双击运行一次，以更新启动路径。之后即可继续从 Steam 或应用菜单启动。多个 AppImage 共用入口时，以最后运行的文件为准。可用此方法回滚旧版本，回滚后请关闭自动检查更新。','muted'));
    const feedback=node('p',error,'inline-error');feedback.setAttribute('role','status');dialog.append(feedback);renderedShape=shape();present();
    if(!dialog.open)window.uiState.showDialog(dialog);else if(oldStep===String(step)&&focusedId)dialog.querySelector('#'+focusedId)?.focus({preventScroll:true});else dialog.querySelector('select,input,button')?.focus({preventScroll:true});
  }
  return {start(){if(snapshot()?.setupComplete||dialog)return;const global=snapshot().regions.find(x=>x.id==='ffxiv');draft={language:snapshot().language||'en',region:snapshot().selectedRegion,clientLanguage:global?.clientLanguage??1,euShopLanguage:global?.euShopLanguage||'en-gb',cdKeyVersion:global?.cdKeyVersion||'NA',enableDalamud:snapshot().regions.find(x=>x.id===snapshot().selectedRegion)?.enableDalamud||false};render();}};
};
