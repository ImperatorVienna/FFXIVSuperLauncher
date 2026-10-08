// Minimal web shell; remote pages receive no Node or preload APIs.
const { app, BrowserWindow, WebContentsView, Menu, session, ipcMain, shell } = require('electron');
const path = require('node:path');
const profile = process.argv.find(arg => arg.startsWith('--user-data-dir='));
const officialArg = process.argv.find(arg => arg.startsWith('--official-url='));
if (!profile) process.exit(2);
const official = officialArg?.slice('--official-url='.length);
function webUrl(value) { try { return ['https:', 'http:'].includes(new URL(value).protocol); } catch { return false; } }
if (official && !webUrl(official)) process.exit(2);
app.setPath('userData', profile.slice('--user-data-dir='.length));
app.enableSandbox();
app.whenReady().then(() => {
  Menu.setApplicationMenu(null);
  session.defaultSession.setPermissionRequestHandler((_contents, _permission, callback) => callback(false));
  session.defaultSession.setPermissionCheckHandler(() => false);
  session.defaultSession.on('will-download', event => event.preventDefault());
  const secure = { sandbox: true, nodeIntegration: false, contextIsolation: true, webSecurity: true };
  const window = new BrowserWindow({ width: official ? 1100 : 620, height: official ? 800 : 650,
    title: 'FFXIV Super Launcher', webPreferences: { ...secure, ...(official ? { preload: path.join(__dirname, 'toolbar.cjs'), additionalArguments: process.argv.filter(x=>x.startsWith('--ui-language=') || x.startsWith('--toolbar-labels=')) } : {}) } });
  if (!official) {
    window.webContents.setWindowOpenHandler(() => ({ action: 'deny' }));
    window.webContents.on('will-navigate', (event, url) => {
      if (url !== 'https://launcher.ffxiv.com.tw/index.html' && url !== 'about:blank') event.preventDefault();
    });
    window.loadURL('about:blank');
    return;
  }
  const memorySession = session.fromPartition('official');
  memorySession.setPermissionRequestHandler((_contents, _permission, callback) => callback(false));
  memorySession.setPermissionCheckHandler(() => false);
  memorySession.on('will-download', event => event.preventDefault());
  const view = new WebContentsView({ webPreferences: { ...secure, session: memorySession } });
  window.contentView.addChildView(view);
  function resize() { const [width, height] = window.getContentSize(); view.setBounds({ x: 0, y: 48, width, height: Math.max(0, height - 48) }); }
  window.on('resize', resize); resize();
  view.webContents.setWindowOpenHandler(({ url }) => { if (webUrl(url)) view.webContents.loadURL(url); return { action: 'deny' }; });
  view.webContents.on('will-navigate', (event, url) => { if (!webUrl(url)) event.preventDefault(); });
  window.webContents.on('will-navigate', event => event.preventDefault());
  window.webContents.setWindowOpenHandler(() => ({ action: 'deny' }));
  ipcMain.on('official-action', (event, action) => {
    if (event.sender !== window.webContents || event.senderFrame !== window.webContents.mainFrame) return;
    if (action === 'external' && webUrl(view.webContents.getURL())) shell.openExternal(view.webContents.getURL());
    if (action === 'reload') view.webContents.reload();
    if (action === 'back' && view.webContents.navigationHistory.canGoBack()) view.webContents.navigationHistory.goBack();
    if (action === 'forward' && view.webContents.navigationHistory.canGoForward()) view.webContents.navigationHistory.goForward();
  });
  window.on('closed', () => { if (!view.webContents.isDestroyed()) view.webContents.close(); });
  window.loadFile(path.join(__dirname, 'toolbar.html'));
  view.webContents.loadURL(official);
});
app.on('window-all-closed', () => app.quit());
