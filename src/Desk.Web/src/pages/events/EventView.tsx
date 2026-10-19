import { useCallback, useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { api } from '../../api'
import { useSession } from '../../App'
import { formatDateTime, formatLocal, nowPacific } from '../../format'
import type { SecurityEvent } from '../../types'

// One event log entry with Complete / Change / Void.
export default function EventView() {
  const { id } = useParams()
  const { lookups } = useSession()
  const [e, setE] = useState<SecurityEvent | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [voiding, setVoiding] = useState(false)
  const [reason, setReason] = useState('')
  const [doneAt, setDoneAt] = useState(nowPacific())

  const load = useCallback(() => { api.get<SecurityEvent>(`/api/events/${id}`).then(setE).catch(x => setError(x.message)) }, [id])
  useEffect(load, [load])
  const run = async (fn: () => Promise<unknown>) => {
    setError(null)
    try { await fn(); load() } catch (x) { setError((x as Error).message) }
  }

  if (error && !e) return <p className="alert">{error}</p>
  if (!e) return <p className="muted">Loading...</p>
  const dept = (d: number | null) => lookups.departments.find(x => x.id === d)?.name
  const hotel = lookups.hotels.find(h => h.id === e.hotelId)

  return (
    <div className="stack report">
      <div className="spread">
        <div>
          <p className="muted small no-print"><Link to="/events">Event log</Link></p>
          <h1>{e.categoryName}{e.detail ? `: ${e.detail}` : ''}</h1>
          <p className="row">
            <span className={`pill ${e.voidedUtc ? 'st-Voided' : e.completedLocal ? 'st-Approved' : 'st-AwaitingReview'}`}>
              {e.voidedUtc ? 'Voided' : e.completedLocal ? 'Done' : 'Open'}</span>
            <span className={`pill prio-${e.priority}`}>{e.priority}</span>
            <span className="muted">{hotel?.code} | {e.shift}</span>
          </p>
        </div>
        <div className="row no-print">
          {e.can.edit && <Link className="button secondary" to={`/events/${e.id}/edit`}>Change</Link>}
          {e.can.flag && <button className={e.forHandover ? '' : 'secondary'} aria-pressed={e.forHandover}
            onClick={() => run(() => api.post(`/api/events/${e.id}/handover`, { flag: !e.forHandover }))}>
            {e.forHandover ? 'On handover' : 'Flag for handover'}</button>}
          <button className="secondary" onClick={() => window.print()}>Print</button>
        </div>
      </div>

      {error && <p className="alert">{error}</p>}
      {e.voidedUtc && <p className="alert"><b>Voided</b> {formatDateTime(e.voidedUtc)}: {e.voidReason}</p>}

      {e.can.complete && (
        <div className="card row no-print">
          <label className="field">Completed at<input type="datetime-local" value={doneAt} onChange={x => setDoneAt(x.target.value)} /></label>
          <button onClick={() => run(() => api.post(`/api/events/${e.id}/complete`, { completedLocal: doneAt }))}>Mark done</button>
        </div>
      )}

      <section className="card block">
        <dl className="facts">
          <F k="Started" v={formatLocal(e.startedLocal)} />
          <F k="Completed" v={formatLocal(e.completedLocal)} />
          <F k="Location" v={[e.areaName, e.locationName, e.locationDetail].filter(Boolean).join(' - ')} />
          <F k="Room #" v={e.room} />
          <F k="Requested by" v={[e.requestedByName, e.requestedByType, dept(e.requestedByDepartmentId), formatLocal(e.requestedLocal)].filter(Boolean).join(', ')} />
          <F k="Relates to" v={[e.relatesToName, e.relatesToType, e.relatesToRoom && `room ${e.relatesToRoom}`].filter(Boolean).join(', ')} />
          <F k="Assigned to" v={[e.assignedToName, dept(e.assignedDepartmentId)].filter(Boolean).join(', ')} />
          <F k="Logged by" v={`${e.createdByName}, ${formatDateTime(e.createdUtc)}`} />
        </dl>
      </section>
      {e.narrative && <section className="card block"><h2>Narrative</h2><p className="narrative">{e.narrative}</p></section>}

      {e.can.void && (
        <div className="no-print">
          {!voiding ? <button className="link" onClick={() => setVoiding(true)}>Void entry</button> : (
            <div className="card form">
              <label className="field wide">Why void it? (kept on the entry)<textarea rows={2} value={reason} onChange={x => setReason(x.target.value)} maxLength={1000} /></label>
              <div className="row">
                <button className="danger" disabled={!reason.trim()} onClick={() => run(() => api.post(`/api/events/${e.id}/void`, { reason }))}>Void entry</button>
                <button className="secondary" onClick={() => setVoiding(false)}>Cancel</button>
              </div>
            </div>
          )}
        </div>
      )}
    </div>
  )
}

function F({ k, v }: { k: string; v: string | null | undefined }) {
  if (!v) return null
  return <><dt>{k}</dt><dd>{v}</dd></>
}
