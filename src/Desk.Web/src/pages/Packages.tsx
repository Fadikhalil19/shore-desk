import { useCallback, useEffect, useRef, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { api } from '../api'
import { useSession } from '../App'
import { formatLocal, nowPacific } from '../format'
import SignaturePad from '../SignaturePad'
import type { Package, PackageDirection, PackageStatus, Person } from '../types'

const CARRIERS = ['UPS', 'FedEx', 'USPS', 'DHL', 'Amazon', 'OnTrac', 'Courier', 'Other']
const TYPES = ['Box', 'Envelope', 'Bag', 'Tube', 'Pallet', 'Flowers', 'Food', 'Other']
const PEOPLE = ['Guest', 'Employee', 'Department', 'Group', 'Non Guest', 'Other']
const NOTIFY = ['Phone', 'Email', 'Text', 'Note under door', 'In person', 'Front desk']
const STATUS: Record<PackageStatus, string> = { Waiting: 'Waiting', PickedUp: 'Picked up', Delivered: 'Delivered', ReturnedToSender: 'Returned to sender', Shipped: 'Shipped' }

// Packages: waiting at the desk, today, search; log incoming/outgoing; scan to find.
export default function Packages() {
  const { me, lookups } = useSession()
  const [params, setParams] = useSearchParams()
  const access = me.modules.find(m => m.key === 'packages')
  const canEdit = access?.level === 'Edit' || access?.level === 'Manage'
  const hotelId = Number(params.get('hotelId') ?? access?.hotelId ?? me.hotelId ?? lookups.hotels[0].id)
  const tab = params.get('tab') ?? 'waiting'
  const [rows, setRows] = useState<Package[] | null>(null)
  const [open, setOpen] = useState<number | null>(null)
  const [adding, setAdding] = useState<PackageDirection | null>(null)
  const [q, setQ] = useState('')
  const [code, setCode] = useState('')
  const [msg, setMsg] = useState<string | null>(null)
  const scan = useRef<HTMLInputElement>(null)

  const load = useCallback(() => {
    const p = new URLSearchParams({ hotelId: String(hotelId), view: tab })
    if (tab === 'search' && q.trim()) p.set('q', q.trim())
    api.get<Package[]>(`/api/packages?${p}`).then(setRows).catch(() => setRows([]))
  }, [hotelId, tab, q])
  useEffect(() => { setRows(null); load() }, [hotelId, tab]) // eslint-disable-line react-hooks/exhaustive-deps
  useEffect(() => { scan.current?.focus() }, [])

  const done = (text: string) => { setMsg(text); setOpen(null); setAdding(null); load(); scan.current?.focus() }

  return (
    <div className="stack">
      <div className="spread">
        <h1>Packages</h1>
        <div className="row">
          {access?.hotelId == null && lookups.hotels.length > 1 && (
            <select value={hotelId} style={{ width: 'auto' }} onChange={e => setParams({ tab, hotelId: e.target.value })}>
              {lookups.hotels.filter(h => h.isActive).map(h => <option key={h.id} value={h.id}>{h.code}</option>)}
            </select>
          )}
          {canEdit && <button onClick={() => setAdding('Incoming')}>Log incoming</button>}
          {canEdit && <button className="secondary" onClick={() => setAdding('Outgoing')}>Log outgoing</button>}
        </div>
      </div>

      <form className="card row" onSubmit={async e => {
        e.preventDefault(); setMsg(null)
        if (!code.trim()) return
        try {
          const r = await api.get<{ id: number }>(`/api/packages/lookup?hotelId=${hotelId}&code=${encodeURIComponent(code.trim())}`)
          const p = await api.get<Package>(`/api/packages/${r.id}`)
          setRows([p]); setOpen(p.id)
        } catch { setMsg(`No package with "${code.trim()}". Log it as incoming?`); setAdding('Incoming') }
        setCode('')
      }}>
        <label className="field" style={{ flex: 1 }}>Scan or type a tracking #<input ref={scan} value={code} onChange={e => setCode(e.target.value)} autoComplete="off" /></label>
        <button type="submit">Find</button>
      </form>
      {msg && <p className="notice">{msg}</p>}
      {adding && <PackageForm hotelId={hotelId} direction={adding} tracking={msg?.startsWith('No package') ? msg.split('"')[1] : ''}
        onDone={done} onCancel={() => setAdding(null)} />}

      <div className="tabs" role="tablist">
        {[['waiting', 'Waiting'], ['today', 'Today'], ['search', 'Search']].map(([k, label]) => (
          <button key={k} role="tab" aria-selected={tab === k} className={tab === k ? 'tab active' : 'tab'}
            onClick={() => setParams({ tab: k, ...(params.get('hotelId') ? { hotelId: String(hotelId) } : {}) })}>{label}</button>
        ))}
      </div>
      {tab === 'search' && (
        <form className="card row" onSubmit={e => { e.preventDefault(); load() }}>
          <label className="field" style={{ flex: 1 }}>Name, room, group, tracking or number<input value={q} onChange={e => setQ(e.target.value)} /></label>
          <button type="submit">Search</button>
        </form>
      )}
      {!rows ? <p className="muted">Loading...</p> : rows.length === 0 ? <p className="muted">No packages.</p> : (
        <div className="report-list">
          {rows.map(p => (
            <div key={p.id} className={`report-row ${p.old ? 'prio-High' : ''}`}>
              <div className="spread" role="button" tabIndex={0} style={{ cursor: 'pointer' }}
                onClick={() => setOpen(open === p.id ? null : p.id)} onKeyDown={e => e.key === 'Enter' && setOpen(open === p.id ? null : p.id)}>
                <b>{p.number} {p.direction === 'Outgoing' ? 'from' : 'for'} {p.personName}{p.room ? `, room ${p.room}` : ''}</b>
                <span className="row">
                  {p.direction === 'Outgoing' && <span className="pill">Outgoing</span>}
                  <span className={`pill ${p.status === 'Waiting' ? (p.old ? 'st-Voided' : 'st-AwaitingReview') : 'st-Approved'}`}>
                    {p.status === 'Waiting' && p.old ? `${Math.floor(p.daysWaiting)} days` : STATUS[p.status]}</span>
                </span>
              </div>
              <div className="muted small">{[`${p.count} ${p.packageType.toLowerCase()}${p.count > 1 ? 's' : ''}`, p.carrier, p.trackingNumber,
                formatLocal(p.receivedLocal), p.storageLocation && `on ${p.storageLocation}`, p.groupName,
                p.notifiedLocal ? `told by ${p.notifiedHow?.toLowerCase()} ${formatLocal(p.notifiedLocal)}` : p.direction === 'Incoming' && p.status === 'Waiting' ? 'not told yet' : null]
                .filter(Boolean).join(' | ')}</div>
              {p.closedLocal && <div className="muted small">{STATUS[p.status]} {formatLocal(p.closedLocal)}{p.closedToName ? ` to ${p.closedToName}` : ''}
                {p.hasSignature && <> - <a href={`/api/packages/${p.id}/signature`} target="_blank" rel="noreferrer">signature</a></>}</div>}
              {open === p.id && canEdit && p.status === 'Waiting' && <PackageActions p={p} done={done} />}
            </div>
          ))}
        </div>
      )}
    </div>
  )
}

function PackageForm({ hotelId, direction, tracking, onDone, onCancel }: {
  hotelId: number; direction: PackageDirection; tracking: string; onDone: (msg: string) => void; onCancel: () => void
}) {
  const [people, setPeople] = useState<Person[]>([])
  const [error, setError] = useState<string | null>(null)
  const [f, setF] = useState({ carrier: 'UPS', packageType: 'Box', trackingNumber: tracking, count: '1', weight: '', personType: 'Guest',
    personName: '', personUserId: null as number | null, room: '', company: '', groupName: '', otherParty: '', storageLocation: '', notes: '' })
  useEffect(() => { api.get<Person[]>('/api/people').then(setPeople).catch(() => setPeople([])) }, [])
  const text = (k: keyof typeof f, label: string, max = 200, wide = false) => (
    <label className={wide ? 'field wide' : 'field'}>{label}<input value={String(f[k] ?? '')} maxLength={max} onChange={e => setF({ ...f, [k]: e.target.value })} /></label>
  )
  const choose = (k: keyof typeof f, label: string, options: string[]) => (
    <label className="field">{label}<select value={String(f[k])} onChange={e => setF({ ...f, [k]: e.target.value })}>{options.map(o => <option key={o}>{o}</option>)}</select></label>
  )
  return (
    <section className="card form">
      <h2 className="wide">{direction === 'Incoming' ? 'Incoming package' : 'Outgoing package'}</h2>
      {choose('carrier', 'Carrier', CARRIERS)}{choose('packageType', 'Type', TYPES)}{text('trackingNumber', 'Tracking #', 80)}
      <label className="field">How many<input type="number" min={1} max={500} value={f.count} onChange={e => setF({ ...f, count: e.target.value })} /></label>
      {choose('personType', direction === 'Incoming' ? 'For a' : 'From a', PEOPLE)}
      {f.personType === 'Employee' && <label className="field">Employee<select value={f.personUserId ?? ''} onChange={e => setF({ ...f, personUserId: e.target.value ? Number(e.target.value) : null })}>
        <option value="">Someone else (type the name)</option>{people.map(p => <option key={p.id} value={p.id}>{p.name}</option>)}</select></label>}
      {f.personUserId == null && text('personName', 'Name')}
      {(f.personType === 'Guest' || f.personType === 'Group') && <>{text('room', 'Room #', 40)}{text('groupName', 'Group / convention')}</>}
      {text('company', 'Company')}
      {text('otherParty', direction === 'Incoming' ? 'Sender' : 'Ship to (address)', 400, direction === 'Outgoing')}
      {text('storageLocation', 'Stored at', 100)}{text('weight', 'Weight', 40)}
      {text('notes', 'Notes', 2000, true)}
      {error && <p className="alert wide">{error}</p>}
      <div className="row wide">
        <button disabled={f.personUserId == null && !f.personName.trim()} onClick={async () => {
          setError(null)
          try {
            const r = await api.post<{ number: string }>('/api/packages', {
              ...Object.fromEntries(Object.entries(f).map(([k, v]) => [k, v === '' ? null : v])), hotelId, direction, receivedLocal: null,
              count: Number(f.count) || 1, handlingFee: null, arrivalDate: null, departureDate: null,
            })
            onDone(`Logged ${r.number}.`)
          } catch (e) { setError((e as Error).message) }
        }}>Save</button>
        <button className="secondary" onClick={onCancel}>Cancel</button>
      </div>
    </section>
  )
}

function PackageActions({ p, done }: { p: Package; done: (msg: string) => void }) {
  const [mode, setMode] = useState<'pickup' | 'deliver' | 'return' | null>(null)
  const [name, setName] = useState(p.personName)
  const [sig, setSig] = useState<string | null>(null)
  const [note, setNote] = useState('')
  const [error, setError] = useState<string | null>(null)
  const run = async (fn: () => Promise<unknown>, msg: string) => {
    setError(null)
    try { await fn(); done(msg) } catch (e) { setError((e as Error).message) }
  }
  const close = (status: PackageStatus, msg: string) => run(() => api.post(`/api/packages/${p.id}/close`, { status, name: name || null, signature: sig, note: note || null }), msg)

  if (p.direction === 'Outgoing') return (
    <div className="row" style={{ marginTop: '0.5rem' }}>
      <input placeholder="Note (e.g. UPS pickup 3 pm)" value={note} onChange={e => setNote(e.target.value)} style={{ maxWidth: 320 }} />
      <button onClick={() => close('Shipped', `${p.number} shipped.`)}>Collected by carrier</button>
      {error && <p className="alert">{error}</p>}
    </div>
  )
  return (
    <div className="stack" style={{ marginTop: '0.5rem' }}>
      <div className="row">
        <button onClick={() => setMode('pickup')}>Picked up</button>
        <button className="secondary" onClick={() => setMode('deliver')}>Delivered to room</button>
        <button className="link" onClick={() => setMode('return')}>Return to sender</button>
        <span className="muted small">Told them:</span>
        {NOTIFY.slice(0, 4).map(n => <button key={n} className="link small" onClick={() => run(() => api.post(`/api/packages/${p.id}/notify`, { how: n, atLocal: nowPacific() }), `${p.number}: noted that they were told by ${n.toLowerCase()}.`)}>{n}</button>)}
      </div>
      {mode && (
        <div className="form">
          {mode !== 'return' && <label className="field">{mode === 'pickup' ? 'Picked up by' : 'Received by'}<input value={name} onChange={e => setName(e.target.value)} maxLength={200} /></label>}
          <label className="field">Note<input value={note} onChange={e => setNote(e.target.value)} maxLength={1000} /></label>
          {mode === 'pickup' && <SignaturePad label="Signature of the person picking it up" onChange={setSig} />}
          <div><button disabled={(mode !== 'return' && !name.trim()) || (mode === 'pickup' && !sig)}
            onClick={() => close(mode === 'pickup' ? 'PickedUp' : mode === 'deliver' ? 'Delivered' : 'ReturnedToSender', `${p.number} closed.`)}>Save</button></div>
        </div>
      )}
      {error && <p className="alert">{error}</p>}
    </div>
  )
}
