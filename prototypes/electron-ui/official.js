'use strict';
// Public content is independent of authentication, verification and operation polling.
window.createOfficialContentController = ({call,changed,report,schedule=setTimeout,cancel=clearTimeout}) => {
  let signature,revision=0,timer=null,value=null;
  const notify=()=>changed(value);
  async function read(refresh=false) {
    const ticket=++revision;cancel(timer);
    value={key:value?.key,state:'loading',links:[],activities:[],notices:[]};notify();
    async function poll(first) {
      try {
        const result=await call('readOfficial',{refresh:first&&refresh});
        if(ticket!==revision)return;
        value=result;notify();
        if(value.state==='loading')timer=schedule(()=>void poll(false),500);
        else if(value.state==='failed')report('资讯读取失败，请重试或打开游戏官网。');
      } catch {
        if(ticket!==revision)return;
        value={...value,state:'failed',error:'unavailable'};notify();
        report('资讯读取失败，请重试或打开游戏官网。');
      }
    }
    await poll(true);
  }
  return Object.freeze({
    sync(snapshot) {
      const global=snapshot?.regions.find(x=>x.id==='ffxiv');
      const next=snapshot&&JSON.stringify([snapshot.selectedRegion,...(snapshot.selectedRegion==='ffxiv'?[global?.cdKeyVersion,global?.euShopLanguage]:[])]);
      if(next===signature)return;
      signature=next;value=null;revision++;cancel(timer);notify();
      if(snapshot)void read();
    },
    refresh:()=>read(true),
    view:()=>value,
    async open(key,item) {
      if(!key||key!==value?.key)return;
      try {await call('openOfficial',{key,item});}
      catch {report('无法打开页面，请刷新资讯后重试；若仍失败，请查看日志。',true);}
    }
  });
};

window.createOfficialContentPage = ({call,ready,report}) => {
  const {t}=window.previewI18n;
  let message='';const rendered=new WeakMap();
  const controller=window.createOfficialContentController({call,changed:()=>{message='';present();},report:(text,inline)=>{message=inline?text:'';report(text);present();}});
  function present() {
    const value=controller.view();
    for(const section of document.querySelectorAll('[data-news-kind]')) {
      const kind=section.dataset.newsKind,list=section.querySelector('.news');
      let refresh=section.querySelector('[data-action="refresh-official"]');
      if(!refresh) {
        refresh=document.createElement('button');refresh.className='quiet';refresh.dataset.action='refresh-official';
        section.querySelector('.sample-label')?.replaceWith(refresh);
      }
      const label=t(value?.state==='loading'?'正在读取…':'刷新');if(refresh.textContent!==label)refresh.textContent=label;refresh.disabled=!ready()||value?.state==='loading';
      const key=JSON.stringify([document.documentElement.lang,value?.key,value?.state,value?.[kind]]);
      if(rendered.get(list)===key){for(const button of list.querySelectorAll('button'))button.disabled=!ready();continue;}
      rendered.set(list,key);list.replaceChildren();
      if(value?.state!=='ready'||!value[kind].length) {
        const line=document.createElement('li');line.className='news-feedback';line.setAttribute('role','status');
        line.textContent=t(value?.state==='failed'?'资讯读取失败，请重试或打开游戏官网。':value?.state==='ready'?'暂无资讯。':'正在读取…');list.append(line);
      } else for(const item of value[kind]) {
        const row=document.createElement('li'),button=document.createElement('button');
        button.className='news-title';button.dataset.action='open-official';button.dataset.officialItem=item.id;button.dataset.officialKey=value.key;
        button.setAttribute('data-no-translate','');button.textContent=item.title;button.title=item.title;button.disabled=!ready();row.append(button);
        if(item.date){const date=document.createElement('time');date.dateTime=item.date;date.title=item.date;date.textContent=item.date.slice(5).replace('-',' / ');row.append(date);}
        list.append(row);
      }
    }
    for(const button of document.querySelectorAll('.links [data-official-item]')) {
      button.dataset.officialKey=value?.key||'';button.disabled=!ready()||!value?.links.includes(button.dataset.officialItem);
      if(!button.disabled)button.removeAttribute('title');
    }
    let feedback=document.querySelector('#official-feedback');
    const host=document.querySelector('.news-column');
    if(host&&!feedback){feedback=document.createElement('p');feedback.id='official-feedback';feedback.className='inline-error';feedback.setAttribute('role','status');host.append(feedback);}
    if(feedback){feedback.hidden=!message;feedback.textContent=t(message);}
  }
  return Object.freeze({...controller,present});
};
