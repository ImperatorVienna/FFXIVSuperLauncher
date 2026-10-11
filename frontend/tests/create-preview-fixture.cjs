'use strict';
const fs=require('node:fs');const path=require('node:path');
const root=path.resolve(__dirname,'../..');
const out=path.join(root,'artifacts/frontend-ui-tests');fs.mkdirSync(out,{recursive:true});
let html=fs.readFileSync(path.join(root,'prototypes/electron-ui/index.html'),'utf8');
html=html.replaceAll('src="../../src/','src="/src/').replace(/(src|href)="(?!\/|data:)([^"]+)"/g,'$1="/prototypes/electron-ui/$2"');
html=html.replace('<script defer src=', '<script defer src="/frontend/tests/fixture-bridge.js"></script>\n  <script defer src=');
// Re-generating the fixture must also invalidate cached development scripts/styles.
html=html.replace(/(src|href)="(\/(?:prototypes|frontend)\/[^"?]+\.(?:js|css))"/g,
  (_,attribute,url)=>`${attribute}="${url}?v=${fs.statSync(path.join(root,url)).mtimeMs}"`);
fs.writeFileSync(path.join(out,'index.html'),html);
console.log('Browser fixture: http://127.0.0.1:8765/artifacts/frontend-ui-tests/');
