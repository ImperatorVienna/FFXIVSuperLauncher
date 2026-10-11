'use strict';
// This auxiliary tool works without the backend; input never leaves the renderer or enters a log.
async function localOtp(secret,seconds=Date.now()/1000,subtle=crypto.subtle){
  const input=secret.replaceAll(' ','').trim().replace(/=+$/,'').toUpperCase(),bytes=[];let buffer=0,bits=0;
  for(const char of input){const value='ABCDEFGHIJKLMNOPQRSTUVWXYZ234567'.indexOf(char);if(value<0)throw new Error('Invalid Base32');buffer=(buffer<<5)|value;bits+=5;if(bits>=8){bits-=8;bytes.push((buffer>>bits)&255);}}
  if(bytes.length<10||(bits&&(buffer&((1<<bits)-1))))throw new Error('Invalid Base32');
  const keyBytes=Uint8Array.from(bytes);bytes.fill(0);
  try{
    const key=await subtle.importKey('raw',keyBytes,{name:'HMAC',hash:'SHA-1'},false,['sign']);
    const counter=new Uint8Array(8);new DataView(counter.buffer).setBigUint64(0,BigInt(Math.floor(seconds/30)));
    const hash=new Uint8Array(await subtle.sign('HMAC',key,counter)),offset=hash[hash.length-1]&15;
    return String((((hash[offset]&127)<<24)|(hash[offset+1]<<16)|(hash[offset+2]<<8)|hash[offset+3])%1000000).padStart(6,'0');
  }finally{keyBytes.fill(0);}
}
if(typeof module!=='undefined')module.exports={localOtp};
if(typeof window!=='undefined')window.openLocalOtp=(initial='')=>{
  const {t}=window.previewI18n;const dialog=document.createElement('dialog');dialog.dataset.otpUi='';
  const heading=document.createElement('h2');heading.textContent=t('查看 2FA 验证码');
  const note=document.createElement('p');note.textContent=t('完全本地计算，不发送登录请求、不保存输入。可与手机验证器同时显示的验证码对照。使用当前系统时间（30 秒周期）。');
  const secret=document.createElement('input');secret.type='password';secret.autocomplete='off';secret.placeholder=t('Base32 密钥');secret.value=initial;initial='';secret.maxLength=4096;
  const code=document.createElement('input');code.readOnly=true;code.className='local-otp-code';code.setAttribute('aria-label',t('验证码'));
  const status=document.createElement('p');status.className='muted';
  const copy=document.createElement('button');copy.textContent=t('复制验证码');copy.disabled=true;copy.addEventListener('click',()=>void navigator.clipboard.writeText(code.value).catch(()=>{status.textContent=t('复制失败，请手动复制。');}));
  const close=document.createElement('button');close.textContent=t('关闭');close.addEventListener('click',()=>dialog.close());
  let generation=0,closed=false;
  async function refresh(){const version=++generation,now=Date.now()/1000;try{const value=await localOtp(secret.value,now);if(closed||version!==generation)return;code.value=value;copy.disabled=false;status.textContent=t('有效时间剩余 {seconds} 秒',{seconds:30-Math.floor(now)%30});}
    catch{if(closed||version!==generation)return;code.value='';copy.disabled=true;status.textContent=t('请输入有效的 Base32 密钥。');}}
  secret.addEventListener('input',()=>void refresh());const timer=setInterval(()=>void refresh(),1000);
  dialog.addEventListener('close',()=>{closed=true;generation++;clearInterval(timer);secret.value='';code.value='';dialog.remove();},{once:true});
  const actions=document.createElement('div');actions.className='row dialog-actions';actions.append(copy,close);
  dialog.append(heading,note,secret,code,status,actions);document.body.append(dialog);dialog.showModal();void refresh();
};
