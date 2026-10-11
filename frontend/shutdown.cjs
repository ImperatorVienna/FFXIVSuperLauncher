'use strict';
// Every owner is released even if an earlier cleanup fails. Steam may intentionally
// keep the backend alive for the running game, after the verification view is gone.
async function shutdown({placement,verification,backend,removeSession,report=()=>{}}){
 for(const [name,action] of [
  ['verification-close',()=>verification?.close()],
  ['window-save',()=>placement?.flush()],
  ['window-observer',()=>placement?.stop()],
  ['backend-close',()=>backend?.close()],
  ['session-remove',()=>removeSession?.()]
 ])try{await action();}catch{report(name);}
}
module.exports={shutdown};
