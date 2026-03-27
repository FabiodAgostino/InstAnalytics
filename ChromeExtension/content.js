'use strict';

const SERVER = 'http://localhost:27182';

const NON_PROFILE_PATHS = new Set([
  '', 'accounts', 'explore', 'reels', 'stories', 'direct',
  'p', 'tv', 'reel', 'ar', 'challenge', 'about', 'legal',
]);

function getProfileUsername() {
  const parts = window.location.pathname.replace(/^\/|\/$/g, '').split('/');
  const segment = parts[0];
  if (!segment || NON_PROFILE_PATHS.has(segment)) return null;
  if (!/^[a-zA-Z0-9._]{1,30}$/.test(segment)) return null;
  return segment;
}


// ─── Inline button on profile page ───────────────────────────────────────────

const SVG_X = `<svg width="14" height="14" viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg">
  <path d="M18 6L6 18M6 6l12 12" stroke="white" stroke-width="2.5" stroke-linecap="round"/>
</svg>`;

const SVG_CHECK = `<svg width="14" height="14" viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg">
  <path d="M5 13l4 4L19 7" stroke="white" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"/>
</svg>`;

const SVG_SPIN = `<svg width="14" height="14" viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg" style="animation:ia-spin 0.8s linear infinite;">
  <circle cx="12" cy="12" r="9" stroke="white" stroke-width="2.5" stroke-dasharray="40" stroke-dashoffset="20" stroke-linecap="round"/>
</svg>`;

function injectProfileButton() {
  if (document.getElementById('ia-profile-btn')) return;

  const username = getProfileUsername();
  if (!username) return;

  const svg = document.querySelector('svg[aria-label="Account simili"]');
  if (!svg) return;

  const anchor = svg.closest('[role="button"]'); // bottone "Account simili"
  if (!anchor) return;

  const rect = anchor.getBoundingClientRect();
  if (!rect.width) return; // non ancora visibile

  if (!document.getElementById('ia-style')) {
    const style = document.createElement('style');
    style.id = 'ia-style';
    style.textContent = '@keyframes ia-spin { to { transform: rotate(360deg); } }';
    document.head.appendChild(style);
  }

  // Il bottone è figlio di document.body con position:fixed —
  // completamente fuori dall'albero CSS di IG, nessun antenato può interferire
  const btn = document.createElement('div');
  btn.id = 'ia-profile-btn';
  btn.setAttribute('role', 'button');
  btn.setAttribute('tabindex', '0');
  btn.title = 'Smetti di seguire — InstAnalytics';
  btn.style.cssText = [
    'position:fixed',
    `left:${rect.right + 8}px`,
    `top:${rect.top + (rect.height - 32) / 2}px`,
    'width:32px', 'height:32px', 'border-radius:8px',
    'background:#E94560', 'cursor:pointer',
    'display:flex', 'align-items:center', 'justify-content:center',
    'transition:background 0.2s',
    'z-index:2147483647',
    'box-sizing:border-box',
  ].join(';');
  btn.innerHTML = SVG_X;

  // Aggiorna posizione se la pagina scrolla o viene ridimensionata
  function updatePos() {
    const r = anchor.getBoundingClientRect();
    btn.style.left = (r.right + 8) + 'px';
    btn.style.top  = (r.top + (r.height - 32) / 2) + 'px';
  }
  window.addEventListener('scroll', updatePos, { passive: true });
  window.addEventListener('resize', updatePos, { passive: true });

  document.body.appendChild(btn);

  // Rilevamento click tramite coordinate in capture phase —
  // i listener del content script non possono essere bloccati da stopPropagation della pagina
  let done = false;
  document.addEventListener('mousedown', function onMouseDown(e) {
    const b = document.getElementById('ia-profile-btn');
    if (!b) { document.removeEventListener('mousedown', onMouseDown, true); return; }
    const r = b.getBoundingClientRect();
    const hit = e.clientX >= r.left && e.clientX <= r.right &&
                e.clientY >= r.top  && e.clientY <= r.bottom;
    if (!hit) return;
    if (done) return;
    done = true;
    b.innerHTML = SVG_SPIN;
    b.style.cursor = 'default';
    chrome.runtime.sendMessage({ type: 'UNFOLLOW' });
    setTimeout(() => {
      b.style.background = '#4ECCA3';
      b.innerHTML = SVG_CHECK;
      b.title = 'Smesso di seguire!';
    }, 1800);
  }, true); // capture phase
}

// ─── Broken-profile detection ─────────────────────────────────────────────────

function checkBrokenProfile() {
  const username = getProfileUsername();
  if (!username) return;
  if (document.getElementById('ia-broken-alert')) return;

  const text = document.body.innerText || '';
  const isBroken =
    text.includes('questa pagina non è disponibile') ||
    text.includes("this page isn't available");

  if (!isBroken) return;
  showBrokenAlert(username);
}

function showBrokenAlert(username) {
  const overlay = document.createElement('div');
  overlay.id = 'ia-broken-alert';
  overlay.style.cssText = [
    'position:fixed', 'inset:0', 'background:rgba(0,0,0,0.55)',
    'display:flex', 'align-items:center', 'justify-content:center',
    'z-index:2147483647',
    'font-family:-apple-system,BlinkMacSystemFont,"Segoe UI",sans-serif',
  ].join(';');

  const box = document.createElement('div');
  box.style.cssText = [
    'background:#1A1A2E', 'color:#EAEAEA', 'border-radius:14px',
    'padding:28px 30px', 'max-width:380px', 'width:90%',
    'box-shadow:0 8px 40px rgba(0,0,0,0.6)',
  ].join(';');

  box.innerHTML = `
    <div style="font-size:18px;font-weight:700;margin-bottom:12px;">📊 InstAnalytics</div>
    <p style="font-size:14px;line-height:1.6;margin-bottom:22px;color:#CCCCCC;">
      Il profilo <strong style="color:#EAEAEA;">@${username}</strong> non è disponibile.<br>
      Vuoi rimuoverlo dal tracking?
    </p>
    <div style="display:flex;gap:10px;justify-content:flex-end;">
      <button id="ia-cancel" style="padding:9px 18px;border:1px solid #444;background:transparent;color:#EAEAEA;border-radius:8px;cursor:pointer;font-size:13px;">Annulla</button>
      <button id="ia-confirm" style="padding:9px 18px;background:#E94560;color:#fff;border:none;border-radius:8px;cursor:pointer;font-size:13px;font-weight:600;">Rimuovi</button>
    </div>
  `;

  overlay.appendChild(box);
  document.body.appendChild(overlay);

  overlay.addEventListener('click', e => { if (e.target === overlay) overlay.remove(); });
  document.getElementById('ia-cancel').addEventListener('click', () => overlay.remove());

  document.getElementById('ia-confirm').addEventListener('click', async () => {
    const confirmBtn = document.getElementById('ia-confirm');
    confirmBtn.textContent = 'Rimozione…';
    confirmBtn.disabled = true;
    try {
      await fetch(`${SERVER}/exclude`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ username }),
        signal: AbortSignal.timeout(3000),
      });
    } catch { /* ignore */ }
    overlay.remove();
    chrome.runtime.sendMessage({ type: 'CLOSE_TAB' });
  });
}

// ─── Auto-unfollow (triggered by ?ia_unfollow=1 from the desktop app) ─────────

let autoUnfollowTriggered = false;

function reportUnfollowResult(username, status) {
  fetch(`${SERVER}/unfollow-result`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ username, status }),
    signal: AbortSignal.timeout(4000),
  }).catch(() => {});
}

function checkAutoUnfollow() {
  if (autoUnfollowTriggered) return;
  if (!new URLSearchParams(location.search).has('ia_unfollow')) return;

  const username = getProfileUsername();
  if (!username) return;

  // Mark as started immediately to prevent re-entry from MutationObserver
  autoUnfollowTriggered = true;

  const MAX_WAIT = 10000;
  const POLL_INTERVAL = 400;
  let elapsed = 0;

  const poll = setInterval(() => {
    elapsed += POLL_INTERVAL;

    // Check for broken profile first
    const text = document.body.innerText || '';
    const isBroken =
      text.includes('questa pagina non è disponibile') ||
      text.includes("this page isn't available");

    if (isBroken) {
      clearInterval(poll);
      fetch(`${SERVER}/exclude`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ username }),
        signal: AbortSignal.timeout(3000),
      }).catch(() => {});
      reportUnfollowResult(username, 'excluded');
      setTimeout(() => chrome.runtime.sendMessage({ type: 'CLOSE_TAB' }), 1000);
      return;
    }

    const buttons = Array.from(document.querySelectorAll('button'));

    // Following → unfollow
    const followingBtn = buttons.find(b => b.textContent.includes('Segui già'));
    if (followingBtn) {
      clearInterval(poll);
      chrome.runtime.sendMessage({ type: 'UNFOLLOW' });
      setTimeout(() => {
        reportUnfollowResult(username, 'unfollowed');
        setTimeout(() => chrome.runtime.sendMessage({ type: 'CLOSE_TAB' }), 600);
      }, 4000);
      return;
    }

    // Page fully loaded but "Segui già" absent → not following, close immediately.
    // Indicators: "Segui" / "Richiedi" button, or the "Account simili" SVG which
    // only renders once the profile header is complete.
    const pageReady =
      buttons.some(b => {
        const t = b.textContent.trim();
        return t === 'Segui' || t.startsWith('Richiedi');
      }) ||
      document.querySelector('svg[aria-label="Account simili"]') !== null ||
      document.querySelector('svg[aria-label="Similar accounts"]') !== null;

    if (pageReady) {
      clearInterval(poll);
      reportUnfollowResult(username, 'already_removed');
      setTimeout(() => chrome.runtime.sendMessage({ type: 'CLOSE_TAB' }), 400);
      return;
    }

    if (elapsed >= MAX_WAIT) {
      clearInterval(poll);
      reportUnfollowResult(username, 'already_removed');
      setTimeout(() => chrome.runtime.sendMessage({ type: 'CLOSE_TAB' }), 600);
    }
  }, POLL_INTERVAL);
}

// ─── SPA navigation & observer ────────────────────────────────────────────────

function run() {
  const isAutoMode = new URLSearchParams(location.search).has('ia_unfollow');
  if (!isAutoMode) {
    injectProfileButton();
    checkBrokenProfile();
  }
  checkAutoUnfollow();
}

let lastPath = location.pathname;
let runTimer = null;

function scheduleRun(delay = 300) {
  clearTimeout(runTimer);
  runTimer = setTimeout(run, delay);
}

const observer = new MutationObserver(() => {
  if (location.pathname !== lastPath) {
    lastPath = location.pathname;
    document.getElementById('ia-profile-btn')?.remove();
    document.getElementById('ia-broken-alert')?.remove();
    scheduleRun(900);
  } else {
    scheduleRun(300);
  }
});

observer.observe(document.body, { childList: true, subtree: true });
scheduleRun(600);

// ─── Listener messaggi dal popup ──────────────────────────────────────────────

chrome.runtime.onMessage.addListener((message, _sender, sendResponse) => {
  if (message.type === 'GET_USERNAME') {
    sendResponse({ username: getProfileUsername() });
  }
  return true;
});
