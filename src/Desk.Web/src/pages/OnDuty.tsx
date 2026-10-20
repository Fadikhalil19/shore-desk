import { useCallback, useEffect, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { api } from '../api'
import { useSession } from '../App'
import { formatLocal, nowPacific } from '../format'
import { HOURS, addDays, nextShift, shiftTimes } from '../shifts'
import type { CallOut, Person, Roster, Shift } from '../types'

const REASONS = ['Sick', 'Family emergency', 'Personal', 'Transportation', 'No call / no show', 'Other']

const time = (local: string) => formatLocal(local).split(', ').slice(-1)[0]

// On duty: who is on a shift (default: the current one) and who called out.
// The call-out log tab lists the last 30 days.
export default function OnDuty() {
  const { me, lookups } = useSession()
  const [params, setParams] = useSearchParams()
  const [r, setR] = useState<Roster | null>(null)
  const [people, setPeople] = useState<Person[]>([])
  const [error, setError] = useState<string | null>(null)
  const [msg, setMsg] = useState<string | null>(null)

  const limit = me.modules.find(m => m.key === 'onduty')?.hotelId
  const hotelId = Number(params.get('hotelId') ?? limit ?? me.hotelId ?? lookups.hotels[0].id)
  const tab = params.get('tab') ?? 'roster'
  const qs = params.toString()

  useEffect(() => { api.get<Person[]>('/api/people').then(setPeople).catch(() => setPeople([])) }, [])
  const load = useCallback(() => {
    const p = new URLSearchParams(qs)
    p.delete('tab')
    p.set('hotelId', String(hotelId))
    api.get<Roster>(`/api/onduty?${p}`).then(setR).catch(e => setError(e.message))
  }, [qs, hotelId])
  useEffect(load, [load])

  const run = async (fn: () => Promise<unknown>, done?: string) => {
    setError(null); setMsg(null)
    try { await fn(); if (done) setMsg(done); load(); return true } catch (e) { setError((e as Error).message); return false }
  }
  const go = (date: string, shift: Shift) => setParams({ ...(params.get('hotelId') ? { hotelId: String(hotelId) } : {}), date, shift })

  if (error && !r) return <p className="alert">{error}</p>
  if (!r) return <p className="muted">Loading...</p>
  const isCurrent = r.shiftDate === r.currentDate && r.shift === r.currentShift
  const [nextDate, next] = nextShift(r.shiftDate, r.shift)
  const dept = (id: number | null) => lookups.departments.find(d => d.id === id)?.name

  return (
    <div className="stack report">
      <div className="spread">
        <h1>On duty</h1>
        <button className="secondary no-print" onClick={() => window.print()}>Print</button>
      </div>
      <div className="tabs no-print" role="tablist">
        {[['roster', 'Shift'], ['log', 'Call-out log']].map(([k, label]) => (
          <button key={k} role="tab" aria-selected={tab === k} className={tab === k ? 'tab active' : 'tab'}
            onClick={() => setParams(k === 'roster' ? {} : { tab: k })}>{label}</button>
        ))}
      </div>

      {tab === 'log' ? <CallOutLog hotelId={limit ?? null} /> : <>
        <h2>{r.shift}, {formatLocal(r.shiftDate)} <span className="muted small">{HOURS[r.shift]}{isCurrent ? ' (current shift)' : ''}</span></h2>
        <div className="row no-print">
          {limit == null && lookups.hotels.length > 1 && (
            <select value={hotelId} style={{ width: 'auto' }} onChange={e => setParams({ ...Object.fromEntries(params), hotelId: e.target.value })}>
              {lookups.hotels.filter(x => x.isActive).map(x => <option key={x.id} value={x.id}>{x.code}</option>)}
            </select>
          )}
          <button className="secondary" onClick={() => go(r.previousDate, r.previousShift)}>&lt; Previous shift</button>
          <button className="secondary" onClick={() => go(nextDate, next)}>Next shift &gt;</button>
          {!isCurrent && <button className="link" onClick={() => go(r.currentDate, r.currentShift)}>Current shift</button>}
        </div>
        {msg && <p className="notice">{msg}</p>}
        {error && <p className="alert">{error}</p>}

        <section className="card block">
          <div className="spread">
            <h2>Roster ({r.roster.length})</h2>
            {r.canWrite && <button className="link no-print" onClick={() => run(() => api.post('/api/onduty/copy', {
              hotelId: r.hotelId, shiftDate: r.shiftDate, shift: r.shift, fromDate: addDays(r.shiftDate, -1), fromShift: r.shift,
            }), 'Copied from yesterday.')}>Copy yesterday's {r.shift.toLowerCase()} roster</button>}
          </div>
          {r.roster.length === 0 ? <p className="muted small">Nobody on the roster.</p> : (
            <div className="table-wrap">
              <table className="grid">
                <thead><tr><th>Name</th><th>Radio</th><th>Post</th><th>From</th><th>To</th>{r.canWrite && <th className="no-print"></th>}</tr></thead>
                <tbody>
                  {r.roster.map(a => (
                    <tr key={a.id}>
                      <td>{a.name}{a.departmentId && <span className="muted small"> {dept(a.departmentId)}</span>}</td>
                      <td>{a.radio}</td><td>{a.post}</td>
                      <td className="nowrap">{time(a.startLocal)}</td><td className="nowrap">{time(a.endLocal)}</td>
                      {r.canWrite && <td className="no-print"><button className="link small" onClick={() => run(() => api.post(`/api/onduty/assignments/${a.id}/remove`))}>Remove</button></td>}
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
          {r.canWrite && <AddToRoster r={r} people={people} run={run} />}
        </section>

        <section className="card block">
          <h2>Call-outs ({r.callOuts.length})</h2>
          {r.callOuts.map(o => (
            <p key={o.id}><b>{o.name}</b> <span className="muted small">- {o.reason}, called {formatLocal(o.calledLocal)} (taken by {o.takenBy})
              {o.coveredBy ? `, covered by ${o.coveredBy}` : ', not covered'}{o.notes ? `. ${o.notes}` : ''}</span>
              {r.canWrite && <> <button className="link small no-print" onClick={() => run(() => api.post(`/api/onduty/callouts/${o.id}/remove`))}>Remove</button></>}
            </p>
          ))}
          {r.canWrite && <AddCallOut r={r} people={people} run={run} />}
        </section>
      </>}
    </div>
  )
}

type Run = (fn: () => Promise<unknown>, done?: string) => Promise<boolean>

function PersonFields({ people, userId, name, setUser, setName }: {
  people: Person[]; userId: number | null; name: string; setUser: (id: number | null) => void; setName: (n: string) => void
}) {
  return <>
    <label className="field">Person
      <select value={userId ?? ''} onChange={e => setUser(e.target.value ? Number(e.target.value) : null)}>
        <option value="">Someone else (type the name)</option>
        {people.map(p => <option key={p.id} value={p.id}>{p.name}</option>)}
      </select>
    </label>
    {userId == null && <label className="field">Name<input value={name} onChange={e => setName(e.target.value)} maxLength={200} /></label>}
  </>
}

function AddToRoster({ r, people, run }: { r: Roster; people: Person[]; run: Run }) {
  const [start, end] = shiftTimes(r.shiftDate, r.shift)
  const empty = { userId: null as number | null, name: '', radio: '', post: '', start, end }
  const [n, setN] = useState(empty)
  const [key, setKey] = useState(`${r.shiftDate}${r.shift}`)
  if (key !== `${r.shiftDate}${r.shift}`) { setKey(`${r.shiftDate}${r.shift}`); setN(empty) }
  return (
    <div className="form no-print add-row">
      <PersonFields people={people} userId={n.userId} name={n.name} setUser={id => setN({ ...n, userId: id })} setName={v => setN({ ...n, name: v })} />
      <label className="field">Radio #<input value={n.radio} onChange={e => setN({ ...n, radio: e.target.value })} maxLength={40} /></label>
      <label className="field">Post<input value={n.post} onChange={e => setN({ ...n, post: e.target.value })} maxLength={100} placeholder="e.g. Lobby, Patrol, Garage" /></label>
      <label className="field">From<input type="datetime-local" value={n.start} onChange={e => setN({ ...n, start: e.target.value })} /></label>
      <label className="field">To<input type="datetime-local" value={n.end} onChange={e => setN({ ...n, end: e.target.value })} /></label>
      <div><button disabled={n.userId == null && !n.name.trim()} onClick={async () => {
        const p = people.find(x => x.id === n.userId)
        if (await run(() => api.post('/api/onduty/assignments', {
          hotelId: r.hotelId, shiftDate: r.shiftDate, shift: r.shift, userId: n.userId, name: n.name || null,
          departmentId: p?.departmentId ?? null, radio: n.radio || null, post: n.post || null, startLocal: n.start, endLocal: n.end,
        }))) setN(empty)
      }}>Add to roster</button></div>
    </div>
  )
}

function AddCallOut({ r, people, run }: { r: Roster; people: Person[]; run: Run }) {
  const empty = { userId: null as number | null, name: '', called: nowPacific(), reason: '', coveredBy: '', notes: '' }
  const [n, setN] = useState(empty)
  const [open, setOpen] = useState(false)
  if (!open) return <p className="no-print"><button className="secondary" onClick={() => setOpen(true)}>Log a call-out</button></p>
  return (
    <div className="form no-print add-row">
      <PersonFields people={people} userId={n.userId} name={n.name} setUser={id => setN({ ...n, userId: id })} setName={v => setN({ ...n, name: v })} />
      <label className="field">Called at<input type="datetime-local" value={n.called} onChange={e => setN({ ...n, called: e.target.value })} /></label>
      <label className="field">Reason
        <select value={n.reason} onChange={e => setN({ ...n, reason: e.target.value })}>
          <option value="">-</option>{REASONS.map(x => <option key={x}>{x}</option>)}
        </select>
      </label>
      <label className="field">Covered by<input value={n.coveredBy} onChange={e => setN({ ...n, coveredBy: e.target.value })} maxLength={200} /></label>
      <label className="field wide">Notes (no medical details)<input value={n.notes} onChange={e => setN({ ...n, notes: e.target.value })} maxLength={1000} /></label>
      <div className="row">
        <button disabled={(n.userId == null && !n.name.trim()) || !n.reason} onClick={async () => {
          const p = people.find(x => x.id === n.userId)
          if (await run(() => api.post('/api/onduty/callouts', {
            hotelId: r.hotelId, shiftDate: r.shiftDate, shift: r.shift, userId: n.userId, name: n.name || null,
            departmentId: p?.departmentId ?? null, calledLocal: n.called, reason: n.reason, notes: n.notes || null, coveredBy: n.coveredBy || null,
          }))) { setN(empty); setOpen(false) }
        }}>Save call-out</button>
        <button className="secondary" onClick={() => setOpen(false)}>Cancel</button>
      </div>
    </div>
  )
}

function CallOutLog({ hotelId }: { hotelId: number | null }) {
  const [list, setList] = useState<CallOut[] | null>(null)
  const [q, setQ] = useState('')
  const [from, setFrom] = useState(addDays(nowPacific().slice(0, 10), -30))
  const load = useCallback(() => {
    const p = new URLSearchParams({ from })
    if (q.trim()) p.set('q', q.trim())
    if (hotelId) p.set('hotelId', String(hotelId))
    api.get<CallOut[]>(`/api/onduty/callouts?${p}`).then(setList).catch(() => setList([]))
  }, [q, from, hotelId])
  useEffect(load, [from]) // eslint-disable-line react-hooks/exhaustive-deps

  return (
    <div className="stack">
      <form className="card form no-print" onSubmit={e => { e.preventDefault(); load() }}>
        <label className="field">Name<input value={q} onChange={e => setQ(e.target.value)} /></label>
        <label className="field">Since<input type="date" value={from} onChange={e => setFrom(e.target.value)} /></label>
        <div><button type="submit">Search</button></div>
      </form>
      {!list ? <p className="muted">Loading...</p> : list.length === 0 ? <p className="muted">No call-outs.</p> : (
        <div className="table-wrap">
          <table className="grid">
            <thead><tr><th>Shift</th><th>Name</th><th>Reason</th><th>Called</th><th>Covered by</th><th>Taken by</th></tr></thead>
            <tbody>
              {list.map(o => (
                <tr key={o.id}><td className="nowrap">{formatLocal(o.shiftDate)} {o.shift}</td><td>{o.name}</td><td>{o.reason}</td>
                  <td className="nowrap">{formatLocal(o.calledLocal)}</td><td>{o.coveredBy}</td><td>{o.takenBy}</td></tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}
