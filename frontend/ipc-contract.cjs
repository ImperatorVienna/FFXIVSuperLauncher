'use strict';
const text = limit => value => typeof value === 'string' && value.length <= limit;
const bool = value => typeof value === 'boolean';
const region = value => ['ffxiv_cn','ffxiv_tc','ffxiv'].includes(value);
const fields = (shape, required=[]) => value => value && typeof value === 'object' && !Array.isArray(value) &&
  required.every(key => Object.hasOwn(value,key)) && Object.entries(value).every(([key,item]) => Object.hasOwn(shape,key) && shape[key](item));
const identity = {region,name:text(256)};
const operation = {operationId:text(64)};
const array = (validate,limit) => value => Array.isArray(value) && value.length<=limit && value.every(validate);
const content = value => ['third-party-repositories','installed-plugins','plugin-settings'].includes(value);
const pluginAction = fields({region,kind:v=>['apply','check-updates','install-updates','dalamud-update','sync','check-repository'].includes(v),revision:text(64),
  states:v=>v && typeof v==='object' && !Array.isArray(v) && Object.keys(v).length<=1000 && Object.entries(v).every(([k,x])=>text(256)(k)&&bool(x)),
  url:text(4096),planId:text(64),plugins:array(text(256),1000),content,source:region,targets:array(region,2)},['region','kind']);
const contracts = Object.freeze({
  snapshot:fields({}),
  'system.initialize':fields({}),
  'captcha.page':fields({...operation,challengeId:text(64),value:v=>v===''},['operationId','challengeId','value']),
  'captcha.fail':fields({...operation,challengeId:text(64),value:v=>['renderer-exited','page-load','verification-timeout','invalid-token','token-delivery','page-command','page-initialization'].includes(v)},['operationId','challengeId','value']),
  'captcha.reply':fields({...operation,challengeId:text(64),value:text(16384)},['operationId','challengeId','value']),
  'steam.attach':fields({}),
  'window.save':fields({x:Number.isInteger,y:Number.isInteger,width:Number.isFinite,height:Number.isFinite,maximized:bool},['x','y','width','height','maximized']),
  'setup.complete':fields({enableDalamud:bool},['enableDalamud']),
  'about.read':fields({}),
  'about.open':fields({key:text(128)},['key']),
  'about.notice':fields({key:v=>['license','third-party'].includes(v)},['key']),
  'desktop.install':fields({}),
  'updates.read':fields({refresh:bool}),
  'updates.open':fields({release:text(64)},['release']),
  'updates.install':fields({release:text(64)},['release']),
  'official.read':fields({refresh:bool}),
  'official.open':fields({key:text(64),item:text(64)},['key','item']),
  'preferences.update':fields({autoCheckUpdates:bool,language:text(32),region,clientPaths:fields({ffxiv_cn:text(4096),ffxiv_tc:text(4096),ffxiv:text(4096)}),
    cdKeyVersion:v=>['JP','NA','EU'].includes(v),euShopLanguage:v=>['en-gb','fr-fr','de-de'].includes(v),clientLanguage:v=>Number.isInteger(v)&&v>=0&&v<=3}),
  'accounts.list':fields({region},['region']),
  'accounts.read':fields(identity,['region','name']),
  'accounts.save':fields({...identity,existing:bool,password:text(4096),secret:text(4096),delete:bool,revision:text(64)},['region','name','existing']),
  'plugins.read':fields({region},['region']),
  'plugins.repositories.read':fields({region},['region']),
  'plugins.repositories.save':fields({region,revision:text(64),presetsEnabled:bool,entries:array(fields({url:text(4096),isEnabled:bool},['url','isEnabled']),100)},['region','revision','entries','presetsEnabled']),
  'plugins.sources':fields({content},['content']),
  'plugins.start':pluginAction,
  'travel.start':fields({kind:v=>['open','source','character','history','transfer','return'].includes(v),account:text(256),session:text(64),revision:text(64),source:text(64),character:text(64),target:text(64),order:text(64),current:text(64),page:v=>Number.isInteger(v)&&v>0&&v<=10000},['kind']),
  'travel.close':fields({}),
  'compatibility.read':fields({}),
  'compatibility.update':fields({protonScript:text(4096),steamRoot:text(4096),dataDirectory:text(4096),registered:bool,useSteamPrefix:bool,steamPrefix:text(4096)}),
  'compatibility.diagnose':fields({}),
  'areas.list':fields({region},['region']),
  'operations.start':fields({region,kind:v=>['launch','game-update'].includes(v),account:text(256),areaName:text(256),autoOtp:bool,steamAccount:bool,freeTrial:bool,enableDalamud:bool,noPlugins:bool},['region','kind']),
  'operations.poll':fields(operation,['operationId']),
  'operations.clear-log':fields(operation,['operationId']),
  'operations.cancel':fields(operation,['operationId']),
  'operations.reply':fields({...operation,challengeId:text(64),value:text(32)},['operationId','challengeId','value'])
});
module.exports={contracts};
