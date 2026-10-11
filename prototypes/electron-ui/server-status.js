// Official-page routing only. The Electron frontend will use the shared browser opener.
window.serverStatus = Object.freeze({
  url(region, cdkey = "NA", euLanguage = "English(UK)") {
    const pages = {
      ffxiv_cn: 'https://ff.web.sdo.com/web8/index.html#/servers',
      ffxiv_tc: 'https://www.ffxiv.com.tw/web/worldstatus/index.aspx',
      ffxiv: `https://${cdkey === 'JP' ? 'jp' : cdkey === 'EU' ? ({'Français':'fr','Deutsch':'de'}[euLanguage] || 'eu') : 'na'}.finalfantasyxiv.com/lodestone/worldstatus/`
    };
    if (!Object.hasOwn(pages, region)) throw new TypeError('Unknown region');
    return pages[region];
  }
});
