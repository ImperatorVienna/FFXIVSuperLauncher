'use strict';
const path=require('node:path');
// Select once in Electron main; renderers consume backend capabilities only.
function runtime(platform,env,home,repository){
 if(platform==='win32')return {
  steamControls:false,
  defaultData:env.FFXIV_PORTABLE_EXECUTABLE?path.dirname(env.FFXIV_PORTABLE_EXECUTABLE):repository,
  backend:path.join(repository,'xivlauncher-backend.exe'),
  launcher:env.FFXIV_PORTABLE_EXECUTABLE||path.join(repository,'FFXIVSuperLauncher.exe'),
 };
 if(platform!=='linux'&&platform!==undefined)throw new Error('Unsupported launcher platform.');
 return {
  steamControls:true,
  defaultData:env.APPIMAGE?path.dirname(env.APPIMAGE):repository,
  backend:path.join(repository,'xivlauncher-backend'),
  launcher:path.join(repository,'xivlauncher-super'),
 };
}
module.exports={runtime};
