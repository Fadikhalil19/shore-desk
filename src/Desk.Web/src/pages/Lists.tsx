import { useState } from 'react'
import { api } from '../api'
import { useSession } from '../App'
import type { Area, Department, Hotel, LocationSpot } from '../types'

// Settings > Hotels / Departments / Locations. Nothing is deleted: an entry is
// switched off so it disappears from new forms but old records keep its name.

function useSaver() {
  const { reloadLookups } = useSession()
  const [error, setError] = useState<string | null>(null)
  const [saved, setSaved] = useState(false)
  const run = async (fn: () => Promise<unknown>) => {
    setError(null)
    try { await fn(); await reloadLookups(); setSaved(true); setTimeout(() => setSaved(false), 2000); return true }
    catch (e) { setError((e as Error).message); return false }
  }
  const status = <>{saved && <p className="ok small">Saved</p>}{error && <p className="alert">{error}</p>}</>
  return { run, status }
}

export function Hotels() {
  const { lookups } = useSession()
  const { run, status } = useSaver()
  const [n, setN] = useState({ code: '', name: '', address: '' })
  const put = (h: Hotel, patch: Partial<Hotel>) => run(() => api.put(`/api/admin/lists/hotels/${h.id}`, { ...h, ...patch }))
  return (
    <div className="stack">
      <p className="muted small">The address is printed at the top of incident reports.</p>
      {status}
      <div className="table-wrap">
        <table className="grid">
          <thead><tr><th>Code</th><th>Name</th><th>Address</th><th>In use</th></tr></thead>
          <tbody>
            {lookups.hotels.map(h => (
              <tr key={h.id} className={h.isActive ? '' : 'off'}>
                <td className="nowrap"><b>{h.code}</b></td>
                <td><input defaultValue={h.name} onBlur={e => e.target.value !== h.name && put(h, { name: e.target.value })} aria-label={`${h.code} name`} /></td>
                <td><input defaultValue={h.address ?? ''} onBlur={e => e.target.value !== (h.address ?? '') && put(h, { address: e.target.value })} aria-label={`${h.code} address`} /></td>
                <td><input type="checkbox" checked={h.isActive} onChange={e => put(h, { isActive: e.target.checked })} aria-label={`${h.code} in use`} /></td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <div className="card form">
        <label className="field">New code<input value={n.code} onChange={e => setN({ ...n, code: e.target.value })} maxLength={10} /></label>
        <label className="field">Name<input value={n.name} onChange={e => setN({ ...n, name: e.target.value })} /></label>
        <label className="field">Address<input value={n.address} onChange={e => setN({ ...n, address: e.target.value })} /></label>
        <div><button disabled={!n.code || !n.name} onClick={async () => {
          if (await run(() => api.post('/api/admin/lists/hotels', { ...n, isActive: true }))) setN({ code: '', name: '', address: '' })
        }}>Add hotel</button></div>
      </div>
    </div>
  )
}

export function Departments() {
  const { lookups } = useSession()
  const { run, status } = useSaver()
  const [name, setName] = useState('')
  const put = (d: Department, patch: Partial<Department>) => run(() => api.put(`/api/admin/lists/departments/${d.id}`, { ...d, ...patch }))
  return (
    <div className="stack">
      <p className="muted small">
        Each person belongs to one department, which sets their default access. Hotel-specific names from MSShift
        (e.g. "SHO-Engineering") became one department each; the hotel is set on the person instead.
      </p>
      {status}
      <div className="table-wrap">
        <table className="grid">
          <thead><tr><th>Department</th><th>In use</th></tr></thead>
          <tbody>
            {lookups.departments.map(d => (
              <tr key={d.id} className={d.isActive ? '' : 'off'}>
                <td><input defaultValue={d.name} onBlur={e => e.target.value !== d.name && put(d, { name: e.target.value })} aria-label="Department name" /></td>
                <td><input type="checkbox" checked={d.isActive} onChange={e => put(d, { isActive: e.target.checked })} aria-label={`${d.name} in use`} /></td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <div className="card row">
        <input value={name} onChange={e => setName(e.target.value)} placeholder="New department" style={{ flex: '1 1 14rem' }} />
        <button disabled={!name.trim()} onClick={async () => { if (await run(() => api.post('/api/admin/lists/departments', { name, isActive: true }))) setName('') }}>Add</button>
      </div>
    </div>
  )
}

export function Areas() {
  const { lookups } = useSession()
  const { run, status } = useSaver()
  const [hotel, setHotel] = useState<string>('all')
  const [openId, setOpenId] = useState<number | null>(null)
  const [n, setN] = useState({ name: '', hotelId: '' })
  const hotelCode = (id: number | null) => id === null ? 'Every hotel' : lookups.hotels.find(h => h.id === id)?.code
  const shown = lookups.areas.filter(a => hotel === 'all' || (hotel === 'any' ? a.hotelId === null : a.hotelId === Number(hotel)))
  const putArea = (a: Area, patch: Partial<Area>) => run(() => api.put(`/api/admin/lists/areas/${a.id}`, { ...a, ...patch }))

  return (
    <div className="stack">
      <p className="muted small">
        Two levels, as in MSShift: an <b>area</b> (e.g. "A Building 1st Floor") and the <b>spots</b> inside it (e.g. "Room 101").
        The spots were not copied from MSShift yet; add them here or they come with the MSShift import.
      </p>
      {status}
      <label className="field" style={{ maxWidth: '20rem' }}>Show
        <select value={hotel} onChange={e => setHotel(e.target.value)}>
          <option value="all">All areas</option>
          <option value="any">Areas for every hotel</option>
          {lookups.hotels.map(h => <option key={h.id} value={h.id}>{h.code} areas</option>)}
        </select>
      </label>
      <div className="people">
        {shown.map(a => (
          <div key={a.id} className="card">
            <div className="spread">
              <button className="link" onClick={() => setOpenId(openId === a.id ? null : a.id)}>
                <b>{a.name}</b>
              </button>
              <span className="row small">
                <span className="pill">{hotelCode(a.hotelId)}</span>
                <span className="muted">{a.locations.length} spots</span>
                {!a.isActive && <span className="pill st-Warning">Off</span>}
              </span>
            </div>
            {openId === a.id && <AreaEditor a={a} onSave={patch => putArea(a, patch)} run={run} />}
          </div>
        ))}
      </div>
      <div className="card form">
        <label className="field">New area<input value={n.name} onChange={e => setN({ ...n, name: e.target.value })} /></label>
        <label className="field">Hotel
          <select value={n.hotelId} onChange={e => setN({ ...n, hotelId: e.target.value })}>
            <option value="">Every hotel</option>
            {lookups.hotels.filter(h => h.isActive).map(h => <option key={h.id} value={h.id}>{h.code}</option>)}
          </select>
        </label>
        <div><button disabled={!n.name.trim()} onClick={async () => {
          const sortOrder = Math.max(0, ...lookups.areas.map(a => a.sortOrder)) + 10
          if (await run(() => api.post('/api/admin/lists/areas', { name: n.name, hotelId: n.hotelId ? Number(n.hotelId) : null, sortOrder, isActive: true })))
            setN({ name: '', hotelId: '' })
        }}>Add area</button></div>
      </div>
    </div>
  )
}

function AreaEditor({ a, onSave, run }: { a: Area; onSave: (p: Partial<Area>) => void; run: (fn: () => Promise<unknown>) => Promise<boolean> }) {
  const { lookups } = useSession()
  const [spot, setSpot] = useState('')
  const putSpot = (l: LocationSpot, patch: Partial<LocationSpot>) => run(() => api.put(`/api/admin/lists/locations/${l.id}`, { ...l, ...patch }))
  return (
    <div className="stack" style={{ marginTop: '0.6rem' }}>
      <div className="form">
        <label className="field">Name<input defaultValue={a.name} onBlur={e => e.target.value !== a.name && onSave({ name: e.target.value })} /></label>
        <label className="field">Hotel
          <select value={a.hotelId ?? ''} onChange={e => onSave({ hotelId: e.target.value ? Number(e.target.value) : null })}>
            <option value="">Every hotel</option>
            {lookups.hotels.map(h => <option key={h.id} value={h.id}>{h.code}</option>)}
          </select>
        </label>
        <label className="row small"><input type="checkbox" checked={a.isActive} onChange={e => onSave({ isActive: e.target.checked })} /> In use</label>
      </div>
      <div className="table-wrap">
        <table className="grid">
          <thead><tr><th>Spot</th><th>In use</th></tr></thead>
          <tbody>
            {a.locations.map(l => (
              <tr key={l.id} className={l.isActive ? '' : 'off'}>
                <td><input defaultValue={l.name} onBlur={e => e.target.value !== l.name && putSpot(l, { name: e.target.value })} aria-label="Spot name" /></td>
                <td><input type="checkbox" checked={l.isActive} onChange={e => putSpot(l, { isActive: e.target.checked })} aria-label={`${l.name} in use`} /></td>
              </tr>
            ))}
            {a.locations.length === 0 && <tr><td colSpan={2} className="muted">No spots yet.</td></tr>}
          </tbody>
        </table>
      </div>
      <div className="row">
        <input value={spot} onChange={e => setSpot(e.target.value)} placeholder="New spot, e.g. Room 101" style={{ flex: '1 1 14rem' }} />
        <button disabled={!spot.trim()} onClick={async () => {
          const sortOrder = Math.max(0, ...a.locations.map(l => l.sortOrder)) + 10
          if (await run(() => api.post(`/api/admin/lists/areas/${a.id}/locations`, { name: spot, sortOrder, isActive: true }))) setSpot('')
        }}>Add spot</button>
      </div>
    </div>
  )
}
