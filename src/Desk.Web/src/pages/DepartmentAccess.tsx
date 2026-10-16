import { useEffect, useState } from 'react'
import { api } from '../api'
import { useSession } from '../App'
import type { DepartmentRow, Grant, Level } from '../types'
import { grouped, LEVEL_HELP, LEVELS, LimitControls } from './GrantControls'

// Settings > Department access: what everyone in a department gets by default.
// A person can then be given more or less on their own page.
export default function DepartmentAccess() {
  const { lookups } = useSession()
  const active = lookups.departments.filter(d => d.isActive)
  const [deptId, setDeptId] = useState<number>(active.find(d => d.name === 'Safety')?.id ?? active[0]?.id ?? 0)
  const [rows, setRows] = useState<DepartmentRow[] | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [saved, setSaved] = useState(false)

  useEffect(() => {
    setRows(null)
    if (deptId) api.get<DepartmentRow[]>(`/api/admin/departments/${deptId}/access`).then(setRows).catch(e => setError(e.message))
  }, [deptId])

  const save = async (module: string, g: Grant | null) => {
    setError(null)
    try {
      setRows(g ? await api.put<DepartmentRow[]>(`/api/admin/departments/${deptId}/access/${module}`, g)
                : await api.del<DepartmentRow[]>(`/api/admin/departments/${deptId}/access/${module}`))
      setSaved(true); setTimeout(() => setSaved(false), 2000)
    } catch (e) { setError((e as Error).message) }
  }

  return (
    <div className="stack">
      <p className="muted small">
        Everyone in the department gets these levels unless their own page says otherwise. Changes apply on their next page load.
        Sensitive modules (High Level and employee injury reports) are separate on purpose: give them only to the people who need them.
      </p>
      <div className="spread">
        <label className="field" style={{ flex: '1 1 16rem' }}>Department
          <select value={deptId} onChange={e => setDeptId(Number(e.target.value))}>
            {active.map(d => <option key={d.id} value={d.id}>{d.name}</option>)}
          </select>
        </label>
        {saved && <span className="ok small">Saved</span>}
      </div>
      {error && <p className="alert">{error}</p>}
      {!rows ? <p className="muted">Loading...</p> : grouped(rows, lookups.modules).map(([group, list]) => (
        <div key={group} className="module-group">
          <h3>{group}</h3>
          {list.map(r => {
            const m = r.info
            const g = r.grant
            return (
              <div key={r.module} className={g && g.level !== 'None' ? 'module-row changed' : 'module-row'}>
                <div className="top">
                  <div>
                    <b>{m.name}</b> {m.sensitive && <span className="pill sensitive">Sensitive</span>}
                    <div className="muted small">{m.description}</div>
                  </div>
                </div>
                <div className="controls">
                  <label className="field">Level
                    <select value={g?.level ?? 'None'} onChange={e => {
                      const level = e.target.value as Level
                      save(r.module, level === 'None' ? null : {
                        level, hotelId: g?.hotelId ?? null,
                        departmentLimit: m.departmentScoped ? (g?.departmentLimit ?? 'Own') : 'All',
                        departmentId: g?.departmentId ?? null,
                      })
                    }}>
                      {LEVELS.map(l => <option key={l} value={l}>{l} - {LEVEL_HELP[l]}</option>)}
                    </select>
                  </label>
                  {g && <LimitControls m={m} g={g} onChange={x => save(r.module, x)} />}
                </div>
              </div>
            )
          })}
        </div>
      ))}
    </div>
  )
}
