'use strict';
const {execFile}=require('node:child_process');
const fs=require('node:fs/promises');
const path=require('node:path');
const os=require('node:os');

// Registration is the single preference. Steam-origin detection is shared by
// initial startup and second-instance activation; Gamescope alone is not enough.
function isSteamLaunch(args=[],env={}) {
  return args.includes('--steam-entry') || ['SteamGameId','SteamAppId'].some(key=>/^[1-9]\d*$/.test(env[key]||''));
}
function validKeyboardRequest(value) {
  return value && Object.keys(value).sort().join(',')==='height,mode,width,x,y' &&
    [0,1,2,3].includes(value.mode) && ['x','y','width','height'].every(k=>Number.isInteger(value[k])&&value[k]>=0&&value[k]<=30000) && value.width>0 && value.height>0;
}
async function steamRunning() {
  try {
    const pid=(await fs.readFile(path.join(os.homedir(),'.steam/steam.pid'),'utf8')).trim();
    if(!/^[1-9]\d*$/.test(pid))return false;
    const dir='/proc/'+pid;
    return (await fs.stat(dir)).uid===process.getuid() && (await fs.readFile(dir+'/comm','utf8')).trim()==='steam';
  } catch { return false; }
}
function sendKeyboard(value) {
  // Fixed command and bounded numeric data only; never forward typed text or a
  // renderer-supplied URL. -ifrunning also closes the exit-after-PID-check race.
  const url='steam://open/keyboard?'+new URLSearchParams({XPosition:value.x,YPosition:value.y,Width:value.width,Height:value.height,Mode:value.mode});
  const env={...process.env};
  for(const key of ['LD_PRELOAD','LD_LIBRARY_PATH','ELECTRON_RUN_AS_NODE','NODE_OPTIONS'])delete env[key];
  return new Promise(resolve=>execFile('/usr/bin/steam',['-ifrunning',url],{env,timeout:2500,maxBuffer:4096},error=>resolve(!error)));
}
function createSteamIntegration({window,placement,enabled=false,fromSteam=false,running=steamRunning,send=sendKeyboard}) {
  let active=false,desktop=null,returning=false,pending=null,lastRequest=0;
  const restoreDesktop=()=>{
    if(!returning)return;
    returning=false;
    if(!window.isDestroyed()){
      window.setBounds(desktop.bounds);
      if(desktop.maximized)window.maximize();else window.unmaximize();
    }
    active=false;
    // A setting or a Steam re-entry can change while fullscreen is exiting.
    apply();
  };
  window.on('leave-full-screen',restoreDesktop);
  function apply() {
    const wanted=enabled&&fromSteam;
    if(window.isDestroyed()||returning||wanted===active)return;
    if(wanted){
      void placement?.flush().catch(()=>{});
      desktop={bounds:window.getNormalBounds(),maximized:window.isMaximized()};
      active=true;window.setFullScreen(true);
    } else {
      returning=true;
      if(window.isFullScreen())window.setFullScreen(false);else restoreDesktop();
    }
  }
  return {
    get active(){return active;},
    update(value){enabled=Boolean(value);apply();},
    attach(){fromSteam=true;apply();},
    async keyboard(value){
      if(!validKeyboardRequest(value))return {status:'invalid'};
      if(!enabled||!fromSteam)return {status:'inactive'};
      if(pending)return pending;
      if(Date.now()-lastRequest<400)return {status:'throttled'};
      lastRequest=Date.now();
      pending=(async()=>{
        try {return {status:await running()&&await send(value)?'requested':'unavailable'};}
        catch {return {status:'unavailable'};}
        finally {pending=null;}
      })();
      return pending;
    }
  };
}
module.exports={isSteamLaunch,validKeyboardRequest,createSteamIntegration};
