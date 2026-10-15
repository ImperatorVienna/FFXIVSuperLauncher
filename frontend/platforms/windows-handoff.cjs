'use strict';
const fs=require('node:fs/promises');const path=require('node:path');
async function handoff(backend,directory){
 if(!directory||!path.isAbsolute(directory)||!Number.isInteger(backend.child.pid))throw new Error('No Windows session supervisor.');
 const temporary=path.join(directory,'backend.tmp');
 await fs.writeFile(temporary,String(backend.child.pid));await fs.rename(temporary,path.join(directory,'backend'));
 for(let i=0;i<100;i++){
  let accepted;try{accepted=await fs.readFile(path.join(directory,'accepted'),'utf8');}catch(error){if(error.code!=='ENOENT')throw error;}
  if(accepted===String(backend.child.pid)){backend.release();return;}
  await new Promise(resolve=>setTimeout(resolve,100));
 }
 throw new Error('Windows session handoff was not acknowledged.');
}
module.exports={handoff};
