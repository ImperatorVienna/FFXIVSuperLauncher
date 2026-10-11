'use strict';
// Development only: bundled Electron already has resources/app for the official-page browser.
// Give this frontend its own bootstrap without modifying that browser or any installed runtime.
const fs=require('node:fs/promises');
const {constants}=require('node:fs');
const {tmpdir}=require('node:os');
const path=require('node:path');
const {spawn}=require('node:child_process');

async function prepareRuntime(executable,entry=path.join(__dirname,'main.cjs')) {
  if(!executable||!path.isAbsolute(executable))throw new Error('Set FFXIV_ELECTRON to the absolute path of the bundled Electron executable.');
  await fs.access(executable,constants.X_OK);
  const source=path.dirname(executable),root=await fs.mkdtemp(path.join(tmpdir(),'ffxiv-frontend-runtime-'));
  const cleanup=()=>fs.rm(root,{recursive:true,force:true});
  try {
    const target=path.join(root,path.basename(executable));
    // A symlink for the executable would resolve resources/app from the original runtime.
    await fs.copyFile(executable,target,constants.COPYFILE_FICLONE);
    await fs.chmod(target,0o700);
    for(const item of await fs.readdir(source))
      if(item!==path.basename(executable)&&item!=='resources')await fs.symlink(path.join(source,item),path.join(root,item));
    const app=path.join(root,'resources','app');await fs.mkdir(app,{recursive:true});
    await fs.writeFile(path.join(app,'package.json'),JSON.stringify({name:'ffxiv-super-launcher-frontend-dev',main:'main.cjs',version:'0.0.0'}));
    await fs.writeFile(path.join(app,'main.cjs'),`require(${JSON.stringify(path.resolve(entry))});\n`);
    return {executable:target,cleanup};
  } catch(error) {await cleanup();throw error;}
}

async function run() {
  if(!process.env.FFXIV_BACKEND||!path.isAbsolute(process.env.FFXIV_BACKEND))throw new Error('Set FFXIV_BACKEND to the absolute path of the newly built launcher.');
  const runtime=await prepareRuntime(process.env.FFXIV_ELECTRON);
  try {
    const args=process.argv.slice(2);
    if(args.some(arg=>arg!=='--steam-entry')||args.length>1)throw new Error('Unknown frontend argument.');
    const child=spawn(runtime.executable,args,{stdio:'inherit',shell:false,env:{...process.env,FFXIV_FRONTEND_NODE:process.execPath}});
    const stop=()=>child.kill('SIGTERM');process.once('SIGINT',stop);process.once('SIGTERM',stop);
    try {process.exitCode=await new Promise((resolve,reject)=>{child.once('error',reject);child.once('exit',code=>resolve(code??1));});}
    finally {process.removeListener('SIGINT',stop);process.removeListener('SIGTERM',stop);}
  } finally {await runtime.cleanup();}
}
if(require.main===module)void run().catch(error=>{console.error(error.message);process.exitCode=1;});
module.exports={prepareRuntime};
