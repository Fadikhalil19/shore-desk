import { useEffect, useState } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router-dom'
import { api } from '../../api'
import { useSession } from '../../App'
import { formatLocal } from '../../format'
import type { IncidentPage } from '../../types'
import { FORMS, STATUS, formInfo, levelFor } from './meta'

const VIEWS: [string, string][] = [['all', 'All reports'], ['mine', 'My reports'], ['review', 'To review'], ['drafts', 'Drafts']]

// Incident reports: one search across all five forms (only those this person may see).
export default function IncidentList() {
  const { me, lookups } = useSession()
  const navigate = useNavigate()
  const [params, setParams] = useSearchParams()
  const [page, setPage] = useState<IncidentPage | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [choosing, setChoosing] = useState(false)
  const [text, setText] = useState(params.get('q') ?? '')

  const view = params.get('view') ?? 'all'
  const writable = FORMS.filter(f => ['Edit', 'Manage'].includes(levelFor(me, f.form)))
  const visible = FORMS.filter(f => levelFor(me, f.form) !== 'None')
  const qs = params.toString()

  useEffect(() => {
    const p = new URLSearchParams(qs)
    const v = p.get('view') ?? 'all'
    p.delete('view')
    if (v === 'mine') p.set('mine', 'true')
    if (v === 'review') p.set('toReview', 'true')
    if (v === 'drafts') p.set('status', 'Draft')
    setPage(null)
    api.get<IncidentPage>(`/api/incidents?${p}`).then(setPage).catch(e => setError(e.message))
  }, [qs])

  const set = (k: string, v: string | null) => {
    const p = new URLSearchParams(params)
    if (v) p.set(k, v); else p.delete(k)
    if (k !== 'page') p.delete('page')
    setParams(p)
  }
  const pageNo = Number(params.get('page') ?? '1')

  return (
    <div className="stack">
      <div className="spread">
        <h1>Incident reports</h1>
        {writable.length > 0 && <button onClick={() => setChoosing(c => !c)}>New report</button>}
      </div>

      {choosing && (
        <div className="tiles">
          {writable.map(f => (
            <button key={f.form} className="tile" onClick={() => navigate(`/incidents/new?form=${f.form}`)}>
              <span className="name">{f.name}</span>
              <span className="muted small">{f.hint}</span>
            </button>
          ))}
        </div>
      )}

      <div className="tabs" role="tablist">
        {VIEWS.map(([k, label]) => (
          <button key={k} role="tab" aria-selected={view === k} className={view === k ? 'tab active' : 'tab'}
            onClick={() => set('view', k === 'all' ? null : k)}>{label}</button>
        ))}
      </div>

      <form className="card form" onSubmit={e => { e.preventDefault(); set('q', text.trim() || null) }}>
        <label className="field">Search<input value={text} onChange={e => setText(e.target.value)} placeholder="Number, name, room, plate, words" /></label>
        {visible.length > 1 && (
          <label className="field">Form
            <select value={params.get('form') ?? ''} onChange={e => set('form', e.target.value || null)}>
              <option value="">All forms</option>
              {visible.map(f => <option key={f.form} value={f.form}>{f.name}</option>)}
            </select>
          </label>
        )}
        {view === 'all' && (
          <label className="field">Status
            <select value={params.get('status') ?? ''} onChange={e => set('status', e.target.value || null)}>
              <option value="">All but voided</option>
              {Object.entries(STATUS).map(([k, v]) => <option key={k} value={k}>{v}</option>)}
            </select>
          </label>
        )}
        {lookups.hotels.length > 1 && (
          <label className="field">Hotel
            <select value={params.get('hotelId') ?? ''} onChange={e => set('hotelId', e.target.value || null)}>
              <option value="">All hotels</option>
              {lookups.hotels.map(h => <option key={h.id} value={h.id}>{h.code}</option>)}
            </select>
          </label>
        )}
        <label className="field">From<input type="date" value={params.get('from') ?? ''} onChange={e => set('from', e.target.value || null)} /></label>
        <label className="field">To<input type="date" value={params.get('to') ?? ''} onChange={e => set('to', e.target.value || null)} /></label>
        <div className="row"><button type="submit">Search</button>
          {qs && <button type="button" className="secondary" onClick={() => { setText(''); setParams(view === 'all' ? {} : { view }) }}>Clear</button>}
        </div>
      </form>

      {error && <p className="alert">{error}</p>}
      {!page ? <p className="muted">Loading...</p> : page.items.length === 0 ? (
        <p className="muted">No reports found.</p>
      ) : (
        <>
          <p className="muted small">{page.total} report{page.total === 1 ? '' : 's'}</p>
          <div className="report-list">
            {page.items.map(r => (
              <Link key={r.id} to={`/incidents/${r.id}`} className="report-row">
                <div className="spread">
                  <b>{r.number} {r.heading}</b>
                  <span className={`pill st-${r.status}`}>{STATUS[r.status]}</span>
                </div>
                <div className="muted small">
                  {[formInfo(r.form).short, r.typeName ?? r.kind, r.hotelCode, formatLocal(r.occurredLocal), r.shift,
                    [r.areaName, r.locationName].filter(Boolean).join(' - ')].filter(Boolean).join(' | ')}
                </div>
                <div className="muted small">
                  By {r.writtenByName}{r.supervisorName ? `, supervisor ${r.supervisorName}` : ''}
                  {r.attachments > 0 ? ` | ${r.attachments} file${r.attachments === 1 ? '' : 's'}` : ''}
                </div>
              </Link>
            ))}
          </div>
          {page.total > 50 && (
            <div className="row">
              <button className="secondary" disabled={pageNo <= 1} onClick={() => set('page', String(pageNo - 1))}>Previous</button>
              <span className="muted small">Page {pageNo} of {Math.ceil(page.total / 50)}</span>
              <button className="secondary" disabled={pageNo * 50 >= page.total} onClick={() => set('page', String(pageNo + 1))}>Next</button>
            </div>
          )}
        </>
      )}
    </div>
  )
}
