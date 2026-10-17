'use strict';
const {test}=require('node:test');
const assert=require('node:assert/strict');
const {backendProcessOptions}=require('../platforms/backend-process.cjs');
test('Windows backend escapes parent job while retaining IPC pipes',()=>{
 assert.deepEqual(backendProcessOptions('win32'),{stdio:['pipe','pipe','pipe'],shell:false,detached:true,windowsHide:true});
});
test('Linux backend spawn policy is unchanged',()=>{
 assert.deepEqual(backendProcessOptions('linux'),{stdio:['pipe','pipe','pipe'],shell:false});
});

// Run this same test on Windows to exercise libuv Job cleanup, not a mocked PID.
test('local service survives its spawning Node process exiting',async()=>{
 const {spawn}=require('node:child_process');
 const {once}=require('node:events');
 const net=require('node:net');
 const childSource=`
  const net=require('node:net');
  const server=net.createServer(socket=>{socket.end('alive');server.close(()=>process.exit(0));});
  server.listen(0,'127.0.0.1',()=>console.log(server.address().port));
  setTimeout(()=>process.exit(1),10000).unref();
 `;
 const policy=require.resolve('../platforms/backend-process.cjs');
 const parentSource=`
  const {spawn}=require('node:child_process');
  const {backendProcessOptions}=require(${JSON.stringify(policy)});
  const child=spawn(process.execPath,['-e',${JSON.stringify(childSource)}],backendProcessOptions());
  child.stdout.once('data',chunk=>{
   process.stdout.write(chunk,()=>{
    child.stdin.end();child.stdout.destroy();child.stderr.destroy();child.unref();process.exit(0);
   });
  });
 `;
 const parent=spawn(process.execPath,['-e',parentSource],{stdio:['ignore','pipe','pipe']});
 let output='',errors='';parent.stdout.on('data',x=>output+=x);parent.stderr.on('data',x=>errors+=x);
 const [code]=await once(parent,'exit');assert.equal(code,0,errors);
 const port=Number(output.trim());assert.ok(port>0);
 // Wait past parent teardown before trying the child's socket.
 await new Promise(resolve=>setTimeout(resolve,150));
 const answer=await new Promise((resolve,reject)=>{
  const socket=net.connect(port,'127.0.0.1');let text='';
  socket.setTimeout(3000,()=>socket.destroy(new Error('Service did not respond')));
  socket.on('data',x=>text+=x);socket.on('end',()=>resolve(text));socket.on('error',reject);
 });
 assert.equal(answer,'alive');
});
