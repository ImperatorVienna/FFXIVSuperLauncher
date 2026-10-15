'use strict';
const {bindExitKeys}=require('./window-ui.cjs');
// One isolated embedded web view. Only the main process sees the CAPTCHA token;
// account credentials stay in .NET and the remote page has no preload/launcher IPC.
function createCaptchaView({window,backend,WebContentsView,View,session,onQuit,commandTimeout=15000,verificationTimeout=300000,watchInterval=1000}){
 let current=null,serial=Promise.resolve(),disposed=false,generation=0,isolated=null;
 function bounded(task,signal,timeout=commandTimeout){
  return new Promise((resolve,reject)=>{
   const abort=()=>finish(reject,new Error('verification-closed'));
   const timer=setTimeout(()=>finish(reject,new Error('verification-timeout')),timeout);
   function finish(done,value){clearTimeout(timer);signal?.removeEventListener('abort',abort);done(value);}
   Promise.resolve(task).then(value=>finish(resolve,value),error=>finish(reject,error));
   signal?.addEventListener('abort',abort,{once:true});if(signal?.aborted)abort();
  });
 }
 function clear(){
  const old=current;current=null;if(!old)return;
  old.abort.abort();clearTimeout(old.timer);clearTimeout(old.watch);
  try{window.contentView.removeChildView(old.frame);}catch{}
  try{if(!old.view.webContents.isDestroyed())old.view.webContents.close();}catch{}
 }
 function position(){
  if(!current?.bounds)return;
  const bounds=current.bounds;
  const [width,height]=window.getContentSize();
  const zoom=window.webContents?.getZoomFactor()??1;
  current.view.webContents.setZoomFactor?.(zoom);
  const clip=current.clip||bounds;
  const x=Math.max(0,Math.min(Math.round(clip.x*zoom),width)),y=Math.max(0,Math.min(Math.round(clip.y*zoom),height));
  current.frame.setBounds({x,y,width:Math.max(0,Math.min(Math.round(clip.width*zoom),width-x)),height:Math.max(0,Math.min(Math.round(clip.height*zoom),height-y))});
  current.view.setBounds({x:Math.round(bounds.x*zoom)-x,y:Math.round(bounds.y*zoom)-y,width:Math.round(bounds.width*zoom),height:Math.round(bounds.height*zoom)});
 }
 window.on?.('resize',position);
 function watch(item){
  item.watch=setTimeout(()=>void(async()=>{
   try{
    const state=await bounded(backend.request('operations.poll',{operationId:item.operationId}),item.abort.signal);
    if(current!==item)return;
    if(state.state!=='running'||state.webVerificationId!==item.id){clear();return;}
    if(state.challenge?.kind!=='web-captcha'&&item.attached){window.contentView.removeChildView(item.frame);item.attached=false;}
    watch(item);
   }catch{if(current===item){clear();void bounded(backend.request('operations.cancel',{operationId:item.operationId})).catch(()=>{});}}
  })(),watchInterval);
 }
 async function sync(request,revision){
  if(disposed||revision!==generation)return;
  const state=await bounded(backend.request('operations.poll',{operationId:request.operationId}));
  if(disposed||revision!==generation)return;
  if(state.state!=='running'||!state.webVerificationId){clear();return;}
  const id=state.webVerificationId;
  if(current?.id!==id){
   clear();
   const identity={operationId:request.operationId,challengeId:id,value:''};
   try{
   const page=await bounded(backend.request('captcha.page',identity));
   if(disposed||revision!==generation)return;
   if(!isolated){
    isolated=session.fromPartition('persist:tc-verification');
    isolated.setPermissionRequestHandler((_contents,_permission,callback)=>callback(false));
    isolated.setPermissionCheckHandler(()=>false);isolated.on('will-download',event=>event.preventDefault());
   }
   const view=new WebContentsView({webPreferences:{session:isolated,sandbox:true,nodeIntegration:false,contextIsolation:true,webSecurity:true,backgroundThrottling:true}});
   const frame=new View();frame.addChildView(view);
   const item={id,operationId:request.operationId,view,frame,contexts:new Set(),status:'waiting',abort:new AbortController(),timer:null,watch:null};current=item;watch(item);
   if(onQuit)bindExitKeys(view.webContents,onQuit);
   const contents=view.webContents,debug=contents.debugger,signal=item.abort.signal;
   contents.on('did-finish-load',()=>{if(current===item)position();});
   const command=(method,params)=>bounded(debug.sendCommand(method,params),signal);
   let bootstrapping=true;
   contents.setWindowOpenHandler(()=>({action:'deny'}));
   contents.on('will-navigate',(event,url)=>{if(url!==page.url&&!(bootstrapping&&url==='about:blank'))event.preventDefault();});
   const fail=reason=>{
    if(current!==item||item.status==='failed')return;
    const delivered=item.status==='delivered';
    item.status='failed';clear();
    void bounded(backend.request(delivered?'operations.cancel':'captcha.fail',delivered?{operationId:item.operationId}:{...identity,value:reason})).catch(()=>{});
   };
   contents.on('render-process-gone',()=>fail('renderer-exited'));
   contents.on('did-fail-load',(_event,code,_description,_url,main)=>{if(main&&code!==-3)fail('page-load');});
   item.timer=setTimeout(()=>fail('verification-timeout'),verificationTimeout);
   try{
    // CDP commands on a never-navigated WebContents can wait for navigation forever.
    // Establish the initial document before attaching or awaiting any CDP command.
    await bounded(contents.loadURL('about:blank'),signal);
    if(current!==item)return;bootstrapping=false;debug.attach('1.3');
    debug.on('message',(_event,method,params)=>{void(async()=>{
     if(current!==item)return;
     if(method==='Runtime.executionContextCreated'&&params.context.origin===new URL(page.url).origin&&params.context.auxData?.isDefault)item.contexts.add(params.context.id);
     if(method==='Runtime.executionContextsCleared')item.contexts.clear();
     if(method==='Runtime.executionContextDestroyed')item.contexts.delete(params.executionContextId);
     if(method==='Fetch.requestPaused')await command('Fetch.fulfillRequest',{requestId:params.requestId,responseCode:200,responseHeaders:[{name:'Content-Type',value:'text/html; charset=utf-8'}],body:Buffer.from(page.html).toString('base64')});
     if(method==='Runtime.bindingCalled'&&params.name===page.binding&&item.contexts.has(params.executionContextId)&&item.status==='waiting'){
      if(typeof params.payload!=='string'||params.payload.length<1||params.payload.length>16384||/[\x00-\x1f\x7f]/.test(params.payload)){fail('invalid-token');return;}
      item.status='replying';
      try{await bounded(backend.request('captcha.reply',{...identity,value:params.payload}),signal);item.status='delivered';clearTimeout(item.timer);}
      catch{fail('token-delivery');}
     }
    })().catch(()=>fail('page-command'));});
    await command('Runtime.enable');await command('Runtime.addBinding',{name:page.binding});
    await command('Fetch.enable',{patterns:[{urlPattern:page.url,resourceType:'Document',requestStage:'Request'}]});
    // Keep the session for subsequent OTP/authentication, even when the view is hidden.
    void bounded(contents.loadURL(page.url),signal,45000).catch(()=>fail('page-load'));
   }catch{fail('page-initialization');return;}
   }catch{
    if(!disposed&&revision===generation){clear();void bounded(backend.request('captcha.fail',{...identity,value:'page-initialization'})).catch(()=>{});}
    return;
   }
  }
  const bounds=request.bounds;
  if(current&&bounds&&state.challenge?.kind==='web-captcha'){
   current.bounds=bounds;current.clip=request.clip;position();
   if(!current.attached){window.contentView.addChildView(current.frame);current.attached=true;}
  }else if(current?.attached){window.contentView.removeChildView(current.frame);current.attached=false;}
 }
 return {
  isActive(){return Boolean(current);},
  update(value){
   if(!value){generation++;clear();return Promise.resolve();}
   const revision=generation;serial=serial.catch(()=>{}).then(()=>sync(value,revision));return serial;
  },
  async close(){window.removeListener?.('resize',position);disposed=true;generation++;clear();}
 };
}
module.exports={createCaptchaView};
