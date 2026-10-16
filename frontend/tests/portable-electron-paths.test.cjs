'use strict';
const test=require('node:test');const assert=require('node:assert/strict');
const {configurePaths}=require('../portable-electron-paths.cjs');
test('all Electron persistent locations are inside the portable directory',()=>{
 const assigned={},created=[];
 const paths=configurePaths({setPath:(key,value)=>assigned[key]=value},'/portable/electron',{mkdir:value=>created.push(value)});
 assert.deepEqual(Object.keys(assigned),['appData','userData','sessionData','logs','crashDumps','temp']);
 for(const value of Object.values(paths)){assert.ok(value==='/portable/electron'||value.startsWith('/portable/electron/'));assert.ok(created.includes(value));}
});
test('unwritable portable directory fails rather than falling back to user profile',()=>{
 let writes=0;assert.throws(()=>configurePaths({setPath:()=>writes++},'/readonly',{mkdir:()=>{throw new Error('EACCES')}}),/EACCES/);
 assert.equal(writes,0);
});
test('relative paths cannot redirect data according to a shortcut working directory',()=>assert.throws(()=>configurePaths({},'relative'),/absolute/));
