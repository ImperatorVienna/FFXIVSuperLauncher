'use strict';
// Keep one Electron runtime for the launcher and the independent official-site browser.
if(process.argv.includes('--super-launcher'))require('../../../../frontend/main.cjs');
else require('./official-main.cjs');
