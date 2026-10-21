import { useCallback, useEffect, useRef, useState } from 'react'
import { Link, useParams, useSearchParams } from 'react-router-dom'
import { api } from '../api'
import { useSession } from '../App'
import { formatLocal } from '../format'
import type { Checkpoint, CheckpointScan, PatrolRound, PatrolRoute, ScanResult } from '../types'

const TAG_BASE = `${window.location.origin}/p/`

function usePatrol() {
  const { me, lookups } = useSession()
  const [params] = useSearchParams()
  const access = me.modules.find(m => m.key === 'patrol')
  const level = access?.level ?? 'None'
  return {
    hotelId: Number(params.get('hotelId') ?? access?.hotelId ?? me.hotelId ?? lookups.hotels[0].id),
    limit: access?.hotelId, canEdit: level === 'Edit' || level === 'Manage', isManager: level === 'Manage',
  }
}

// Patrol: my round (start / progress / finish), rounds, problems, and (managers) set-up.
export default function Patrol() {
  const { lookups } = useSession()
  const [params, setParams] = useSearchParams()
  const { hotelId, limit, canEdit, isManager } = usePatrol()
  const tab = params.get('tab') ?? (canEdit ? 'mine' : 'rounds')
  const tabs: [string, string][] = [...(canEdit ? [['mine', 'My round'] as [string, string]] : []), ['rounds', 'Rounds'], ['problems', 'Problems'],
    ...(isManager ? [['setup', 'Checkpoints and routes'] as [string, string]] : [])]
  return (
    <div className="stack">
      <div className="spread">
        <h1>Patrol</h1>
        {limit == null && lookups.hotels.length > 1 && (
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
      {tab === 'mine' && canEdit && <MyRound hotelId={hotelId} />}
      {tab === 'rounds' && <Rounds hotelId={hotelId} />}
      {tab === 'problems' && <Problems hotelId={hotelId} />}
      {tab === 'setup' && isManager && <Setup hotelId={hotelId} />}
    </div>
  )
}

function Progress({ r }: { r: PatrolRound }) {
  const done = new Set((r.scans ?? []).map(s => s.checkpointId))
  return (
    <ol className="patrol-stops">
      {(r.route ?? []).map(s => (
        <li key={s.checkpointId} className={done.has(s.checkpointId) ? 'done' : ''}>
          <span>{done.has(s.checkpointId) ? 'Done' : 'To do'}</span> {s.name}
          {s.instructions && <span className="muted small"> - {s.instructions}</span>}
        </li>
      ))}
    </ol>
  )
}

function MyRound({ hotelId }: { hotelId: number }) {
  const [round, setRound] = useState<PatrolRound | null | undefined>(undefined)
  const [routes, setRoutes] = useState<PatrolRoute[]>([])
  const [checkpoints, setCheckpoints] = useState<Checkpoint[]>([])
  const [notes, setNotes] = useState('')
  const [error, setError] = useState<string | null>(null)
  const load = useCallback(() => { api.get<PatrolRound | undefined>('/api/patrol/rounds/mine').then(r => setRound(r ?? null)).catch(e => setError(e.message)) }, [])
  useEffect(load, [load])
  useEffect(() => {
    api.get<PatrolRoute[]>(`/api/patrol/routes?hotelId=${hotelId}`).then(setRoutes).catch(() => setRoutes([]))
    api.get<Checkpoint[]>(`/api/patrol/checkpoints?hotelId=${hotelId}`).then(setCheckpoints).catch(() => setCheckpoints([]))
  }, [hotelId])
  const run = async (fn: () => Promise<unknown>) => {
    setError(null)
    try { await fn(); load() } catch (e) { setError((e as Error).message) }
  }
  if (round === undefined) return <p className="muted">Loading...</p>

  if (!round) return (
    <section className="card stack">
      <p>Start a round, then tap your phone on each checkpoint tag. (iPhone: the tag opens a link; Android: turn NFC on.)</p>
      {routes.length === 0 ? <p className="muted">No routes yet. A manager sets them up.</p> : routes.map(r => (
        <div key={r.id} className="spread">
          <span><b>{r.name}</b> <span className="muted small">{r.stops.length} checkpoints{r.expectedMinutes ? `, about ${r.expectedMinutes} min` : ''}</span></span>
          <button onClick={() => run(() => api.post('/api/patrol/rounds', { routeId: r.id }))}>Start</button>
        </div>
      ))}
      {error && <p className="alert">{error}</p>}
    </section>
  )

  return (
    <section className="card stack">
      <div className="spread"><h2>{round.routeName}</h2><span className="pill st-AwaitingReview">{round.scanned} of {round.stops}</span></div>
      <p className="muted small">Started {formatLocal(round.startedLocal)}</p>
      <Progress r={round} />
      <details>
        <summary>A tag is missing or will not scan</summary>
        <p className="muted small">Confirm the checkpoint by hand. It is recorded as a manual check, so say what is wrong with the tag.</p>
        {checkpoints.filter(c => (round.route ?? []).some(s => s.checkpointId === c.id)).map(c => (
          <button key={c.id} className="link small" style={{ display: 'block' }} onClick={() => {
            const note = window.prompt(`What is wrong with the tag at ${c.name}?`)
            if (note) run(() => api.post('/api/patrol/scan', { code: null, checkpointId: c.id, manual: true, problem: true, note }))
          }}>{c.name}</button>
        ))}
      </details>
      <label className="field">Notes<input value={notes} onChange={e => setNotes(e.target.value)} maxLength={1000} /></label>
      <div className="row">
        <button onClick={() => run(() => api.post(`/api/patrol/rounds/${round.id}/end`, { notes: notes || null }))}>Finish round</button>
        <button className="link" onClick={() => run(() => api.post(`/api/patrol/rounds/${round.id}/abandon`, { notes: notes || null }))}>Abandon</button>
      </div>
      {error && <p className="alert">{error}</p>}
    </section>
  )
}

function Rounds({ hotelId }: { hotelId: number }) {
  const [rows, setRows] = useState<PatrolRound[] | null>(null)
  const [open, setOpen] = useState<PatrolRound | null>(null)
  useEffect(() => { api.get<PatrolRound[]>(`/api/patrol/rounds?hotelId=${hotelId}`).then(setRows).catch(() => setRows([])) }, [hotelId])
  if (!rows) return <p className="muted">Loading...</p>
  if (rows.length === 0) return <p className="muted">No rounds in the last 7 days.</p>
  return (
    <div className="report-list">
      {rows.map(r => (
        <div key={r.id} className={`report-row ${r.missed > 0 && r.status !== 'InProgress' ? 'prio-High' : ''}`}>
          <div className="spread" role="button" tabIndex={0} style={{ cursor: 'pointer' }}
            onClick={() => open?.id === r.id ? setOpen(null) : api.get<PatrolRound>(`/api/patrol/rounds/${r.id}`).then(setOpen)}>
            <b>{r.routeName} - {r.officer}</b>
            <span className={`pill ${r.status === 'InProgress' ? 'st-AwaitingReview' : r.missed ? 'st-Voided' : 'st-Approved'}`}>
              {r.status === 'InProgress' ? 'In progress' : r.status === 'Abandoned' ? 'Abandoned' : r.missed ? `${r.missed} missed` : 'Complete'}</span>
          </div>
          <div className="muted small">{formatLocal(r.startedLocal)}{r.endedLocal ? ` - ${formatLocal(r.endedLocal).split(', ').pop()}` : ''} | {r.scanned} of {r.stops} checkpoints</div>
          {open?.id === r.id && (
            <div className="stack" style={{ marginTop: '0.5rem' }}>
              {open.scans!.map(s => <p key={s.id} className="small">{formatLocal(s.scannedLocal).split(', ').pop()} {s.checkpointName}
                {s.manual && <span className="warn"> (by hand)</span>}{s.problem && <span className="error"> problem: {s.note}</span>}</p>)}
              {open.missedStops!.length > 0 && <p className="warn small">Missed: {open.missedStops!.map(s => s.name).join(', ')}</p>}
              {open.notes && <p className="small">{open.notes}</p>}
            </div>
          )}
        </div>
      ))}
    </div>
  )
}

function Problems({ hotelId }: { hotelId: number }) {
  const [rows, setRows] = useState<CheckpointScan[] | null>(null)
  useEffect(() => { api.get<CheckpointScan[]>(`/api/patrol/problems?hotelId=${hotelId}`).then(setRows).catch(() => setRows([])) }, [hotelId])
  if (!rows) return <p className="muted">Loading...</p>
  if (rows.length === 0) return <p className="muted">No problems reported in the last 30 days.</p>
  return (
    <div className="table-wrap">
      <table className="grid">
        <thead><tr><th>When</th><th>Checkpoint</th><th>Problem</th><th>Officer</th></tr></thead>
        <tbody>{rows.map(s => <tr key={s.id}><td className="nowrap">{formatLocal(s.scannedLocal)}</td><td>{s.checkpointName}</td>
          <td>{s.note}{s.manual && <span className="muted small"> (checked by hand)</span>}</td><td>{s.officer}</td></tr>)}</tbody>
      </table>
    </div>
  )
}

function Setup({ hotelId }: { hotelId: number }) {
  const { lookups } = useSession()
  const [cps, setCps] = useState<Checkpoint[] | null>(null)
  const [routes, setRoutes] = useState<PatrolRoute[]>([])
  const [n, setN] = useState({ name: '', areaId: null as number | null, instructions: '' })
  const [route, setRoute] = useState<{ id: number | null; name: string; expectedMinutes: string; ids: number[] }>({ id: null, name: '', expectedMinutes: '30', ids: [] })
  const [error, setError] = useState<string | null>(null)
  const load = useCallback(() => {
    api.get<Checkpoint[]>(`/api/patrol/checkpoints?hotelId=${hotelId}&all=true`).then(setCps).catch(e => setError(e.message))
    api.get<PatrolRoute[]>(`/api/patrol/routes?hotelId=${hotelId}&all=true`).then(setRoutes).catch(() => setRoutes([]))
  }, [hotelId])
  useEffect(load, [load])
  const run = async (fn: () => Promise<unknown>) => {
    setError(null)
    try { await fn(); load(); return true } catch (e) { setError((e as Error).message); return false }
  }
  if (!cps) return <p className="muted">Loading...</p>
  const areas = lookups.areas.filter(a => a.isActive && (a.hotelId == null || a.hotelId === hotelId))
  return (
    <div className="stack">
      {error && <p className="alert">{error}</p>}
      <section className="card block">
        <h2>Checkpoints</h2>
        <p className="muted small">Write each link onto an NFC tag (for example with the free "NFC Tools" app: Write, Add a record, URL) and stick it at the spot.
          If a tag is lost or copied, give the checkpoint a new code and write a new tag.</p>
        <div className="table-wrap">
          <table className="grid">
            <thead><tr><th>Checkpoint</th><th>Tag link</th><th>What to check</th><th>In use</th></tr></thead>
            <tbody>
              {cps.map(c => (
                <tr key={c.id} className={c.isActive ? '' : 'off'}>
                  <td>{c.name}{c.areaName && <div className="muted small">{c.areaName}</div>}</td>
                  <td className="details">{TAG_BASE}{c.code} <button className="link small" onClick={() => navigator.clipboard?.writeText(`${TAG_BASE}${c.code}`)}>Copy</button>
                    {' '}<button className="link small" onClick={() => window.confirm(`Give ${c.name} a new code? The old tag stops working.`) && run(() => api.post(`/api/patrol/checkpoints/${c.id}/newcode`))}>New code</button></td>
                  <td>{c.instructions}</td>
                  <td><input type="checkbox" checked={c.isActive} aria-label="In use" onChange={e => run(() => api.put(`/api/patrol/checkpoints/${c.id}`, { hotelId, name: c.name, areaId: c.areaId, instructions: c.instructions, isActive: e.target.checked }))} /></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        <div className="form" style={{ marginTop: '0.75rem' }}>
          <label className="field">Name<input value={n.name} onChange={e => setN({ ...n, name: e.target.value })} maxLength={100} placeholder="e.g. Pool gate" /></label>
          <label className="field">Location<select value={n.areaId ?? ''} onChange={e => setN({ ...n, areaId: e.target.value ? Number(e.target.value) : null })}>
            <option value="">-</option>{areas.map(a => <option key={a.id} value={a.id}>{a.name}</option>)}</select></label>
          <label className="field">What to check<input value={n.instructions} onChange={e => setN({ ...n, instructions: e.target.value })} maxLength={400} /></label>
          <div><button disabled={!n.name.trim()} onClick={async () => {
            if (await run(() => api.post('/api/patrol/checkpoints', { hotelId, name: n.name, areaId: n.areaId, instructions: n.instructions || null, isActive: true })))
              setN({ name: '', areaId: null, instructions: '' })
          }}>Add checkpoint</button></div>
        </div>
      </section>

      <section className="card block">
        <h2>Routes</h2>
        {routes.map(r => (
          <p key={r.id} className={r.isActive ? '' : 'muted'}><b>{r.name}</b> <span className="small">{r.stops.map(s => s.name).join(' > ')}</span>
            {' '}<button className="link small" onClick={() => setRoute({ id: r.id, name: r.name, expectedMinutes: String(r.expectedMinutes), ids: r.stops.map(s => s.checkpointId) })}>Change</button></p>
        ))}
        <div className="form">
          <label className="field">Route name<input value={route.name} onChange={e => setRoute({ ...route, name: e.target.value })} maxLength={100} /></label>
          <label className="field">Expected minutes<input type="number" min={0} value={route.expectedMinutes} onChange={e => setRoute({ ...route, expectedMinutes: e.target.value })} /></label>
          <div className="wide">
            <p className="small">Checkpoints in order (tap to add or remove):</p>
            <div className="row">{cps.filter(c => c.isActive).map(c => {
              const at = route.ids.indexOf(c.id)
              return <button key={c.id} className={at >= 0 ? 'tab active' : 'tab'}
                onClick={() => setRoute({ ...route, ids: at >= 0 ? route.ids.filter(x => x !== c.id) : [...route.ids, c.id] })}>{at >= 0 ? `${at + 1}. ` : ''}{c.name}</button>
            })}</div>
          </div>
          <div className="row">
            <button disabled={!route.name.trim() || route.ids.length === 0} onClick={async () => {
              const body = { hotelId, name: route.name, expectedMinutes: Number(route.expectedMinutes) || 0, checkpointIds: route.ids, isActive: true }
              if (await run(() => route.id ? api.put(`/api/patrol/routes/${route.id}`, body) : api.post('/api/patrol/routes', body)))
                setRoute({ id: null, name: '', expectedMinutes: '30', ids: [] })
            }}>{route.id ? 'Save route' : 'Add route'}</button>
            {route.id && <button className="secondary" onClick={() => setRoute({ id: null, name: '', expectedMinutes: '30', ids: [] })}>Cancel</button>}
          </div>
        </div>
      </section>
    </div>
  )
}

// /p/<code>: what a checkpoint tag opens on the officer's phone. Records the visit at once.
export function TagScan() {
  const { code } = useParams()
  const [result, setResult] = useState<ScanResult | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [problem, setProblem] = useState('')
  const [reported, setReported] = useState(false)
  const sent = useRef(false)
  useEffect(() => {
    if (sent.current) return
    sent.current = true
    api.post<ScanResult>('/api/patrol/scan', { code, checkpointId: null, manual: false, problem: false, note: null }).then(setResult).catch(e => setError(e.message))
  }, [code])

  if (error) return <div className="stack tag-scan"><p className="alert">{error}</p><Link to="/patrol">Patrol</Link></div>
  if (!result) return <p className="muted">Recording checkpoint...</p>
  return (
    <div className="stack tag-scan">
      <div className="card tag-ok">
        <p className="small">Checkpoint recorded {formatLocal(result.scannedLocal).split(', ').pop()}</p>
        <h1>{result.checkpoint}</h1>
        {result.instructions && <p>{result.instructions}</p>}
        {result.round ? (
          <p><b>{result.round.scanned} of {result.round.stops}</b> on {result.round.routeName}{result.next ? `. Next: ${result.next}` : '. All checkpoints done - finish the round.'}</p>
        ) : <p className="muted small">Not part of a round (start one under Patrol to track a route).</p>}
      </div>
      {!reported ? (
        <div className="card form">
          <label className="field wide">Anything wrong here?<input value={problem} onChange={e => setProblem(e.target.value)} maxLength={1000} placeholder="e.g. door propped open" /></label>
          <div><button className="secondary" disabled={!problem.trim()} onClick={async () => {
            try { await api.post('/api/patrol/scan', { code, checkpointId: null, manual: false, problem: true, note: problem }); setReported(true) }
            catch (e) { setError((e as Error).message) }
          }}>Report problem</button></div>
        </div>
      ) : <p className="notice">Problem reported.</p>}
      <Link className="button" to="/patrol">My round</Link>
    </div>
  )
}
