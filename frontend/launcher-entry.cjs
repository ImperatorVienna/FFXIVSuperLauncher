'use strict';
const fs=require('node:fs/promises');
const path=require('node:path');
const shellQuote=value=>"'"+value.replaceAll("'","'\"'\"'")+"'";

// Development entry points at stable source/runtime locations, never the temporary Electron copy.
// The host must have initialized the private configuration before this script is installed.
async function writeDevelopmentEntry({root,node,runner,backend,electron,dotnetRoot}) {
  for(const value of [root,node,runner,backend,electron])
    if(typeof value!=='string'||!path.isAbsolute(value)||value.includes('\0'))throw new Error('An absolute development path is required.');
  await fs.access(path.join(root,'linux-settings.json'));
  const entry=path.join(root,'electron-launcher');
  const env={FFXIV_BACKEND:backend,FFXIV_ELECTRON:electron,FFXIV_FRONTEND_DATA_DIR:root};
  if(dotnetRoot)env.DOTNET_ROOT=dotnetRoot;
  const script='#!/bin/sh\nset -eu\nexec env '+Object.entries(env).map(([key,value])=>shellQuote(key+'='+value)).join(' ')+' '+shellQuote(node)+' '+shellQuote(runner)+' "$@"\n';
  const temporary=entry+'.'+process.pid+'.tmp';
  try {await fs.writeFile(temporary,script,{mode:0o700,flag:'wx'});await fs.rename(temporary,entry);}
  finally {await fs.rm(temporary,{force:true});}
  return entry;
}
module.exports={writeDevelopmentEntry};
