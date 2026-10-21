import { useCallback, useEffect, useState } from 'react'
import { Link, useNavigate, useParams, useSearchParams } from 'react-router-dom'
import { api } from '../api'
import { useSession } from '../App'
import { formatLocal, nowPacific } from '../format'
import SignaturePad from '../SignaturePad'
import type { FoundItem, FoundRow, LostReport, LostRow, PaymentStatus, Person, Shipment } from '../types'
import { Pick } from './incidents/fields'

const STATUS: Record<string, string> = { InStorage: 'In storage', ReturnedToOwner: 'Returned', Shipped: 'Shipped', Disposed: 'Disposed',
  Open: 'Open', Matched: 'Matched', Closed: 'Closed' }
const PILL: Record<string, string> = { InStorage: 'st-AwaitingReview', ReturnedToOwner: 'st-Approved', Shipped: 'st-Approved', Disposed: 'st-Draft',
  Open: 'st-AwaitingReview', Matched: 'st-Approved', Closed: 'st-Draft' }
const PAYMENT: Record<PaymentStatus, string> = { Waiting: 'Waiting for payment', Paid: 'Paid', NoCharge: 'No charge' }
const FINDERS = ['Guest', 'Employee', 'Non Guest', 'Contractor', 'Other']
const DISPOSAL = ['Donated', 'Discarded', 'Given to finder', 'Destroyed', 'Turned over to police', 'Other']
const CARRIERS = ['UPS', 'FedEx', 'USPS', 'DHL', 'Other']

function useLf() {
  const { me, lookups } = useSession()
  const [params] = useSearchParams()
  const access = me.modules.find(m => m.key === 'lostfound')
  const hotelId = Number(params.get('hotelId') ?? access?.hotelId ?? me.hotelId ?? lookups.hotels[0].id)
  return { hotelId, limit: access?.hotelId, canEdit: access?.level === 'Edit' || access?.level === 'Manage' }
}

const itemName = (r: { category: string | null; detail: string | null; brand?: string | null }) =>
  [r.brand, r.detail || r.category].filter(Boolean).join(' ') || 'Item'

// Lost & found: items in storage, due for disposal, guests' lost reports, shipping, search.
export default function LostFound() {
  const { lookups } = useSession()
  const [params, setParams] = useSearchParams()
  const { hotelId, limit, canEdit } = useLf()
  const tab = params.get('tab') ?? 'storage'
  const set = (patch: Record<string, string>) => setParams({ ...(params.get('hotelId') ? { hotelId: String(hotelId) } : {}), ...patch })
  return (
    <div className="stack">
      <div className="spread">
        <h1>Lost &amp; found</h1>
        <div className="row">
          {limit == null && lookups.hotels.length > 1 && (
            <select value={hotelId} style={{ width: 'auto' }} onChange={e => setParams({ tab, hotelId: e.target.value })}>
              {lookups.hotels.filter(h => h.isActive).map(h => <option key={h.id} value={h.id}>{h.code}</option>)}
            </select>
          )}
          {canEdit && <Link className="button" to={`/lostfound/found/new?hotelId=${hotelId}`}>Log found item</Link>}
          {canEdit && <Link className="button secondary" to={`/lostfound/lost/new?hotelId=${hotelId}`}>Log lost report</Link>}
        </div>
      </div>
      <div className="tabs" role="tablist">
        {[['storage', 'In storage'], ['lost', 'Lost reports'], ['shipping', 'Shipping'], ['due', 'Due for disposal'], ['search', 'Search']].map(([k, label]) => (
          <button key={k} role="tab" aria-selected={tab === k} className={tab === k ? 'tab active' : 'tab'} onClick={() => set({ tab: k })}>{label}</button>
        ))}
      </div>
      {(tab === 'storage' || tab === 'due' || tab === 'search') && <FoundList hotelId={hotelId} view={tab} />}
      {tab === 'lost' && <LostList hotelId={hotelId} />}
      {tab === 'shipping' && <Shipping hotelId={hotelId} />}
    </div>
  )
}

function FoundList({ hotelId, view }: { hotelId: number; view: string }) {
  const [rows, setRows] = useState<FoundRow[] | null>(null)
  const [q, setQ] = useState('')
  const load = useCallback(() => {
    const p = new URLSearchParams({ hotelId: String(hotelId), view: view === 'search' ? 'all' : view })
    if (q.trim()) p.set('q', q.trim())
    api.get<FoundRow[]>(`/api/lostfound/found?${p}`).then(setRows).catch(() => setRows([]))
  }, [hotelId, view, q])
  useEffect(() => { setRows(null); if (view !== 'search') load(); else setRows([]) }, [hotelId, view]) // eslint-disable-line react-hooks/exhaustive-deps
  return (
    <>
      {view === 'search' && (
        <form className="card row" onSubmit={e => { e.preventDefault(); load() }}>
          <label className="field" style={{ flex: 1 }}>Number, words, brand, room, owner or serial<input value={q} onChange={e => setQ(e.target.value)} /></label>
          <button type="submit">Search</button>
        </form>
      )}
      {view === 'due' && <p className="muted small">Items past their purge date. A manager disposes of them (donated, discarded, ...).</p>}
      {!rows ? <p className="muted">Loading...</p> : rows.length === 0 ? <p className="muted">{view === 'search' ? 'Search for an item.' : 'Nothing here.'}</p> : (
        <div className="report-list">
          {rows.map(f => (
            <Link key={f.id} to={`/lostfound/found/${f.id}`} className="report-row">
              <div className="spread">
                <b>{f.number} {itemName(f)}</b>
                <span className="row">
                  {f.highValue && <span className="pill prio-High">High value</span>}
                  {f.shipmentPayment && f.status === 'InStorage' && <span className="pill">{PAYMENT[f.shipmentPayment]}</span>}
                  <span className={`pill ${PILL[f.status]}`}>{STATUS[f.status]}</span>
                </span>
              </div>
              <div className="muted small">{[f.color, f.category, `found ${formatLocal(f.foundLocal)}`, [f.area, f.room && `room ${f.room}`].filter(Boolean).join(' '),
                f.storageLocation && `kept in ${f.storageLocation}`, f.status === 'InStorage' && `purge ${formatLocal(f.purgeDate)}`].filter(Boolean).join(' | ')}</div>
            </Link>
          ))}
        </div>
      )}
    </>
  )
}

function LostList({ hotelId }: { hotelId: number }) {
  const [rows, setRows] = useState<LostRow[] | null>(null)
  const [all, setAll] = useState(false)
  useEffect(() => {
    api.get<LostRow[]>(`/api/lostfound/lost?hotelId=${hotelId}${all ? '&view=all' : ''}`).then(setRows).catch(() => setRows([]))
  }, [hotelId, all])
  return (
    <>
      <label className="field check"><input type="checkbox" checked={all} onChange={e => setAll(e.target.checked)} /> Include closed reports</label>
      {!rows ? <p className="muted">Loading...</p> : rows.length === 0 ? <p className="muted">No lost reports.</p> : (
        <div className="report-list">
          {rows.map(l => (
            <Link key={l.id} to={`/lostfound/lost/${l.id}`} className="report-row">
              <div className="spread"><b>{l.number} {l.ownerName}</b><span className={`pill ${PILL[l.status]}`}>{STATUS[l.status]}</span></div>
              <div className="muted small">{[[l.color, l.brand, l.category].filter(Boolean).join(' '), l.room && `room ${l.room}`, `reported ${formatLocal(l.reportedLocal)}`].filter(Boolean).join(' | ')}</div>
              {l.description && <div className="small">{l.description}</div>}
            </Link>
          ))}
        </div>
      )}
    </>
  )
}

function Shipping({ hotelId }: { hotelId: number }) {
  const [rows, setRows] = useState<Shipment[] | null>(null)
  useEffect(() => { api.get<Shipment[]>(`/api/lostfound/shipments?hotelId=${hotelId}`).then(setRows).catch(() => setRows([])) }, [hotelId])
  return !rows ? <p className="muted">Loading...</p> : rows.length === 0 ? <p className="muted">No shipments waiting.</p> : (
    <div className="report-list">
      {rows.map(s => (
        <Link key={s.id} to={`/lostfound/found/${s.foundItemId}`} className="report-row">
          <div className="spread"><b>{s.itemNumber} {s.itemName} to {s.recipientName}</b>
            <span className={`pill ${s.payment === 'Waiting' ? 'st-AwaitingReview' : 'st-Approved'}`}>{PAYMENT[s.payment]}</span></div>
          <div className="muted small">{[s.city, s.state, s.country, s.carrier, s.cost != null && `$${s.cost.toFixed(2)}`].filter(Boolean).join(' | ')}</div>
        </Link>
      ))}
    </div>
  )
}

// --- Found item: log / view -----------------------------------------------------------

export function FoundPage() {
  const { id } = useParams()
  return id === 'new' || !id ? <FoundForm /> : <FoundView id={Number(id)} />
}

function FoundForm() {
  const navigate = useNavigate()
  const { lookups } = useSession()
  const { hotelId } = useLf()
  const [people, setPeople] = useState<Person[]>([])
  const [error, setError] = useState<string | null>(null)
  const [f, setF] = useState({
    hotelId, categoryId: null as number | null, detail: '', description: '', colorId: null as number | null, brand: '', modelSerial: '',
    highValue: false, foundLocal: nowPacific(), areaId: null as number | null, locationDetail: '', room: '', foundByType: 'Employee',
    foundByName: '', foundByUserId: null as number | null, storageLocation: '', ownerName: '', ownerContact: '', notes: '',
  })
  useEffect(() => { api.get<Person[]>('/api/people').then(setPeople).catch(() => setPeople([])) }, [])
  const areas = lookups.areas.filter(a => a.isActive && (a.hotelId == null || a.hotelId === f.hotelId))
  const text = (k: keyof typeof f, label: string, max = 200, wide = false) => (
    <label className={wide ? 'field wide' : 'field'}>{label}<input value={String(f[k] ?? '')} maxLength={max} onChange={e => setF({ ...f, [k]: e.target.value })} /></label>
  )
  const save = async () => {
    setError(null)
    try {
      const body = Object.fromEntries(Object.entries(f).map(([k, v]) => [k, v === '' ? null : v]))
      const r = await api.post<{ id: number }>('/api/lostfound/found', { ...body, purgeDate: null, foundByDepartmentId: null })
      navigate(`/lostfound/found/${r.id}`)
    } catch (e) { setError((e as Error).message) }
  }
  return (
    <div className="stack incident-form">
      <div className="spread"><h1>Log a found item</h1><Link to="/lostfound">Cancel</Link></div>
      <section className="card form">
        <Pick label="Category" list="property-category" value={f.categoryId} onChange={v => setF({ ...f, categoryId: v })} />
        {text('detail', 'What is it')}
        <Pick label="Color" list="color" value={f.colorId} onChange={v => setF({ ...f, colorId: v })} />
        {text('brand', 'Brand', 100)}{text('modelSerial', 'Model / serial #', 100)}
        <label className="field check"><input type="checkbox" checked={f.highValue} onChange={e => setF({ ...f, highValue: e.target.checked })} /> High value (cash, jewelry, phone, passport) - keep in the safe</label>
        {text('description', 'Description', 2000, true)}
      </section>
      <section className="card form">
        <label className="field">Found<input type="datetime-local" value={f.foundLocal} onChange={e => setF({ ...f, foundLocal: e.target.value })} /></label>
        <label className="field">Location<select value={f.areaId ?? ''} onChange={e => setF({ ...f, areaId: e.target.value ? Number(e.target.value) : null })}>
          <option value="">-</option>{areas.map(a => <option key={a.id} value={a.id}>{a.name}</option>)}</select></label>
        {text('locationDetail', 'Where exactly')}{text('room', 'Room #', 40)}
        <label className="field">Found by<select value={f.foundByType} onChange={e => setF({ ...f, foundByType: e.target.value })}>{FINDERS.map(x => <option key={x}>{x}</option>)}</select></label>
        {f.foundByType === 'Employee' && <label className="field">Employee<select value={f.foundByUserId ?? ''} onChange={e => setF({ ...f, foundByUserId: e.target.value ? Number(e.target.value) : null })}>
          <option value="">Someone else (type the name)</option>{people.map(p => <option key={p.id} value={p.id}>{p.name}</option>)}</select></label>}
        {f.foundByUserId == null && text('foundByName', 'Finder name')}
        {text('storageLocation', 'Kept in (shelf, bin, safe)', 100)}
        {text('ownerName', 'Owner (if known)')}{text('ownerContact', 'Owner contact')}
        {text('notes', 'Notes', 2000, true)}
      </section>
      {error && <p className="alert">{error}</p>}
      <div className="row sticky-actions"><button disabled={!f.categoryId && !f.detail.trim()} onClick={save}>Save</button>
        <span className="muted small">Kept {90} days by default; add a photo on the next page.</span></div>
    </div>
  )
}

function FoundView({ id }: { id: number }) {
  const { me } = useSession()
  const [f, setF] = useState<FoundItem | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [mode, setMode] = useState<'return' | 'ship' | 'dispose' | null>(null)
  const load = useCallback(() => { api.get<FoundItem>(`/api/lostfound/found/${id}`).then(setF).catch(e => setError(e.message)) }, [id])
  useEffect(load, [load])
  const run = async (fn: () => Promise<unknown>) => {
    setError(null)
    try { await fn(); setMode(null); load(); return true } catch (e) { setError((e as Error).message); return false }
  }
  if (!f) return error ? <p className="alert">{error}</p> : <p className="muted">Loading...</p>
  const i = f.item
  const openShipment = f.shipments.find(s => !s.shippedLocal && !s.cancelledUtc)
  const canEdit = me.modules.find(m => m.key === 'lostfound')?.level !== 'View'

  return (
    <div className="stack report">
      <p className="muted small no-print"><Link to="/lostfound">Lost &amp; found</Link></p>
      <div className="spread">
        <h1>{i.number} {itemName(i)}</h1>
        <span className="row">
          {i.highValue && <span className="pill prio-High">High value</span>}
          <span className={`pill ${PILL[i.status]}`}>{STATUS[i.status]}</span>
        </span>
      </div>
      {error && <p className="alert">{error}</p>}
      <section className="card block visit-card">
        {i.hasPhoto && <img className="visit-photo" src={`/api/lostfound/found/${i.id}/photo`} alt="Item" />}
        <dl className="facts">
          {[['Category', i.category], ['Color', i.color], ['Brand', i.brand], ['Model / serial', f.modelSerial], ['Description', f.description],
            ['Found', `${formatLocal(i.foundLocal)} - ${[i.area, f.locationDetail, i.room && `room ${i.room}`].filter(Boolean).join(', ')}`],
            ['Found by', [f.foundByName, f.foundByType].filter(Boolean).join(', ')], ['Kept in', i.storageLocation],
            ['Purge date', i.status === 'InStorage' ? formatLocal(i.purgeDate) : null], ['Owner', [f.ownerName, f.ownerContact].filter(Boolean).join(', ')],
            ['Notes', f.notes], ['Logged by', f.createdBy],
            ['Closed', f.closedLocal && `${formatLocal(f.closedLocal)} by ${f.closedBy}`], ['Returned to', f.returnedToName],
            ['Disposal', f.disposalMethod], ['Closing note', f.closeNote]]
            .filter(([, v]) => v).map(([k, v]) => <><dt key={`k${k}`}>{k}</dt><dd key={`v${k}`}>{v}</dd></>)}
        </dl>
      </section>
      {f.hasReturnSignature && <p><a href={`/api/lostfound/found/${i.id}/signature`} target="_blank" rel="noreferrer">View the owner's signature</a></p>}

      {f.matches.length > 0 && (
        <section className="card block">
          <h2>Possible owners (open lost reports)</h2>
          {f.matches.map(m => <p key={m.id}><Link to={`/lostfound/lost/${m.id}`}><b>{m.number}</b></Link> <span className="small">{m.summary}</span>
            <span className="muted small"> - score {m.score}</span></p>)}
        </section>
      )}

      {f.shipments.length > 0 && (
        <section className="card block">
          <h2>Shipping</h2>
          {f.shipments.map(s => <ShipmentBlock key={s.id} s={s} canEdit={canEdit && !s.shippedLocal && !s.cancelledUtc} run={run} />)}
        </section>
      )}

      {f.canEdit && (
        <div className="card stack no-print">
          <div className="row">
            {!openShipment && <button onClick={() => setMode('return')}>Return to owner</button>}
            {!openShipment && <button className="secondary" onClick={() => setMode('ship')}>Ship to owner</button>}
            {f.canDispose && <button className="secondary" onClick={() => setMode('dispose')}>Dispose</button>}
            <button className="link" onClick={() => run(() => api.post(`/api/lostfound/found/${i.id}/handover`, { flag: !i.forHandover }))}>
              {i.forHandover ? 'Remove from handover' : 'Flag for handover'}</button>
          </div>
          <label className="field" style={{ maxWidth: 360 }}>{i.hasPhoto ? 'Replace photo' : 'Add photo'}
            <input type="file" accept="image/*" capture="environment" onChange={async e => {
              const file = e.target.files?.[0]; if (!file) return
              const body = new FormData(); body.append('file', file)
              await run(() => api.post(`/api/lostfound/found/${i.id}/photo`, body))
            }} />
          </label>
          {mode === 'return' && <ReturnForm f={f} run={run} />}
          {mode === 'ship' && <ShipmentForm onSave={body => run(() => api.post(`/api/lostfound/found/${i.id}/shipments`, body))} initial={{ recipientName: f.ownerName ?? '' }} />}
          {mode === 'dispose' && <DisposeForm onSave={body => run(() => api.post(`/api/lostfound/found/${i.id}/dispose`, body))} />}
        </div>
      )}
    </div>
  )
}

function ReturnForm({ f, run }: { f: FoundItem; run: (fn: () => Promise<unknown>) => Promise<boolean> }) {
  const [name, setName] = useState(f.ownerName ?? '')
  const [note, setNote] = useState('')
  const [sig, setSig] = useState<string | null>(null)
  return (
    <div className="form">
      <label className="field">Collected by<input value={name} onChange={e => setName(e.target.value)} maxLength={200} /></label>
      <label className="field">Note (e.g. ID checked)<input value={note} onChange={e => setNote(e.target.value)} maxLength={1000} /></label>
      <SignaturePad label="Signature of the person collecting it" onChange={setSig} />
      <div><button disabled={!name.trim() || !sig} onClick={() => run(() => api.post(`/api/lostfound/found/${f.item.id}/return`, { name, signature: sig, note: note || null, lostReportId: null }))}>Hand over</button></div>
    </div>
  )
}

function DisposeForm({ onSave }: { onSave: (body: unknown) => Promise<boolean> }) {
  const [method, setMethod] = useState('')
  const [note, setNote] = useState('')
  return (
    <div className="form">
      <label className="field">How<select value={method} onChange={e => setMethod(e.target.value)}><option value="">-</option>{DISPOSAL.map(d => <option key={d}>{d}</option>)}</select></label>
      <label className="field">Note<input value={note} onChange={e => setNote(e.target.value)} maxLength={1000} /></label>
      <div><button className="danger" disabled={!method} onClick={() => onSave({ method, note: note || null })}>Dispose</button></div>
    </div>
  )
}

type ShipForm = { recipientName: string; street: string; city: string; state: string; zip: string; country: string; phone: string; email: string
  carrier: string; trackingNumber: string; cost: string; payment: PaymentStatus; paymentNote: string }

function ShipmentForm({ onSave, initial }: { onSave: (body: unknown) => Promise<boolean>; initial?: Partial<ShipForm> }) {
  const [s, setS] = useState<ShipForm>({ recipientName: '', street: '', city: '', state: '', zip: '', country: 'USA', phone: '', email: '',
    carrier: 'UPS', trackingNumber: '', cost: '', payment: 'Waiting', paymentNote: '', ...initial })
  const text = (k: keyof ShipForm, label: string, max = 200, wide = false) => (
    <label className={wide ? 'field wide' : 'field'}>{label}<input value={s[k]} maxLength={max} onChange={e => setS({ ...s, [k]: e.target.value })} /></label>
  )
  return (
    <div className="form">
      {text('recipientName', 'Ship to')}{text('street', 'Street', 200, true)}{text('city', 'City', 100)}{text('state', 'State', 60)}
      {text('zip', 'Zip', 20)}{text('country', 'Country', 60)}{text('phone', 'Phone', 40)}{text('email', 'Email')}
      <label className="field">Carrier<select value={s.carrier} onChange={e => setS({ ...s, carrier: e.target.value })}>{CARRIERS.map(c => <option key={c}>{c}</option>)}</select></label>
      <label className="field">Shipping cost ($)<input type="number" min={0} step="0.01" value={s.cost} onChange={e => setS({ ...s, cost: e.target.value })} /></label>
      <label className="field">Payment<select value={s.payment} onChange={e => setS({ ...s, payment: e.target.value as PaymentStatus })}>
        {(Object.keys(PAYMENT) as PaymentStatus[]).map(p => <option key={p} value={p}>{PAYMENT[p]}</option>)}</select></label>
      {text('paymentNote', 'Payment note (never a card number)', 400, true)}
      <p className="muted small wide">Card details are never taken or written here. The guest pays through the payment link or at the front desk.</p>
      <div><button disabled={!s.recipientName.trim()} onClick={() => onSave({
        ...Object.fromEntries(Object.entries(s).map(([k, v]) => [k, v === '' ? null : v])), cost: s.cost === '' ? null : Number(s.cost),
      })}>Save shipment</button></div>
    </div>
  )
}

function ShipmentBlock({ s, canEdit, run }: { s: Shipment; canEdit: boolean; run: (fn: () => Promise<unknown>) => Promise<boolean> }) {
  const [editing, setEditing] = useState(false)
  const [tracking, setTracking] = useState(s.trackingNumber ?? '')
  return (
    <div className="note">
      <p><b>To {s.recipientName}</b> <span className={`pill ${s.payment === 'Waiting' ? 'st-AwaitingReview' : 'st-Approved'}`}>{PAYMENT[s.payment]}</span>
        {s.shippedLocal && <span className="pill st-Approved">Shipped {formatLocal(s.shippedLocal)}</span>}{s.cancelledUtc && <span className="pill st-Draft">Cancelled</span>}</p>
      <p className="small">{[s.street, s.city, s.state, s.zip, s.country].filter(Boolean).join(', ')}{s.phone ? ` | ${s.phone}` : ''}{s.email ? ` | ${s.email}` : ''}</p>
      <p className="muted small">{[s.carrier, s.trackingNumber && `tracking ${s.trackingNumber}`, s.cost != null && `$${s.cost.toFixed(2)}`, s.paymentNote].filter(Boolean).join(' | ')}</p>
      {canEdit && !editing && (
        <div className="row">
          <input placeholder="Tracking #" value={tracking} onChange={e => setTracking(e.target.value)} style={{ maxWidth: 260 }} />
          <button disabled={s.payment === 'Waiting' || !tracking.trim()} title={s.payment === 'Waiting' ? 'Mark payment first' : ''}
            onClick={() => run(() => api.post(`/api/lostfound/shipments/${s.id}/shipped`, { shippedLocal: null, carrier: s.carrier, trackingNumber: tracking }))}>Mark shipped</button>
          <button className="secondary" onClick={() => setEditing(true)}>Change / payment</button>
          <button className="link small" onClick={() => run(() => api.post(`/api/lostfound/shipments/${s.id}/cancel`))}>Cancel shipment</button>
        </div>
      )}
      {editing && <ShipmentForm initial={{ ...Object.fromEntries(Object.entries(s).map(([k, v]) => [k, v ?? ''])), cost: s.cost?.toString() ?? '', payment: s.payment } as Partial<ShipForm>}
        onSave={async body => { const ok = await run(() => api.put(`/api/lostfound/shipments/${s.id}`, body)); if (ok) setEditing(false); return ok }} />}
    </div>
  )
}

// --- Lost report: log / view -----------------------------------------------------------

export function LostPage() {
  const { id } = useParams()
  return id === 'new' || !id ? <LostForm /> : <LostView id={Number(id)} />
}

function LostForm() {
  const navigate = useNavigate()
  const { hotelId } = useLf()
  const [error, setError] = useState<string | null>(null)
  const [l, setL] = useState({ hotelId, ownerName: '', phone: '', email: '', room: '', arrivalDate: '', departureDate: '', categoryId: null as number | null,
    description: '', colorId: null as number | null, brand: '', lostWhere: '', lostFrom: '', lostTo: '', notes: '' })
  const text = (k: keyof typeof l, label: string, type = 'text', max = 200) => (
    <label className="field">{label}<input type={type} value={String(l[k] ?? '')} maxLength={max} onChange={e => setL({ ...l, [k]: e.target.value })} /></label>
  )
  return (
    <div className="stack incident-form">
      <div className="spread"><h1>Log a lost report</h1><Link to="/lostfound?tab=lost">Cancel</Link></div>
      <section className="card form">
        {text('ownerName', 'Name')}{text('phone', 'Phone', 'tel', 40)}{text('email', 'Email', 'email')}{text('room', 'Room #', 'text', 40)}
        {text('arrivalDate', 'Arrival', 'date')}{text('departureDate', 'Departure', 'date')}
      </section>
      <section className="card form">
        <Pick label="Category" list="property-category" value={l.categoryId} onChange={v => setL({ ...l, categoryId: v })} />
        <Pick label="Color" list="color" value={l.colorId} onChange={v => setL({ ...l, colorId: v })} />
        {text('brand', 'Brand', 'text', 100)}{text('lostWhere', 'Where it was lost')}
        {text('lostFrom', 'Lost between', 'date')}{text('lostTo', 'and', 'date')}
        <label className="field wide">Description<textarea rows={3} value={l.description} maxLength={2000} onChange={e => setL({ ...l, description: e.target.value })} /></label>
        <label className="field wide">Notes<input value={l.notes} maxLength={2000} onChange={e => setL({ ...l, notes: e.target.value })} /></label>
      </section>
      {error && <p className="alert">{error}</p>}
      <div className="row sticky-actions"><button disabled={!l.ownerName.trim()} onClick={async () => {
        setError(null)
        try {
          const r = await api.post<{ id: number }>('/api/lostfound/lost', Object.fromEntries(Object.entries(l).map(([k, v]) => [k, v === '' ? null : v])))
          navigate(`/lostfound/lost/${r.id}`)
        } catch (e) { setError((e as Error).message) }
      }}>Save and look for matches</button></div>
    </div>
  )
}

function LostView({ id }: { id: number }) {
  const [l, setL] = useState<LostReport | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [closing, setClosing] = useState('')
  const { canEdit } = useLf()
  const load = useCallback(() => { api.get<LostReport>(`/api/lostfound/lost/${id}`).then(setL).catch(e => setError(e.message)) }, [id])
  useEffect(load, [load])
  const run = async (fn: () => Promise<unknown>) => {
    setError(null)
    try { await fn(); load() } catch (e) { setError((e as Error).message) }
  }
  if (!l) return error ? <p className="alert">{error}</p> : <p className="muted">Loading...</p>
  const r = l.report
  return (
    <div className="stack report">
      <p className="muted small no-print"><Link to="/lostfound?tab=lost">Lost reports</Link></p>
      <div className="spread"><h1>{r.number} {r.ownerName}</h1><span className={`pill ${PILL[r.status]}`}>{STATUS[r.status]}</span></div>
      {error && <p className="alert">{error}</p>}
      <section className="card block">
        <dl className="facts">
          {[['Item', [r.color, r.brand, r.category].filter(Boolean).join(' ')], ['Description', r.description], ['Lost', [l.lostWhere,
            l.lostFrom && `${formatLocal(l.lostFrom)}${l.lostTo ? ` - ${formatLocal(l.lostTo)}` : ''}`].filter(Boolean).join(', ')],
            ['Room', r.room], ['Stay', l.arrivalDate && `${formatLocal(l.arrivalDate)} - ${formatLocal(l.departureDate)}`], ['Phone', l.phone], ['Email', l.email],
            ['Notes', l.notes], ['Reported', `${formatLocal(r.reportedLocal)} by ${l.createdBy}`]]
            .filter(([, v]) => v).map(([k, v]) => <><dt key={`k${k}`}>{k}</dt><dd key={`v${k}`}>{v}</dd></>)}
        </dl>
        {r.foundItemId && <p><Link to={`/lostfound/found/${r.foundItemId}`}>Matched found item</Link></p>}
      </section>
      {r.status === 'Open' && (
        <section className="card block">
          <h2>Possible matches</h2>
          {l.matches.length === 0 ? <p className="muted small">No item in storage looks like it yet. Check again when new items are logged.</p> : l.matches.map(m => (
            <p key={m.id}><Link to={`/lostfound/found/${m.id}`}><b>{m.number}</b></Link> <span className="small">{m.summary}</span>
              <span className="muted small"> - found {formatLocal(m.when)}, score {m.score}</span>
              {canEdit && <> <button className="link small" onClick={() => run(() => api.post(`/api/lostfound/lost/${r.id}/match`, { foundItemId: m.id }))}>This is it</button></>}</p>
          ))}
        </section>
      )}
      {canEdit && r.status !== 'Closed' && (
        <div className="row no-print">
          <input placeholder="Why close it (not found, guest found it...)" value={closing} onChange={e => setClosing(e.target.value)} style={{ maxWidth: 360 }} />
          <button className="secondary" disabled={!closing.trim()} onClick={() => run(() => api.post(`/api/lostfound/lost/${r.id}/close`, { note: closing }))}>Close report</button>
          <button className="link" onClick={() => run(() => api.post(`/api/lostfound/lost/${r.id}/handover`, { flag: !r.forHandover }))}>{r.forHandover ? 'Remove from handover' : 'Flag for handover'}</button>
        </div>
      )}
    </div>
  )
}
