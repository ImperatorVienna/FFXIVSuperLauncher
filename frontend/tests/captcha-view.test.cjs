'use strict';
const {test}=require('node:test');const assert=require('node:assert/strict');const {EventEmitter}=require('node:events');const {createCaptchaView}=require('../captcha-view.cjs');
test('embedded verification isolates remote content, bounds it, keeps it for OTP, and accepts only its origin binding',async()=>{
 let state={state:'running',webVerificationId:'test-id',challenge:{kind:'web-captcha'}},view;const calls=[],children=new Set();
 const backend={async request(method,value){calls.push({method,value});if(method==='operations.poll')return state;if(method==='captcha.page')return {url:'https://launcher.ffxiv.com.tw/index.html',binding:'test-binding',html:'<p>synthetic page</p>'};return {};}};
 class View{constructor(options){view=this;this.options=options;this.webContents=new EventEmitter();const c=this.webContents;c.debugger=new EventEmitter();c.debugger.attach=()=>{};c.debugger.sendCommand=async()=>{};c.setWindowOpenHandler=fn=>this.open=fn;c.loadURL=async url=>this.url=url;c.isDestroyed=()=>this.closed;c.close=()=>{this.closed=true};}setBounds(bounds){this.bounds=bounds}}
 const controller=createCaptchaView({window:{contentView:{addChildView:v=>children.add(v),removeChildView:v=>children.delete(v)},getContentSize:()=>[800,700]},backend,WebContentsView:View,session:{fromPartition:()=>({setPermissionRequestHandler(){},setPermissionCheckHandler(){},on(){}})}});
 const request={operationId:'operation',bounds:{x:600,y:650,width:500,height:400}};
 try{
  await controller.update(request);assert.deepEqual(view.bounds,{x:600,y:650,width:200,height:50});assert.equal(children.size,1);
  assert.equal(view.options.webPreferences.nodeIntegration,false);assert.equal(view.options.webPreferences.sandbox,true);assert.equal(view.options.webPreferences.preload,undefined);
  assert.equal(view.open().action,'deny');let denied=false;view.webContents.emit('will-navigate',{preventDefault(){denied=true}},'file:///private');assert.equal(denied,true);
  const debug=view.webContents.debugger;
  debug.emit('message',{},'Runtime.bindingCalled',{name:'test-binding',executionContextId:1,payload:'unexpected'});
  assert.equal(calls.some(x=>x.method==='captcha.reply'),false);
  debug.emit('message',{},'Runtime.executionContextCreated',{context:{id:2,origin:'https://launcher.ffxiv.com.tw',auxData:{isDefault:true}}});
  debug.emit('message',{},'Runtime.bindingCalled',{name:'test-binding',executionContextId:2,payload:'synthetic-token'});
  await new Promise(resolve=>setImmediate(resolve));assert.equal(calls.filter(x=>x.method==='captcha.reply').length,1);
  state={...state,challenge:{kind:'otp'}};await controller.update({...request,bounds:null});assert.equal(children.size,0);assert.equal(Boolean(view.closed),false);
  state={state:'completed'};await controller.update(request);assert.equal(view.closed,true);
 }finally{await controller.close();}
});

function setup({stall,replyFails=false,commandTimeout=50,watchInterval=1000}={}){
 let state={state:'running',webVerificationId:'test-id',challenge:{kind:'web-captcha'}},view;
 const calls=[],events=[],children=new Set();let sessionListeners=0;
 const backend={async request(method,value){calls.push({method,value});if(method==='operations.poll')return state;
  if(method==='captcha.page')return {url:'https://launcher.ffxiv.com.tw/index.html',binding:'test-binding',html:'<p>synthetic page</p>'};
  if(method==='captcha.reply'&&replyFails)throw new Error('do not log synthetic-token');return {};}};
 class View{constructor(){view=this;const c=this.webContents=new EventEmitter();let navigated=false;
  c.debugger=new EventEmitter();c.debugger.attach=()=>events.push('attach');
  c.debugger.sendCommand=async method=>{events.push(method);if(!navigated||method===stall)await new Promise(()=>{});};
  c.setWindowOpenHandler=()=>{};c.loadURL=async url=>{events.push(url);navigated=true;};c.isDestroyed=()=>this.closed;c.close=()=>{this.closed=true};}
  setBounds(){}
 }
 const controller=createCaptchaView({window:{contentView:{addChildView:v=>children.add(v),removeChildView:v=>children.delete(v)},getContentSize:()=>[800,700]},backend,WebContentsView:View,
  session:{fromPartition:()=>({setPermissionRequestHandler(){},setPermissionCheckHandler(){},on(){sessionListeners++;}})},commandTimeout,watchInterval});
 return {controller,calls,events,view:()=>view,children,listeners:()=>sessionListeners,
  update:()=>controller.update({operationId:'operation',bounds:{x:0,y:0,width:300,height:200}}),
  state:value=>{state=value;},
  token(){const d=view.webContents.debugger;d.emit('message',{},'Runtime.executionContextCreated',{context:{id:2,origin:'https://launcher.ffxiv.com.tw',auxData:{isDefault:true}}});d.emit('message',{},'Runtime.bindingCalled',{name:'test-binding',executionContextId:2,payload:'synthetic-token'});}};
}
test('initial navigation precedes CDP commands that cannot resolve on a fresh view',async()=>{
 const h=setup();try{await h.update();assert.deepEqual(h.events.slice(0,5),['about:blank','attach','Runtime.enable','Runtime.addBinding','Fetch.enable']);assert.equal(h.events.at(-1),'https://launcher.ffxiv.com.tw/index.html');assert.equal(h.children.size,1);}finally{await h.controller.close();}
});
test('stalled initialization is bounded, reported by stage, and destroys the view',async()=>{
 const h=setup({stall:'Runtime.enable',commandTimeout:10});try{await h.update();assert.equal(h.view().closed,true);assert.equal(h.calls.find(c=>c.method==='captcha.fail').value.value,'page-initialization');assert.equal(h.events.includes('Fetch.enable'),false);}finally{await h.controller.close();}
});
test('cancel and shutdown interrupt a stuck CDP command without waiting for its deadline',async()=>{
 for(const close of [false,true]){
  const h=setup({stall:'Runtime.enable',commandTimeout:5000});const pending=h.update();
  await new Promise(resolve=>setImmediate(resolve));
  await (close?h.controller.close():h.controller.update(null));
  await Promise.race([pending,new Promise((_,reject)=>{const timer=setTimeout(()=>reject(new Error('cancel hung')),100);timer.unref();})]);
  assert.equal(h.view().closed,true);assert.equal(h.calls.some(c=>c.method==='captcha.fail'),false);await h.controller.close();
 }
});
test('token delivery errors do not disappear behind the duplicate-token guard',async()=>{
 const h=setup({replyFails:true});try{await h.update();h.token();h.token();await new Promise(resolve=>setImmediate(resolve));assert.equal(h.calls.filter(c=>c.method==='captcha.reply').length,1);assert.equal(h.calls.find(c=>c.method==='captcha.fail').value.value,'token-delivery');assert.equal(h.view().closed,true);}finally{await h.controller.close();}
});
test('repeated authentication reuses permission/download policy without accumulating handlers',async()=>{
 const h=setup();try{await h.update();await h.controller.update(null);await h.update();assert.equal(h.listeners(),1);}finally{await h.controller.close();}
});

for(const state of ['cancelled','failed','completed','cancelling'])test('view owner cleans up '+state+' login without renderer layout events',async()=>{
 const h=setup({watchInterval:5});try{await h.update();h.state({state});
  await new Promise(resolve=>setTimeout(resolve,20));assert.equal(h.view().closed,true);assert.equal(h.children.size,0);
  const calls=h.calls.length;await new Promise(resolve=>setTimeout(resolve,20));assert.equal(h.calls.length,calls);
 }finally{await h.controller.close();}
});
test('web renderer crash after token delivery cancels its own authentication and closes the hidden view',async()=>{
 const h=setup();try{await h.update();h.token();await new Promise(resolve=>setImmediate(resolve));
  h.state({state:'running',webVerificationId:'test-id',challenge:{kind:'otp'}});
  h.view().webContents.emit('render-process-gone');await new Promise(resolve=>setImmediate(resolve));
  assert.equal(h.view().closed,true);assert.deepEqual(h.calls.find(c=>c.method==='operations.cancel').value,{operationId:'operation'});
 }finally{await h.controller.close();}
});
test('late events from a cancelled verification cannot reply to or destroy a replacement',async()=>{
 const h=setup();try{await h.update();const old=h.view();await h.controller.update(null);await h.update();
  old.webContents.emit('render-process-gone');old.webContents.debugger.emit('message',{},'Runtime.bindingCalled',{name:'test-binding',executionContextId:2,payload:'late-token'});
  await new Promise(resolve=>setImmediate(resolve));assert.equal(Boolean(h.view().closed),false);assert.equal(h.calls.some(c=>c.method==='captcha.reply'||c.method==='captcha.fail'),false);
 }finally{await h.controller.close();}
});

test('view/session construction failures fail the pending challenge promptly',async()=>{
 const calls=[];const controller=createCaptchaView({window:{},backend:{async request(method,value){calls.push({method,value});
  if(method==='operations.poll')return {state:'running',webVerificationId:'id'};
  if(method==='captcha.page')return {url:'https://launcher.ffxiv.com.tw/index.html',html:'synthetic'};return {}; }},
  session:{fromPartition(){throw new Error('synthetic session failure')}},WebContentsView:class{}});
 try{await controller.update({operationId:'op',bounds:null});await new Promise(resolve=>setImmediate(resolve));
  assert.equal(calls.find(c=>c.method==='captcha.fail').value.value,'page-initialization');
 }finally{await controller.close();}
});
