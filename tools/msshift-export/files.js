// Copies MSShift incident photos and PDF attachments, from inside a signed-in MSShift
// tab (Fadi's decision 2026-10-20). READ ONLY. The to-do list comes from the local
// receiver (receiver.py on 127.0.0.1:8765, started on the server), and each file is
// posted straight back to it, so nothing lands in Downloads. Resumable: files the
// receiver already has are skipped. Progress (counts only) is in window.__msxf.
//
// Usage: run this file in the MSShift tab, then __msxfRun('<key printed by receiver.py>').
(() => {
  const RECEIVER = 'http://127.0.0.1:8765';
  const PAUSE_MS = 1000;          // gentle: the event-log read-out may be running too
  const sleep = ms => new Promise(r => setTimeout(r, ms));
  const state = window.__msxf = { stage: 'ready', saved: 0, skipped: 0, failed: 0, log: [] };
  const signedOut = r => /default|login/i.test(new URL(r.url).pathname);

  async function source(item) {
    if (item.kind === 'photo') return item.url;
    // A PDF attachment opens in a viewer page that embeds the file.
    const r = await fetch(item.url, { credentials: 'same-origin' });
    if (signedOut(r)) throw new Error('SIGNED_OUT');
    const doc = new DOMParser().parseFromString(await r.text(), 'text/html');
    const el = doc.querySelector('object[data],embed[src],iframe[src]');
    const src = el && (el.getAttribute('data') || el.getAttribute('src'));
    if (!src) throw new Error('no file in viewer');
    return src;
  }

  window.__msxfRun = async (key) => {
    const headers = { 'X-Run-Key': key };
    const list = await (await fetch(`${RECEIVER}/list`, { headers })).json();
    const done = new Set(list.done);
    const items = list.items.filter(i => !done.has(i.id.replace(/#/g, '')));
    state.total = list.items.length;
    state.skipped = list.items.length - items.length;
    try {
      for (let n = 0; n < items.length; n++) {
        const item = items[n];
        state.stage = `file ${n + 1} of ${items.length}`;
        try {
          const r = await fetch(await source(item), { credentials: 'same-origin' });
          if (signedOut(r)) throw new Error('SIGNED_OUT');
          if (!r.ok) throw new Error(`MSShift answered ${r.status}`);
          const blob = await r.blob();
          const type = (blob.type || r.headers.get('content-type') || '').split(';')[0];
          const post = await fetch(`${RECEIVER}/file/${encodeURIComponent(item.id)}`, {
            method: 'POST', headers: { ...headers, 'X-Source-Type': type }, body: blob,
          });
          if (!post.ok) throw new Error(`receiver answered ${post.status}`);
          state.saved++;
        } catch (e) {
          if (String(e).includes('SIGNED_OUT')) throw e;
          state.failed++;
          state.log.push(`${item.kind}: ${String(e).slice(0, 80)}`);
        }
        await sleep(PAUSE_MS);
      }
      state.stage = 'finished';
    } catch (e) {
      state.stage = String(e).includes('SIGNED_OUT') ? 'stopped: MSShift signed out - sign in again and re-run' : `stopped: ${e}`;
    }
  };
})();
