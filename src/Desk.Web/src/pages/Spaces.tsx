import { useCallback, useEffect, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { api } from '../api'
import { useSession } from '../App'
import { formatLocal } from '../format'
import type { Person, Space, SpaceHistoryRow } from '../types'

type Kind = 'lockers' | 'parking'
const SECTIONS: Record<Kind, string[]> = { lockers: ["Men's", "Women's", 'Admin'], parking: ['Employee', 'Motorcycle', 'Visitor', 'Reserved'] }
const MODULE: Record<Kind, string> = { lockers: 'lockers', parking: 'parking' }

// Lockers and parking: the same screen over numbered spaces (vacant / assigned).
export default function Spaces({ kind }: { kind: Kind }) {
  const { me, lookups } = useSession()
  const [params, setParams] = useSearchParams()
  const access = me.modules.find(m => m.key === MODULE[kind])
  const level = access?.level ?? 'None'
  const hotelId = Number(params.get('hotelId') ?? access?.hotelId ?? me.hotelId ?? lookups.hotels[0].id)
  const tab = params.get('tab') ?? 'board'
  const tabs: [string, string][] = [['board', kind === 'lockers' ? 'Lockers' : 'Spaces'], ['history', 'History']]
  if (level === 'Manage') tabs.push(['list', kind === 'lockers' ? 'Locker list' : 'Space list'])

  return (
    <div className="stack">
      <div className="spread">
        <h1>{kind === 'lockers' ? 'Lockers' : 'Parking'}</h1>
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
      {tab === 'board' && <Board kind={kind} hotelId={hotelId} canEdit={level === 'Edit' || level === 'Manage'} />}
      {tab === 'history' && <History kind={kind} hotelId={hotelId} />}
      {tab === 'list' && level === 'Manage' && <SpaceList kind={kind} hotelId={hotelId} />}
    </div>
  )
}

function useSpaces(kind: Kind, hotelId: number, all = false) {
  const [spaces, setSpaces] = useState<Space[] | null>(null)
  const [error, setError] = useState<string | null>(null)
  const load = useCallback(() => {
    api.get<Space[]>(`/api/${kind}?hotelId=${hotelId}${all ? '&all=true' : ''}`).then(setSpaces).catch(e => setError(e.message))
  }, [kind, hotelId, all])
  useEffect(load, [load])
  return { spaces, error, load }
}

function Board({ kind, hotelId, canEdit }: { kind: Kind; hotelId: number; canEdit: boolean }) {
  const { spaces, error, load } = useSpaces(kind, hotelId)
  const [filter, setFilter] = useState<'all' | 'vacant' | 'taken'>('all')
  const [section, setSection] = useState('')
  const [q, setQ] = useState('')
  const [open, setOpen] = useState<number | null>(null)
  const [people, setPeople] = useState<Person[]>([])
  useEffect(() => { api.get<Person[]>('/api/people').then(setPeople).catch(() => setPeople([])) }, [])

  if (error) return <p className="alert">{error}</p>
  if (!spaces) return <p className="muted">Loading...</p>
  const text = q.trim().toLowerCase()
  const shown = spaces.filter(s => (!section || s.section === section) && (filter === 'all' || (filter === 'vacant' ? !s.holder : !!s.holder))
    && (!text || s.number.toLowerCase().includes(text) || s.holder?.name.toLowerCase().includes(text) || s.holder?.plate?.toLowerCase().includes(text)))
  const taken = spaces.filter(s => s.holder).length

  return (
    <>
      <div className="stat-tiles">
        <div className="stat-tile"><span className="muted small">Total</span><b>{spaces.length}</b></div>
        <div className="stat-tile"><span className="muted small">Assigned</span><b>{taken}</b></div>
        <div className="stat-tile"><span className="muted small">Vacant</span><b>{spaces.length - taken}</b></div>
      </div>
      <div className="card form">
        <label className="field">Find<input value={q} onChange={e => setQ(e.target.value)} placeholder={kind === 'parking' ? 'Number, name or plate' : 'Number or name'} /></label>
        <label className="field">Section<select value={section} onChange={e => setSection(e.target.value)}><option value="">All</option>{SECTIONS[kind].map(s => <option key={s}>{s}</option>)}</select></label>
        <label className="field">Show<select value={filter} onChange={e => setFilter(e.target.value as typeof filter)}>
          <option value="all">All</option><option value="vacant">Vacant</option><option value="taken">Assigned</option></select></label>
      </div>
      {spaces.length === 0 && <p className="muted">No {kind === 'lockers' ? 'lockers' : 'spaces'} yet. A manager adds them on the list tab.</p>}
      <div className="report-list">
        {shown.map(s => (
          <div key={s.id} className="report-row">
            <div className="spread" role="button" tabIndex={0} style={{ cursor: canEdit ? 'pointer' : 'default' }}
              onClick={() => canEdit && setOpen(open === s.id ? null : s.id)} onKeyDown={e => e.key === 'Enter' && canEdit && setOpen(open === s.id ? null : s.id)}>
              <b>{s.section} {s.number}</b>
              <span className={`pill ${s.holder ? 'st-AwaitingReview' : 'st-Approved'}`}>{s.holder ? 'Assigned' : 'Vacant'}</span>
            </div>
            {s.holder && <div className="small">{s.holder.name} since {formatLocal(s.holder.fromDate)}
              {s.holder.vehicle && <span className="muted"> - {s.holder.vehicle}</span>}{s.holder.plate && <span className="muted"> ({s.holder.plate})</span>}
              {s.holder.stickerNumber && <span className="muted">, sticker {s.holder.stickerNumber}</span>}</div>}
            {s.lockCode && <div className="muted small">Lock code {s.lockCode}{s.lockSerial ? `, serial ${s.lockSerial}` : ''}</div>}
            {open === s.id && <SpaceActions kind={kind} s={s} people={people} done={() => { setOpen(null); load() }} />}
          </div>
        ))}
      </div>
    </>
  )
}

function SpaceActions({ kind, s, people, done }: { kind: Kind; s: Space; people: Person[]; done: () => void }) {
  const { lookups } = useSession()
  const [f, setF] = useState({ userId: null as number | null, name: '', departmentId: null as number | null, vehicleMake: '', vehicleModel: '',
    vehicleYear: '', vehicleColor: '', plate: '', plateState: 'CA', stickerNumber: '', notes: '' })
  const [error, setError] = useState<string | null>(null)
  const run = async (fn: () => Promise<unknown>) => {
    setError(null)
    try { await fn(); done() } catch (e) { setError((e as Error).message) }
  }
  if (s.holder) return (
    <div className="row" style={{ marginTop: '0.5rem' }}>
      <button className="secondary" onClick={() => run(() => api.post(`/api/${kind}/${s.id}/release`, { toDate: null }))}>Release {s.section} {s.number}</button>
      {error && <p className="alert">{error}</p>}
    </div>
  )
  const text = (k: keyof typeof f, label: string, max = 60) => (
    <label className="field">{label}<input value={String(f[k] ?? '')} maxLength={max} onChange={e => setF({ ...f, [k]: e.target.value })} /></label>
  )
  return (
    <div className="form" style={{ marginTop: '0.5rem' }}>
      <label className="field">Person
        <select value={f.userId ?? ''} onChange={e => {
          const id = e.target.value ? Number(e.target.value) : null
          setF({ ...f, userId: id, departmentId: people.find(p => p.id === id)?.departmentId ?? f.departmentId })
        }}>
          <option value="">Someone else (type the name)</option>{people.map(p => <option key={p.id} value={p.id}>{p.name}</option>)}
        </select>
      </label>
      {f.userId == null && text('name', 'Name', 200)}
      <label className="field">Department
        <select value={f.departmentId ?? ''} onChange={e => setF({ ...f, departmentId: e.target.value ? Number(e.target.value) : null })}>
          <option value="">-</option>{lookups.departments.filter(d => d.isActive).map(d => <option key={d.id} value={d.id}>{d.name}</option>)}
        </select>
      </label>
      {kind === 'parking' && <>
        {text('vehicleMake', 'Make')}{text('vehicleModel', 'Model')}{text('vehicleYear', 'Year', 10)}{text('vehicleColor', 'Color')}
        {text('plate', 'Plate', 20)}{text('plateState', 'Plate state')}{text('stickerNumber', 'Sticker #', 40)}
      </>}
      {text('notes', 'Notes', 1000)}
      <div><button disabled={f.userId == null && !f.name.trim()} onClick={() => run(() => api.post(`/api/${kind}/${s.id}/assign`, {
        ...Object.fromEntries(Object.entries(f).map(([k, v]) => [k, v === '' ? null : v])), fromDate: null,
      }))}>Assign {s.section} {s.number}</button></div>
      {error && <p className="alert wide">{error}</p>}
    </div>
  )
}

function History({ kind, hotelId }: { kind: Kind; hotelId: number }) {
  const [rows, setRows] = useState<SpaceHistoryRow[] | null>(null)
  const [q, setQ] = useState('')
  const load = useCallback(() => {
    const p = new URLSearchParams({ hotelId: String(hotelId) })
    if (q.trim()) p.set('q', q.trim())
    api.get<SpaceHistoryRow[]>(`/api/${kind}/history?${p}`).then(setRows).catch(() => setRows([]))
  }, [kind, hotelId, q])
  useEffect(() => { load() }, [kind, hotelId]) // eslint-disable-line react-hooks/exhaustive-deps
  return (
    <div className="stack">
      <form className="card row" onSubmit={e => { e.preventDefault(); load() }}>
        <label className="field" style={{ flex: 1 }}>Name, number{kind === 'parking' ? ', plate or sticker' : ''}<input value={q} onChange={e => setQ(e.target.value)} /></label>
        <button type="submit">Search</button>
      </form>
      {!rows ? <p className="muted">Loading...</p> : rows.length === 0 ? <p className="muted">Nothing found.</p> : (
        <div className="table-wrap">
          <table className="grid">
            <thead><tr><th>Space</th><th>Who</th>{kind === 'parking' && <th>Vehicle</th>}<th>From</th><th>To</th><th>By</th></tr></thead>
            <tbody>
              {rows.map(r => (
                <tr key={r.assignmentId}>
                  <td className="nowrap">{r.section} {r.number}</td><td>{r.name}</td>
                  {kind === 'parking' && <td>{[r.vehicle, r.plate, r.stickerNumber && `sticker ${r.stickerNumber}`].filter(Boolean).join(', ')}</td>}
                  <td className="nowrap">{formatLocal(r.fromDate)}</td><td className="nowrap">{r.toDate ? formatLocal(r.toDate) : 'Now'}</td>
                  <td>{r.assignedBy}{r.releasedBy && <div className="muted small">released by {r.releasedBy}</div>}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}

function SpaceList({ kind, hotelId }: { kind: Kind; hotelId: number }) {
  const { spaces, load } = useSpaces(kind, hotelId, true)
  const [n, setN] = useState({ section: SECTIONS[kind][0], number: '', lockCode: '', lockSerial: '', count: '1' })
  const [error, setError] = useState<string | null>(null)
  const save = async (fn: () => Promise<unknown>) => {
    setError(null)
    try { await fn(); load(); return true } catch (e) { setError((e as Error).message); return false }
  }
  const put = (s: Space, patch: Partial<Space>) => save(() => api.put(`/api/${kind}/${s.id}`, { ...s, ...patch }))
  if (!spaces) return <p className="muted">Loading...</p>
  return (
    <div className="stack">
      <p className="muted small">Spaces are never deleted: untick "In use" to retire one. Several numbered spaces can be added at once (e.g. 1 to 40).</p>
      {error && <p className="alert">{error}</p>}
      <div className="card form">
        <label className="field">Section<select value={n.section} onChange={e => setN({ ...n, section: e.target.value })}>{SECTIONS[kind].map(s => <option key={s}>{s}</option>)}</select></label>
        <label className="field">First number<input value={n.number} onChange={e => setN({ ...n, number: e.target.value })} maxLength={40} /></label>
        <label className="field">How many<input type="number" min={1} max={200} value={n.count} onChange={e => setN({ ...n, count: e.target.value })} /></label>
        {kind === 'lockers' && Number(n.count) === 1 && <>
          <label className="field">Lock code<input value={n.lockCode} onChange={e => setN({ ...n, lockCode: e.target.value })} maxLength={40} /></label>
          <label className="field">Lock serial<input value={n.lockSerial} onChange={e => setN({ ...n, lockSerial: e.target.value })} maxLength={60} /></label>
        </>}
        <div><button disabled={!n.number.trim()} onClick={async () => {
          const count = Math.min(200, Math.max(1, Number(n.count) || 1))
          const first = Number(n.number)
          const numbers = count > 1 && Number.isInteger(first) ? Array.from({ length: count }, (_, i) => String(first + i)) : [n.number.trim()]
          for (const number of numbers) {
            if (!await save(() => api.post(`/api/${kind}`, { hotelId, section: n.section, number, lockCode: n.lockCode || null, lockSerial: n.lockSerial || null, notes: null, isActive: true }))) return
          }
          setN({ ...n, number: '', lockCode: '', lockSerial: '', count: '1' })
        }}>Add</button></div>
      </div>
      <div className="table-wrap">
        <table className="grid">
          <thead><tr><th>Space</th>{kind === 'lockers' && <><th>Lock code</th><th>Serial</th></>}<th>Notes</th><th>In use</th></tr></thead>
          <tbody>
            {spaces.map(s => (
              <tr key={s.id} className={s.isActive ? '' : 'off'}>
                <td className="nowrap">{s.section} {s.number}</td>
                {kind === 'lockers' && <>
                  <td><input defaultValue={s.lockCode ?? ''} aria-label="Lock code" onBlur={e => e.target.value !== (s.lockCode ?? '') && put(s, { lockCode: e.target.value || null })} /></td>
                  <td><input defaultValue={s.lockSerial ?? ''} aria-label="Serial" onBlur={e => e.target.value !== (s.lockSerial ?? '') && put(s, { lockSerial: e.target.value || null })} /></td>
                </>}
                <td><input defaultValue={s.notes ?? ''} aria-label="Notes" onBlur={e => e.target.value !== (s.notes ?? '') && put(s, { notes: e.target.value || null })} /></td>
                <td><input type="checkbox" checked={s.isActive} aria-label="In use" onChange={e => put(s, { isActive: e.target.checked })} /></td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  )
}
