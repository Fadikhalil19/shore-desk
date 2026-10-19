import { useState } from 'react'
import { api } from '../api'
import { useSession } from '../App'
import type { ListItem } from '../types'

// Settings > Report lists: incident types, injury types, colors, ... Entries are
// switched off instead of deleted so old reports keep their names.
export default function ReportLists() {
  const { lookups, reloadLookups } = useSession()
  const [key, setKey] = useState(lookups.lists[0]?.key ?? '')
  const [name, setName] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [saved, setSaved] = useState(false)

  const list = lookups.lists.find(l => l.key === key)
  const items = lookups.listItems.filter(i => i.listKey === key)
  const run = async (fn: () => Promise<unknown>) => {
    setError(null)
    try { await fn(); await reloadLookups(); setSaved(true); setTimeout(() => setSaved(false), 2000); return true }
    catch (e) { setError((e as Error).message); return false }
  }
  const put = (i: ListItem, patch: Partial<ListItem>) => run(() => api.put(`/api/admin/lists/items/${i.id}`, { ...i, ...patch }))

  return (
    <div className="stack">
      <label className="field" style={{ maxWidth: 360 }}>List
        <select value={key} onChange={e => setKey(e.target.value)}>
          {lookups.lists.map(l => <option key={l.key} value={l.key}>{l.name}</option>)}
        </select>
      </label>
      {list && <p className="muted small">{list.description} Lower numbers are listed first.</p>}
      {saved && <p className="ok small">Saved</p>}
      {error && <p className="alert">{error}</p>}
      <div className="table-wrap">
        <table className="grid">
          <thead><tr><th>Name</th><th>Order</th><th>In use</th></tr></thead>
          <tbody>
            {items.map(i => (
              <tr key={i.id} className={i.isActive ? '' : 'off'}>
                <td><input defaultValue={i.name} onBlur={e => e.target.value !== i.name && put(i, { name: e.target.value })} aria-label="Name" /></td>
                <td style={{ width: 90 }}><input type="number" defaultValue={i.sortOrder} aria-label="Order"
                  onBlur={e => Number(e.target.value) !== i.sortOrder && put(i, { sortOrder: Number(e.target.value) })} /></td>
                <td><input type="checkbox" checked={i.isActive} onChange={e => put(i, { isActive: e.target.checked })} aria-label={`${i.name} in use`} /></td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <div className="card form">
        <label className="field">New entry<input value={name} onChange={e => setName(e.target.value)} maxLength={200} /></label>
        <div><button disabled={!name.trim()} onClick={async () => {
          const order = Math.max(0, ...items.map(i => i.sortOrder)) + 10
          if (await run(() => api.post('/api/admin/lists/items', { listKey: key, name, sortOrder: order, isActive: true }))) setName('')
        }}>Add to {list?.name.toLowerCase()}</button></div>
      </div>
    </div>
  )
}
