import { Link } from 'react-router-dom'
import { useSession } from '../App'
import type { ModuleAccess } from '../types'

// Start page: the modules this person may use, grouped as in the plan.
// Modules are built in phases, so most show "coming" for now.
export default function Home() {
  const { me, lookups } = useSession()
  const visible = me.modules.filter(m => m.level !== 'None')
  const groups = [...new Set(visible.map(m => m.group))]
  const dept = lookups.departments.find(d => d.id === me.departmentId)?.name
  const hotel = lookups.hotels.find(h => h.id === me.hotelId)?.code

  return (
    <>
      <h1>Hello, {me.name.split(' ')[0]}</h1>
      <p className="muted small">
        {[dept, hotel].filter(Boolean).join(' - ') || 'No department set yet'}
      </p>

      {visible.length === 0 && (
        <div className="notice" style={{ marginTop: '1rem' }}>
          <p><b>You do not have access to any part of Shore Desk yet.</b></p>
          <p>Ask Fadi Khalil to set up your access. You can stay signed in; this page updates when you reload it.</p>
        </div>
      )}

      {groups.map(g => (
        <section key={g}>
          <h2>{g}</h2>
          <div className="tiles">
            {visible.filter(m => m.group === g).map(m => <Tile key={m.key} m={m} />)}
          </div>
        </section>
      ))}
    </>
  )
}

// Modules that are built, and where their tile leads.
const READY: Record<string, string> = {
  incidents: '/incidents',
  'incidents-highlevel': '/incidents?form=HighLevel',
  'incidents-employee': '/incidents?form=EmployeeInjury',
  eventlog: '/events',
  handover: '/handover',
  onduty: '/onduty',
  'safety-reports': '/reports',
  keys: '/keys',
  visitors: '/visitors',
  lockers: '/lockers',
  parking: '/parking',
}

function Tile({ m }: { m: ModuleAccess }) {
  if (READY[m.key]) return (
    <Link className="tile" to={READY[m.key]} title={m.description}>
      <span className="name">{m.name}</span>
      <span className="muted small">{m.description}</span>
      <span><span className={`pill lv-${m.level}`}>{m.level}</span></span>
    </Link>
  )
  return (
    <div className="tile soon" title={m.description}>
      <span className="name">{m.name}</span>
      <span className="muted small">Coming in phase {m.phase}</span>
      <span><span className={`pill lv-${m.level}`}>{m.level}</span></span>
    </div>
  )
}
