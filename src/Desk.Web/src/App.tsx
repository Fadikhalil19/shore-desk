import { createContext, useContext, useEffect, useState } from 'react'
import { BrowserRouter, Link, NavLink, Route, Routes } from 'react-router-dom'
import { api } from './api'
import type { Lookups, Me } from './types'
import Home from './pages/Home'
import Settings from './pages/Settings'
import IncidentEdit from './pages/incidents/IncidentEdit'
import IncidentList from './pages/incidents/IncidentList'
import IncidentView from './pages/incidents/IncidentView'

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
        {me.isAdmin && <NavLink to="/settings">Settings</NavLink>}
        <span className="who">{me.name}{me.isAdmin ? ' (admin)' : ''}</span>
        <a href="/auth/logout">Sign out</a>
      </nav>
    </header>
  )
}
