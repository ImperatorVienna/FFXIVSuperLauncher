'use strict';
window.createSetupWizard=({call,snapshot,apply,completed,chooseProton,chooseDirectory,report})=>{
  const {t,languages}=window.previewI18n;
  let dialog,step=1,pending=false,compat=null,draft=null,error='';
  const node=(tag,text,cls)=>{const n=document.createElement(tag);if(text)n.textContent=t(text);if(cls)n.className=cls;return n;};
  function render(){
    if(!dialog){dialog=node('dialog',null,'setup-dialog');dialog.dataset.setupUi='';dialog.addEventListener('cancel',event=>event.preventDefault());document.body.append(dialog);}
    dialog.replaceChildren();dialog.append(node('h2','首次设置'));
    function select(id,label,choices,value,change){const field=node('label',label,'field'),input=node('select');input.id=id;input.replaceChildren(...choices.map(([key,text])=>new Option(text,key)));input.value=value;input.addEventListener('change',()=>change(input.value));field.append(input);dialog.append(field);return input;}
    function field(id,label,value,key){const parent=node('label',label,'field'),row=node('div',null,'row'),input=node('input');input.id=id;input.value=value;input.addEventListener('input',()=>draft[key]=input.value.trim());row.append(input);
      const browse=node('button','浏览…');browse.addEventListener('click',()=>void chooseDirectory().then(path=>{if(path){draft[key]=path;render();}}).catch(fail));row.append(browse);parent.append(row);dialog.append(parent);}
    function button(label,fn){const b=node('button',label);b.addEventListener('click',()=>void execute(fn));dialog.append(b);return b;}
    if(step===1){
      dialog.append(node('p','欢迎使用 FFXIV Super Launcher。第一步：选择启动器界面语言及游戏区服，国际区请同时选择账户CDKey版本；客户端路径可稍后填写。','muted'));
      select('setup-language','启动器界面语言',Object.entries(languages).map(([name,id])=>[id,name]),draft.language,value=>{draft.language=value;});
      select('setup-region','游戏区服',[['ffxiv_cn',t('中国区')],['ffxiv_tc',t('繁中区')],['ffxiv',t('国际区')]],draft.region,value=>{draft.region=value;render();});
      if(draft.region==='ffxiv')select('setup-cdkey','账户 CDKey 版本',['NA','JP','EU'].map(v=>[v,v]),draft.cdKeyVersion,value=>{draft.cdKeyVersion=value;});
      button('下一步：兼容性工具设置',async()=>{await apply({language:draft.language,region:draft.region,...(draft.region==='ffxiv'?{cdKeyVersion:draft.cdKeyVersion}:{})},true);compat=await call('readCompatibility');
        Object.assign(draft,{proton:compat.protonScript||'',steamRoot:compat.steamRoot||compat.runtimes.find(x=>x.ready)?.steamRoot||'',dataDirectory:compat.dataDirectory,registered:compat.registered});step=2;});
    }else{
      dialog.append(node('p','第二步：确认自动发现的 Steam / Proton。未发现时请手动选择；注册 Steam 兼容性工具为可选项。','muted'));
      select('setup-proton','Proton 版本',compat.runtimes.map(x=>[x.script,x.name]),draft.proton,value=>{draft.proton=value;});
      button('手动添加 Proton…',async()=>{const script=await chooseProton();if(script){const saved=await call('applyCompatibility',{protonScript:script});draft.proton=saved.protonScript;try{compat=await call('readCompatibility');}catch(ex){error='修改已保存，但刷新失败，请重新载入。';report('Proton selection saved, but refreshing installed environments failed.');}}});
      field('setup-steam','Steam 安装目录（用于手动添加工具）',draft.steamRoot,'steamRoot');
      field('setup-data','Proton 数据目录（自动在此创建 pfx，建议独立使用）',draft.dataDirectory,'dataDirectory');
      for(const [key,label] of [['registered','注册为 Steam 兼容性工具（可随时关闭）'],['enableDalamud','启用 Dalamud']]){const parent=node('label',null,'check'),input=node('input');input.type='checkbox';input.checked=draft[key];input.addEventListener('change',()=>draft[key]=input.checked);parent.append(input,document.createTextNode(t(label)));dialog.append(parent);}
      dialog.append(node('p','Dalamud 功能违反游戏用户协议，使用后果由用户自行承担。','muted'));
      button('上一步',async()=>{step=1;});
      button('完成配置',async()=>{
        await call('applyCompatibility',{protonScript:draft.proton,steamRoot:draft.steamRoot,dataDirectory:draft.dataDirectory,registered:draft.registered});
        const result=await call('completeSetup',{enableDalamud:draft.enableDalamud});dialog.close();dialog.remove();dialog=null;await completed(result);
      });
    }
    if(snapshot()?.isAppImage)dialog.append(node('p','手动移动或重命名 AppImage 后，请双击运行一次，以更新启动路径。之后即可继续从 Steam 或应用菜单启动。多个 AppImage 共用入口时，以最后运行的文件为准。可用此方法回滚旧版本，回滚后请关闭自动检查更新。','muted'));
    const feedback=node('p',error,'inline-error');feedback.setAttribute('role','status');dialog.append(feedback);
    for(const control of dialog.querySelectorAll('button,input,select'))control.disabled=pending;
    if(!dialog.open)dialog.showModal();
  }
  function fail(ex){error=ex.message||'操作失败，请查看日志。';report(ex.logMessage||'Initial setup failed.');}
  async function execute(fn){if(pending)return;pending=true;error='';render();try{await fn();}catch(ex){fail(ex);}finally{pending=false;if(dialog)render();}}
  return {start(){if(snapshot()?.setupComplete)return;draft={language:snapshot().language,region:snapshot().selectedRegion,cdKeyVersion:snapshot().regions.find(x=>x.id==='ffxiv')?.cdKeyVersion||'NA',enableDalamud:false};render();}};
};
