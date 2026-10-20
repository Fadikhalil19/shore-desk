import { createContext, useContext, useEffect, useState } from 'react'
import { BrowserRouter, Link, NavLink, Route, Routes } from 'react-router-dom'
import { api } from './api'
import type { Lookups, Me } from './types'
import Home from './pages/Home'
import Settings from './pages/Settings'
import IncidentEdit from './pages/incidents/IncidentEdit'
import IncidentList from './pages/incidents/IncidentList'
import IncidentView from './pages/incidents/IncidentView'
import EventEdit from './pages/events/EventEdit'
import EventLog from './pages/events/EventLog'
import EventView from './pages/events/EventView'
import Handover from './pages/Handover'
import OnDuty from './pages/OnDuty'
import SafetyReports from './pages/SafetyReports'
import Keys from './pages/Keys'
import Visitors, { VisitBadge, VisitPage } from './pages/Visitors'
import Spaces from './pages/Spaces'

interface Session { me: Me; lookups: Lookups; reloadLookups: () => Promise<void> }
const SessionContext = createContext<Session | null>(null)

export function useSession(): Session {
  const s = useContext(SessionContext)
  if (!s) throw new Error('Session not loaded')
  return s
}

export default function App() {
  const [me, setMe] = useState<Me | null>(null)
  const [lookups, setLookups] = useState<Lookups | null>(null)
  const [error, setError] = useState<string | null>(null)

  const reloadLookups = async () => setLookups(await api.get<Lookups>('/api/lookups'))

  useEffect(() => {
    Promise.all([api.get<Me>('/api/me'), api.get<Lookups>('/api/lookups')])
      .then(([m, l]) => { setMe(m); setLookups(l) })
      .catch(e => setError(e.message))
  }, [])

  if (error) return <div className="page"><p className="error">{error}</p></div>
  if (!me || !lookups) return <div className="page"><p className="muted">Loading...</p></div>

  return (
    <SessionContext.Provider value={{ me, lookups, reloadLookups }}>
      <BrowserRouter>
        <TopBar />
        <main className="page">
          <Routes>
            <Route path="/" element={<Home />} />
            <Route path="/settings" element={<Settings />} />
            <Route path="/incidents" element={<IncidentList />} />
            <Route path="/incidents/new" element={<IncidentEdit />} />
            <Route path="/incidents/:id" element={<IncidentView />} />
            <Route path="/incidents/:id/edit" element={<IncidentEdit />} />
            <Route path="/events" element={<EventLog />} />
            <Route path="/events/new" element={<EventEdit />} />
            <Route path="/events/:id" element={<EventView />} />
            <Route path="/events/:id/edit" element={<EventEdit />} />
            <Route path="/handover" element={<Handover />} />
            <Route path="/onduty" element={<OnDuty />} />
            <Route path="/reports" element={<SafetyReports />} />
            <Route path="/keys" element={<Keys />} />
            <Route path="/visitors" element={<Visitors />} />
            <Route path="/visitors/new" element={<VisitPage />} />
            <Route path="/visitors/:id" element={<VisitPage />} />
            <Route path="/visitors/:id/badge" element={<VisitBadge />} />
            <Route path="/lockers" element={<Spaces key="lockers" kind="lockers" />} />
            <Route path="/parking" element={<Spaces key="parking" kind="parking" />} />
            <Route path="*" element={<p>Page not found. <Link to="/">Back to the start page</Link></p>} />
          </Routes>
        </main>
      </BrowserRouter>
    </SessionContext.Provider>
  )
}

// On a phone the menu folds behind a button; on a desktop it is always shown.
function TopBar() {
  const { me } = useSession()
  const [open, setOpen] = useState(false)

  return (
    <header className="topbar">
      <Link to="/" className="brand"><img src="/favicon.svg" alt="" />Shore Desk</Link>
      <button className="menu-button" aria-expanded={open} aria-controls="main-nav" onClick={() => setOpen(o => !o)}>
        {open ? 'Close' : 'Menu'}
      </button>
      <nav id="main-nav" className={open ? 'nav open' : 'nav'} onClick={() => setOpen(false)}>
        <NavLink to="/" end>Home</NavLink>
        {me.modules.some(m => m.key.startsWith('incidents') && m.level !== 'None') && <NavLink to="/incidents">Incidents</NavLink>}
        {me.modules.some(m => m.key === 'eventlog' && m.level !== 'None') && <NavLink to="/events">Event log</NavLink>}
        {me.modules.some(m => m.key === 'handover' && m.level !== 'None') && <NavLink to="/handover">Handover</NavLink>}
        {me.modules.some(m => m.key === 'onduty' && m.level !== 'None') && <NavLink to="/onduty">On duty</NavLink>}
        {me.modules.some(m => m.key === 'safety-reports' && m.level !== 'None') && <NavLink to="/reports">Reports</NavLink>}
        {me.modules.some(m => m.key === 'keys' && m.level !== 'None') && <NavLink to="/keys">Keys</NavLink>}
        {me.modules.some(m => m.key === 'visitors' && m.level !== 'None') && <NavLink to="/visitors">Visitors</NavLink>}
        {me.modules.some(m => m.key === 'lockers' && m.level !== 'None') && <NavLink to="/lockers">Lockers</NavLink>}
        {me.modules.some(m => m.key === 'parking' && m.level !== 'None') && <NavLink to="/parking">Parking</NavLink>}
        {me.isAdmin && <NavLink to="/settings">Settings</NavLink>}
        <span className="who">{me.name}{me.isAdmin ? ' (admin)' : ''}</span>
        <a href="/auth/logout">Sign out</a>
      </nav>
    </header>
  )
}
