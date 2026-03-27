'use strict';

// Eseguita nel MAIN world via executeScript — bypassa il CSP della pagina.
// Codice identico a quello testato e funzionante in DevTools.
function executeUnfollow() {
  function simClick(el) {
    const rect = el.getBoundingClientRect();
    const opts = {
      bubbles: true, cancelable: true, view: window,
      clientX: rect.left + rect.width / 2,
      clientY: rect.top + rect.height / 2,
    };
    ['pointerdown', 'mousedown', 'pointerup', 'mouseup', 'click'].forEach(type =>
      el.dispatchEvent(new MouseEvent(type, opts))
    );
  }

  const followingBtn = Array.from(document.querySelectorAll('button'))
    .find(b => b.textContent.includes('Segui già'));

  if (!followingBtn) return;

  simClick(followingBtn);

  const obs = new MutationObserver(() => {
    for (const el of document.querySelectorAll('span, div')) {
      if (el.textContent.trim() === 'Non seguire più') {
        obs.disconnect();
        const target = el.closest('[role="button"]') || el;
        setTimeout(() => simClick(target), 200);
        return;
      }
    }
  });
  obs.observe(document.body, { childList: true, subtree: true });
  setTimeout(() => obs.disconnect(), 5000);
}

chrome.runtime.onMessage.addListener((message, sender) => {
  if (message.type === 'CLOSE_TAB' && sender.tab?.id != null) {
    chrome.tabs.remove(sender.tab.id);
  }

  if (message.type === 'UNFOLLOW' && sender.tab?.id != null) {
    chrome.scripting.executeScript({
      target: { tabId: sender.tab.id },
      world: 'MAIN',
      func: executeUnfollow,
    }).catch(err => console.error('[IA background] executeScript error:', err));
  }
});
