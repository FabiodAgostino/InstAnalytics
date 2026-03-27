const SERVER = 'http://localhost:27182';

async function isServerRunning() {
  try {
    const res = await fetch(`${SERVER}/status`, { signal: AbortSignal.timeout(1500) });
    return res.ok;
  } catch {
    return false;
  }
}

document.addEventListener('DOMContentLoaded', async () => {
  const profileDiv = document.getElementById('profile');
  const stopBtn    = document.getElementById('stopBtn');
  const msgDiv     = document.getElementById('msg');

  function setMsg(text, isError = false) {
    msgDiv.textContent = text;
    msgDiv.className = 'msg ' + (isError ? 'err' : 'ok');
  }

  // 1. Check current tab
  const [tab] = await chrome.tabs.query({ active: true, currentWindow: true });
  if (!tab?.url?.includes('instagram.com')) {
    profileDiv.textContent = 'Apri un profilo Instagram';
    return;
  }

  // 2. Ask content script for username
  let username = null;
  try {
    const resp = await chrome.tabs.sendMessage(tab.id, { type: 'GET_USERNAME' });
    username = resp?.username ?? null;
  } catch {
    // content script not injected yet (e.g. extension just installed)
  }

  if (!username) {
    profileDiv.innerHTML = 'Naviga su un <span>profilo utente</span>';
    return;
  }

  profileDiv.innerHTML = `Profilo: <span>@${username}</span>`;

  // 3. Check if desktop app is running
  if (!(await isServerRunning())) {
    setMsg('InstAnalytics non è in esecuzione', true);
    return;
  }

  stopBtn.disabled = false;
  stopBtn.textContent = `Stop Tracking @${username}`;

  // 4. Handle click
  stopBtn.addEventListener('click', async () => {
    stopBtn.disabled = true;
    msgDiv.textContent = '';

    try {
      const res = await fetch(`${SERVER}/exclude`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ username }),
        signal: AbortSignal.timeout(3000),
      });

      if (res.ok) {
        stopBtn.textContent = 'Escluso!';
        setMsg(`@${username} non verrà più tracciato`);
      } else {
        setMsg('Errore dal server', true);
        stopBtn.disabled = false;
      }
    } catch {
      setMsg('Errore di connessione', true);
      stopBtn.disabled = false;
    }
  });
});
