// Reads the MSShift event log out, from inside a signed-in MSShift tab (Fadi's
// decision 2026-10-20). READ ONLY: runs searches and opens event detail pages.
// One JSON download per month (msshift-events-YYYY-MM.json), newest month first.
// Resumable: finished months are remembered in localStorage (month names only), so
// running it again skips them. Stops if MSShift signs out. Progress (counts only)
// is in window.__msxe.
//
// Usage: run this file in the MSShift tab, then __msxeRun('2020-10') (oldest month).
(() => {
  const SEARCH = '/d/EventLog/Assignment/SearchAssignment/DetailSearchAssignment';
  const PAUSE_MS = 250;
  const DONE_KEY = '__msxEventsDone';
  const sleep = ms => new Promise(r => setTimeout(r, ms));
  const state = window.__msxe = { stage: 'ready', months: {}, log: [] };
  const done = () => JSON.parse(localStorage.getItem(DONE_KEY) || '[]');
  const markDone = m => localStorage.setItem(DONE_KEY, JSON.stringify([...new Set([...done(), m])]));
  const pad = n => String(n).padStart(2, '0');
  const us = d => `${pad(d.getMonth() + 1)}/${pad(d.getDate())}/${d.getFullYear()}`;

  let frame;
  const loaded = (ms = 90000) => new Promise((resolve, reject) => {
    const t = setTimeout(() => reject(new Error('page load timed out')), ms);
    frame.addEventListener('load', () => { clearTimeout(t); setTimeout(resolve, 300); }, { once: true });
  });
  const quiet = w => { try { w.alert = m => state.log.push('msshift says: ' + m); w.confirm = () => true; } catch { } };
  const signedOut = (doc, url) => /default|login/i.test(url || '') || /LOGIN PAGE/i.test(doc.title || '');

  // All events of one date range: [{ row: <list row html>, url }]
  async function search(from, to) {
    frame.src = SEARCH;
    await loaded();
    if (signedOut(frame.contentWindow.document, frame.contentWindow.location.pathname)) throw new Error('SIGNED_OUT');
    quiet(frame.contentWindow);
    const d = frame.contentWindow.document;
    d.getElementById('T1Box').value = `${from} 00:00`;
    d.getElementById('T2Box').value = `${to} 23:59`;
    let next = loaded();
    [...d.querySelectorAll('input[type=button],input[type=submit],button')].find(b => /search/i.test(b.value || b.textContent)).click();
    await next;
    quiet(frame.contentWindow);
    let rd = frame.contentWindow.document;
    const total = Number((rd.body.innerText.match(/Total of (\d+)/) || [])[1] || 0);
    const size = rd.querySelector('select[name=PageSize]');
    if (size && total > 10) {
      size.value = [...size.options].find(o => /all/i.test(o.text)).value;
      next = loaded();
      size.dispatchEvent(new Event('change', { bubbles: true }));
      await Promise.race([next, sleep(8000).then(() => { if (frame.contentWindow.document === rd) { size.form.submit(); return loaded(); } })]);
      quiet(frame.contentWindow);
      rd = frame.contentWindow.document;
    }
    const seen = new Set();
    const out = [];
    for (const el of rd.querySelectorAll('[onclick^=OpenWindowH_Event]')) {
      const url = el.getAttribute('onclick').split("'")[1];
      if (!url || seen.has(url)) continue;
      seen.add(url);
      const tr = el.closest('tr');
      out.push({ url, row: tr ? tr.outerHTML.replace(/\s{2,}/g, ' ') : null });
    }
    return { total, items: out };
  }

  function clean(html) {
    const doc = new DOMParser().parseFromString(html, 'text/html');
    doc.querySelectorAll('script,style,link,meta,noscript,iframe').forEach(e => e.remove());
    return doc.body ? doc.body.innerHTML.replace(/\s{2,}/g, ' ') : '';
  }

  window.__msxeFiles = window.__msxeFiles || {};
  // Only the last 3 files stay in memory for a retry (a month is ~15 MB).
  function save(name, data) {
    window.__msxeFiles[name] = URL.createObjectURL(new Blob([JSON.stringify(data)], { type: 'application/json' }));
    window.__msxeSaveAgain(name);
    for (const old of Object.keys(window.__msxeFiles).slice(0, -3)) {
      URL.revokeObjectURL(window.__msxeFiles[old]);
      delete window.__msxeFiles[old];
    }
  }
  window.__msxeSaveAgain = name => {
    const a = document.createElement('a');
    a.href = window.__msxeFiles[name];
    a.download = name;
    document.body.appendChild(a);
    a.click();
    a.remove();
  };

  // Weeks (Mon..Sun cut at month edges) of one month.
  function weeks(year, month) {
    const out = [];
    const last = new Date(year, month, 0).getDate();
    for (let day = 1; day <= last; day += 7) {
      out.push([us(new Date(year, month - 1, day)), us(new Date(year, month - 1, Math.min(day + 6, last)))]);
    }
    return out;
  }

  window.__msxeRun = async (oldest) => {
    frame = document.createElement('iframe');
    frame.style.cssText = 'position:fixed;left:-3000px;top:0;width:1200px;height:800px;';
    document.body.appendChild(frame);
    const now = new Date();
    const [oy, om] = oldest.split('-').map(Number);
    try {
      for (let y = now.getFullYear(), m = now.getMonth() + 1; y > oy || (y === oy && m >= om); m === 1 ? (y--, m = 12) : m--) {
        const key = `${y}-${pad(m)}`;
        if (done().includes(key)) continue;
        const records = [];
        let listed = 0;
        for (const [from, to] of weeks(y, m)) {
          state.stage = `${key}: searching ${from} - ${to}`;
          const { total, items } = await search(from, to);
          listed += total;
          for (let i = 0; i < items.length; i++) {
            state.stage = `${key}: event ${records.length + 1} (week of ${from})`;
            try {
              const r = await fetch(items[i].url, { credentials: 'same-origin' });
              if (/default|login/i.test(new URL(r.url).pathname)) throw new Error('SIGNED_OUT');
              records.push({ row: items[i].row, status: r.status, html: clean(await r.text()) });
            } catch (e) {
              if (String(e).includes('SIGNED_OUT')) throw e;
              records.push({ row: items[i].row, error: String(e) });
            }
            await sleep(PAUSE_MS);
          }
        }
        state.months[key] = { listed, saved: records.filter(r => r.html).length, failed: records.filter(r => !r.html).length };
        save(`msshift-events-${key}.json`, { source: 'MSShift', kind: 'events', month: key, readOn: new Date().toISOString(), records });
        markDone(key);
        state.log.push(`${key}: saved ${records.length}`);
      }
      state.stage = 'finished';
    } catch (e) {
      state.stage = String(e).includes('SIGNED_OUT') ? 'stopped: MSShift signed out - sign in again and re-run' : `stopped: ${e}`;
    } finally {
      frame.remove();
    }
  };
})();
