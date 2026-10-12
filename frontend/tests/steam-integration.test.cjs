'use strict';
const {test}=require('node:test'),assert=require('node:assert/strict'),{EventEmitter}=require('node:events');
const {isSteamLaunch,validKeyboardRequest,createSteamIntegration}=require('../steam-integration.cjs');
const {observePlacement}=require('../window-state.cjs');
const rect={x:5,y:6,width:200,height:30,mode:0};
function fixture(options={}){
 const window=new EventEmitter(),saved=[];let full=false,maximized=false,bounds={x:20,y:30,width:1100,height:750},integration;
 Object.assign(window,{isDestroyed:()=>false,isMinimized:()=>false,isFullScreen:()=>full,isMaximized:()=>maximized,getNormalBounds:()=>({...bounds}),
  setBounds(value){bounds={...value};window.emit('resize');},maximize(){maximized=true;window.emit('maximize');},unmaximize(){maximized=false;window.emit('unmaximize');},
  setFullScreen(value){full=value;if(value)bounds={x:0,y:0,width:1920,height:1080};window.emit('resize');window.emit(value?'enter-full-screen':'leave-full-screen');}});
 const placement=observePlacement(window,v=>{saved.push(v);return Promise.resolve();},()=>!integration?.active);
 integration=createSteamIntegration({window,placement,...options});return {window,saved,placement,integration};
}
test('Steam origin includes compatibility and non-Steam shortcuts, not desktop Gamescope alone',()=>{
 assert.equal(isSteamLaunch(['--steam-entry'],{}),true);
 assert.equal(isSteamLaunch([],{SteamGameId:'18446744070000000001'}),true);
 assert.equal(isSteamLaunch([],{SteamAppId:'39210'}),true);
 for(const env of [{},{SteamAppId:'0'},{SteamGameId:'abc'},{SteamDeck:'1',XDG_CURRENT_DESKTOP:'gamescope'}])assert.equal(isSteamLaunch([],env),false);
});
test('registration is the only switch; Steam fullscreen never overwrites desktop geometry',async()=>{
 const f=fixture({fromSteam:true});const expected={...f.window.getNormalBounds(),maximized:false};
 f.integration.update(false);assert.equal(f.window.isFullScreen(),false);
 f.integration.update(true);assert.equal(f.window.isFullScreen(),true);
 f.window.setBounds({x:0,y:0,width:2560,height:1440});await f.placement.flush();
 assert.deepEqual(f.saved,[expected]);
 f.integration.update(false);assert.equal(f.window.isFullScreen(),false);assert.deepEqual(f.window.getNormalBounds(),{x:20,y:30,width:1100,height:750});
 await f.placement.flush();assert.deepEqual(f.saved,[expected]);f.placement.stop();
});
test('desktop stays windowed until Steam secondary entry; repeated activation does not replace desktop state',async()=>{
 const f=fixture();f.window.maximize();f.integration.update(true);assert.equal(f.window.isFullScreen(),false);
 f.integration.attach();f.integration.attach();assert.equal(f.window.isFullScreen(),true);
 f.integration.update(false);assert.equal(f.window.isMaximized(),true);assert.equal(f.window.getNormalBounds().width,1100);await f.placement.flush();f.placement.stop();
});
test('keyboard validates bounded metadata and never accepts text or URLs',()=>{
 assert.equal(validKeyboardRequest(rect),true);
 for(const value of [null,{}, {...rect,text:'secret'},{...rect,url:'https://example.org'},{...rect,mode:4},{...rect,width:0},{...rect,x:Infinity},{...rect,y:-1}])assert.ok(!validKeyboardRequest(value));
});
test('keyboard unavailable is isolated, inactive desktop never starts Steam',async()=>{
 let checked=0,sent=0;const f=fixture({running:async()=>{checked++;return false;},send:async()=>{sent++;return true;}});
 f.integration.update(true);assert.equal((await f.integration.keyboard(rect)).status,'inactive');assert.equal(checked,0);
 f.integration.attach();assert.equal((await f.integration.keyboard(rect)).status,'unavailable');assert.equal(sent,0);f.placement.stop();
});
test('keyboard requests are coalesced and transport errors stay local',async()=>{
 let release,sent=0;const f=fixture({fromSteam:true,running:()=>new Promise(r=>release=r),send:async()=>{sent++;throw Error('synthetic');}});f.integration.update(true);
 const first=f.integration.keyboard(rect),second=f.integration.keyboard(rect);release(true);
 assert.equal((await first).status,'unavailable');assert.equal((await second).status,'unavailable');assert.equal(sent,1);
 assert.equal((await f.integration.keyboard(rect)).status,'throttled');assert.equal(f.window.isFullScreen(),true);f.placement.stop();
});
test('successful request means dispatched, not proof the Steam overlay was displayed',async()=>{
 const f=fixture({fromSteam:true,running:async()=>true,send:async v=>{assert.deepEqual(v,rect);return true;}});f.integration.update(true);
 assert.equal((await f.integration.keyboard(rect)).status,'requested');f.placement.stop();
});
