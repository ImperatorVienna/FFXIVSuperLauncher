const {test}=require('node:test');const assert=require('node:assert/strict');
const {createBrowserCacheCleaner}=require('../browser-cache.cjs');
function fixture(fail=false){const calls=[];let size=100;return {calls,getCacheSize:async()=>size,clearCache:async()=>{if(fail)throw Error();size=0;calls.push('http');},clearCodeCaches:async value=>{assert.deepEqual(value,{});calls.push('code');},clearStorageData:async value=>{assert.deepEqual(value,{storages:['shadercache']});calls.push('shader');}};}
test('browser cleanup preserves website data and clears both managed sessions',async()=>{
 const a=fixture(),b=fixture();const clean=createBrowserCacheCleaner({session:{defaultSession:a,fromPartition:name=>{assert.equal(name,'persist:tc-verification');return b;}},busy:()=>false});
 assert.deepEqual(await clean(),{bytes:200,cleared:2,skipped:0});assert.deepEqual(a.calls,['http','code','shader']);assert.deepEqual(b.calls,a.calls);
});
test('active verification blocks cleanup without touching caches',async()=>{
 const a=fixture();await assert.rejects(createBrowserCacheCleaner({session:{defaultSession:a},busy:()=>true})());assert.deepEqual(a.calls,[]);
});
test('cache failure is reported as skipped and does not stop other session',async()=>{
 const clean=createBrowserCacheCleaner({session:{defaultSession:fixture(true),fromPartition:()=>fixture()},busy:()=>false});assert.deepEqual(await clean(),{bytes:100,cleared:1,skipped:1});
});
