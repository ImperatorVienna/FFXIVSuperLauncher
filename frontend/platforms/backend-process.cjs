'use strict';
// Windows libuv jobs terminate non-detached children when Electron exits.
// Keep pipes until acknowledged handoff; release() then closes them and unrefs.
function backendProcessOptions(platform=process.platform){
 const options={stdio:['pipe','pipe','pipe'],shell:false};
 if(platform==='win32')Object.assign(options,{detached:true,windowsHide:true});
 return options;
}
module.exports={backendProcessOptions};
