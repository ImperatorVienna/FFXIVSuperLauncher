'use strict';
const path=require('node:path');
const {mkdirSync}=require('node:fs');
// Call before ready/session/window creation in both launcher and browser modes.
function configurePaths(app,root,{mkdir=mkdirSync}={}){
 if(!path.isAbsolute(root))throw new Error('Electron data directory must be absolute.');
 const locations={appData:root,userData:root,sessionData:path.join(root,'sessions'),
  logs:path.join(root,'logs'),crashDumps:path.join(root,'crashes'),temp:path.join(root,'temp')};
 for(const [name,directory] of Object.entries(locations)){
  mkdir(directory,{recursive:true,mode:0o700});app.setPath(name,directory);
 }
 return locations;
}
module.exports={configurePaths};
