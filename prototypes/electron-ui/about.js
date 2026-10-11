'use strict';
window.aboutTemplate=()=>'<section class="panel settings-panel" data-about-ui><div class="section-heading"><h2>关于</h2></div><div id="about-content"></div></section>';
window.createAboutPage=({call,apply,ready,snapshot,start,clean,cancel,operation,report})=>{
  const {t}=window.previewI18n;
  let info=null,updates=null,mounted=null,timer=null,checkPending=false,manual=false,notified=null,updateDialog=null,error='',started=false,autoChecked=false,lastLoggedUpdate='',cleaningBrowser=false;
  const node=(tag,text,cls)=>{const n=document.createElement(tag);if(text)n.textContent=t(text);if(cls)n.className=cls;return n;};
  function button(parent,label,action,id){const b=node('button',label);if(id)b.id=id;b.addEventListener('click',()=>void Promise.resolve().then(action).catch(failure));parent.append(b);return b;}
  function failure(ex){error=ex.message||'操作失败，请查看日志。';report(ex.logMessage||'Launcher information or update operation failed.');present();}
  async function load(){try{info=await call('readAbout');render();present();}catch(ex){failure(ex);}}
  function render(){
    const host=document.querySelector('#about-content');if(!host)return;mounted=host;host.replaceChildren();
    if(!info){host.append(node('p','正在读取…'));return;}
    host.append(node('h3','FFXIV Super Launcher'));
    const version=node('div',null,'row');version.append(node('span',t('启动器版本：')+info.version));
    button(version,'检查更新',()=>check(true),'launcher-check');if(info.isAppImage)button(version,'添加到应用菜单',async()=>{await call('installDesktop');notice('应用菜单入口已创建。');},'install-desktop');const toggle=node('label',null,'check'),input=node('input');input.type='checkbox';input.id='auto-check-updates';input.checked=snapshot()?.autoCheckUpdates!==false;
    input.addEventListener('change',()=>void apply({autoCheckUpdates:input.checked},true).catch(failure));toggle.append(input,document.createTextNode(t('自动检查更新')));version.append(toggle);host.append(version);
    if(info.notice)host.append(node('p',info.notice,'muted'));
    host.append(node('p',t('维护者：')+info.author,'about-maintainer'));
    const links=node('div',null,'row');host.append(links);
    for(const [key,label] of [['project','GitHub 仓库'],['issues','报告问题'],['author','维护者主页']])button(links,label,()=>call('openAbout',{key}));
    const discord=button(links,'加入 Discord',()=>{});discord.dataset.reserved='';discord.disabled=true;discord.title=t('Discord 社群尚未开放。');
    host.append(node('p','独立维护的社区衍生项目，并非 Square Enix、Shengqu Games、USERJOY 或上游项目的官方发行版本，亦未获其背书。','muted'),node('h3','上游与致谢'));
    const upstream=node('div',null,'row');host.append(upstream);
    for(const key of info.links.filter(key=>key.startsWith('XIV')||key.startsWith('Dalamud')))button(upstream,key,()=>call('openAbout',{key}));
    host.append(node('p','感谢上述项目及 .NET、Proton、Steamworks、Electron、Chromium、Node.js、xdelta3 等组件的贡献者。详细来源和版权声明请查看第三方声明。','muted'));
    host.append(node('p','启动器代码按 GNU GPL v3 分发，不提供担保。原作者版权声明予以保留；第三方组件和图标分别适用各自的许可，不属于统一的 GPL 授权。','muted'));
    const legal=node('div',null,'row');host.append(legal);
    for(const [key,label] of [['license','查看许可证'],['third-party','第三方声明']])button(legal,label,async()=>notice((await call('readNotice',{key})).text,true));
    button(legal,'源码与来源记录',()=>call('openAbout',{key:'sources'}));
    button(legal,'美术素材来源',artSources,'art-sources');
    host.append(node('h3','存储清理','storage-cleanup-heading'));
    const maintenance=node('div',null,'row');host.append(maintenance);
    button(maintenance,'清理下载缓存',()=>clean({kind:'downloads'}),'clean-downloads');
    button(maintenance,'清理日志',()=>clean({kind:'logs'}),'clean-logs');
    button(maintenance,'清理内置浏览器缓存',clearBrowserCache,'clean-browser-cache');
    host.append(node('p','清理启动器管理的缓存和日志，并保留 Dalamud 管理的内容；游戏运行时也可清理，但下载或安装进行中时暂不可清理下载缓存；浏览器缓存清理保留 Cookies 和验证状态。','muted'));
    const status=node('p',null,'muted');status.id='about-feedback';status.setAttribute('role','status');host.append(status);
    button(host,'取消操作',cancel,'cancel-launcher-update');present();
  }
  async function clearBrowserCache(){
    if(!ready()||cleaningBrowser)return;
    cleaningBrowser=true;present();
    try{const result=await call('clearBrowserCache');error='';notice(t('已清理 {count} 个浏览器会话的缓存，HTTP 缓存减少 {size} MiB；跳过 {skipped} 项。Cookie 和验证状态已保留。',{count:result.cleared,size:(result.bytes/1048576).toFixed(1),skipped:result.skipped}));}
    finally{cleaningBrowser=false;present();}
  }
  function artSources(){
    const dialog=node('dialog',null,'art-sources-dialog');dialog.dataset.aboutUi='';
    const title=node('h2','美术素材来源');title.id='art-sources-title';dialog.setAttribute('aria-labelledby',title.id);dialog.append(title);
    const list=node('div',null,'account-options'),status=node('p',null,'inline-error');status.setAttribute('role','status');
    for(const [key,label] of [['icon','启动器图标'],['font-genryu','GenRyu'],['font-cinzel','Cinzel']]){
      const link=button(list,label,async()=>{
        status.textContent='';
        try{await call('openAbout',{key});}
        catch(ex){status.textContent=ex.message||t('操作失败，请查看日志。');report(ex.logMessage||'Could not open artwork source.');}
      });link.className='account-option';
    }
    const actions=node('div',null,'row dialog-actions');button(actions,'关闭',()=>dialog.close());
    dialog.append(list,status,actions);dialog.addEventListener('close',()=>dialog.remove(),{once:true});document.body.append(dialog);dialog.showModal();
  }
  function notice(text,verbatim=false){
    const dialog=node('dialog',null,'notice-dialog');dialog.dataset.aboutUi='';
    const content=node(verbatim?'pre':'p',verbatim?null:text);if(verbatim){content.textContent=text;content.dataset.noTranslate='';}dialog.append(content);
    button(dialog,'关闭',()=>dialog.close());dialog.addEventListener('close',()=>dialog.remove(),{once:true});document.body.append(dialog);dialog.showModal();
  }
  async function check(refresh=false){
    if(checkPending)return;checkPending=true;if(refresh)manual=true;present();
    try{updates=await call('readLauncherUpdate',{refresh});error='';}
    catch(ex){failure(ex);updates={state:'failed'};}
    finally{checkPending=false;present();}
    clearTimeout(timer);
    if(updates?.state==='checking')timer=setTimeout(()=>void check(),400);
    else if(manual&&!updates?.available){notice(updates?.state==='failed'?'检查更新失败，请稍后重试。':updates?.version?'当前已是最新版本。':'暂无可用发行版本。');manual=false;}
    if(updates?.state!=='checking'){
      const key=JSON.stringify(updates);if(key!==lastLoggedUpdate){lastLoggedUpdate=key;
        report(updates?.state==='failed'?'Could not check for launcher updates.':updates?.available?'A launcher update is available.':updates?.version?'The launcher is up to date.':'No launcher release is available.',{error:updates?.state==='failed'});
      }
    }
    maybeNotify();
  }
  function maybeNotify(){
    if(document.hidden||!updates?.available||!manual&&(snapshot()?.autoCheckUpdates===false||notified===updates.release))return;
    if(!ready()||!snapshot()?.setupComplete||document.querySelector('dialog[open]')){clearTimeout(timer);timer=setTimeout(maybeNotify,1000);return;}
    manual=false;notified=updates.release;
    const selected={...updates};updateDialog=node('dialog',null,'notice-dialog');updateDialog.dataset.aboutUi='';
    updateDialog.append(node('h2','启动器更新'),node('p',t('发现新版本：')+selected.version),node('p',t('当前版本：')+info?.version));
    button(updateDialog,'查看发行说明',()=>call('openLauncherRelease',{release:selected.release}));
    if(selected.canInstall){updateDialog.append(node('p','请先退出游戏。更新安装完成后将自动打开新版启动器。'));
      button(updateDialog,'下载并更新',async()=>{updateDialog.close();await start({release:selected.release});});}
    button(updateDialog,'稍后',()=>updateDialog.close());updateDialog.addEventListener('close',()=>{updateDialog.remove();updateDialog=null;},{once:true});document.body.append(updateDialog);updateDialog.showModal();
  }
  function present(){
    const host=document.querySelector('#about-content');
    if(host){for(const b of host.querySelectorAll('button'))b.disabled=cleaningBrowser||!ready()||b.hasAttribute('data-reserved');
      const check=host.querySelector('#launcher-check');if(check)check.disabled ||= checkPending||updates?.state==='checking';
      const stop=host.querySelector('#cancel-launcher-update');if(stop){stop.hidden=!operation();stop.disabled=operation()?.state!=='running';}
      const status=host.querySelector('#about-feedback');if(status)status.textContent=t(error||(updates?.state==='checking'?'正在检查启动器更新…':operation()?'正在处理，请稍候…':''));}
    let path=document.querySelector('#diagnostic-path');
    if(!path){path=node('div',null,'diagnostic-path');path.id='diagnostic-path';document.querySelector('.logs').before(path);}
    path.hidden=!info;const label=info?t('完整诊断日志：')+' '+info.diagnosticPath:'';if(path.textContent!==label)path.textContent=label;
  }
  document.addEventListener('visibilitychange',()=>{if(!document.hidden)maybeNotify();});
  function autoCheck(){if(started&&!autoChecked&&ready()&&snapshot()?.setupComplete&&snapshot()?.autoCheckUpdates!==false){autoChecked=true;void check();}}
  return {start(){started=true;void load();autoCheck();},sync(){if(mounted!==document.querySelector('#about-content'))render();const toggle=document.querySelector('#auto-check-updates');if(toggle){toggle.checked=snapshot()?.autoCheckUpdates!==false;toggle.disabled=!ready();}present();autoCheck();},
    completed(result){error=result.error||'';if(result.state==='completed'&&Number.isInteger(result.result?.deleted)){const r=result.result;notice(t('已清理 {count} 个文件，释放 {size} MiB；跳过 {skipped} 项。',{count:r.deleted,size:(r.bytes/1048576).toFixed(1),skipped:r.skipped}));}present();}};
};
