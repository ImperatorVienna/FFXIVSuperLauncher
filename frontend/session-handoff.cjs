'use strict';
const fs=require('node:fs/promises');
const path=require('node:path');
async function handoff(backend,directory){
 if(!directory||!path.isAbsolute(directory))throw new Error('No session supervisor.');
 const stat=await fs.readFile(`/proc/${backend.child.pid}/stat`,'utf8');
 const fields=stat.slice(stat.lastIndexOf(')')+2).trim().split(/\s+/);
 if(fields[0]==='Z'||!/^\d+$/.test(fields[19]))throw new Error('Backend is no longer running.');
 const temp=path.join(directory,'backend.tmp');
 await fs.writeFile(temp,`${backend.child.pid} ${fields[19]}\n`,{mode:0o600});
 await fs.rename(temp,path.join(directory,'backend'));
 backend.release();
}
module.exports={handoff};
