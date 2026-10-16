import { useCallback, useEffect, useState } from 'react'
import { api } from '../api'
import { useSession } from '../App'
import { formatDateTime, words } from '../format'
import type { ActivityPage, HealthReport, SignInReport } from '../types'

// Settings > Activity log: every view, change, download and sign-in.
export function ActivityLog() {
  const { lookups } = useSession()
  const [f, setF] = useState({ q: '', action: '', module: '', from: '', to: '' })
  const [page, setPage] = useState(1)
  const [data, setData] = useState<ActivityPage | null>(null)
  const [actions, setActions] = useState<string[]>([])
  const [error, setError] = useState<string | null>(null)

  useEffect(() => { api.get<string[]>('/api/admin/activity/actions').then(setActions).catch(() => {}) }, [])
  const load = useCallback(() => {
    const p = new URLSearchParams({ page: String(page) })
    Object.entries(f).forEach(([k, v]) => v && p.set(k, v))
    api.get<ActivityPage>(`/api/admin/activity?${p}`).then(setData).catch(e => setError(e.message))
  }, [f, page])
  useEffect(load, [load])

  const set = (k: keyof typeof f) => (e: { target: { value: string } }) => { setPage(1); setF({ ...f, [k]: e.target.value }) }
  return (
    <div className="stack">
      <div className="form">
        <label className="field">Search<input type="search" value={f.q} onChange={set('q')} placeholder="Name or detail" /></label>
        <label className="field">Action
          <select value={f.action} onChange={set('action')}>
            <option value="">All</option>
            {actions.map(a => <option key={a} value={a}>{words(a)}</option>)}
          </select>
        </label>
        <label className="field">Module
          <select value={f.module} onChange={set('module')}>
            <option value="">All</option>
            {lookups.modules.map(m => <option key={m.key} value={m.key}>{m.name}</option>)}
          </select>
        </label>
        <label className="field">From<input type="date" value={f.from} onChange={set('from')} /></label>
        <label className="field">To<input type="date" value={f.to} onChange={set('to')} /></label>
      </div>
      {error && <p className="error">{error}</p>}
      {data && (
        <>
          <div className="table-wrap">
            <table className="grid">
              <thead><tr><th>When (Pacific)</th><th>Who</th><th>Action</th><th>What</th><th>Details</th><th>From</th></tr></thead>
              <tbody>
                {data.items.map(a => (
                  <tr key={a.id}>
                    <td className="nowrap">{formatDateTime(a.utc)}</td>
                    <td>{a.userName}</td>
                    <td className="nowrap">{words(a.action)}</td>
                    <td className="small">{[lookups.modules.find(m => m.key === a.module)?.name, a.entityType && `${a.entityType} ${a.entityId ?? ''}`].filter(Boolean).join(' - ')}</td>
                    <td className="details">{a.details}</td>
                    <td className="small nowrap">{a.ipAddress}</td>
                  </tr>
                ))}
                {data.items.length === 0 && <tr><td colSpan={6} className="muted">Nothing found.</td></tr>}
              </tbody>
            </table>
          </div>
          <Pager page={page} total={data.total} onPage={setPage} />
        </>
      )}
    </div>
  )
}

function Pager({ page, total, onPage }: { page: number; total: number; onPage: (p: number) => void }) {
  const pages = Math.max(1, Math.ceil(total / 100))
  return (
    <div className="pager small">
      <button className="secondary" disabled={page <= 1} onClick={() => onPage(page - 1)}>Previous</button>
      <span>Page {page} of {pages} ({total.toLocaleString()} entries)</span>
      <button className="secondary" disabled={page >= pages} onClick={() => onPage(page + 1)}>Next</button>
    </div>
  )
}

// Settings > Sign-ins: one line per person, then every sign-in and refused attempt.
export function SignIns() {
  const [action, setAction] = useState('')
  const [page, setPage] = useState(1)
  const [data, setData] = useState<SignInReport | null>(null)
  const [error, setError] = useState<string | null>(null)
  useEffect(() => {
    const p = new URLSearchParams({ page: String(page) })
    if (action) p.set('action', action)
    api.get<SignInReport>(`/api/admin/reports/signins?${p}`).then(setData).catch(e => setError(e.message))
  }, [action, page])
  if (!data) return error ? <p className="error">{error}</p> : <p className="muted">Loading...</p>
  return (
    <div className="stack">
      <div className="table-wrap">
        <table className="grid">
          <thead><tr><th>Person</th><th>Role</th><th>Sign-ins</th><th>Last sign-in</th><th>From</th></tr></thead>
          <tbody>
            {data.people.map(p => (
              <tr key={p.id}>
                <td>{p.name}<div className="muted small">{p.email}</div></td>
                <td>{p.role ?? ''}</td>
                <td>{p.signIns}</td>
                <td className="nowrap">{p.lastSignInUtc ? formatDateTime(p.lastSignInUtc) : <span className="muted">Never</span>}</td>
                <td className="small">{p.lastIp}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <h2>History</h2>
      <label className="field" style={{ maxWidth: '16rem' }}>Show
        <select value={action} onChange={e => { setPage(1); setAction(e.target.value) }}>
          <option value="">Sign-ins and refused</option>
          <option value="SignIn">Sign-ins only</option>
          <option value="Denied">Refused only</option>
        </select>
      </label>
      <div className="table-wrap">
        <table className="grid">
          <thead><tr><th>When (Pacific)</th><th>Who</th><th>Result</th><th>Reason / role</th><th>From</th></tr></thead>
          <tbody>
            {data.events.items.map(e => (
              <tr key={e.id}>
                <td className="nowrap">{formatDateTime(e.utc)}</td>
                <td>{e.userName}</td>
                <td>{e.action === 'Denied' ? <span className="pill st-Problem">Refused</span> : <span className="pill st-Ok">Signed in</span>}</td>
                <td className="small">{e.details}</td>
                <td className="small">{e.ipAddress}</td>
              </tr>
            ))}
            {data.events.items.length === 0 && <tr><td colSpan={5} className="muted">Nothing yet.</td></tr>}
          </tbody>
        </table>
      </div>
      <Pager page={page} total={data.events.total} onPage={setPage} />
    </div>
  )
}

// Settings > App health: background jobs, backups, security, server, expiry dates.
export function Health() {
  const [data, setData] = useState<HealthReport | null>(null)
  const [error, setError] = useState<string | null>(null)
  const load = useCallback(() => { setData(null); api.get<HealthReport>('/api/admin/reports/health').then(setData).catch(e => setError(e.message)) }, [])
  useEffect(load, [load])
  if (!data) return error ? <p className="error">{error}</p> : <p className="muted">Checking...</p>
  const areas = [...new Set(data.checks.map(c => c.area))]
  return (
    <div className="stack">
      <div className="spread">
        <span className="muted small">Checked {formatDateTime(data.checkedUtc)}</span>
        <button className="secondary" onClick={load}>Check again</button>
      </div>
      {areas.map(a => (
        <section key={a}>
          <h2>{a}</h2>
          <div className="people">
            {data.checks.filter(c => c.area === a).map(c => (
              <div key={c.name} className="card">
                <div className="spread"><b>{c.name}</b><span className={`pill st-${c.status}`}>{c.status === 'Ok' ? 'OK' : c.status}</span></div>
                <p className="small">{c.message}</p>
                {c.detail && <details className="small"><summary>More</summary><pre style={{ whiteSpace: 'pre-wrap', margin: 0 }}>{c.detail}</pre></details>}
              </div>
            ))}
          </div>
        </section>
      ))}
    </div>
  )
}
