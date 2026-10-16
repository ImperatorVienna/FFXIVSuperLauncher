'use strict';
const path=require('node:path');
// Select once in Electron main; renderers consume backend capabilities only.
function runtime(platform,env,home,repository){
 if(platform!=='linux'&&platform!==undefined)throw new Error('Unsupported launcher platform.');
 return {
  steamControls:true,
  defaultData:env.APPIMAGE?path.dirname(env.APPIMAGE):repository,
  backend:path.join(repository,'xivlauncher-backend'),
  launcher:path.join(repository,'xivlauncher-super'),
 };
}
module.exports={runtime};
