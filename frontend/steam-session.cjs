'use strict';
const fs=require('node:fs/promises');
const path=require('node:path');
const {randomUUID}=require('node:crypto');
const file=root=>path.join(root,'electron-session.json');
async function publishSession(root,pid=process.pid) {
  const value={pid,id:randomUUID()};await fs.writeFile(file(root),JSON.stringify(value),{mode:0o600});
  return async()=>{try{if(JSON.parse(await fs.readFile(file(root),'utf8')).id===value.id)await fs.rm(file(root));}catch(error){if(error.code!=='ENOENT')throw error;}};
}
// A second Steam invocation keeps its own Steam-tracked process alive while the primary owns the UI/game.
async function waitForPrimary(root,{delay=()=>new Promise(resolve=>setTimeout(resolve,1000)),transfer=null,alive=pid=>{try{process.kill(pid,0);return true;}catch{return false;}}}={}) {
  let first;
  for(let i=0;i<10;i++){
    try{first=JSON.parse(await fs.readFile(file(root),'utf8'));if(Number.isInteger(first.pid)&&first.id&&alive(first.pid))break;first=null;}catch{}
    await delay();
  }
  if(!first)return;
  if(transfer){await transfer(first.pid,first.id);return;}
  while(alive(first.pid)){
    await delay();
    try{if(JSON.parse(await fs.readFile(file(root),'utf8')).id!==first.id)return;}catch{return;}
  }
}
module.exports={publishSession,waitForPrimary};
