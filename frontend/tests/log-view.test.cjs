'use strict';
const {test}=require('node:test'),assert=require('node:assert/strict'),vm=require('node:vm'),fs=require('node:fs'),path=require('node:path');
const code=fs.readFileSync(path.join(__dirname,'../../prototypes/electron-ui/log-view.js'),'utf8');
function harness(){
 const window={},events={};let mutations=0,selection=false,hidden=false;
 const doc={addEventListener(name,fn){events[name]=fn;},createElement(){return {tagName:'DIV',get textContent(){return this.text||'';},set textContent(v){this.text=v;mutations++;},remove(){host.children.splice(host.children.indexOf(this),1);mutations++;}};}};
 const host={ownerDocument:doc,children:[],addEventListener(name,fn){events[name]=fn;},append(node){this.children.push(node);mutations++;},get lastElementChild(){return this.children.at(-1);},replaceChildren(){this.children=[];mutations++;}};
 vm.runInNewContext(code,{window});const view=window.createLogView(host,{selected:()=>selection,hidden:()=>hidden});
 return {host,view,events,mutations:()=>mutations,select:value=>{selection=value;events.selectionchange();},hidden:value=>{hidden=value;events.visibilitychange();}};
}
test('hundreds of unchanged operation polls do not mutate log text nodes',()=>{
 const h=harness(),lines=Array.from({length:500},(_,i)=>'Log '+i);h.view.update(lines);const nodes=[...h.host.children],before=h.mutations();
 for(let i=0;i<1000;i++)h.view.update(lines);
 assert.equal(h.mutations(),before);assert.deepEqual(h.host.children,nodes);
 h.view.update([...lines,'new']);assert.equal(h.host.children[0],nodes[0]);assert.equal(h.mutations()-before,2);
});
test('selection survives progress changes and trimming until the user deselects',()=>{
 const h=harness();h.view.update(['old progress','selected line']);const selected=h.host.children[1],before=h.mutations();h.select(true);
 h.view.update(['new progress','selected line','new']);assert.equal(h.mutations(),before);assert.equal(h.host.children[1],selected);
 h.view.update(Array.from({length:1200},(_,i)=>'Log '+i));assert.equal(h.mutations(),before);assert.equal(h.view.lines().length,1000);
 h.events.pointerup();assert.equal(h.mutations(),before);h.select(false);assert.equal(h.host.children.length,1000);assert.equal(h.host.children[0].textContent,'Log 200');
});
test('background rendering is deferred, latest content resumes, and clear discards pending logs',()=>{
 const h=harness();h.view.update(['old']);const before=h.mutations();h.hidden(true);h.view.update(['new']);h.view.update(['latest']);assert.equal(h.mutations(),before);h.hidden(false);assert.equal(h.host.children[0].textContent,'latest');
 h.select(true);h.view.update(['pending']);h.view.clear();h.select(false);assert.equal(h.host.children.length,0);
});
test('a pointer drag protects text before the selection range has formed',()=>{
 const h=harness();h.view.update(['old']);h.events.pointerdown();h.view.update(['new']);assert.equal(h.host.children[0].textContent,'old');h.events.pointercancel();assert.equal(h.host.children[0].textContent,'new');
});
