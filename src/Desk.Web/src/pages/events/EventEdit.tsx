import { useEffect, useMemo, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { api } from '../../api'
import { useSession } from '../../App'
import { nowPacific } from '../../format'
import type { EventSave, Person, Priority, SecurityEvent } from '../../types'
import { Choice, DepartmentPick, Note, Pick, Section, Text, UserPick, When } from '../incidents/fields'

const TYPES = ['Guest', 'Non Guest', 'Employee', 'Visitor', 'Hotel Property', 'N/A']
const PRIORITIES: Priority[] = ['Low', 'Medium', 'High', 'Highest']

// Log an event (/events/new) or change one (/events/12/edit). Built to be quick on a
// phone: only category is required; times default to now.
export default function EventEdit() {
  const { id } = useParams()
  const { me, lookups } = useSession()
  const navigate = useNavigate()
  const [e, setE] = useState<EventSave | null>(null)
  const [people, setPeople] = useState<Person[]>([])
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const limit = me.modules.find(m => m.key === 'eventlog')?.hotelId

  useEffect(() => { api.get<Person[]>('/api/people').then(setPeople).catch(() => setPeople([])) }, [])
  useEffect(() => {
    if (id) {
      api.get<SecurityEvent>(`/api/events/${id}`).then(x => {
        if (!x.can.edit) { navigate(`/events/${x.id}`, { replace: true }); return }
        setE(x)
      }).catch(err => setError(err.message))
    } else {
      const now = nowPacific()
      setE({
        hotelId: limit ?? me.hotelId ?? lookups.hotels[0].id, categoryId: null, detail: null, areaId: null, locationId: null,
        locationDetail: null, room: null, requestedByType: null, requestedByName: null, requestedByUserId: null,
        requestedByDepartmentId: null, requestedLocal: now, relatesToType: null, relatesToName: null, relatesToRoom: null,
        priority: 'Medium', assignedDepartmentId: null, assignedToId: me.id, startedLocal: now, completedLocal: null, narrative: null,
      })
    }
  }, [id, me, lookups, limit, navigate])

  const hotels = useMemo(() => lookups.hotels.filter(h => (h.isActive || h.id === e?.hotelId) && (limit == null || h.id === limit)),
    [lookups, limit, e?.hotelId])

  if (error && !e) return <p className="alert">{error}</p>
  if (!e) return <p className="muted">Loading...</p>

  const up = <K extends keyof EventSave>(k: K, v: EventSave[K]) => setE({ ...e, [k]: v })
  const areas = lookups.areas.filter(a => (a.isActive || a.id === e.areaId) && (a.hotelId == null || a.hotelId === e.hotelId))
  const area = lookups.areas.find(a => a.id === e.areaId)

  const save = async (done: boolean) => {
    setBusy(true); setError(null)
    const body = done && !e.completedLocal ? { ...e, completedLocal: nowPacific() } : e
    try {
      const eventId = id ? (await api.put(`/api/events/${id}`, body), Number(id)) : (await api.post<{ id: number }>('/api/events', body)).id
      navigate(id ? `/events/${eventId}` : '/events')
    } catch (err) { setError((err as Error).message) } finally { setBusy(false) }
  }

  return (
    <div className="stack incident-form">
      <div className="spread">
        <h1>{id ? 'Change event' : 'Log event'}</h1>
        <Link to={id ? `/events/${id}` : '/events'}>Cancel</Link>
      </div>

      <Section title="Event" on>
        {hotels.length > 1 && (
          <label className="field"><span>Hotel</span>
            <select value={e.hotelId} onChange={x => setE({ ...e, hotelId: Number(x.target.value), areaId: null, locationId: null })}>
              {hotels.map(h => <option key={h.id} value={h.id}>{h.code} - {h.name}</option>)}
            </select>
          </label>
        )}
        <Pick label="Category" list="event-category" required value={e.categoryId} onChange={v => up('categoryId', v)} />
        <Text label="Detail" value={e.detail} onChange={v => up('detail', v ?? null)} placeholder="e.g. Room 305 lockout" />
        <Choice label="Priority" options={PRIORITIES} value={e.priority} onChange={v => up('priority', (v as Priority) ?? 'Medium')} />
        <label className="field"><span>Location</span>
          <select value={e.areaId ?? ''} onChange={x => setE({ ...e, areaId: x.target.value ? Number(x.target.value) : null, locationId: null })}>
            <option value="">-</option>
            {areas.map(a => <option key={a.id} value={a.id}>{a.name}</option>)}
          </select>
        </label>
        {area && area.locations.length > 0 && (
          <label className="field"><span>Spot</span>
            <select value={e.locationId ?? ''} onChange={x => up('locationId', x.target.value ? Number(x.target.value) : null)}>
              <option value="">-</option>
              {area.locations.filter(l => l.isActive || l.id === e.locationId).map(l => <option key={l.id} value={l.id}>{l.name}</option>)}
            </select>
          </label>
        )}
        <Text label="Location detail" value={e.locationDetail} onChange={v => up('locationDetail', v ?? null)} />
        <Text label="Room #" value={e.room} onChange={v => up('room', v ?? null)} maxLength={40} />
      </Section>

      <Section title="Requested by" on>
        <Choice label="Type" options={TYPES} value={e.requestedByType} onChange={v => up('requestedByType', v ?? null)} />
        {e.requestedByType === 'Employee' ? <>
          <UserPick label="Employee" people={people} value={e.requestedByUserId} onChange={v => {
            const p = people.find(x => x.id === v)
            setE({ ...e, requestedByUserId: v, requestedByName: p?.name ?? e.requestedByName, requestedByDepartmentId: p?.departmentId ?? e.requestedByDepartmentId })
          }} />
          <DepartmentPick value={e.requestedByDepartmentId} onChange={v => up('requestedByDepartmentId', v)} />
        </> : <Text label="Name" value={e.requestedByName} onChange={v => up('requestedByName', v ?? null)} />}
        <When label="Request time" value={e.requestedLocal} onChange={v => up('requestedLocal', v ?? null)} />
      </Section>

      <Section title="Relates to" on={!!(e.relatesToType || e.relatesToName || e.relatesToRoom)}
        onToggle={on => setE({ ...e, relatesToType: on ? 'Guest' : null, relatesToName: null, relatesToRoom: null })}>
        <Choice label="Type" options={TYPES} value={e.relatesToType} onChange={v => up('relatesToType', v ?? null)} />
        <Text label="Name" value={e.relatesToName} onChange={v => up('relatesToName', v ?? null)} />
        <Text label="Room #" value={e.relatesToRoom} onChange={v => up('relatesToRoom', v ?? null)} maxLength={40} />
      </Section>

      <Section title="Assigned to" on>
        <DepartmentPick label="Department" value={e.assignedDepartmentId} onChange={v => up('assignedDepartmentId', v)} />
        <UserPick label="Officer / employee" people={people} value={e.assignedToId} onChange={v => up('assignedToId', v)} />
        <When label="Started" value={e.startedLocal} onChange={v => up('startedLocal', v ?? null)} />
        <When label="Completed" value={e.completedLocal} onChange={v => up('completedLocal', v ?? null)} />
      </Section>

      <Section title="Narrative" on>
        <Note label="What happened / what was done" rows={6} max={20000} value={e.narrative} onChange={v => up('narrative', v ?? null)} />
      </Section>

      {error && <p className="alert">{error}</p>}
      <div className="row sticky-actions">
        <button disabled={busy || !e.categoryId} className="secondary" onClick={() => save(false)}>{e.completedLocal ? 'Save' : 'Save as open'}</button>
        {!e.completedLocal && <button disabled={busy || !e.categoryId} onClick={() => save(true)}>Save as done</button>}
        {!e.categoryId && <span className="muted small">Choose a category to save.</span>}
      </div>
    </div>
  )
}
