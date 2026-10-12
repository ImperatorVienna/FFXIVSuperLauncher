'use strict';
const pages = ['游戏', '登录凭据管理', 'Steam 和 Proton 设置', '语言和区服', 'Dalamud 和插件', '关于'];
const regions = [{id:'ffxiv_cn',name:'中国区'},{id:'ffxiv_tc',name:'繁中区'},{id:'ffxiv',name:'国际区'}];
const state = {page:0, region:0, cdkey:'NA', euLanguage:'English(UK)', clientLanguage:'English', uiLanguage:'简体中文', timer:null, challenge:null, sequence:0, logs:0};
const {t, apply:localize} = window.previewI18n;
const $ = selector => document.querySelector(selector);
const escapeHtml = value => String(value).replace(/[&<>"']/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
const options = (values, selected) => values.map(v=>`<option value="${escapeHtml(v)}" ${window.previewI18n.languages[v]?`lang="${window.previewI18n.languages[v]}" data-no-translate`:""} ${v===selected?'selected':''}>${escapeHtml(v)}</option>`).join('');
$('nav').innerHTML = pages.map((p,i)=>`<button data-page="${i}">${p}</button>`).join('');
function log(message, {progress=false, error=false}={}) {
  $('.empty-log')?.remove();
  let line = progress ? $('#download-line') : null;
  if (!line) {line=document.createElement('div');if(progress)line.id='download-line';$('#log').append(line);state.logs++;}
  line.classList.toggle('log-error',error);
  line.textContent=`[${new Date().toLocaleTimeString('en-GB')}] [${error?'ERROR':'INFO'}] [${regions[state.region].id}] ${message}`;
  updateLogCount();return line.textContent;
}
function updateLogCount(){$('#log-count').textContent=t(state.logs===1?'1 条':'{count} 条',{count:state.logs});}
function clearLog(){state.logs=0;$('#log').innerHTML='<p class="empty-log">暂无日志</p>';updateLogCount();localize($('#log'));}
function closeVerification(){state.challenge=null;const p=$('#verification');if(p){p.hidden=true;p.replaceChildren();}}
function stop(){clearInterval(state.timer);state.timer=null;state.sequence++;closeVerification();}
function sampleNotice(){log('Preview only: this action is not connected to the backend.');}
function newsList(items,type,kind){return `<section class="news-section" data-news-kind="${kind}"><div class="section-heading"><h2>${type}</h2><span class="sample-label">示例内容</span></div><ul class="news">${items.map(([tag,title],i)=>`<li><span class="tag ${tag==='重要'?'important':''}">[${tag}]</span><button class="news-title" data-demo title="${title}">${title}</button><time>10 / ${String(9-i).padStart(2,'0')}</time></li>`).join('')}</ul></section>`;}
function regionLink(){return `<button class="region-link" data-action="region-settings" title="前往语言和区服设置"><span data-no-translate>${escapeHtml(t('游戏区服：{region}',{region:t(regions[state.region].name)}))}</span></button>`;}
function game(){
  const news=[['维护','游戏服务器维护公告'],['更新','版本更新内容一览'],['重要','关于账户安全的提醒'],['通知','服务器网络维护安排'],['公告','近期游戏问题处理进展']];
  const events=[['活动','近期游戏活动一览'],['联动','联动活动专题'],['活动','节日特别活动'],['资讯','冒险者社区资讯'],['商城','官方商城新品上架']];
  return `<div class="grid"><div class="news-column">${newsList(news,'官方公告','notices')}${newsList(events,'活动资讯','activities')}<div class="links"><button data-demo data-action="open-official" data-official-item="home">游戏官网</button><button data-action="status" data-official-item="status">服务器状态</button><button data-demo data-action="open-official" data-official-item="shop">官方商城</button><button data-demo data-action="open-official" data-official-item="${state.region===2?'mog':'subscription'}">${state.region===2?'Mog Station':'游戏时长充值'}</button></div></div><div class="login-column"><section class="panel login-card"><div class="section-heading"><h2>开始游戏</h2>${regionLink()}</div><label class="field">登录名<div class="row"><input id="login-name" value="DemoAccount" placeholder="输入登录名" autocomplete="off" aria-label="登录名"><button data-action="accounts">选择登录名</button></div></label>${state.region===0?'<label class="field">大区<div class="row"><select id="china-area" aria-label="大区"><option>陆行鸟</option><option>莫古力</option></select><div class="travel-actions"><button data-demo data-action="travel">超域传送</button><button class="integration-only" hidden data-action="recover-game-auth">重新授权游戏内跨区</button></div></div></label>':'<label class="check"><input type="checkbox" id="auto-otp">使用 2FA 密钥自动生成验证码</label>'}${state.region===2?'<label class="check"><input type="checkbox" id="steam-account">我的 CDKey 来自 Steam</label><label class="check integration-only" hidden><input id="free-trial" type="checkbox">试玩账号</label>':''}<label class="check"><input id="enable-dalamud" type="checkbox" checked>启用 Dalamud</label><label class="check integration-only" hidden><input type="checkbox" id="no-plugins">本次禁用所有插件（故障排查）</label><div class="actions"><button data-action="download">检查并更新游戏</button><button class="primary" data-action="login">登录并启动</button></div></section><section id="verification" class="panel verification" aria-labelledby="verification-title" hidden></section></div></div>`;
}
function regionSettings(){return `<section class="panel settings-panel"><div class="section-heading"><h2>语言和区服</h2></div><label class="field">启动器界面语言<div class="row"><select id="ui-language">${options(['简体中文','繁體中文','日本語','English'],state.uiLanguage)}</select></div></label><div class="settings-pair"><label class="field">游戏区服<select id="region">${regions.map((r,i)=>`<option value="${i}" ${i===state.region?'selected':''}>${r.name}</option>`).join('')}</select></label>${state.region===2?`<label class="field">国际区客户端语言<div class="row"><select id="client-language">${options(['日本語','English','Français','Deutsch'],state.clientLanguage)}</select></div></label>`:'<div></div>'}</div>${state.region===2?`<div class="settings-pair"><label class="field">账户 CDKey 版本<select id="cdkey">${options(['JP','NA','EU'],state.cdkey)}</select></label>${state.cdkey==='EU'?`<label class="field">官网及商城偏好语言<select id="eu-language">${options(['English(UK)','Français','Deutsch'],state.euLanguage)}</select></label>`:'<div></div>'}</div>`:''}<div class="divider"></div>${regions.map(r=>`<label class="field">${r.name}客户端目录（包含 game 文件夹）<div class="row"><input data-client-path="${r.id}" placeholder="选择客户端目录" aria-label="${r.name}客户端目录"><button data-demo data-action="browse-client" data-region="${r.id}">浏览</button></div></label><p class="version" data-client-version="${r.id}">客户端版本：未读取 · 原型预览</p>`).join('')}<p class="setting-hint">本预览中的选项只影响当前页面，不写入启动器配置。真实路径读取尚未接入。</p></section>`;}
function credentials(){return `<section class="panel settings-panel"><div class="section-heading"><h2>登录凭据管理</h2>${regionLink()}</div><label class="field">已保存账户<select id="credential-account"><option>DemoAccount · 示例账户</option></select></label><div class="row"><button data-demo data-action="add-account">添加账户</button><button data-demo data-action="delete-account">删除账户</button></div><label class="field">登录名<input id="credential-name" value="DemoAccount" autocomplete="off"></label>${state.region?'<label class="field">密码<input id="credential-password" type="password" autocomplete="off" placeholder="请勿在原型中输入真实密码"></label><label class="field">2FA 密钥（可选）<input id="credential-secret" type="password" autocomplete="off" placeholder="请勿输入真实密钥"></label>':'<p class="muted">扫码登录后自动保存快捷登录凭据。</p>'}<div class="row"><button data-action="discard-account">放弃修改</button><button class="primary" data-demo data-action="save-account">保存凭据</button>${state.region?'<button data-action="local-otp">查看 2FA 验证码</button>':''}</div></section>`;}
function plugins(){return `<div class="plugin-layout"><section class="panel settings-panel" data-plugin-ui><div class="section-heading"><h2>Dalamud 和插件</h2>${regionLink()}</div><div data-plugin-body><p class="muted">Dalamud 版本：未读取 · 原型预览</p><div class="row"><button data-demo>检查并更新 Dalamud</button><button data-demo>检查并更新插件</button></div>${['示例插件 A','示例插件 B','示例插件 C'].map((p,i)=>`<label class="check plugin-row"><input type="checkbox" ${i!==1?'checked':''}>${p}<span class="muted">示例数据</span></label>`).join('')}<div class="row"><button data-action="render">刷新插件启用状态</button></div><div class="row plugin-actions"><button data-demo>同步已安装插件</button><button data-demo>同步插件设置</button></div></div></section><section class="panel settings-panel repository-panel" data-plugin-ui data-repository-body></section></div>`;}
function render(){
  const restore=window.uiState.capture($('#page'));
  stop();
  document.querySelectorAll('nav button').forEach((b,i)=>{if(i===state.page)b.setAttribute('aria-current','page');else b.removeAttribute('aria-current');});
  const views=[game,credentials,()=>(window.launcherBridge||new URL(location.href).searchParams.get('integration')==='1')?window.compatibilityTemplate():(()=>'<section class="panel settings-panel"><div class="section-heading"><h2>Steam 和 Proton 设置</h2></div><label class="field">Proton 版本<select id="proton-choice"><option>Proton · 示例版本</option></select></label><button data-demo data-action="refresh-proton">刷新已安装的 Proton</button><label class="check"><input type="checkbox">适配从 Steam 启动</label><p class="muted">原型布局，尚未连接系统设置。</p></section>')(),regionSettings,plugins,()=>(window.launcherBridge||new URL(location.href).searchParams.get('integration')==='1')?window.aboutTemplate():(()=>'<section class="panel settings-panel"><div class="section-heading"><h2>关于</h2></div><h3>FFXIV Super Launcher</h3><p>ImperatorVienna</p><p class="muted">前端交互预览 · 非发行版本</p><div class="row"><button data-demo>GitHub 仓库</button><button data-demo>反馈问题</button><button disabled>加入 Discord</button></div><p class="muted divider" style="padding-top:20px">正式版将保留完整的许可证、第三方声明和诊断日志路径。原型未读取真实日志。</p></section>')()];
  $('#page').innerHTML=views[state.page]();
  localize();updateLogCount();window.frontendConnection.present();restore();
}
function ensureGame(){if(state.page!==0){state.page=0;render();}}
function showVerification(kind){
  ensureGame();stop();state.challenge=kind;
  const title={otp:'手动验证码',qr:'扫码验证','mobile-confirmation':'手机滑动验证'};
  const body={
    otp:'<p class="muted">请输入验证器中的当前验证码。</p><label class="field">验证码<input id="otp" inputmode="numeric" autocomplete="off" maxlength="6" placeholder="六位验证码" aria-label="六位验证码"></label><p class="muted">验证码仅用于本次登录 · 此处为模拟验证</p><div id="otp-error" class="inline-error" role="alert"></div><div class="row"><button class="primary" data-action="submit">确认</button><button data-action="cancel">取消登录</button></div>',
    qr:'<div class="qr-layout"><div class="qr-placeholder" role="img" aria-label="二维码占位，不可扫描"><span>二维码区域</span><small>原型占位 · 不可扫描</small></div><div><p>请使用叨鱼扫码并确认登录</p><p class="muted">等待手机确认 · 模拟状态</p><div class="row"><button class="primary" data-action="submit">模拟确认</button><button data-action="cancel">取消登录</button></div></div></div>',
    'mobile-confirmation':'<div class="mobile-confirmation"><p>请在手机上打开叨鱼 App，完成滑动验证。</p><div class="mobile-code"><span class="muted">验证码（示例）</span><output aria-label="五位数验证码（示例）">12345</output></div><div class="mobile-actions"><span class="muted" role="status">等待手机确认 · 模拟状态</span><button data-action="cancel">取消登录</button></div></div>'
  };
  const p=$('#verification');p.classList.remove('error');p.innerHTML=`<div class="section-heading"><h2 id="verification-title">登录验证</h2><span class="badge">${title[kind]}</span></div><div class="verification-body">${body[kind]}</div>`;p.hidden=false;localize(p);
  // The reserved challenge area and focused input must not move the page.
  if(kind==='otp')$('#otp').focus({preventScroll:true});
}
function submitVerification(){
  if(state.challenge!=='otp'&&state.challenge!=='qr')return;
  if(state.challenge==='otp'&&!/^\d{6}$/.test($('#otp').value)){$('#otp-error').textContent=t('请输入六位数字。');return;}
  closeVerification();log('Verification simulation completed. No sign-in request was sent.');
}
function showError(){ensureGame();stop();const p=$('#verification');p.innerHTML='<div class="section-heading"><h2 id="verification-title">登录未完成</h2></div><p class="verification-result">连接超时，请检查网络后重试。</p><div class="row"><button class="primary" data-action="login">重试（模拟）</button><button data-action="cancel">关闭</button></div>';p.classList.add('error');p.hidden=false;localize(p);log('Failed: connection timed out. (Simulation)',{error:true});}
function download(){stop();$('#download-line')?.removeAttribute('id');let n=0;const generation=state.sequence;log('patch-example.patch · 0%',{progress:true});state.timer=setInterval(()=>{if(generation!==state.sequence)return;n+=5;log(`patch-example.patch · ${n}%`,{progress:true});if(n===100){clearInterval(state.timer);state.timer=null;log('Download simulation completed.');}},160);}
document.addEventListener('click',e=>{
  const b=e.target.closest('button');if(!b)return;
  if(b.closest('.preview-tools')){$('.preview-tools').open=false;}
  if(b.dataset.page!==undefined){state.page=Number(b.dataset.page);render();return;}
  if(b.hasAttribute('data-demo')){sampleNotice();return;}
  switch(b.dataset.action){
    case 'region-settings':state.page=3;render();break;
    case 'accounts':window.uiState.showDialog($('#account-dialog'));break;
    case 'close-dialog':$('#account-dialog').close();break;
    case 'select-account':$('#login-name').value='DemoAccount';$('#account-dialog').close();break;
    case 'status':window.open(window.serverStatus.url(regions[state.region].id,state.cdkey,state.euLanguage),'_blank','noopener,noreferrer');break;
    case 'discard-account':case 'render':render();break;
    case 'reset':render();clearLog();break;
    case 'clear-log':clearLog();break;
    case 'login':if(state.region!==0&&$('#auto-otp')?.checked){log('Automatic OTP selected. No stored credentials are read in this preview.');}else showVerification(state.region===0?'qr':'otp');break;
    case 'otp':case 'qr':case 'mobile-confirmation':showVerification(b.dataset.action);break;
    case 'submit':submitVerification();break;
    case 'cancel':stop();log('Cancelled.');break;
    case 'error':showError();break;
    case 'download':download();break;
  }
});
document.addEventListener('change',e=>{
  if(e.target.id==='ui-language'){state.uiLanguage=e.target.value;window.previewI18n.set(state.uiLanguage);render();}
  if(e.target.id==='client-language')state.clientLanguage=e.target.value;
  if(e.target.id==='region'){state.region=Number(e.target.value);render();}
  if(e.target.id==='cdkey'){state.cdkey=e.target.value;render();}
  if(e.target.id==='eu-language')state.euLanguage=e.target.value;
});
document.addEventListener('input',e=>{
  if(e.target.id==='otp'){
    const error=$('#otp-error');
    if(error)error.textContent='';
  }
});
document.addEventListener('keydown',e=>{if(e.key==='Escape'&&state.challenge){stop();log('Cancelled.');}if(e.key==='Enter'&&e.target.id==='otp')submitVerification();});
render();

let snapshotShape=null;
void window.frontendConnection.start(snapshot=>{
  const language=Object.entries(window.previewI18n.languages).find(([,id])=>id===snapshot.language)?.[0] || 'English';
  state.uiLanguage=language;
  state.region=Math.max(0,regions.findIndex(region=>region.id===snapshot.selectedRegion));
  const global=snapshot.regions.find(region=>region.id==='ffxiv');
  if(global?.cdKeyVersion)state.cdkey=global.cdKeyVersion;
  if(global?.euShopLanguage)state.euLanguage=({'en-gb':'English(UK)','fr-fr':'Français','de-de':'Deutsch'})[global.euShopLanguage];
  if(global?.clientLanguage!==null && global?.clientLanguage!==undefined)state.clientLanguage=['日本語','English','Deutsch','Français'][global.clientLanguage];
  window.previewI18n.set(language);
  const shape=JSON.stringify([language,snapshot.selectedRegion,state.cdkey==='EU']);
  if(snapshotShape!==shape){snapshotShape=shape;render();}
  else {for(const [id,value] of [['ui-language',state.uiLanguage],['region',state.region],['cdkey',state.cdkey],['eu-language',state.euLanguage],['client-language',state.clientLanguage]]){const input=$('#'+id);if(input)input.value=value;}window.frontendConnection.present();}
},(message,{error=true}={})=>log(message,{error}));
