import { useCallback, useEffect, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { api } from '../api'
import { useSession } from '../App'
import { formatDateTime } from '../format'
import type { AdminUser, Grant, Level, ModuleInfo, ModuleRow, PersonAccess } from '../types'
import { describe, grouped, LEVEL_HELP, LEVELS, LimitControls } from './GrantControls'

// Settings > People: everyone who signed in (or was added), their department and
// hotel, and what they may see. Click a person to change their access.
export default function People() {
  const { lookups } = useSession()
  const [params, setParams] = useSearchParams()
  const selected = params.get('person')
  const [users, setUsers] = useState<AdminUser[] | null>(null)
  const [showRemoved, setShowRemoved] = useState(false)
  const [filter, setFilter] = useState('')
  const [adding, setAdding] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const load = useCallback(() => {
    api.get<AdminUser[]>(`/api/admin/users${showRemoved ? '?includeRemoved=true' : ''}`).then(setUsers).catch(e => setError(e.message))
  }, [showRemoved])
  useEffect(load, [load])

  const open = (id: number | null) => setParams(id ? { tab: 'people', person: String(id) } : { tab: 'people' })

  if (selected) return <PersonPanel id={Number(selected)} onBack={() => { open(null); load() }} />
  if (!users) return error ? <p className="error">{error}</p> : <p className="muted">Loading...</p>

  const shown = users.filter(u => !filter || `${u.name} ${u.email}`.toLowerCase().includes(filter.toLowerCase()))
  const deptName = (id: number | null) => lookups.departments.find(d => d.id === id)?.name

  return (
    <div className="stack">
      <p className="muted small">
        People appear here after they sign in once, or earlier if you add them with their Entra Object ID.
        A person gets their <b>department's default access</b> (Settings &gt; Department access) unless you set
        something different for them. Signing in at all needs the DESK-Users or DESK-Admins group in Entra.
      </p>
      <div className="row">
        <input type="search" placeholder="Find a person" value={filter} onChange={e => setFilter(e.target.value)} style={{ flex: '1 1 14rem' }} />
        <label className="row small"><input type="checkbox" checked={showRemoved} onChange={e => setShowRemoved(e.target.checked)} /> Show removed</label>
        <button className="secondary" onClick={() => setAdding(a => !a)}>{adding ? 'Cancel' : 'Add a person'}</button>
      </div>
      {adding && <AddPerson onDone={id => { setAdding(false); open(id) }} />}
      {error && <p className="error">{error}</p>}
      <div className="people">
        {shown.map(u => (
          <button key={u.id} className={u.blockedUtc ? 'person blocked' : 'person'} onClick={() => open(u.id)}>
            <span>
              <b>{u.name}</b>
              <span className="muted small"> {u.email}</span>
              <br />
              <span className="small">{deptName(u.departmentId) ?? <span className="warn">No department</span>}</span>
              <span className="muted small"> - {u.firstSignInUtc ? `last seen ${formatDateTime(u.lastSeenUtc)}` : 'not signed in yet'}</span>
            </span>
            <span className="row" style={{ justifyContent: 'flex-end' }}>
              {u.personalSettings > 0 && <span className="pill">{u.personalSettings} personal</span>}
              {u.removedUtc ? <span className="pill st-Problem">Removed</span> : u.blockedUtc ? <span className="pill st-Problem">Disabled</span> : null}
            </span>
          </button>
        ))}
        {shown.length === 0 && <p className="muted">Nobody found.</p>}
      </div>
    </div>
  )
}

function AddPerson({ onDone }: { onDone: (id: number) => void }) {
  const { lookups } = useSession()
  const [f, setF] = useState({ entraObjectId: '', name: '', email: '', departmentId: '', hotelId: '' })
  const [error, setError] = useState<string | null>(null)
  const save = async () => {
    setError(null)
    try {
      const u = await api.post<AdminUser>('/api/admin/users', {
        entraObjectId: f.entraObjectId.trim(), name: f.name, email: f.email || null,
        departmentId: f.departmentId ? Number(f.departmentId) : null, hotelId: f.hotelId ? Number(f.hotelId) : null,
      })
      onDone(u.id)
    } catch (e) { setError((e as Error).message) }
  }
  const set = (k: keyof typeof f) => (e: { target: { value: string } }) => setF({ ...f, [k]: e.target.value })
  return (
    <div className="card stack">
      <p className="small muted">Entra admin center &gt; Users &gt; the person &gt; copy <b>Object ID</b>. Their name and email update by themselves when they sign in.</p>
      <div className="form">
        <label className="field">Object ID<input value={f.entraObjectId} onChange={set('entraObjectId')} placeholder="xxxxxxxx-xxxx-..." /></label>
        <label className="field">Name<input value={f.name} onChange={set('name')} /></label>
        <label className="field">Email<input type="email" value={f.email} onChange={set('email')} /></label>
        <label className="field">Department
          <select value={f.departmentId} onChange={set('departmentId')}>
            <option value="">Not set</option>
            {lookups.departments.filter(d => d.isActive).map(d => <option key={d.id} value={d.id}>{d.name}</option>)}
          </select>
        </label>
        <label className="field">Home hotel
          <select value={f.hotelId} onChange={set('hotelId')}>
            <option value="">Not set</option>
            {lookups.hotels.filter(h => h.isActive).map(h => <option key={h.id} value={h.id}>{h.code} - {h.name}</option>)}
          </select>
        </label>
      </div>
      <div className="row"><button onClick={save} disabled={!f.entraObjectId.trim() || !f.name.trim()}>Add</button></div>
      {error && <p className="error">{error}</p>}
    </div>
  )
}

// One person: details, Disable / Remove, and access per module.
function PersonPanel({ id, onBack }: { id: number; onBack: () => void }) {
  const { me, lookups } = useSession()
  const [data, setData] = useState<PersonAccess | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [saved, setSaved] = useState<string | null>(null)
  const [removing, setRemoving] = useState(false)

  const load = useCallback(() => {
    api.get<PersonAccess>(`/api/admin/users/${id}/access`).then(setData).catch(e => setError(e.message))
  }, [id])
  useEffect(load, [load])

  if (!data) return error ? <p className="error">{error}</p> : <p className="muted">Loading...</p>
  const u = data.person
  const blocked = u.blockedUtc !== null
  const self = u.id === me.id

  const run = async (fn: () => Promise<unknown>, what: string) => {
    setError(null)
    try { await fn(); setSaved(what); setTimeout(() => setSaved(null), 2500) }
    catch (e) { setError((e as Error).message) }
  }

  const saveDetails = (patch: Partial<{ departmentId: number | null; hotelId: number | null; jobTitle: string | null }>) =>
    run(async () => {
      await api.put(`/api/admin/users/${id}`, { departmentId: u.departmentId, hotelId: u.hotelId, jobTitle: u.jobTitle, ...patch })
      load()
    }, 'details')

  const setPersonal = (module: string, g: Grant | null) => run(async () => {
    setData(g ? await api.put<PersonAccess>(`/api/admin/users/${id}/access/${module}`, g)
              : await api.del<PersonAccess>(`/api/admin/users/${id}/access/${module}`))
  }, 'access')

  const act = (action: 'disable' | 'enable') => run(async () => { await api.post(`/api/admin/users/${id}/${action}`); load() }, action)

  return (
    <div className="stack">
      <div className="spread">
        <button className="link" onClick={onBack}>Back to people</button>
        {saved && <span className="ok small">Saved</span>}
      </div>
      <div className="card stack">
        <div className="spread">
          <div>
            <h2 style={{ margin: 0 }}>{u.name}</h2>
            <div className="muted small">{u.email}</div>
            <div className="muted small">
              {u.firstSignInUtc ? `First signed in ${formatDateTime(u.firstSignInUtc)}, last seen ${formatDateTime(u.lastSeenUtc)}` : 'Has not signed in yet'}
            </div>
          </div>
          <div>
            {u.removedUtc ? <span className="pill st-Problem">Removed {formatDateTime(u.removedUtc)}</span>
              : blocked ? <span className="pill st-Problem">Disabled {formatDateTime(u.blockedUtc)}</span>
              : <span className="pill st-Ok">Active</span>}
          </div>
        </div>
        <div className="form">
          <label className="field">Department (sets their default access)
            <select value={u.departmentId ?? ''} onChange={e => saveDetails({ departmentId: e.target.value ? Number(e.target.value) : null })}>
              <option value="">Not set</option>
              {lookups.departments.filter(d => d.isActive || d.id === u.departmentId).map(d => <option key={d.id} value={d.id}>{d.name}</option>)}
            </select>
          </label>
          <label className="field">Home hotel
            <select value={u.hotelId ?? ''} onChange={e => saveDetails({ hotelId: e.target.value ? Number(e.target.value) : null })}>
              <option value="">Not set</option>
              {lookups.hotels.filter(h => h.isActive || h.id === u.hotelId).map(h => <option key={h.id} value={h.id}>{h.code} - {h.name}</option>)}
            </select>
          </label>
          <label className="field">Job title
            <input defaultValue={u.jobTitle ?? ''} onBlur={e => { if ((e.target.value || null) !== u.jobTitle) saveDetails({ jobTitle: e.target.value || null }) }} />
          </label>
        </div>
        {!self && (
          <div className="row">
            {blocked
              ? <button className="secondary" onClick={() => act('enable')}>{u.removedUtc ? 'Restore' : 'Enable'}</button>
              : <button className="secondary" onClick={() => act('disable')}>Disable</button>}
            {!u.removedUtc && <button className="secondary" onClick={() => setRemoving(true)}>Remove</button>}
          </div>
        )}
        <p className="muted small">
          <b>Disable</b> stops them getting in straight away (even if signed in now); you can enable them again.
          <b> Remove</b> is for people who have left: it also clears their personal access and hides them from the list. History is kept.
          Also take them out of the DESK group in Entra and the Cloudflare allow list.
        </p>
        {removing && <RemoveBox name={u.name} id={id} onDone={() => { setRemoving(false); onBack() }} onCancel={() => setRemoving(false)} />}
      </div>

      {error && <p className="alert">{error}</p>}

      <h2>Access</h2>
      {u.departmentId === null && <p className="notice small">No department set, so there is no default access. Set a department, or set access per module below.</p>}
      <p className="muted small">
        "Department default" comes from {data.departmentName ?? 'their department'}. Choose a level here to give this person
        more or less than that; "Use department default" puts them back in line with their team.
      </p>
      {grouped(data.modules, lookups.modules).map(([group, rows]) => (
        <div key={group} className="module-group">
          <h3>{group}</h3>
          {rows.map(r => <PersonModuleRow key={r.module} r={r} disabled={!!u.removedUtc} onChange={g => setPersonal(r.module, g)} />)}
        </div>
      ))}
    </div>
  )
}

function PersonModuleRow({ r, disabled, onChange }: { r: ModuleRow & { info: ModuleInfo }; disabled: boolean; onChange: (g: Grant | null) => void }) {
  const { lookups } = useSession()
  const m = r.info
  const eff = r.effective
  const choice = r.personal ? r.personal.level : 'inherit'
  return (
    <div className={r.personal ? 'module-row changed' : 'module-row'}>
      <div className="top">
        <div>
          <b>{m.name}</b> {m.sensitive && <span className="pill sensitive">Sensitive</span>}
          <div className="muted small">{m.description}</div>
        </div>
        <span className={`pill lv-${eff.level}`} title={`From: ${eff.source}`}>{eff.level}</span>
      </div>
      <div className="controls">
        <label className="field">This person
          <select value={choice} disabled={disabled} onChange={e => {
            const v = e.target.value
            if (v === 'inherit') onChange(null)
            else onChange({ level: v as Level, hotelId: r.personal?.hotelId ?? null,
              departmentLimit: m.departmentScoped ? (r.personal?.departmentLimit ?? 'Own') : 'All',
              departmentId: r.personal?.departmentId ?? null })
          }}>
            <option value="inherit">Use department default</option>
            {LEVELS.map(l => <option key={l} value={l}>{l} - {LEVEL_HELP[l]}</option>)}
          </select>
        </label>
        {r.personal && <LimitControls m={m} g={r.personal} disabled={disabled} onChange={onChange} />}
      </div>
      <div className="muted small" style={{ marginTop: '0.35rem' }}>
        Department default: {describe(r.departmentDefault, m, lookups.hotels, lookups.departments)}
      </div>
    </div>
  )
}

// Removing someone needs their name typed in, so it can't happen by a slip of the finger.
function RemoveBox({ name, id, onDone, onCancel }: { name: string; id: number; onDone: () => void; onCancel: () => void }) {
  const [typed, setTyped] = useState('')
  const [error, setError] = useState<string | null>(null)
  const remove = async () => {
    setError(null)
    try { await api.post(`/api/admin/users/${id}/remove`, { confirmName: typed }); onDone() }
    catch (e) { setError((e as Error).message) }
  }
  return (
    <div className="alert stack">
      <p><b>Remove {name}?</b> They are signed out and refused at sign-in, and their personal access is cleared. Their history stays in the activity log.</p>
      <input value={typed} onChange={e => setTyped(e.target.value)} placeholder={`Type ${name} to confirm`} aria-label="Type the name to confirm" />
      <div className="row">
        <button className="danger" disabled={typed.trim().toLowerCase() !== name.trim().toLowerCase()} onClick={remove}>Remove</button>
        <button className="secondary" onClick={onCancel}>Cancel</button>
      </div>
      {error && <p className="error">{error}</p>}
    </div>
  )
}
