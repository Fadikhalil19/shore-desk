import { useCallback, useEffect, useState } from 'react'
import { Link, useNavigate, useParams, useSearchParams } from 'react-router-dom'
import { api } from '../api'
import { useSession } from '../App'
import { formatLocal, nowPacific } from '../format'
import type { Companion, Person, ScheduledVisit, Visit } from '../types'

const TYPES = ['Visitor', 'Contractor', 'Vendor', 'Owner', 'Member', 'Interview', 'Other']
const COLORS = ['Red', 'Blue', 'Green', 'Yellow', 'White', 'Orange']

function useHotel(): [number, boolean, string] {
  const { me, lookups } = useSession()
  const [params] = useSearchParams()
  const access = me.modules.find(m => m.key === 'visitors')
  const hotelId = Number(params.get('hotelId') ?? access?.hotelId ?? me.hotelId ?? lookups.hotels[0].id)
  return [hotelId, access?.level === 'Edit' || access?.level === 'Manage', access?.level ?? 'None']
}

// Visitors: who is on property now, today, booked visits, search; check in / out.
export default function Visitors() {
  const { me, lookups } = useSession()
  const [params, setParams] = useSearchParams()
  const [hotelId, canEdit] = useHotel()
  const limit = me.modules.find(m => m.key === 'visitors')?.hotelId
  const tab = params.get('tab') ?? 'in'
  const set = (patch: Record<string, string>) => setParams({ ...(params.get('hotelId') ? { hotelId: String(hotelId) } : {}), ...patch })

  return (
    <div className="stack">
      <div className="spread">
        <h1>Visitors</h1>
        <div className="row">
          {limit == null && lookups.hotels.length > 1 && (
            <select value={hotelId} style={{ width: 'auto' }} onChange={e => setParams({ tab, hotelId: e.target.value })}>
              {lookups.hotels.filter(h => h.isActive).map(h => <option key={h.id} value={h.id}>{h.code}</option>)}
            </select>
          )}
          {canEdit && <Link className="button" to={`/visitors/new?hotelId=${hotelId}`}>Check in</Link>}
        </div>
      </div>
      <div className="tabs" role="tablist">
        {[['in', 'On property'], ['today', 'Today'], ['booked', 'Booked'], ['search', 'Search']].map(([k, label]) => (
          <button key={k} role="tab" aria-selected={tab === k} className={tab === k ? 'tab active' : 'tab'} onClick={() => set({ tab: k })}>{label}</button>
        ))}
      </div>
      {tab === 'booked' ? <Booked hotelId={hotelId} canEdit={canEdit} /> : <VisitList hotelId={hotelId} view={tab} canEdit={canEdit} />}
    </div>
  )
}

function VisitList({ hotelId, view, canEdit }: { hotelId: number; view: string; canEdit: boolean }) {
  const [list, setList] = useState<Visit[] | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [q, setQ] = useState('')
  const today = nowPacific().slice(0, 10)
  const [from, setFrom] = useState(today)
  const [to, setTo] = useState(today)
  const load = useCallback(() => {
    const p = new URLSearchParams({ hotelId: String(hotelId), view })
    if (view === 'search') { p.set('from', from); p.set('to', to); if (q.trim()) p.set('q', q.trim()) }
    api.get<Visit[]>(`/api/visitors?${p}`).then(setList).catch(e => setError(e.message))
  }, [hotelId, view, from, to, q])
  useEffect(() => { setList(null); load() }, [hotelId, view]) // eslint-disable-line react-hooks/exhaustive-deps

  const out = async (v: Visit) => {
    try { await api.post(`/api/visitors/${v.id}/checkout`, { outLocal: null }); load() } catch (e) { setError((e as Error).message) }
  }

  return (
    <>
      {view === 'search' && (
        <form className="card form" onSubmit={e => { e.preventDefault(); load() }}>
          <label className="field">Name, company, host or badge<input value={q} onChange={e => setQ(e.target.value)} /></label>
          <label className="field">From<input type="date" value={from} onChange={e => setFrom(e.target.value)} /></label>
          <label className="field">To<input type="date" value={to} onChange={e => setTo(e.target.value)} /></label>
          <div><button type="submit">Search</button></div>
        </form>
      )}
      {error && <p className="alert">{error}</p>}
      {!list ? <p className="muted">Loading...</p> : list.length === 0 ? <p className="muted">{view === 'in' ? 'No visitors on property.' : 'No visitors.'}</p> : (
        <div className="report-list">
          {list.map(v => (
            <div key={v.id} className={`report-row ${v.overdue ? 'prio-Highest' : ''}`}>
              <div className="spread">
                <Link to={`/visitors/${v.id}`}><b>{v.visitorName}</b>{v.companions.length > 0 && ` +${v.companions.length}`}</Link>
                <span className="row">
                  {v.badgeNumber && <span className="pill">{v.badgeColor ? `${v.badgeColor} ` : ''}{v.badgeNumber}</span>}
                  <span className={`pill ${v.outLocal ? 'st-Draft' : v.overdue ? 'st-Voided' : 'st-Approved'}`}>{v.outLocal ? 'Left' : v.overdue ? `In ${v.hoursIn} h` : 'On property'}</span>
                </span>
              </div>
              <div className="muted small">
                {[v.visitorType, v.company, v.hostName && `visiting ${v.hostName}`, [v.areaName, v.areaDetail].filter(Boolean).join(' - ')].filter(Boolean).join(' | ')}
              </div>
              <div className="muted small">In {formatLocal(v.inLocal)}{v.outLocal ? `, out ${formatLocal(v.outLocal)}` : ''}</div>
              {canEdit && !v.outLocal && <div><button className="secondary" onClick={() => out(v)}>Check out</button></div>}
            </div>
          ))}
        </div>
      )}
    </>
  )
}

function Booked({ hotelId, canEdit }: { hotelId: number; canEdit: boolean }) {
  const [list, setList] = useState<ScheduledVisit[] | null>(null)
  const [n, setN] = useState({ visitorName: '', company: '', visitorType: 'Visitor', expectedLocal: '', hostName: '', purpose: '' })
  const [error, setError] = useState<string | null>(null)
  const load = useCallback(() => { api.get<ScheduledVisit[]>(`/api/visitors/scheduled?hotelId=${hotelId}`).then(setList).catch(e => setError(e.message)) }, [hotelId])
  useEffect(load, [load])
  const run = async (fn: () => Promise<unknown>) => {
    setError(null)
    try { await fn(); load(); return true } catch (e) { setError((e as Error).message); return false }
  }
  return (
    <>
      {error && <p className="alert">{error}</p>}
      {!list ? <p className="muted">Loading...</p> : list.length === 0 ? <p className="muted">No visits booked for the next 7 days.</p> : (
        <div className="report-list">
          {list.map(s => (
            <div key={s.id} className="report-row">
              <div className="spread"><b>{s.visitorName}</b><span className="muted small">{formatLocal(s.expectedLocal)}</span></div>
              <div className="muted small">{[s.visitorType, s.company, s.hostName && `visiting ${s.hostName}`, s.purpose].filter(Boolean).join(' | ')} - booked by {s.bookedBy}</div>
              {canEdit && (s.visitId
                ? <Link to={`/visitors/${s.visitId}`}>Arrived</Link>
                : <div className="row">
                    <Link className="button" to={`/visitors/new?hotelId=${hotelId}&booked=${s.id}`}>Check in</Link>
                    <button className="link small" onClick={() => run(() => api.post(`/api/visitors/scheduled/${s.id}/cancel`))}>Cancel booking</button>
                  </div>)}
            </div>
          ))}
        </div>
      )}
      {canEdit && (
        <section className="card form">
          <h2 className="wide">Book a visit</h2>
          <label className="field">Visitor name<input value={n.visitorName} onChange={e => setN({ ...n, visitorName: e.target.value })} maxLength={200} /></label>
          <label className="field">Company<input value={n.company} onChange={e => setN({ ...n, company: e.target.value })} maxLength={200} /></label>
          <label className="field">Type<select value={n.visitorType} onChange={e => setN({ ...n, visitorType: e.target.value })}>{TYPES.map(t => <option key={t}>{t}</option>)}</select></label>
          <label className="field">Expected<input type="datetime-local" value={n.expectedLocal} onChange={e => setN({ ...n, expectedLocal: e.target.value })} /></label>
          <label className="field">Visiting<input value={n.hostName} onChange={e => setN({ ...n, hostName: e.target.value })} maxLength={200} /></label>
          <label className="field">Purpose<input value={n.purpose} onChange={e => setN({ ...n, purpose: e.target.value })} maxLength={400} /></label>
          <div><button disabled={!n.visitorName.trim() || !n.expectedLocal} onClick={async () => {
            if (await run(() => api.post('/api/visitors/scheduled', { ...n, hotelId, company: n.company || null, hostName: n.hostName || null, purpose: n.purpose || null, hostUserId: null, notes: null })))
              setN({ visitorName: '', company: '', visitorType: 'Visitor', expectedLocal: '', hostName: '', purpose: '' })
          }}>Book</button></div>
        </section>
      )}
    </>
  )
}

// --- Check in / one visit ------------------------------------------------------------

export function VisitPage() {
  const { id } = useParams()
  const [params] = useSearchParams()
  const navigate = useNavigate()
  const { lookups } = useSession()
  const [hotelFromUrl, canEdit, level] = useHotel()
  const [v, setV] = useState<Visit | null>(null)
  const [people, setPeople] = useState<Person[]>([])
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const [f, setF] = useState({
    hotelId: hotelFromUrl, visitorName: '', company: '', visitorType: 'Visitor', phone: '', email: '', areaId: null as number | null,
    areaDetail: '', hostName: '', hostUserId: null as number | null, purpose: '', badgeColor: '', badgeNumber: '', expectedOutLocal: '',
    notes: '', companions: [] as Companion[], scheduledVisitId: null as number | null,
  })
  const [voiding, setVoiding] = useState('')
  const isNew = !id

  useEffect(() => { api.get<Person[]>('/api/people').then(setPeople).catch(() => setPeople([])) }, [])
  const load = useCallback(() => { if (id) api.get<Visit>(`/api/visitors/${id}`).then(setV).catch(e => setError(e.message)) }, [id])
  useEffect(load, [load])
  // Checking in a booked visit: start from the booking.
  useEffect(() => {
    const booked = params.get('booked')
    if (!isNew || !booked) return
    api.get<ScheduledVisit[]>(`/api/visitors/scheduled?hotelId=${hotelFromUrl}`).then(list => {
      const s = list.find(x => x.id === Number(booked))
      if (s) setF(x => ({ ...x, visitorName: s.visitorName, company: s.company ?? '', visitorType: s.visitorType, hostName: s.hostName ?? '', purpose: s.purpose ?? '', scheduledVisitId: s.id }))
    }).catch(() => undefined)
  }, [isNew, params, hotelFromUrl])

  const areas = lookups.areas.filter(a => a.isActive && (a.hotelId == null || a.hotelId === f.hotelId))

  if (!isNew) {
    if (error) return <p className="alert">{error}</p>
    if (!v) return <p className="muted">Loading...</p>
    return (
      <div className="stack report">
        <p className="muted small no-print"><Link to="/visitors">Visitors</Link></p>
        <div className="spread">
          <h1>{v.visitorName}</h1>
          <div className="row no-print">
            <button className="secondary" onClick={() => window.open(`/visitors/${v.id}/badge`, '_blank')}>Print badge</button>
            {canEdit && !v.outLocal && !v.voidedUtc && <button onClick={async () => { await api.post(`/api/visitors/${v.id}/checkout`, { outLocal: null }); load() }}>Check out</button>}
          </div>
        </div>
        {v.voidedUtc && <p className="alert"><b>Voided:</b> {v.voidReason}</p>}
        <section className="card block visit-card">
          {v.hasPhoto && <img className="visit-photo" src={`/api/visitors/${v.id}/photo`} alt={`Photo of ${v.visitorName}`} />}
          <dl className="facts">
            <dt>Type</dt><dd>{v.visitorType}</dd>
            {v.company && <><dt>Company</dt><dd>{v.company}</dd></>}
            {v.hostName && <><dt>Visiting</dt><dd>{v.hostName}</dd></>}
            {(v.areaName || v.areaDetail) && <><dt>Going to</dt><dd>{[v.areaName, v.areaDetail].filter(Boolean).join(' - ')}</dd></>}
            {v.purpose && <><dt>Purpose</dt><dd>{v.purpose}</dd></>}
            {v.badgeNumber && <><dt>Badge</dt><dd>{v.badgeColor} {v.badgeNumber}</dd></>}
            <dt>In</dt><dd>{formatLocal(v.inLocal)} ({v.checkedInBy})</dd>
            {v.outLocal ? <><dt>Out</dt><dd>{formatLocal(v.outLocal)} ({v.checkedOutBy})</dd></> : <><dt>On property</dt><dd className={v.overdue ? 'error' : ''}>{v.hoursIn} h</dd></>}
            {v.companions.length > 0 && <><dt>With</dt><dd>{v.companions.map(c => c.name + (c.badgeNumber ? ` (${c.badgeNumber})` : '')).join(', ')}</dd></>}
            {v.phone && <><dt>Phone</dt><dd>{v.phone}</dd></>}
            {v.email && <><dt>Email</dt><dd>{v.email}</dd></>}
            {v.notes && <><dt>Notes</dt><dd>{v.notes}</dd></>}
          </dl>
        </section>
        {canEdit && !v.voidedUtc && (
          <label className="field no-print" style={{ maxWidth: 360 }}>{v.hasPhoto ? 'Replace photo' : 'Add photo'}
            <input type="file" accept="image/*" capture="user" onChange={async e => {
              const file = e.target.files?.[0]; if (!file) return
              const body = new FormData(); body.append('file', file)
              try { await api.post(`/api/visitors/${v.id}/photo`, body); load() } catch (x) { setError((x as Error).message) }
            }} />
          </label>
        )}
        {level === 'Manage' && !v.voidedUtc && (
          <div className="row no-print">
            <input placeholder="Reason to void (wrong entry, duplicate...)" value={voiding} onChange={e => setVoiding(e.target.value)} style={{ maxWidth: 360 }} />
            <button className="danger" disabled={!voiding.trim()} onClick={async () => { await api.post(`/api/visitors/${v.id}/void`, { reason: voiding }); load() }}>Void</button>
          </div>
        )}
      </div>
    )
  }

  if (!canEdit) return <p className="alert">You cannot check visitors in.</p>
  const save = async () => {
    setBusy(true); setError(null)
    try {
      const r = await api.post<{ id: number }>('/api/visitors', {
        ...f, company: f.company || null, phone: f.phone || null, email: f.email || null, areaDetail: f.areaDetail || null,
        hostName: f.hostName || null, purpose: f.purpose || null, badgeColor: f.badgeColor || null, badgeNumber: f.badgeNumber || null,
        expectedOutLocal: f.expectedOutLocal || null, notes: f.notes || null, inLocal: null,
      })
      navigate(`/visitors/${r.id}`)
    } catch (e) { setError((e as Error).message) } finally { setBusy(false) }
  }
  return (
    <div className="stack incident-form">
      <div className="spread"><h1>Check in a visitor</h1><Link to="/visitors">Cancel</Link></div>
      <section className="card form">
        <label className="field">Name<b className="req"> *</b><input value={f.visitorName} onChange={e => setF({ ...f, visitorName: e.target.value })} maxLength={200} autoFocus /></label>
        <label className="field">Type<select value={f.visitorType} onChange={e => setF({ ...f, visitorType: e.target.value })}>{TYPES.map(t => <option key={t}>{t}</option>)}</select></label>
        <label className="field">Company<input value={f.company} onChange={e => setF({ ...f, company: e.target.value })} maxLength={200} /></label>
        <label className="field">Phone<input type="tel" value={f.phone} onChange={e => setF({ ...f, phone: e.target.value })} maxLength={40} /></label>
        <label className="field">Visiting (employee)
          <select value={f.hostUserId ?? ''} onChange={e => setF({ ...f, hostUserId: e.target.value ? Number(e.target.value) : null })}>
            <option value="">Someone else (type below)</option>{people.map(p => <option key={p.id} value={p.id}>{p.name}</option>)}
          </select>
        </label>
        {f.hostUserId == null && <label className="field">Visiting<input value={f.hostName} onChange={e => setF({ ...f, hostName: e.target.value })} maxLength={200} /></label>}
        <label className="field">Going to
          <select value={f.areaId ?? ''} onChange={e => setF({ ...f, areaId: e.target.value ? Number(e.target.value) : null })}>
            <option value="">-</option>{areas.map(a => <option key={a.id} value={a.id}>{a.name}</option>)}
          </select>
        </label>
        <label className="field">Detail<input value={f.areaDetail} onChange={e => setF({ ...f, areaDetail: e.target.value })} maxLength={200} /></label>
        <label className="field">Purpose<input value={f.purpose} onChange={e => setF({ ...f, purpose: e.target.value })} maxLength={400} /></label>
        <label className="field">Badge color<select value={f.badgeColor} onChange={e => setF({ ...f, badgeColor: e.target.value })}><option value="">-</option>{COLORS.map(c => <option key={c}>{c}</option>)}</select></label>
        <label className="field">Badge #<input value={f.badgeNumber} onChange={e => setF({ ...f, badgeNumber: e.target.value })} maxLength={40} /></label>
        <label className="field">Expected to leave<input type="datetime-local" value={f.expectedOutLocal} onChange={e => setF({ ...f, expectedOutLocal: e.target.value })} /></label>
        <label className="field wide">Notes<input value={f.notes} onChange={e => setF({ ...f, notes: e.target.value })} maxLength={2000} /></label>
      </section>
      <section className="card form">
        <h2 className="wide">In the same group</h2>
        {f.companions.map((c, i) => (
          <div key={i} className="row wide">
            <input placeholder="Name" value={c.name} style={{ flex: 2 }} onChange={e => setF({ ...f, companions: f.companions.map((x, j) => j === i ? { ...x, name: e.target.value } : x) })} />
            <input placeholder="Badge #" value={c.badgeNumber ?? ''} style={{ flex: 1 }} onChange={e => setF({ ...f, companions: f.companions.map((x, j) => j === i ? { ...x, badgeNumber: e.target.value || null } : x) })} />
            <button className="link small" onClick={() => setF({ ...f, companions: f.companions.filter((_, j) => j !== i) })}>Remove</button>
          </div>
        ))}
        {f.companions.length < 5 && <div><button className="secondary" onClick={() => setF({ ...f, companions: [...f.companions, { name: '', badgeNumber: null }] })}>Add person</button></div>}
      </section>
      {error && <p className="alert">{error}</p>}
      <div className="row sticky-actions"><button disabled={busy || !f.visitorName.trim()} onClick={save}>Check in</button>
        <span className="muted small">A photo can be added on the next page.</span></div>
    </div>
  )
}

// A badge to print on a label printer or plain paper (browser print).
export function VisitBadge() {
  const { id } = useParams()
  const { lookups } = useSession()
  const [v, setV] = useState<Visit | null>(null)
  useEffect(() => { api.get<Visit>(`/api/visitors/${id}`).then(x => { setV(x); setTimeout(() => window.print(), 600) }) }, [id])
  if (!v) return <p className="muted">Loading...</p>
  const hotel = lookups.hotels.find(h => h.id === v.hotelId)
  return (
    <div className="badge" style={{ borderColor: v.badgeColor?.toLowerCase() || '#123a3a' }}>
      <div className="badge-head">{hotel?.name} - {v.visitorType.toUpperCase()}</div>
      {v.hasPhoto && <img src={`/api/visitors/${v.id}/photo`} alt="" />}
      <div className="badge-name">{v.visitorName}</div>
      {v.company && <div>{v.company}</div>}
      <div className="small">{formatLocal(v.inLocal).split(',').slice(0, 2).join(',')}{v.badgeNumber ? ` - #${v.badgeNumber}` : ''}</div>
      {v.hostName && <div className="small">Visiting {v.hostName}</div>}
    </div>
  )
}
