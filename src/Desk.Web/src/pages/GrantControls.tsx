import { useSession } from '../App'
import type { DepartmentLimit, Grant, Level, ModuleInfo } from '../types'

export const LEVELS: Level[] = ['None', 'View', 'Edit', 'Manage']
export const LEVEL_HELP: Record<Level, string> = {
  None: 'Hidden from their menu',
  View: 'Can look, cannot change',
  Edit: 'Can add and change records',
  Manage: 'Also assigns, closes, edits lists, runs reports',
}

// Reads a grant in plain words: "Edit - SHO only - own department".
export function describe(g: Grant | null, m: ModuleInfo, hotels: { id: number; code: string }[],
  departments: { id: number; name: string }[]): string {
  if (!g) return 'Not set'
  if (g.level === 'None') return 'No access'
  const parts: string[] = [g.level]
  parts.push(g.hotelId ? `${hotels.find(h => h.id === g.hotelId)?.code ?? '?'} only` : 'all hotels')
  if (m.departmentScoped) {
    parts.push(g.departmentLimit === 'Own' ? 'own department'
      : g.departmentLimit === 'Specific' ? `${departments.find(d => d.id === g.departmentId)?.name ?? '?'} only` : 'all departments')
  }
  return parts.join(' - ')
}

// Hotel and department limits for one module. Changing either saves straight away.
export function LimitControls({ m, g, onChange, disabled }: {
  m: ModuleInfo; g: Grant; onChange: (g: Grant) => void; disabled?: boolean
}) {
  const { lookups } = useSession()
  if (g.level === 'None') return null
  return (
    <>
      <label className="field">Hotels
        <select value={g.hotelId ?? ''} disabled={disabled}
          onChange={e => onChange({ ...g, hotelId: e.target.value ? Number(e.target.value) : null })}>
          <option value="">All hotels</option>
          {lookups.hotels.filter(h => h.isActive || h.id === g.hotelId).map(h => <option key={h.id} value={h.id}>{h.code} only</option>)}
        </select>
      </label>
      {m.departmentScoped && (
        <label className="field">Departments
          <select disabled={disabled}
            value={g.departmentLimit === 'Specific' ? `d${g.departmentId}` : g.departmentLimit}
            onChange={e => {
              const v = e.target.value
              onChange(v.startsWith('d')
                ? { ...g, departmentLimit: 'Specific', departmentId: Number(v.slice(1)) }
                : { ...g, departmentLimit: v as DepartmentLimit, departmentId: null })
            }}>
            <option value="All">All departments</option>
            <option value="Own">Their own department</option>
            {lookups.departments.filter(d => d.isActive || d.id === g.departmentId).map(d =>
              <option key={d.id} value={`d${d.id}`}>{d.name} only</option>)}
          </select>
        </label>
      )}
    </>
  )
}

// Modules in their groups, in the order of the plan.
export function grouped<T extends { module: string }>(rows: T[], modules: ModuleInfo[]): [string, (T & { info: ModuleInfo })[]][] {
  const withInfo = rows.map(r => ({ ...r, info: modules.find(m => m.key === r.module)! })).filter(r => r.info)
  const groups = [...new Set(withInfo.map(r => r.info.group))]
  return groups.map(g => [g, withInfo.filter(r => r.info.group === g)])
}
