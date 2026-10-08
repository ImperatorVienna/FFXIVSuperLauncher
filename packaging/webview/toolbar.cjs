const { contextBridge, ipcRenderer } = require('electron');
contextBridge.exposeInMainWorld('official', { action: name => {
  if (['back', 'forward', 'reload', 'external'].includes(name)) ipcRenderer.send('official-action', name);
} });

contextBridge.exposeInMainWorld("launcherLanguage", process.argv.find(x=>x.startsWith("--ui-language="))?.split("=")[1] || "en");

let labels = ["Back", "Forward", "Reload", "Open in system browser"];
try { const arg = process.argv.find(x=>x.startsWith('--toolbar-labels=')); const value=JSON.parse(arg.slice('--toolbar-labels='.length)); if(Array.isArray(value)&&value.length===4&&value.every(x=>typeof x==='string')) labels=value; } catch {}
contextBridge.exposeInMainWorld('toolbarLabels', labels);
