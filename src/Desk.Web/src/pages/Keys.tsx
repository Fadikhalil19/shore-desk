import { useCallback, useEffect, useRef, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { api } from '../api'
import { useSession } from '../App'
import { formatDateTime, formatLocal, nowPacific } from '../format'
import SignaturePad from '../SignaturePad'
import { HOURS } from '../shifts'
import type { HotelKey, KeyCount, KeyHistoryRow, KeyStatus, Person, Shift } from '../types'

const STATUS: Record<KeyStatus, string> = { In: 'In', SignedOut: 'Signed out', Missing: 'Missing', Broken: 'Broken' }
const PILL: Record<KeyStatus, string> = { In: 'st-Approved', SignedOut: 'st-AwaitingReview', Missing: 'st-Voided', Broken: 'st-Voided' }
const HOLDERS = ['Employee', 'Contractor', 'Vendor', 'Other']

// Keys: status board with scan box, sign out / in with signatures, history, shift
// count, and (managers) the key list.
export default function Keys() {
  const { me, lookups } = useSession()
  const [params, setParams] = useSearchParams()
  const access = me.modules.find(m => m.key === 'keys')
  const level = access?.level ?? 'None'
  const hotelId = Number(params.get('hotelId') ?? access?.hotelId ?? me.hotelId ?? lookups.hotels[0].id)
  const tab = params.get('tab') ?? 'keys'
  const tabs: [string, string][] = [['keys', 'Keys'], ['history', 'History'], ['count', 'Shift count']]
  if (level === 'Manage') tabs.push(['list', 'Key list'])

  return (
    <div className="stack">
      <div className="spread">
        <h1>Keys</h1>
        {access?.hotelId == null && lookups.hotels.length > 1 && (
          <select value={hotelId} style={{ width: 'auto' }} onChange={e => setParams({ tab, hotelId: e.target.value })}>
            {lookups.hotels.filter(h => h.isActive).map(h => <option key={h.id} value={h.id}>{h.code}</option>)}
          </select>
        )}
      </div>
      <div className="tabs" role="tablist">
        {tabs.map(([k, label]) => (
          <button key={k} role="tab" aria-selected={tab === k} className={tab === k ? 'tab active' : 'tab'}
            onClick={() => setParams({ tab: k, ...(params.get('hotelId') ? { hotelId: String(hotelId) } : {}) })}>{label}</button>
        ))}
      </div>
      {tab === 'keys' && <Board hotelId={hotelId} canEdit={level === 'Edit' || level === 'Manage'} isManager={level === 'Manage'} />}
      {tab === 'history' && <History hotelId={hotelId} />}
      {tab === 'count' && <Count hotelId={hotelId} canEdit={level === 'Edit' || level === 'Manage'} />}
      {tab === 'list' && level === 'Manage' && <KeyList hotelId={hotelId} />}
    </div>
  )
}

function useKeys(hotelId: number, all = false) {
  const [keys, setKeys] = useState<HotelKey[] | null>(null)
  const [error, setError] = useState<string | null>(null)
  const load = useCallback(() => {
    api.get<HotelKey[]>(`/api/keys?hotelId=${hotelId}${all ? '&all=true' : ''}`).then(setKeys).catch(e => setError(e.message))
  }, [hotelId, all])
  useEffect(load, [load])
  return { keys, error, load }
}

// --- Board -----------------------------------------------------------------------

function Board({ hotelId, canEdit, isManager }: { hotelId: number; canEdit: boolean; isManager: boolean }) {
  const { keys, error, load } = useKeys(hotelId)
  const [filter, setFilter] = useState<'all' | 'out' | 'problem'>('all')
  const [open, setOpen] = useState<number | null>(null)
  const [code, setCode] = useState('')
  const [msg, setMsg] = useState<string | null>(null)
  const [people, setPeople] = useState<Person[]>([])
  const scan = useRef<HTMLInputElement>(null)

  useEffect(() => { api.get<Person[]>('/api/people').then(setPeople).catch(() => setPeople([])) }, [])
  useEffect(() => { scan.current?.focus() }, [])

  if (error) return <p className="alert">{error}</p>
  if (!keys) return <p className="muted">Loading...</p>
  const shown = keys.filter(k => filter === 'all' || (filter === 'out' ? k.status === 'SignedOut' : k.status === 'Missing' || k.status === 'Broken' || k.holder?.overdue))
  const out = keys.filter(k => k.status === 'SignedOut').length
  const overdue = keys.filter(k => k.holder?.overdue).length
  const done = (text: string) => { setMsg(text); setOpen(null); load(); scan.current?.focus() }

  return (
    <>
      {/* A USB/Bluetooth scanner types the barcode and presses Enter. */}
      <form className="card row" onSubmit={async e => {
        e.preventDefault(); setMsg(null)
        if (!code.trim()) return
        try {
          const r = await api.get<{ id: number }>(`/api/keys/lookup?hotelId=${hotelId}&code=${encodeURIComponent(code.trim())}`)
          setOpen(r.id); setFilter('all')
        } catch { setMsg(`No key matches "${code.trim()}".`) }
        setCode('')
      }}>
        <label className="field" style={{ flex: 1 }}>Scan or type a key
          <input ref={scan} value={code} onChange={e => setCode(e.target.value)} placeholder="Barcode or key name" autoComplete="off" />
        </label>
        <button type="submit">Find</button>
      </form>

      <div className="stat-tiles">
        <div className="stat-tile"><span className="muted small">Keys</span><b>{keys.length}</b></div>
        <div className="stat-tile"><span className="muted small">Signed out</span><b>{out}</b></div>
        <div className="stat-tile"><span className="muted small">Out too long</span><b className={overdue ? 'error' : ''}>{overdue}</b></div>
      </div>

      <div className="tabs">
        {([['all', 'All'], ['out', 'Signed out'], ['problem', 'Problems']] as const).map(([k, label]) => (
          <button key={k} className={filter === k ? 'tab active' : 'tab'} onClick={() => setFilter(k)}>{label}</button>
        ))}
      </div>
      {msg && <p className="notice">{msg}</p>}
      {keys.length === 0 && <p className="muted">No keys yet. {isManager ? 'Add them on the Key list tab.' : 'A manager adds them.'}</p>}

      <div className="report-list">
        {shown.map(k => (
          <div key={k.id} className={`report-row ${k.holder?.overdue ? 'prio-Highest' : ''}`}>
            <div className="spread" role="button" tabIndex={0} style={{ cursor: 'pointer' }}
              onClick={() => setOpen(open === k.id ? null : k.id)} onKeyDown={e => e.key === 'Enter' && setOpen(open === k.id ? null : k.id)}>
              <b>{k.name}{k.description && <span className="muted small"> - {k.description}</span>}</b>
              <span className={`pill ${PILL[k.status]}`}>{k.holder?.overdue ? 'Out too long' : STATUS[k.status]}</span>
            </div>
            {k.holder && <div className="small">
              {k.holder.name} ({k.holder.toType}{k.holder.company ? `, ${k.holder.company}` : ''}) since {formatLocal(k.holder.outLocal)}
              <span className="muted"> - {k.holder.hoursOut} h{k.holder.dueBackLocal ? `, due back ${formatLocal(k.holder.dueBackLocal)}` : ''}</span>
            </div>}
            {open === k.id && canEdit && <KeyActions k={k} people={people} isManager={isManager} done={done} />}
          </div>
        ))}
      </div>
    </>
  )
}

function KeyActions({ k, people, isManager, done }: { k: HotelKey; people: Person[]; isManager: boolean; done: (msg: string) => void }) {
  const [mode, setMode] = useState<'missing' | 'broken' | 'back' | null>(null)
  const [note, setNote] = useState('')
  const [error, setError] = useState<string | null>(null)
  const run = async (fn: () => Promise<unknown>, msg: string) => {
    setError(null)
    try { await fn(); done(msg) } catch (e) { setError((e as Error).message) }
  }

  return (
    <div className="stack" style={{ marginTop: '0.5rem' }}>
      {k.status === 'In' && <SignOutForm k={k} people={people} run={run} />}
      {k.status === 'SignedOut' && <SignInForm k={k} run={run} />}
      <div className="row">
        {(k.status === 'In' || k.status === 'SignedOut') && <>
          <button className="link small" onClick={() => setMode('missing')}>Report missing</button>
          <button className="link small" onClick={() => setMode('broken')}>Report broken</button>
        </>}
        {(k.status === 'Missing' || k.status === 'Broken') && isManager && <button className="link small" onClick={() => setMode('back')}>Back in service</button>}
        {(k.status === 'Missing' || k.status === 'Broken') && !isManager && <span className="muted small">A manager puts it back in service.</span>}
      </div>
      {mode && (
        <div className="form">
          <label className="field wide">What happened?<input value={note} onChange={e => setNote(e.target.value)} maxLength={1000} /></label>
          <div className="row">
            <button disabled={!note.trim()} onClick={() => run(() => api.post(`/api/keys/${k.id}/status`, {
              status: mode === 'missing' ? 'Missing' : mode === 'broken' ? 'Broken' : 'In', note,
            }), mode === 'back' ? `${k.name} is back in service.` : `${k.name} reported ${mode}.`)}>Save</button>
            <button className="secondary" onClick={() => setMode(null)}>Cancel</button>
          </div>
        </div>
      )}
      {error && <p className="alert">{error}</p>}
    </div>
  )
}

type Run = (fn: () => Promise<unknown>, msg: string) => Promise<void>

function SignOutForm({ k, people, run }: { k: HotelKey; people: Person[]; run: Run }) {
  const [f, setF] = useState({ toType: 'Employee', userId: null as number | null, name: '', company: '', departmentId: null as number | null, due: '', notes: '' })
  const [sig, setSig] = useState<string | null>(null)
  const { lookups } = useSession()
  return (
    <div className="form">
      <label className="field">Signed out to
        <select value={f.toType} onChange={e => setF({ ...f, toType: e.target.value })}>{HOLDERS.map(h => <option key={h}>{h}</option>)}</select>
      </label>
      {f.toType === 'Employee' && (
        <label className="field">Employee
          <select value={f.userId ?? ''} onChange={e => {
            const id = e.target.value ? Number(e.target.value) : null
            setF({ ...f, userId: id, departmentId: people.find(p => p.id === id)?.departmentId ?? f.departmentId })
          }}>
            <option value="">Someone else (type the name)</option>
            {people.map(p => <option key={p.id} value={p.id}>{p.name}</option>)}
          </select>
        </label>
      )}
      {f.userId == null && <label className="field">Name<input value={f.name} onChange={e => setF({ ...f, name: e.target.value })} maxLength={200} /></label>}
      {f.toType !== 'Employee' && <label className="field">Company<input value={f.company} onChange={e => setF({ ...f, company: e.target.value })} maxLength={200} /></label>}
      <label className="field">Department
        <select value={f.departmentId ?? ''} onChange={e => setF({ ...f, departmentId: e.target.value ? Number(e.target.value) : null })}>
          <option value="">-</option>{lookups.departments.filter(d => d.isActive).map(d => <option key={d.id} value={d.id}>{d.name}</option>)}
        </select>
      </label>
      <label className="field">Due back (optional)<input type="datetime-local" value={f.due} onChange={e => setF({ ...f, due: e.target.value })} /></label>
      <label className="field wide">Notes<input value={f.notes} onChange={e => setF({ ...f, notes: e.target.value })} maxLength={1000} /></label>
      <SignaturePad label="Signature of the person taking the key" onChange={setSig} />
      <div><button disabled={!sig || (f.userId == null && !f.name.trim())} onClick={() => run(() => api.post(`/api/keys/${k.id}/signout`, {
        toType: f.toType, userId: f.userId, name: f.name || null, company: f.company || null, departmentId: f.departmentId,
        outLocal: null, dueBackLocal: f.due || null, notes: f.notes || null, signature: sig,
      }), `${k.name} signed out.`)}>Sign out {k.name}</button></div>
    </div>
  )
}

function SignInForm({ k, run }: { k: HotelKey; run: Run }) {
  const [sig, setSig] = useState<string | null>(null)
  const [notes, setNotes] = useState('')
  return (
    <div className="form">
      <label className="field wide">Notes<input value={notes} onChange={e => setNotes(e.target.value)} maxLength={1000} /></label>
      <SignaturePad label="Signature of the person returning it (optional)" onChange={setSig} />
      <div><button onClick={() => run(() => api.post(`/api/keys/${k.id}/signin`, { inLocal: null, notes: notes || null, signature: sig }),
        `${k.name} is back in.`)}>Sign in {k.name}</button></div>
    </div>
  )
}

// --- History ---------------------------------------------------------------------

function History({ hotelId }: { hotelId: number }) {
  const [rows, setRows] = useState<KeyHistoryRow[] | null>(null)
  const [q, setQ] = useState('')
  const today = nowPacific().slice(0, 10)
  const [from, setFrom] = useState(() => { const d = new Date(`${today}T12:00:00`); d.setDate(d.getDate() - 7); return d.toISOString().slice(0, 10) })
  const [to, setTo] = useState(today)
  const load = useCallback(() => {
    const p = new URLSearchParams({ hotelId: String(hotelId), from, to })
    if (q.trim()) p.set('q', q.trim())
    api.get<KeyHistoryRow[]>(`/api/keys/history?${p}`).then(setRows).catch(() => setRows([]))
  }, [hotelId, from, to, q])
  useEffect(() => { load() }, [hotelId, from, to]) // eslint-disable-line react-hooks/exhaustive-deps

  const sig = (id: number, which: string) => <a href={`/api/keys/checkouts/${id}/signature/${which}`} target="_blank" rel="noreferrer">signature</a>
  return (
    <div className="stack">
      <form className="card form" onSubmit={e => { e.preventDefault(); load() }}>
        <label className="field">Key or person<input value={q} onChange={e => setQ(e.target.value)} /></label>
        <label className="field">From<input type="date" value={from} onChange={e => setFrom(e.target.value)} /></label>
        <label className="field">To<input type="date" value={to} onChange={e => setTo(e.target.value)} /></label>
        <div><button type="submit">Search</button></div>
      </form>
      {!rows ? <p className="muted">Loading...</p> : rows.length === 0 ? <p className="muted">Nothing in this period.</p> : (
        <div className="table-wrap">
          <table className="grid">
            <thead><tr><th>When</th><th>Key</th><th>What</th><th>Who</th><th>Back</th><th>By</th></tr></thead>
            <tbody>
              {rows.map((r, i) => (
                <tr key={i}>
                  <td className="nowrap">{formatLocal(r.atLocal)}</td>
                  <td>{r.keyName}</td>
                  <td>{r.kind}{r.note && <div className="muted small">{r.note}</div>}</td>
                  <td>{r.who}{r.company && <div className="muted small">{r.company}</div>}{r.hasSignature && r.checkoutId && <div className="small">{sig(r.checkoutId, 'out')}</div>}</td>
                  <td>{r.backLocal ? <>{formatLocal(r.backLocal)}{r.backBy && <div className="muted small">{r.backBy}</div>}
                    {r.hasBackSignature && r.checkoutId && <div className="small">{sig(r.checkoutId, 'in')}</div>}</> : r.checkoutId ? <span className="warn">Not back</span> : ''}</td>
                  <td>{r.by}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}

// --- Shift count -----------------------------------------------------------------

function Count({ hotelId, canEdit }: { hotelId: number; canEdit: boolean }) {
  const { keys, load } = useKeys(hotelId)
  const [counts, setCounts] = useState<KeyCount[] | null>(null)
  const [onHand, setOnHand] = useState<Record<number, boolean>>({})
  const [notes, setNotes] = useState('')
  const [shift, setShift] = useState<Shift>(currentShift())
  const [msg, setMsg] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [detail, setDetail] = useState<KeyCount | null>(null)

  const loadCounts = useCallback(() => { api.get<KeyCount[]>(`/api/keys/counts?hotelId=${hotelId}`).then(setCounts).catch(() => setCounts([])) }, [hotelId])
  useEffect(loadCounts, [loadCounts])
  // Keys signed out are expected to be away; everything else is expected on hand.
  useEffect(() => { if (keys) setOnHand(Object.fromEntries(keys.map(k => [k.id, k.status === 'In']))) }, [keys])

  if (!keys) return <p className="muted">Loading...</p>
  const missing = keys.filter(k => k.status === 'In' && !onHand[k.id])
  return (
    <div className="stack">
      {canEdit && keys.length > 0 && (
        <section className="card block">
          <div className="spread">
            <h2>Count now</h2>
            <select value={shift} style={{ width: 'auto' }} onChange={e => setShift(e.target.value as Shift)}>
              {(['Morning', 'Afternoon', 'Overnight'] as Shift[]).map(s => <option key={s} value={s}>{s} ({HOURS[s]})</option>)}
            </select>
          </div>
          <p className="muted small">Untick any key that should be here but is not.</p>
          <div className="count-list">
            {keys.map(k => (
              <label key={k.id} className="field check">
                <input type="checkbox" checked={!!onHand[k.id]} onChange={e => setOnHand({ ...onHand, [k.id]: e.target.checked })} />
                {k.name} <span className="muted small">({STATUS[k.status]}{k.holder ? `: ${k.holder.name}` : ''})</span>
              </label>
            ))}
          </div>
          {missing.length > 0 && <p className="warn small">Not on hand but should be: {missing.map(k => k.name).join(', ')}</p>}
          <label className="field">Notes<input value={notes} onChange={e => setNotes(e.target.value)} maxLength={1000} /></label>
          <div><button onClick={async () => {
            setError(null); setMsg(null)
            try {
              const shiftDate = shift === 'Overnight' && Number(nowPacific().slice(11, 13)) < 5
                ? new Date(Date.now() - 86400000).toISOString().slice(0, 10) : nowPacific().slice(0, 10)
              const r = await api.post<{ problems: number }>('/api/keys/counts', {
                hotelId, shiftDate, shift, notes: notes || null, lines: keys.map(k => ({ keyId: k.id, onHand: !!onHand[k.id], note: null })),
              })
              setMsg(r.problems ? `Count saved. ${r.problems} key(s) not on hand - report them missing if they cannot be found.` : 'Count saved. All keys accounted for.')
              setNotes(''); load(); loadCounts()
            } catch (e) { setError((e as Error).message) }
          }}>Save count</button></div>
          {msg && <p className="notice">{msg}</p>}
          {error && <p className="alert">{error}</p>}
        </section>
      )}
      <section className="card block">
        <h2>Recent counts</h2>
        {!counts || counts.length === 0 ? <p className="muted small">No counts yet.</p> : counts.map(c => (
          <p key={c.id}>
            <button className="link" onClick={() => api.get<KeyCount>(`/api/keys/counts/${c.id}`).then(setDetail)}>
              {formatLocal(c.shiftDate)} {c.shift}</button>
            <span className="muted small"> - {c.onHand} of {c.keys} on hand, by {c.countedBy}, {formatDateTime(c.countedUtc)}</span>
          </p>
        ))}
      </section>
      {detail && (
        <section className="card block">
          <div className="spread"><h2>Count {formatLocal(detail.shiftDate)} {detail.shift}</h2><button className="link" onClick={() => setDetail(null)}>Close</button></div>
          {detail.lines!.map(l => (
            <p key={l.keyId} className={l.expectedStatus === 'In' && !l.onHand ? 'warn' : ''}>
              {l.onHand ? 'On hand' : 'Not on hand'}: {l.keyName} <span className="muted small">(was {STATUS[l.expectedStatus]})</span>
            </p>
          ))}
          {detail.notes && <p className="muted">{detail.notes}</p>}
        </section>
      )}
    </div>
  )
}

function currentShift(): Shift {
  const h = Number(nowPacific().slice(11, 13))
  return h >= 5 && h < 13 ? 'Morning' : h >= 13 && h < 21 ? 'Afternoon' : 'Overnight'
}

// --- Key list (managers) ---------------------------------------------------------

function KeyList({ hotelId }: { hotelId: number }) {
  const { keys, load } = useKeys(hotelId, true)
  const [n, setN] = useState({ name: '', barcode: '', description: '' })
  const [error, setError] = useState<string | null>(null)
  const save = async (fn: () => Promise<unknown>) => {
    setError(null)
    try { await fn(); load(); return true } catch (e) { setError((e as Error).message); return false }
  }
  const put = (k: HotelKey, patch: Partial<HotelKey>) => save(() => api.put(`/api/keys/${k.id}`, { ...k, ...patch }))
  if (!keys) return <p className="muted">Loading...</p>
  return (
    <div className="stack">
      <p className="muted small">Keys are never deleted: untick "In service" when one is retired. The barcode is what a scanner types.</p>
      {error && <p className="alert">{error}</p>}
      <div className="table-wrap">
        <table className="grid">
          <thead><tr><th>Name</th><th>Barcode</th><th>Opens</th><th>Order</th><th>In service</th></tr></thead>
          <tbody>
            {keys.map(k => (
              <tr key={k.id} className={k.isActive ? '' : 'off'}>
                <td><input defaultValue={k.name} aria-label="Name" onBlur={e => e.target.value !== k.name && put(k, { name: e.target.value })} /></td>
                <td><input defaultValue={k.barcode ?? ''} aria-label="Barcode" onBlur={e => e.target.value !== (k.barcode ?? '') && put(k, { barcode: e.target.value || null })} /></td>
                <td><input defaultValue={k.description ?? ''} aria-label="Opens" onBlur={e => e.target.value !== (k.description ?? '') && put(k, { description: e.target.value || null })} /></td>
                <td style={{ width: 90 }}><input type="number" defaultValue={k.sortOrder} aria-label="Order" onBlur={e => Number(e.target.value) !== k.sortOrder && put(k, { sortOrder: Number(e.target.value) })} /></td>
                <td><input type="checkbox" checked={k.isActive} aria-label={`${k.name} in service`} onChange={e => put(k, { isActive: e.target.checked })} /></td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <div className="card form">
        <label className="field">Key name<input value={n.name} onChange={e => setN({ ...n, name: e.target.value })} maxLength={100} placeholder="e.g. Officer Ring 1" /></label>
        <label className="field">Barcode<input value={n.barcode} onChange={e => setN({ ...n, barcode: e.target.value })} maxLength={100} /></label>
        <label className="field">Opens<input value={n.description} onChange={e => setN({ ...n, description: e.target.value })} maxLength={400} /></label>
        <div><button disabled={!n.name.trim()} onClick={async () => {
          const order = Math.max(0, ...keys.map(k => k.sortOrder)) + 10
          if (await save(() => api.post('/api/keys', { hotelId, name: n.name, barcode: n.barcode || null, description: n.description || null, sortOrder: order, isActive: true })))
            setN({ name: '', barcode: '', description: '' })
        }}>Add key</button></div>
      </div>
    </div>
  )
}
