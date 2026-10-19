import { useCallback, useEffect, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { api } from '../../api'
import { useSession } from '../../App'
import { formatLocal, nowPacific } from '../../format'
import type { EventPage, Reminder } from '../../types'

const VIEWS: [string, string][] = [['day', 'Day'], ['open', 'Open'], ['mine', 'Mine'], ['search', 'Search']]

// Event log: the day's entries (default today), open entries from any day, or a
// search. Task reminders due now sit on top.
export default function EventLog() {
  const { me, lookups } = useSession()
  const [params, setParams] = useSearchParams()
  const [page, setPage] = useState<EventPage | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [text, setText] = useState(params.get('q') ?? '')

  const access = me.modules.find(m => m.key === 'eventlog')
  const canWrite = access?.level === 'Edit' || access?.level === 'Manage'
  const view = params.get('view') ?? 'day'
  const today = nowPacific().slice(0, 10)
  const day = params.get('day') ?? today
  const qs = params.toString()

  useEffect(() => {
    const p = new URLSearchParams(qs)
    const v = p.get('view') ?? 'day'
    p.delete('view')
    if (v === 'day' && !p.get('day')) p.set('day', today)
    if (v === 'open') { p.delete('day'); p.set('open', 'true') }
    if (v === 'mine') { p.delete('day'); p.set('open', 'true'); p.set('mine', 'true') }
    if (v === 'search' && !p.get('q') && !p.get('from')) { setPage({ total: 0, items: [] }); return }
    setPage(null)
    api.get<EventPage>(`/api/events?${p}`).then(setPage).catch(e => setError(e.message))
  }, [qs, today])

  const set = (patch: Record<string, string | null>) => {
    const p = new URLSearchParams(params)
    for (const [k, v] of Object.entries(patch)) { if (v) p.set(k, v); else p.delete(k) }
    setParams(p)
  }
  const shiftDay = (n: number) => {
    const d = new Date(`${day}T12:00:00`)
    d.setDate(d.getDate() + n)
    set({ day: d.toISOString().slice(0, 10) })
  }

  return (
    <div className="stack">
      <div className="spread">
        <h1>Event log</h1>
        {canWrite && <Link className="button" to="/events/new">Log event</Link>}
      </div>

      <Reminders canWrite={canWrite} />

      <div className="tabs" role="tablist">
        {VIEWS.map(([k, label]) => (
          <button key={k} role="tab" aria-selected={view === k} className={view === k ? 'tab active' : 'tab'}
            onClick={() => setParams(k === 'day' ? {} : { view: k })}>{label}</button>
        ))}
      </div>

      {view === 'day' && (
        <div className="row">
          <button className="secondary" onClick={() => shiftDay(-1)} aria-label="Previous day">&lt;</button>
          <input type="date" value={day} max={today} onChange={e => set({ day: e.target.value || null })} style={{ width: 'auto' }} />
          <button className="secondary" onClick={() => shiftDay(1)} disabled={day >= today} aria-label="Next day">&gt;</button>
          {day !== today && <button className="link" onClick={() => set({ day: null })}>Today</button>}
        </div>
      )}

      {view === 'search' && (
        <form className="card form" onSubmit={e => { e.preventDefault(); set({ q: text.trim() || null }) }}>
          <label className="field">Words, name or room<input value={text} onChange={e => setText(e.target.value)} /></label>
          <label className="field">From<input type="date" value={params.get('from') ?? ''} onChange={e => set({ from: e.target.value || null })} /></label>
          <label className="field">To<input type="date" value={params.get('to') ?? ''} onChange={e => set({ to: e.target.value || null })} /></label>
          <label className="field">Category
            <select value={params.get('categoryId') ?? ''} onChange={e => set({ categoryId: e.target.value || null })}>
              <option value="">All</option>
              {lookups.listItems.filter(i => i.listKey === 'event-category').map(i => <option key={i.id} value={i.id}>{i.name}</option>)}
            </select>
          </label>
          <div className="row"><button type="submit">Search</button></div>
        </form>
      )}

      {error && <p className="alert">{error}</p>}
      {!page ? <p className="muted">Loading...</p> : page.items.length === 0 ? (
        <p className="muted">{view === 'search' ? 'Type something to search for, or choose dates.' : 'Nothing logged.'}</p>
      ) : (
        <div className="report-list">
          {view !== 'day' && <p className="muted small">{page.total} entr{page.total === 1 ? 'y' : 'ies'}</p>}
          {page.items.map(e => (
            <Link key={e.id} to={`/events/${e.id}`} className={`report-row prio-${e.priority}`}>
              <div className="spread">
                <b>{e.categoryName}{e.detail ? `: ${e.detail}` : ''}</b>
                <span className="row">
                  {(e.priority === 'High' || e.priority === 'Highest') && <span className={`pill prio-${e.priority}`}>{e.priority}</span>}
                  <span className={`pill ${e.completedLocal ? 'st-Approved' : 'st-AwaitingReview'}`}>{e.completedLocal ? 'Done' : 'Open'}</span>
                </span>
              </div>
              <div className="muted small">
                {[formatLocal(e.startedLocal), e.hotelCode, [e.areaName, e.locationName].filter(Boolean).join(' - '), e.room && `room ${e.room}`]
                  .filter(Boolean).join(' | ')}
              </div>
              {e.narrative && <div className="small">{e.narrative}</div>}
              <div className="muted small">By {e.createdByName}{e.assignedToName ? `, assigned to ${e.assignedToName}` : ''}</div>
            </Link>
          ))}
        </div>
      )}
    </div>
  )
}

function Reminders({ canWrite }: { canWrite: boolean }) {
  const { me, lookups } = useSession()
  const [list, setList] = useState<Reminder[]>([])
  const [adding, setAdding] = useState(false)
  const [n, setN] = useState({ title: '', due: '', daily: false })
  const [error, setError] = useState<string | null>(null)
  const hotelLimit = me.modules.find(m => m.key === 'eventlog')?.hotelId

  const load = useCallback(() => { api.get<Reminder[]>('/api/events/reminders').then(setList).catch(() => setList([])) }, [])
  useEffect(load, [load])
  const run = async (fn: () => Promise<unknown>) => {
    setError(null)
    try { await fn(); load(); return true } catch (e) { setError((e as Error).message); return false }
  }

  if (list.length === 0 && !canWrite) return null
  return (
    <section className="card stack reminders">
      <div className="spread">
        <h2>Reminders</h2>
        {canWrite && <button className="link" onClick={() => setAdding(a => !a)}>{adding ? 'Close' : 'Add reminder'}</button>}
      </div>
      {list.length === 0 && <p className="muted small">Nothing due.</p>}
      {list.map(r => (
        <div key={r.id} className="spread reminder">
          <span>
            <b className={r.overdue ? 'error' : ''}>{formatLocal(r.dueLocal)}</b> {r.title}
            {r.repeatsDaily && <span className="muted small"> (daily)</span>}
            {r.notes && <span className="muted small"> - {r.notes}</span>}
          </span>
          {canWrite && <span className="row">
            <button onClick={() => run(() => api.post(`/api/events/reminders/${r.id}/done`))}>Done</button>
            <button className="link small" onClick={() => run(() => api.post(`/api/events/reminders/${r.id}/cancel`))}>Cancel</button>
          </span>}
        </div>
      ))}
      {adding && (
        <div className="form">
          <label className="field">What<input value={n.title} onChange={e => setN({ ...n, title: e.target.value })} maxLength={200} placeholder="e.g. Check pool gate" /></label>
          <label className="field">When<input type="datetime-local" value={n.due} onChange={e => setN({ ...n, due: e.target.value })} /></label>
          <label className="field check"><input type="checkbox" checked={n.daily} onChange={e => setN({ ...n, daily: e.target.checked })} /> Every day</label>
          <div><button disabled={!n.title.trim() || !n.due} onClick={async () => {
            const hotelId = hotelLimit ?? me.hotelId ?? lookups.hotels[0].id
            if (await run(() => api.post('/api/events/reminders', { hotelId, title: n.title, dueLocal: n.due, repeatsDaily: n.daily, notes: null, assignedDepartmentId: null }))) {
              setN({ title: '', due: '', daily: false }); setAdding(false)
            }
          }}>Add</button></div>
        </div>
      )}
      {error && <p className="alert">{error}</p>}
    </section>
  )
}
