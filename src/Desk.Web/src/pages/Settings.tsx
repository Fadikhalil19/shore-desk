import { useSearchParams } from 'react-router-dom'
import { useSession } from '../App'
import People from './People'
import DepartmentAccess from './DepartmentAccess'
import { Areas, Departments, Hotels } from './Lists'
import { ActivityLog, Health, SignIns } from './Reports'
import ReportLists from './ReportLists'

const TABS: [string, string][] = [
  ['people', 'People'], ['defaults', 'Department access'], ['hotels', 'Hotels'], ['departments', 'Departments'],
  ['locations', 'Locations'], ['lists', 'Report lists'], ['activity', 'Activity log'], ['signins', 'Sign-ins'], ['health', 'App health'],
]

// Admin settings. Nothing is deleted here: list entries are switched off so old
// records keep making sense, and people are disabled or removed.
export default function Settings() {
  const { me } = useSession()
  const [params, setParams] = useSearchParams()
  const tab = params.get('tab') ?? 'people'

  if (!me.isAdmin) return <p className="error">Only administrators can change settings.</p>

  return (
    <>
      <h1>Settings</h1>
      <div className="tabs" role="tablist">
        {TABS.map(([k, label]) => (
          <button key={k} role="tab" aria-selected={tab === k} className={tab === k ? 'tab active' : 'tab'}
            onClick={() => setParams({ tab: k })}>{label}</button>
        ))}
      </div>
      {tab === 'people' && <People />}
      {tab === 'defaults' && <DepartmentAccess />}
      {tab === 'hotels' && <Hotels />}
      {tab === 'departments' && <Departments />}
      {tab === 'locations' && <Areas />}
      {tab === 'lists' && <ReportLists />}
      {tab === 'activity' && <ActivityLog />}
      {tab === 'signins' && <SignIns />}
      {tab === 'health' && <Health />}
    </>
  )
}
