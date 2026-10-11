'use strict';
// Presentation only: no account values, option values or diagnostic log lines are translated.
window.previewI18n = (() => {
  const languages = {'简体中文':'zh-CN', '繁體中文':'zh-TW', '日本語':'ja', English:'en'};
  const columns = {'zh-TW':0, ja:1, en:2};
  const originals = new WeakMap();
  let language = 'zh-CN';
  function t(source, parameters = {}) {
    const translation = window.previewStrings[source]?.[columns[language]] ?? source;
    return translation.replace(/\{(\w+)\}/g, (match, key) => parameters[key] ?? match);
  }
  function translate(source) {
    return source.replace(/^(\s*)([\s\S]*?)(\s*)$/, (_, before, text, after) => before + t(text) + after);
  }
  function apply(root = document.body) {
    const walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT);
    for (let node; (node = walker.nextNode());) {
      if (node.parentElement.closest('script,style,textarea,[data-no-translate],#log > div')) continue;
      const previous = originals.get(node);
      const source = previous?.output === node.textContent ? previous.source : node.textContent;
      const output = translate(source);
      originals.set(node, {source, output});
      if (node.textContent !== output) node.textContent = output;
    }
    for (const element of [root, ...root.querySelectorAll('[title],[placeholder],[aria-label]')]) {
      if (element.closest('[data-no-translate]')) continue;
      for (const name of ['title','placeholder','aria-label']) {
        const attribute = element.getAttributeNode(name);
        if (!attribute) continue;
        const previous = originals.get(attribute);
        const source = previous?.output === attribute.value ? previous.source : attribute.value;
        const output = translate(source);
        originals.set(attribute, {source, output});
        if (attribute.value !== output) attribute.value = output;
      }
    }
    document.title = t('FFXIV Super Launcher · 交互预览');
  }
  function set(name) {
    language = languages[name] ?? 'zh-CN';
    document.documentElement.lang = language;
  }
  return Object.freeze({t, apply, set, languages});
})();
