'use strict';
window.compatibilityTemplate=()=>`<section class="panel settings-panel" data-compatibility-ui>
  <div class="section-heading"><h2>兼容性工具设置</h2></div><div id="compatibility-content"></div>
  <p id="compatibility-feedback" class="muted" role="status"></p></section>`;

window.createCompatibilityPage=({call,ready,start,cancel,operation,chooseDirectory,chooseProton,report})=>{
  const {t}=window.previewI18n;
  let state=null,pending=false,error='',message='',mounted=null,attempted=false;
  const host=()=>document.querySelector('#compatibility-content');
  const element=(tag,text,className)=>{const node=document.createElement(tag);if(text)node.textContent=t(text);if(className)node.className=className;return node;};
  function render() {
    const panel=host();if(!panel)return;mounted=panel;panel.replaceChildren();
    if(!state){const retry=element('button','刷新已安装的 Proton');retry.addEventListener('click',()=>void refresh());panel.append(retry);present();return;}
    function button(parent,id,label,action){const node=element('button',label);node.id=id;node.addEventListener('click',()=>void action().catch(failure));parent.append(node);return node;}
    function field(id,label,value,apply,browse=false){
      const labelNode=element('label',label,'field');const row=element('div',null,'row');
      const input=element('input');input.id=id;input.value=value;input.spellcheck=false;
      input.addEventListener('change',()=>{if(input.value.trim()!==value)void update(apply(input.value.trim()));});
      row.append(input);if(browse)button(row,'browse-'+id,'浏览…',async()=>{const path=await chooseDirectory();if(path)await update(apply(path));});
      labelNode.append(row);panel.append(labelNode);
    }
    function select(id,label,entries,value,apply){
      const labelNode=element('label',label,'field'),node=element('select');node.id=id;
      node.replaceChildren(...entries.map(([key,text])=>new Option(text,key)));node.value=value;
      node.addEventListener('change',()=>{if(node.value)void update(apply(node.value));});labelNode.append(node);panel.append(labelNode);
    }
    function check(id,label,value,apply){const node=element('input');node.type='checkbox';node.id=id;node.checked=value;
      const labelNode=element('label',null,'check');labelNode.append(node,document.createTextNode(t(label)));
      node.addEventListener('change',()=>void update(apply(node.checked)));panel.append(labelNode);return node;}
    select('proton-choice','Proton 版本',state.runtimes.map(x=>[x.script,x.name+(x.ready?'':t('（缺少 Steam Runtime {id}）',{id:x.requiredRuntime}))]),state.protonScript,value=>({protonScript:value}));
    const actions=element('div',null,'row compatibility-actions');panel.append(actions);
    button(actions,'refresh-proton','刷新已安装的 Proton',refresh);
    button(actions,'add-proton','手动添加 Proton…',async()=>{const path=await chooseProton();if(path)await update({protonScript:path});});
    field('steam-root','Steam 安装目录（用于手动添加工具）',state.steamRoot,value=>({steamRoot:value}),true);
    field('compatibility-data','Proton 数据目录（自动在此创建 pfx，建议独立使用）',state.dataDirectory,value=>({dataDirectory:value}),true);
    const registered=check('register-steam','注册为 Steam 兼容性工具（可随时关闭）',state.registered,value=>({registered:value}));
    if(!state.canRegister&&!state.registered){registered.dataset.unavailable='';panel.append(element('p','当前启动方式尚未提供可注册的启动入口。','muted'));}
    if(state.registered&&state.registeredRoot!==state.steamRoot)panel.append(element('p',t('已注册目录：{path}',{path:state.registeredRoot}),'muted'));
    select('steam-prefix-choice','扫描 Steam 国际区兼容环境',state.prefixes.map(x=>[x.path,x.name]),state.steamPrefix,value=>({steamPrefix:value}));
    field('steam-prefix-path','Steam 兼容数据目录',state.steamPrefix,value=>({steamPrefix:value}),true);
    check('use-steam-prefix','国际区使用 Steam 已有 pfx',state.useSteamPrefix,value=>({useSteamPrefix:value}));
    panel.append(element('p','未勾选时三端共用上方 Proton 数据目录。勾选后仅国际区直接读写 Steam 已有环境，沿用其中的本地游戏设置和角色配置；不会复制文件。更换 Proton 可能升级该环境，请勿同时通过 Steam 启动游戏或官方启动器。','muted'));
    const diagnostic=element('div',null,'row compatibility-actions');panel.append(diagnostic);
    button(diagnostic,'diagnose-proton','环境检测',()=>start({}));
    button(diagnostic,'cancel-diagnosis','取消',cancel);
    present();
  }
  function present(){
    const panel=host();if(!panel)return;
    for(const node of panel.querySelectorAll('input,select,button'))node.disabled=!ready()||pending||node.hasAttribute('data-unavailable');
    const stop=panel.querySelector('#cancel-diagnosis');if(stop){stop.hidden=!operation();stop.disabled=operation()?.state!=='running';}
    const feedback=document.querySelector('#compatibility-feedback');
    feedback.className=error?'inline-error':'muted';feedback.textContent=t(error||(pending?'正在处理，请稍候…':operation()?'正在检测环境…':message));
  }
  function failure(ex){error=ex.message||'操作失败，请查看日志。';report(ex.logMessage||'Compatibility operation failed.');present();}
  function refresh(){return read(true);}
  async function read(clearFeedback=false){
    if(!ready()||pending)return;pending=true;attempted=true;error='';if(clearFeedback)message='';present();
    try{state=await call('readCompatibility');if(clearFeedback)report('Installed Proton versions refreshed.',{error:false});render();}catch(ex){failure(ex);}
    finally{pending=false;present();}
  }
  async function update(change){
    if(!ready()||pending){render();return;}pending=true;error='';message='';present();
    try{
      const saved=await call('applyCompatibility',change);
      state={...saved,runtimes:state?.runtimes||[],prefixes:state?.prefixes||[]};message='修改已保存。';render();
      try{state=await call('readCompatibility');}
      catch(ex){error='修改已保存，但刷新失败，请重新载入。';report('Compatibility settings saved, but refreshing installed environments failed.');}
    }
    catch(ex){failure(ex);}
    finally{pending=false;render();}
  }
  return {sync(){if(!host())return;if(mounted!==host())render();if(!state&&!attempted&&!pending&&ready())void read();present();},
    completed(result){error=result.error||'';message=result.state==='completed'?result.result?.message||'操作已完成。':result.state==='cancelled'?'已取消。':'';present();}};
};
