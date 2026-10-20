import { useCallback, useEffect, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { api } from '../api'
import { useSession } from '../App'
import { formatDateTime, formatLocal } from '../format'
import type { Handover as HandoverData, HandoverEvent, Shift } from '../types'
import { STATUS, formInfo } from './incidents/meta'
import { HOURS, nextShift } from '../shifts'

// Handover for one hotel and shift (default: the current shift). Notes are written
// for the next shift; flagged incidents/events of the shift and open events are listed.
export default function Handover() {
  const { me, lookups } = useSession()
  const [params, setParams] = useSearchParams()
  const [h, setH] = useState<HandoverData | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [text, setText] = useState('')
  const [editing, setEditing] = useState<{ id: number; text: string } | null>(null)

  const limit = me.modules.find(m => m.key === 'handover')?.hotelId
  const hotelId = Number(params.get('hotelId') ?? limit ?? me.hotelId ?? lookups.hotels[0].id)
  const qs = params.toString()

  const load = useCallback(() => {
    const p = new URLSearchParams(qs)
    p.set('hotelId', String(hotelId))
    api.get<HandoverData>(`/api/handover?${p}`).then(setH).catch(e => setError(e.message))
  }, [qs, hotelId])
  useEffect(load, [load])

  const run = async (fn: () => Promise<unknown>) => {
    setError(null)
    try { await fn(); load(); return true } catch (e) { setError((e as Error).message); return false }
  }
  const go = (date: string, shift: Shift) => setParams({ ...(params.get('hotelId') ? { hotelId: String(hotelId) } : {}), date, shift })

  if (error && !h) return <p className="alert">{error}</p>
  if (!h) return <p className="muted">Loading...</p>
  const isCurrent = h.shiftDate === h.currentDate && h.shift === h.currentShift
  const hotel = lookups.hotels.find(x => x.id === h.hotelId)
  const [nextDate, next] = nextShift(h.shiftDate, h.shift)

  return (
    <div className="stack report">
      <div className="print-only report-head"><b>{hotel?.name}</b> - Shift handover</div>
      <div className="spread">
        <h1>Handover: {h.shift}, {formatLocal(h.shiftDate)}</h1>
        <button className="secondary no-print" onClick={() => window.print()}>Print</button>
      </div>
      <p className="muted small">{HOURS[h.shift]}{isCurrent ? ' (current shift)' : ''}</p>

      <div className="row no-print">
        {limit == null && lookups.hotels.length > 1 && (
          <select value={hotelId} style={{ width: 'auto' }} onChange={e => setParams({ ...Object.fromEntries(params), hotelId: e.target.value })}>
            {lookups.hotels.filter(x => x.isActive).map(x => <option key={x.id} value={x.id}>{x.code}</option>)}
          </select>
        )}
        <button className="secondary" onClick={() => go(h.previousDate, h.previousShift)}>&lt; Previous shift</button>
        {!isCurrent && <button className="secondary" onClick={() => go(nextDate, next)}>Next shift &gt;</button>}
        {!isCurrent && <button className="link" onClick={() => go(h.currentDate, h.currentShift)}>Current shift</button>}
      </div>

      {error && <p className="alert">{error}</p>}

      <section className="card block">
        <h2>Notes for the next shift</h2>
        {h.notes.length === 0 && <p className="muted small">No notes.</p>}
        {h.notes.map(n => (
          <div key={n.id} className="note">
            {editing?.id === n.id ? (
              <div className="stack no-print">
                <textarea rows={3} value={editing.text} maxLength={4000} onChange={e => setEditing({ id: n.id, text: e.target.value })} />
                <div className="row">
                  <button onClick={async () => { if (await run(() => api.put(`/api/handover/notes/${n.id}`, { text: editing.text }))) setEditing(null) }}>Save</button>
                  <button className="secondary" onClick={() => setEditing(null)}>Cancel</button>
                </div>
              </div>
            ) : <p className="narrative">{n.text}</p>}
            <p className="muted small">
              {n.by}, {formatDateTime(n.createdUtc)}{n.updatedUtc !== n.createdUtc ? ' (changed)' : ''}
              {n.canEdit && editing?.id !== n.id && <span className="no-print"> - <button className="link small" onClick={() => setEditing({ id: n.id, text: n.text })}>Change</button>
                {' '}<button className="link small" onClick={() => run(() => api.post(`/api/handover/notes/${n.id}/remove`))}>Remove</button></span>}
            </p>
          </div>
        ))}
        {h.canWrite && (
          <div className="stack no-print">
            <textarea rows={3} value={text} maxLength={4000} placeholder="What does the next shift need to know?" onChange={e => setText(e.target.value)} />
            <div><button disabled={!text.trim()} onClick={async () => {
              if (await run(() => api.post('/api/handover/notes', { hotelId: h.hotelId, shiftDate: h.shiftDate, shift: h.shift, text }))) setText('')
            }}>Add note</button></div>
          </div>
        )}
      </section>

      {h.incidents && (
        <section className="card block">
          <h2>Incidents flagged for handover</h2>
          {h.incidents.length === 0 ? <p className="muted small">None.</p> : h.incidents.map(i => (
            <p key={i.id}><Link to={`/incidents/${i.id}`}><b>{i.number}</b> {i.heading}</Link>
              <span className="muted small"> - {formInfo(i.form).short}{i.type ? `, ${i.type}` : ''}, {formatLocal(i.occurredLocal)}, {STATUS[i.status]}</span></p>
          ))}
        </section>
      )}

      {h.events && (
        <section className="card block">
          <h2>Events flagged for handover</h2>
          {h.events.length === 0 ? <p className="muted small">None.</p> : h.events.map(e => <EventLine key={e.id} e={e} />)}
        </section>
      )}

      {h.openEvents && h.openEvents.length > 0 && (
        <section className="card block">
          <h2>Events still open</h2>
          {h.openEvents.map(e => <EventLine key={e.id} e={e} />)}
        </section>
      )}

      {h.callOuts && (
        <section className="card block">
          <h2>Call-outs for this shift</h2>
          {h.callOuts.length === 0 ? <p className="muted small">None.</p> : h.callOuts.map(o => (
            <p key={o.id}><b>{o.name}</b> <span className="muted small">- {o.reason}, called {formatLocal(o.calledLocal)}
              {o.coveredBy ? `, covered by ${o.coveredBy}` : ', not covered'}</span></p>
          ))}
        </section>
      )}

      {h.keysOut && h.keysOut.length > 0 && (
        <section className="card block">
          <h2>Keys not in</h2>
          {h.keysOut.map(k => (
            <p key={k.id}><Link to="/keys"><b>{k.name}</b></Link> <span className="muted small">- {k.holder
              ? `${k.holder.name} since ${formatLocal(k.holder.outLocal)}${k.holder.overdue ? ' (out too long)' : ''}`
              : k.status}</span></p>
          ))}
        </section>
      )}

      <section className="card block">
        <h2>Read by</h2>
        {h.reads.length === 0 ? <p className="muted small">Nobody has marked it read yet.</p>
          : <p>{h.reads.map(r => `${r.name} (${formatDateTime(r.readUtc)})`).join(', ')}</p>}
        {!h.readByMe && (
          <div className="no-print"><button onClick={() => run(() => api.post('/api/handover/read', { hotelId: h.hotelId, shiftDate: h.shiftDate, shift: h.shift }))}>
            I have read this handover</button></div>
        )}
      </section>
    </div>
  )
}

function EventLine({ e }: { e: HandoverEvent }) {
  return (
    <div className="note">
      <Link to={`/events/${e.id}`}><b>{e.category}</b>{e.detail ? `: ${e.detail}` : ''}</Link>
      <span className="muted small"> - {formatLocal(e.startedLocal)}{e.completedLocal ? ', done' : ', open'}{e.assignedTo ? `, ${e.assignedTo}` : ''}</span>
      {e.narrative && <p className="small narrative">{e.narrative}</p>}
    </div>
  )
}
