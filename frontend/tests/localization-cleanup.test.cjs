const {test}=require('node:test');const assert=require('node:assert/strict');const fs=require('node:fs');const vm=require('node:vm');const path=require('node:path');
const root=path.resolve(__dirname,'../..');
test('all literal Chinese about-page labels have complete translations',()=>{
 const ctx={window:{}};vm.runInNewContext(fs.readFileSync(path.join(root,'prototypes/electron-ui/locales.js'),'utf8'),ctx);
 const source=fs.readFileSync(path.join(root,'prototypes/electron-ui/about.js'),'utf8');
 for(const [,label] of source.matchAll(/'([^'\n]*[\u4e00-\u9fff][^'\n]*)'/g)){
  if(label.includes('<')||label.includes('=>'))continue;
  const translations=ctx.window.previewStrings[label];assert.ok(translations,`Missing: ${label}`);
  assert.equal(translations.length,3);for(const value of translations){assert.ok(value);assert.deepEqual([...value.matchAll(/\{(\w+)\}/g)].map(m=>m[1]).sort(),[...label.matchAll(/\{(\w+)\}/g)].map(m=>m[1]).sort());}
 }
});
test('cleanup result templates substitute numbers in every UI language',()=>{
 const ctx={window:{},document:{documentElement:{}}};for(const name of ['locales.js','i18n.js'])vm.runInNewContext(fs.readFileSync(path.join(root,'prototypes/electron-ui',name),'utf8'),ctx);
 for(const language of ['简体中文','繁體中文','日本語','English']){
  ctx.window.previewI18n.set(language);const text=ctx.window.previewI18n.t('已清理 {count} 个文件，释放 {size} MiB；跳过 {skipped} 项。',{count:2,size:'1.5',skipped:0});
  assert.ok(text.includes('2')&&text.includes('1.5')&&text.includes('0'));assert.ok(!text.includes('{'));
 }
});
