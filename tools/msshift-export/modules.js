// Reads other MSShift modules (lost & found, on duty, call outs) from inside a signed-in
// MSShift tab (Fadi's decision 2026-10-20). READ ONLY: runs date searches (one per month)
// and, where the results link to detail pages, opens those. Each month is posted to the
// local receiver (receiver.py, 127.0.0.1:8765) which writes it to
// C:\DeskData\import\msshift\<module>\<module>-YYYY-MM.json - nothing goes to Downloads.
// Resumable: months the receiver already has are skipped. Progress (counts only) is in
// window.__msxm.
//
// Usage: run this file in the MSShift tab, then __msxmRun('<receiver key>', ['found', 'onduty', 'callout'], '2020-10').
(() => {
  const RECEIVER = 'http://127.0.0.1:8765';
  const MODULES = {
    found: { path: '/d/LostandFound/SearchFound/SearchFound', details: true },
    onduty: { path: '/d/EventLog/dispatch/Search/SearchOfficer', details: false },
    callout: { path: '/d/EventLog/SickLog/Out_View_Tab', details: false },
  };
  const PAUSE_MS = 400;
  const sleep = ms => new Promise(r => setTimeout(r, ms));
  const pad = n => String(n).padStart(2, '0');
  const state = window.__msxm = { stage: 'ready', months: {}, log: [] };

  let frame, loads = 0;
  const waitLoad = n => new Promise((res, rej) => {
    const t0 = Date.now();
    const i = setInterval(() => {
      if (loads >= n) { clearInterval(i); setTimeout(res, 600); }
      else if (Date.now() - t0 > 90000) { clearInterval(i); rej(new Error('page load timed out')); }
    }, 200);
  });
  const quiet = w => { try { w.alert = m => state.log.push('msshift says: ' + String(m).slice(0, 80)); w.confirm = () => true; } catch { } };
  const signedOut = path => /default|login/i.test(path || '');

  function clean(html) {
    const doc = new DOMParser().parseFromString(html, 'text/html');
    doc.querySelectorAll('script,style,link,meta,noscript,iframe').forEach(e => e.remove());
    return doc.body ? doc.body.innerHTML.replace(/\s{2,}/g, ' ') : '';
  }

  async function search(path, from, to) {
    loads = 0;
    frame.src = path;
    await waitLoad(1);
    if (signedOut(frame.contentWindow.location.pathname)) throw new Error('SIGNED_OUT');
    quiet(frame.contentWindow);
    let d = frame.contentWindow.document;
    const ids = [...d.querySelectorAll('a[href^="javascript:NewCal"]')].map(a => (a.getAttribute('href').match(/NewCal\('([^']+)'/) || [])[1]).filter(Boolean);
    if (ids.length < 2) throw new Error('date boxes not found');
    d.getElementById(ids[0]).value = from;
    d.getElementById(ids[1]).value = to;
    [...d.querySelectorAll('input[type=button],input[type=submit],button')].find(b => /search/i.test(b.value || b.textContent)).click();
    await waitLoad(2);
    quiet(frame.contentWindow);
    d = frame.contentWindow.document;
    const total = Number((d.body.innerText.match(/Total of (\d+)/) || [])[1] || 0);
    const size = d.querySelector('select[name=PageSize]');
    if (size && total > 10) {
      size.value = [...size.options].find(o => /all/i.test(o.text)).value;
      size.dispatchEvent(new Event('change', { bubbles: true }));
      await Promise.race([waitLoad(3), sleep(8000).then(() => { if (loads < 3) { size.form.submit(); return waitLoad(3); } })]);
      quiet(frame.contentWindow);
      d = frame.contentWindow.document;
    }
    const links = [...new Set([...d.querySelectorAll('[onclick]')].map(e => e.getAttribute('onclick'))
      .filter(x => /^\s*OpenWindow/.test(x)).map(x => x.split("'")[1]).filter(Boolean))];
    return { total, page: clean(d.documentElement.outerHTML), links };
  }

  window.__msxmRun = async (key, names, oldest) => {
    const headers = { 'X-Run-Key': key };
    frame = document.createElement('iframe');
    frame.style.cssText = 'position:fixed;left:-3000px;top:0;width:1200px;height:800px;';
    frame.addEventListener('load', () => loads++);
    document.body.appendChild(frame);
    const [oy, om] = oldest.split('-').map(Number);
    try {
      for (const name of names) {
        const mod = MODULES[name];
        const have = new Set((await (await fetch(`${RECEIVER}/data/${name}`, { headers })).json()).months);
        const now = new Date();
        for (let y = now.getFullYear(), m = now.getMonth() + 1; y > oy || (y === oy && m >= om); m === 1 ? (y--, m = 12) : m--) {
          const month = `${y}-${pad(m)}`;
          if (have.has(month)) continue;
          state.stage = `${name} ${month}: searching`;
          const last = new Date(y, m, 0).getDate();
          const { total, page, links } = await search(mod.path, `${pad(m)}/01/${y} 00:00`, `${pad(m)}/${pad(last)}/${y} 23:59`);
          const records = [];
          if (mod.details) {
            for (let i = 0; i < links.length; i++) {
              state.stage = `${name} ${month}: record ${i + 1} of ${links.length}`;
              try {
                const r = await fetch(links[i], { credentials: 'same-origin' });
                if (signedOut(new URL(r.url).pathname)) throw new Error('SIGNED_OUT');
                records.push({ status: r.status, html: clean(await r.text()) });
              } catch (e) {
                if (String(e).includes('SIGNED_OUT')) throw e;
                records.push({ error: String(e) });
              }
              await sleep(PAUSE_MS);
            }
          }
          const body = JSON.stringify({ source: 'MSShift', kind: name, month, readOn: new Date().toISOString(), listed: total, page, records });
          const post = await fetch(`${RECEIVER}/data/${name}/${month}`, { method: 'POST', headers: { ...headers, 'Content-Type': 'application/json' }, body });
          if (!post.ok) throw new Error(`receiver answered ${post.status}`);
          state.months[`${name} ${month}`] = { listed: total, links: links.length, saved: records.filter(r => r.html).length };
          await sleep(PAUSE_MS);
        }
      }
      state.stage = 'finished';
    } catch (e) {
      state.stage = String(e).includes('SIGNED_OUT') ? 'stopped: MSShift signed out - sign in again and re-run' : `stopped: ${e}`;
    } finally {
      frame.remove();
    }
  };
})();
