'use strict';
// Plugin data stays local to this page controller; a read/update failure does not disable login.
window.createPluginPage = ({call,ready,region,start,cancel,operation,report}) => {
  const {t}=window.previewI18n;
  const caches=new Map();
  let host=null,hostRegion=null,dialog=null;
  const names={ffxiv_cn:'中国区',ffxiv_tc:'繁中区',ffxiv:'国际区'};
  const labels={'third-party-repositories':'同步第三方仓库','installed-plugins':'同步已安装插件','plugin-settings':'同步插件设置'};
  function state(key=region()) {
    if(!caches.has(key))caches.set(key,{list:null,loading:false,repositoryLoading:false,initialized:false,error:'',message:'',plan:null,repositories:null,repositoryFeedback:{error:'',message:''},operationTarget:'plugins'});
    return caches.get(key);
  }

  function node(tag,text='',className='') {const el=document.createElement(tag);if(text)el.textContent=text;if(className)el.className=className;return el;}
  function feedback(key,target){const s=state(key);return target==='repositories'?s.repositoryFeedback:s;}
  function error(err,target='plugins',key=region()) {const f=feedback(key,target);f.error=err.message||t('操作失败，请查看日志。');f.message='';report(err.logMessage||'Plugin or repository operation failed.',{expand:false});if(region()===key){render();if(dialog?.open&&dialog.dataset.feedbackTarget===target){let message=dialog.querySelector('.inline-error');if(!message){message=node('p','','inline-error');message.setAttribute('role','alert');dialog.append(message);}message.textContent=f.error;}}}
  function button(text,action,{disabled=false,primary=false,target='plugins'}={}) {
    const b=node('button',t(text),primary?'primary':'');b.type='button';b.disabled=disabled;
    const key=region();b.addEventListener('click',()=>void Promise.resolve().then(action).catch(err=>error(err,target,key)));return b;
  }
  function closeDialog() {if(dialog){const old=dialog;dialog=null;old.close();old.remove();}}
  function openDialog(title,target='plugins') {
    closeDialog();dialog=node('dialog','','plugin-dialog');dialog.dataset.pluginUi='';dialog.dataset.feedbackTarget=target;dialog.dataset.noTranslate='';
    const heading=node('h2',t(title));heading.id='plugin-dialog-title';dialog.setAttribute('aria-labelledby',heading.id);dialog.append(heading);
    dialog.addEventListener('close',()=>{if(dialog&&!dialog.open)closeDialog();});document.body.append(dialog);return dialog;
  }
  function clearFeedback(key,target) {
    const f=feedback(key,target);f.error='';f.message='';delete f.url;
  }
  async function load(key=region(),clear=false) {
    const s=state(key);if(s.loading||!ready())return;
    s.loading=true;if(clear)clearFeedback(key,'plugins');render();
    try {
      const list=await call('readPlugins',{region:key});s.list=list;
      if(clear)report('Plugin status refreshed.',{error:false});
    } catch(err){s.list=null;error(err,'plugins',key);}
    finally{s.loading=false;if(region()===key)render();}
  }
  async function loadRepositories(key=region(),clear=false) {
    const s=state(key);if(s.repositoryLoading||!ready())return;
    s.repositoryLoading=true;if(clear)clearFeedback(key,'repositories');render();
    try {
      s.repositories=await call('readPluginRepositories',{region:key});
      if(clear)report('Repository status refreshed.',{error:false});
    } catch(err){error(err,'repositories',key);}
    finally{s.repositoryLoading=false;if(region()===key)render();}
  }
  function refreshPlugins(){return load(region(),true);}
  function refreshRepositories(){return loadRepositories(region(),true);}
  async function loadPage(key=region()) {
    state(key).initialized=true;
    await Promise.all([load(key),loadRepositories(key)]);
  }
  async function run(request) {
    const s=state();s.operationTarget=request.kind==='check-repository'||request.content==='third-party-repositories'?'repositories':'plugins';const f=feedback(region(),s.operationTarget);f.error='';f.message='';if(s.operationTarget==='repositories')f.url=request.kind==='check-repository'?request.url:null;if(s.operationTarget==='plugins')s.plan=null;closeDialog();
    await start({region:region(),...request});
  }
  function render() {
    const body=document.querySelector('[data-plugin-body]');if(!body)return;
    host=body;hostRegion=region();
    body.dataset.noTranslate='';body.replaceChildren();
    const key=region(),s=state(key);const busy=!ready()||s.loading;
    body.append(node('p',t('Dalamud 版本：{version}',{version:s.list?.dalamudVersion||t('未安装或未读取')}),'muted'));
    const updates=node('div','','row');
    updates.append(button('检查并更新 Dalamud',()=>{return run({kind:'dalamud-update'});},{disabled:busy}),
      button('检查并更新插件',()=>{return run({kind:'check-updates'});},{disabled:busy}));body.append(updates);
    const actions=node('div','','row plugin-actions');
    actions.append(button('刷新插件启用状态',refreshPlugins,{disabled:busy}));
    body.append(actions);
    if(s.loading)body.append(node('p',t('正在读取插件列表…'),'muted'));
    if(s.error)body.append(node('p',s.error,'inline-error'));
    if(s.message)body.append(node('p',s.message,'muted'));
    const running=operation();
    if(running&&s.operationTarget==='plugins') {
      const status=node('p',t(running.state==='cancelling'?'正在取消…':'正在处理，请稍候…'),'muted');status.setAttribute('role','status');body.append(status);
      body.append(button('取消操作',cancel,{disabled:running.state!=='running'}));
    }
    if(s.plan)body.append(button('选择更新插件',()=>showPlan(s.plan),{disabled:busy}));
    if(s.list) {
      body.append(node('p',t('勾选启用、取消勾选禁用，更改后立即保存，下次启动游戏生效。横线表示受插件集合条件控制；不修改则保留原规则。'),'muted'));
      body.append(node('p',t('手动启用会加入默认集合；禁用会关闭此插件在所有集合中的启用项。不会删除插件或插件设置。'),'muted'));
      if(s.list.problemCount)body.append(node('p',t('有 {count} 个插件无法读取，其余插件仍可管理。',{count:s.list.problemCount}),'inline-error'));
      if(!s.list.plugins.length)body.append(node('p',t('当前游戏区服尚未安装插件。'),'muted'));
      for(const p of s.list.plugins) {
        const label=node('label','','check plugin-row');const check=node('input');check.type='checkbox';check.dataset.pluginId=p.id;
        check.checked=p.enabled===true;check.indeterminate=p.enabled===null;check.disabled=busy||p.scheduledForDeletion;
        check.addEventListener('change',()=>void run({kind:'apply',revision:s.list.revision,states:{[p.id]:check.checked}}).catch(err=>error(err,'plugins',key)));
        const caption=node('span',p.name);caption.append(node('span',` · ${p.version}`,'muted'));label.append(check,caption);
        if(p.scheduledForDeletion)label.append(node('span',t('已标记删除'),'muted'));
        else if(p.enabled===null)label.append(node('span',t('由插件集合控制'),'muted'));
        body.append(label);
      }
    }
    const sync=node('div','','row plugin-actions');for(const [content,label] of Object.entries(labels).filter(([key])=>key!=='third-party-repositories'))sync.append(button(label,()=>chooseSync(content),{disabled:busy}));body.append(sync);
    renderRepositories(s,!ready()||s.repositoryLoading);
  }
  function renderRepositories(s,busy) {
    const key=region(),repoButton=(text,action,options={})=>button(text,action,{...options,target:'repositories'});
    const panel=document.querySelector('[data-repository-body]');if(!panel)return;
    panel.dataset.noTranslate='';panel.replaceChildren(node('h2',t('第三方插件仓库')));
    const f=s.repositoryFeedback;
    if(f.error&&!f.url)panel.append(node('p',f.error,'inline-error'));
    if(f.message&&!f.url)panel.append(node('p',f.message,'muted'));
    const running=operation();if(running&&s.operationTarget==='repositories'){panel.append(node('p',t(running.state==='cancelling'?'正在取消…':'正在处理，请稍候…'),'muted'),repoButton('取消操作',cancel,{disabled:running.state!=='running'}));}
    if(!s.repositories){panel.append(repoButton('刷新仓库状态',refreshRepositories,{disabled:busy}));return;}
    const data=s.repositories;
    const save=async(entries,presetsEnabled=data.presetsEnabled)=>{
      const result=await call('savePluginRepositories',{region:key,revision:data.revision,entries,presetsEnabled});
      state(key).repositoryFeedback={error:'',message:t('操作已完成。')};state(key).repositories=result;state(key).plan=null;closeDialog();render();
    };
    const clearRepositoryFeedback=()=>{clearFeedback(key,'repositories');render();};
    const edit=(index=-1)=>{
      clearRepositoryFeedback();const d=openDialog(index<0?'添加仓库':'编辑仓库','repositories');
      const label=node('label',t('仓库地址'),'field'),input=node('input');input.type='url';input.value=index<0?'':data.entries[index].url;label.append(input);d.append(label);
      input.addEventListener('change',()=>{
        const entries=data.entries.map(x=>({...x}));const value={url:input.value.trim(),isEnabled:index<0?true:entries[index].isEnabled};
        if(index<0)entries.push(value);else entries[index]=value;
        input.disabled=true;void save(entries).catch(err=>{input.disabled=false;error(err,'repositories',key);});
      });
      input.addEventListener('keydown',event=>{if(event.key==='Enter'){event.preventDefault();input.blur();}});
      const row=node('div','','row dialog-actions');row.append(repoButton('关闭',closeDialog));d.append(row);d.showModal();input.focus();
    };
    const controls=node('div','','row');controls.append(repoButton('添加仓库',()=>edit(),{disabled:busy}),repoButton('刷新仓库状态',refreshRepositories,{disabled:busy}),repoButton('同步第三方仓库',()=>chooseSync('third-party-repositories'),{disabled:busy}));panel.append(controls);
    panel.append(node('p',t('仓库设置与游戏内 Dalamud 共用，仅影响当前游戏区服。'),'muted'));
    if(data.presets.length){
      const label=node('label','','check'),check=node('input');check.type='checkbox';check.checked=data.presetsEnabled;check.disabled=busy;
      check.addEventListener('change',()=>void save(data.entries,check.checked).catch(err=>error(err,'repositories',key)));label.append(check,document.createTextNode(t('启用国服预置仓库')));panel.append(label);
      panel.append(node('p',t('预置仓库由国服 Dalamud 提供，开启时不受下方同地址条目的停用或删除影响。'),'muted'));
      const details=node('details');details.append(node('summary',t('查看预置仓库')));
      for(const url of data.presets)details.append(node('p',url,'plugin-repository'));panel.append(details);
    }
    for(const [index,entry] of data.entries.entries()){
      const box=node('div','','repository-entry'),label=node('label','','check'),check=node('input');check.type='checkbox';check.checked=entry.isEnabled;check.disabled=busy;
      check.addEventListener('change',()=>void save(data.entries.map((x,i)=>i===index?{...x,isEnabled:check.checked}:x)).catch(err=>error(err,'repositories',key)));
      label.append(check,document.createTextNode(entry.url));box.append(label);
      const row=node('div','','row');row.append(repoButton('编辑仓库',()=>edit(index),{disabled:busy}),repoButton('检查连接',()=>{return run({kind:'check-repository',url:entry.url});},{disabled:busy}),repoButton('删除仓库',()=>{
        clearRepositoryFeedback();const d=openDialog('删除仓库','repositories');d.append(node('p',entry.url),node('p',t('仅移除仓库地址，不删除已安装插件。'),'muted'));
        const actions=node('div','','row dialog-actions');actions.append(repoButton('删除仓库',()=>save(data.entries.filter((_,i)=>i!==index))),repoButton('取消',closeDialog));d.append(actions);d.showModal();
      },{disabled:busy}));box.append(row);
      if(f.url===entry.url&&(f.error||f.message)){const result=node('p',f.error||f.message,f.error?'inline-error':'muted');result.dataset.repositoryResult='';result.setAttribute('role','status');box.append(result);}
      panel.append(box);
    }
    if(!data.entries.length)panel.append(node('p',t('尚未添加第三方仓库。'),'muted'));
  }
  function sync() {
    const body=document.querySelector('[data-plugin-body]');
    if(hostRegion&&hostRegion!==region())closeDialog();
    if(!body){host=null;return;}
    // Load explicitly on first entry, not on unrelated polling or rendering.
    const s=state();if(!s.initialized&&ready())void loadPage();
    else render();
  }
  async function chooseSync(content) {
    const key=region();const sources=await call('pluginSources',{content});
    if(region()!==key||!ready())return;
    const target=content==='third-party-repositories'?'repositories':'plugins';const d=openDialog(labels[content],target);
    d.append(node('p',t(content==='third-party-repositories'?'选择来源及目标游戏区服。仅替换目标的第三方仓库列表及启用状态，始终保留目标主仓库、预置仓库开关和其他 Dalamud 设置。原配置会保留备份。':'选择来源游戏区服及需要覆盖的目标游戏区服。仅替换勾选目标的对应内容，目标中来源没有的文件也会被移除。被覆盖的内容会保留备份。'),'muted'));
    if(content==='installed-plugins')d.append(node('p',t('请勿将繁中区的插件 DLL 与中国区或国际区的版本互相覆盖。同步插件文件不代表它们兼容目标游戏区服。'),'muted'));
    const label=node('label',t('以哪个游戏区服为基准'),'field'),select=node('select');
    for(const source of sources){const option=node('option',t(names[source.region])+(source.hasSource?'':t('（无来源数据）')));option.value=source.region;option.disabled=!source.hasSource;select.append(option);}
    select.value=sources.find(x=>x.hasSource&&x.region===key)?.region||sources.find(x=>x.hasSource)?.region||'';label.append(select);d.append(label,node('p',t('覆盖目标：')));
    const targets=[];
    const apply=button('同步到所选游戏区服',()=>run({kind:'sync',content,source:select.value,targets:targets.filter(x=>x.checked&&!x.disabled).map(x=>x.value)}),{primary:true,disabled:true,target});
    const changed=()=>{apply.disabled=!select.value||!targets.some(x=>x.checked&&!x.disabled);};
    for(const [id,name] of Object.entries(names)){const row=node('label','','check sync-target'),check=node('input');check.type='checkbox';check.value=id;check.addEventListener('change',changed);targets.push(check);row.append(check,document.createTextNode(t(name)));d.append(row);}
    const resetTargets=()=>{for(const check of targets){check.checked=false;check.disabled=!select.value||check.value===select.value;}changed();};select.addEventListener('change',resetTargets);resetTargets();
    if(!select.value)d.append(node('p',t('各游戏区服均无此项数据，请先在来源游戏区服生成设置或安装插件。'),'muted'));
    const row=node('div','','row');row.append(apply,button('取消',closeDialog));d.append(row);d.showModal();
  }
  function showPlan(plan) {
    const d=openDialog('选择更新插件');d.append(node('p',t('可更新插件：{count}。只从各插件原安装仓库更新。',{count:plan.updates.length}),'muted'));
    const checks=[];
    const apply=button('更新所选插件',()=>run({kind:'install-updates',planId:plan.planId,plugins:checks.filter(x=>x.checked).map(x=>x.value)}),{primary:true,disabled:true});
    for(const item of plan.updates){
      const label=node('label','','check plugin-row'),check=node('input');check.type='checkbox';check.value=item.id;
      check.addEventListener('change',()=>{apply.disabled=!checks.some(x=>x.checked);});checks.push(check);
      const caption=node('span',`${item.name}：${item.fromVersion} → ${item.toVersion}`);caption.append(node('small',item.repository,'plugin-repository'));label.append(check,caption);d.append(label);
    }
    for(const problem of plan.problems)d.append(node('p',problem,'inline-error'));
    const row=node('div','','row');row.append(button('全选',()=>{checks.forEach(x=>{x.checked=true;});apply.disabled=!checks.length;},{disabled:!checks.length}),apply,button('关闭',closeDialog));d.append(row);d.showModal();
  }
  async function completed(view) {
    const s=state(view.region),f=feedback(view.region,s.operationTarget);f.error=view.state==='failed'?(view.error||t('操作失败，请查看日志。')):'';f.message=view.state==='completed'?t('操作已完成。'):view.state==='failed'?'':t('已取消。');
    if(view.result?.kind==='repository-checked')f.message=t('仓库可访问，共 {count} 个插件。',{count:view.result.count});
    if(view.result?.kind==='plugin-updates')s.plan=view.result.plan;
    else if(view.state==='completed'&&view.result?.kind!=='repository-checked') {
      const target=s.operationTarget;
      await Promise.all((view.result?.targets||[view.region]).map(async key=>{
        state(key).plan=null;
        if(target==='repositories')await loadRepositories(key);
        else await load(key);
      }));
    }
    if(view.result?.backupDirectory)f.message+=' '+t('原数据备份：')+view.result.backupDirectory;
    sync();if(view.state==='completed'&&s.plan&&host&&region()===view.region)showPlan(s.plan);
  }
  window.addEventListener('focus',()=>{
    if(!document.querySelector('[data-plugin-body]')||!ready())return;
    const s=state();if(s.loading||s.repositoryLoading||dialog?.open)return;
    // Returning from the game: reload the saved states.
    void loadPage();
  });
  return Object.freeze({sync,completed});
};
