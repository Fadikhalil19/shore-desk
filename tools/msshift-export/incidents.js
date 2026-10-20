// Reads incident reports out of MSShift, from inside a signed-in MSShift tab
// (Fadi's decision 2026-10-20; see CLAUDE.md). READ ONLY: it only runs searches
// and opens report view pages, never saves or changes anything in MSShift.
// Record contents never leave the browser except as the downloaded JSON file;
// progress (counts only) is in window.__msx.
//
// Usage (in the MSShift tab's console, or injected): run this file, then
//   __msxRun(['1','2','3','5','4'])   form codes: 1 IR, 2 GAI, 3 EAI, 5 MD, 4 HI
// MSShift limits: searches reach 6 years back, at most 732 days each.
(() => {
  const FORMS = {
    '1': { name: 'IR', path: '/d/EventLog/IncidentReport/IncidentReport/Search/SearchIncident' },
    '2': { name: 'GAI', path: '/d/EventLog/IncidentReport/LiabilityIncident/Search/SearchLiability' },
    '3': { name: 'EAI', path: '/d/EventLog/IncidentReport/AidedAssociate/Search/SearchAidedAssociate' },
    '5': { name: 'MD', path: '/d/EventLog/IncidentReport/MissingDamage/Search/SearchMissingProperty' },
    '4': { name: 'HI', path: '/d/EventLog/IncidentReport/Arrest/Search/SearchArrest' },
  };
  const WINDOWS = [
    ['10/22/2020 00:00', '10/21/2022 00:00'],
    ['10/22/2022 00:00', '10/21/2024 00:00'],
    ['10/21/2024 00:00', '10/21/2026 23:59'],
  ];
  const PAUSE_MS = 900;           // between report pages, to be gentle with MSShift
  const sleep = ms => new Promise(r => setTimeout(r, ms));
  const state = window.__msx = { stage: 'ready', log: [] };
  const note = s => { state.log.push(s); state.stage = s; };

  let frame;
  function loaded(timeoutMs = 60000) {
    return new Promise((resolve, reject) => {
      const t = setTimeout(() => reject(new Error('page load timed out')), timeoutMs);
      frame.addEventListener('load', () => { clearTimeout(t); setTimeout(resolve, 400); }, { once: true });
    });
  }
  // MSShift warns with alert(); an unanswered alert would freeze the tab.
  const quiet = w => { try { w.alert = m => state.log.push('msshift says: ' + m); w.confirm = () => true; } catch { } };

  async function search(path, from, to) {
    frame.src = path;
    await loaded();
    const w = frame.contentWindow, d = w.document;
    quiet(w);
    const status = [...d.querySelectorAll('select')].find(s => [...s.options].some(o => /Deleted Report/.test(o.text)));
    if (status) status.value = [...status.options].find(o => o.text.trim() === 'All')?.value ?? status.value;
    // Date boxes are the inputs named in the calendar links: NewCal('T1Box', ...)
    const ids = [...d.querySelectorAll('a[href^="javascript:NewCal"]')].map(a => a.getAttribute('href').match(/NewCal\('([^']+)'/)?.[1]).filter(Boolean);
    if (ids.length < 2) throw new Error('date boxes not found on ' + path);
    d.getElementById(ids[0]).value = from;
    d.getElementById(ids[1]).value = to;
    const button = [...d.querySelectorAll('input[type=button],input[type=submit],button')].find(b => /search/i.test(b.value || b.textContent));
    if (!button) throw new Error('search button not found on ' + path);
    const done = loaded();
    button.click();
    await done;
    quiet(frame.contentWindow);
    // Show all results on one page.
    let rd = frame.contentWindow.document;
    const size = rd.querySelector('select[name=PageSize]');
    const total = Number((rd.body.innerText.match(/Total of (\d+)/) || [])[1] || 0);
    if (size && total > 10) {
      const all = [...size.options].find(o => /all/i.test(o.text));
      size.value = all.value;
      const next = loaded();
      size.dispatchEvent(new Event('change', { bubbles: true }));
      await Promise.race([next, sleep(4000).then(() => { if (frame.contentWindow.document === rd) { size.form.submit(); return loaded(); } })]);
      quiet(frame.contentWindow);
      rd = frame.contentWindow.document;
    }
    const urls = [...new Set([...rd.querySelectorAll('[onclick]')].map(e => e.getAttribute('onclick'))
      .filter(x => x.startsWith('OpenWindowH')).map(x => x.split("'")[1]))];
    return { total, urls };
  }

  // The report page without scripts, styles and MSShift's own buttons.
  function clean(html) {
    const doc = new DOMParser().parseFromString(html, 'text/html');
    doc.querySelectorAll('script,style,link,meta,noscript,iframe').forEach(e => e.remove());
    return doc.body ? doc.body.innerHTML.replace(/\s{2,}/g, ' ') : '';
  }

  function save(name, data) {
    const blob = new Blob([JSON.stringify(data)], { type: 'application/json' });
    const a = document.createElement('a');
    a.href = URL.createObjectURL(blob);
    a.download = name;
    document.body.appendChild(a);
    a.click();
    setTimeout(() => { URL.revokeObjectURL(a.href); a.remove(); }, 5000);
  }

  function makeFrame() {
    frame = document.createElement('iframe');
    frame.style.cssText = 'position:fixed;left:-3000px;top:0;width:1200px;height:800px;';
    document.body.appendChild(frame);
  }

  // Dry run: one search, counts only.
  window.__msxTry = async (code, w) => {
    makeFrame();
    try {
      const { total, urls } = await search(FORMS[code].path, WINDOWS[w][0], WINDOWS[w][1]);
      state.trial = { form: FORMS[code].name, total, links: urls.length };
    } catch (e) { state.trial = { error: String(e) }; }
    frame.remove();
  };

  window.__msxRun = async (codes) => {
    makeFrame();
    state.summary = {};
    for (const code of codes) {
      const form = FORMS[code];
      const records = [];
      let listed = 0;
      for (const [from, to] of WINDOWS) {
        note(`${form.name}: searching ${from} - ${to}`);
        let found;
        try { found = await search(form.path, from, to); } catch (e) { state.log.push(`${form.name} ${from}: ${e}`); continue; }
        const { total, urls } = found;
        listed += total;
        state.log.push(`${form.name} ${from}-${to}: MSShift total ${total}, links ${urls.length}`);
        for (let i = 0; i < urls.length; i++) {
          note(`${form.name}: report ${i + 1} of ${urls.length} (${from.slice(6, 10)}-${to.slice(6, 10)})`);
          try {
            const r = await fetch(urls[i], { credentials: 'same-origin' });
            records.push({ form: form.name, window: [from, to], status: r.status, html: clean(await r.text()) });
          } catch (e) {
            records.push({ form: form.name, window: [from, to], error: String(e) });
          }
          await sleep(PAUSE_MS);
        }
      }
      state.summary[form.name] = { listed, saved: records.filter(r => r.html).length, failed: records.filter(r => !r.html).length };
      save(`msshift-incidents-${form.name}.json`, { source: 'MSShift', form: form.name, readOn: new Date().toISOString(), records });
      note(`${form.name}: saved`);
    }
    frame.remove();
    note('finished');
  };
})();
