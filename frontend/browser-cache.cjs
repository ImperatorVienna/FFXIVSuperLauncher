'use strict';
// Only cache APIs: cookies, authentication and website storage are deliberately retained.
function createBrowserCacheCleaner({session,busy}) {
 let running=false;
 return async()=>{
  if(running)throw new Error('请等待当前操作或网页验证结束后再清理浏览器缓存。');
  running=true;
  try{
   if(await busy())throw new Error('请等待当前操作或网页验证结束后再清理浏览器缓存。');
   let bytes=0,cleared=0,skipped=0;
   for(const item of [session.defaultSession,session.fromPartition('persist:tc-verification')]){
    try{
     const before=await item.getCacheSize();
     await item.clearCache();
     await item.clearCodeCaches({});
     await item.clearStorageData({storages:['shadercache']});
     bytes+=Math.max(0,before-await item.getCacheSize());cleared++;
    }catch{skipped++;}
   }
   return {bytes,cleared,skipped};
  }finally{running=false;}
 };
}
module.exports={createBrowserCacheCleaner};
