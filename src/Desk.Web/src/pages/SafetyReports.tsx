import { useEffect, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { api } from '../api'
import { useSession } from '../App'
import { formatLocal, nowPacific } from '../format'
import { HOURS, nextShift } from '../shifts'
import type { Count, EventStats, IncidentStats, ShiftSummary } from '../types'
import { STATUS } from './incidents/meta'

const TABS: [string, string][] = [['shift', 'Shift summary'], ['incidents', 'Incidents'], ['events', 'Events']]
const STATUS_NAMES: Record<string, string> = STATUS

// Safety reports. Every number only counts what this person may see in the
// underlying module (the server makes sure of that).
export default function SafetyReports() {
  const [params, setParams] = useSearchParams()
  const tab = params.get('tab') ?? 'shift'
  return (
    <div className="stack report">
      <div className="spread">
        <h1>Safety reports</h1>
        <button className="secondary no-print" onClick={() => window.print()}>Print</button>
      </div>
      <div className="tabs no-print" role="tablist">
        {TABS.map(([k, label]) => (
          <button key={k} role="tab" aria-selected={tab === k} className={tab === k ? 'tab active' : 'tab'}
            onClick={() => setParams(k === 'shift' ? {} : { tab: k })}>{label}</button>
        ))}
      </div>
      {tab === 'shift' && <Shift />}
      {tab === 'incidents' && <Stats kind="incidents" />}
      {tab === 'events' && <Stats kind="events" />}
    </div>
  )
}

function useHotel(): [number | null, number | null] {
  const { me } = useSession()
  const limit = me.modules.find(m => m.key === 'safety-reports')?.hotelId ?? null
  return [limit, limit ?? me.hotelId]
}

// --- Shift summary ---------------------------------------------------------------

function Shift() {
  const { lookups } = useSession()
  const [params, setParams] = useSearchParams()
  const [limit, defaultHotel] = useHotel()
  const hotelId = Number(params.get('hotelId') ?? defaultHotel ?? lookups.hotels[0].id)
  const [s, setS] = useState<ShiftSummary | null>(null)
  const [error, setError] = useState<string | null>(null)
  const qs = params.toString()

  useEffect(() => {
    const p = new URLSearchParams(qs)
    p.set('hotelId', String(hotelId))
    api.get<ShiftSummary>(`/api/reports/shift?${p}`).then(setS).catch(e => setError(e.message))
  }, [qs, hotelId])

  if (error) return <p className="alert">{error}</p>
  if (!s) return <p className="muted">Loading...</p>
  const go = (date: string, shift: string) => setParams({ ...(params.get('hotelId') ? { hotelId: String(hotelId) } : {}), date, shift })
  const [nd, ns] = nextShift(s.shiftDate, s.shift)
  const hotel = lookups.hotels.find(h => h.id === s.hotelId)

  return (
    <>
      <h2>{hotel?.code} {s.shift}, {formatLocal(s.shiftDate)} <span className="muted small">{HOURS[s.shift]}</span></h2>
      <div className="row no-print">
        {limit == null && lookups.hotels.length > 1 && (
          <select value={hotelId} style={{ width: 'auto' }} onChange={e => setParams({ ...Object.fromEntries(params), hotelId: e.target.value })}>
            {lookups.hotels.filter(x => x.isActive).map(x => <option key={x.id} value={x.id}>{x.code}</option>)}
          </select>
        )}
        <button className="secondary" onClick={() => go(s.previousDate, s.previousShift)}>&lt; Previous shift</button>
        <button className="secondary" onClick={() => go(nd, ns)}>Next shift &gt;</button>
      </div>

      <div className="stat-tiles">
        {s.onDuty && <Tile label="On duty" value={s.onDuty.length} />}
        {s.callOuts && <Tile label="Call-outs" value={s.callOuts.length} />}
        {s.incidents && <Tile label="Incidents" value={s.incidents.length} />}
        {s.eventsTotal != null && <Tile label="Events" value={s.eventsTotal} note={s.eventsOpen ? `${s.eventsOpen} still open` : undefined} />}
      </div>

      {s.onDuty && (
        <section className="card block">
          <h2>On duty</h2>
          {s.onDuty.length === 0 ? <p className="muted small">Nobody on the roster.</p>
            : <p>{s.onDuty.map(a => [a.name, a.post, a.radio && `radio ${a.radio}`].filter(Boolean).join(', ')).join(' | ')}</p>}
          {s.callOuts && s.callOuts.length > 0 && <p className="small"><b>Call-outs:</b> {s.callOuts.map(o =>
            `${o.name} (${o.reason}${o.coveredBy ? `, covered by ${o.coveredBy}` : ', not covered'})`).join('; ')}</p>}
        </section>
      )}
      {s.incidents && (
        <section className="card block">
          <h2>Incidents</h2>
          {s.incidents.length === 0 ? <p className="muted small">None.</p> : s.incidents.map(i => (
            <p key={i.id}><Link to={`/incidents/${i.id}`}><b>{i.number}</b> {i.heading}</Link>
              <span className="muted small"> - {[i.form, i.type, formatLocal(i.occurredLocal), STATUS_NAMES[i.status] ?? i.status, i.writtenBy].filter(Boolean).join(', ')}</span></p>
          ))}
        </section>
      )}
      {s.eventsByCategory && s.eventsByCategory.length > 0 && <Bars title="Events by category" data={s.eventsByCategory} />}
      {s.handoverNotes && s.handoverNotes.length > 0 && (
        <section className="card block">
          <h2>Handover notes</h2>
          {s.handoverNotes.map((n, i) => <p key={i} className="narrative note">{n}</p>)}
        </section>
      )}
    </>
  )
}

// --- Statistics ------------------------------------------------------------------

// Date range presets: last 30 days, this quarter, last quarter, this year.
function presets(): [string, string, string][] {
  const today = nowPacific().slice(0, 10)
  const [y, m] = today.split('-').map(Number)
  const q = Math.floor((m - 1) / 3)
  const pad = (n: number) => String(n).padStart(2, '0')
  const qStart = (yy: number, qq: number) => `${yy}-${pad(qq * 3 + 1)}-01`
  const qEnd = (yy: number, qq: number) => new Date(yy, qq * 3 + 3, 0).toISOString().slice(0, 10)
  const [ly, lq] = q === 0 ? [y - 1, 3] : [y, q - 1]
  const d30 = new Date(`${today}T12:00:00`); d30.setDate(d30.getDate() - 29)
  return [
    ['Last 30 days', d30.toISOString().slice(0, 10), today],
    [`Q${q + 1} ${y}`, qStart(y, q), today],
    [`Q${lq + 1} ${ly}`, qStart(ly, lq), qEnd(ly, lq)],
    [`${y} so far`, `${y}-01-01`, today],
  ]
}

function Stats({ kind }: { kind: 'incidents' | 'events' }) {
  const { lookups } = useSession()
  const [params, setParams] = useSearchParams()
  const [limit] = useHotel()
  const [data, setData] = useState<IncidentStats | EventStats | null>(null)
  const [error, setError] = useState<string | null>(null)
  const ranges = presets()
  const from = params.get('from') ?? ranges[0][1]
  const to = params.get('to') ?? ranges[0][2]
  const hotelId = params.get('hotelId') ?? ''
  const query = new URLSearchParams({ from, to, ...(hotelId ? { hotelId } : {}) }).toString()

  useEffect(() => {
    setError(null)
    api.get<IncidentStats | EventStats>(`/api/reports/${kind}?${query}`).then(setData).catch(e => setError(e.message))
  }, [kind, query])

  const set = (patch: Record<string, string>) => setParams({ tab: kind, from, to, ...(hotelId ? { hotelId } : {}), ...patch })

  return (
    <>
      <div className="card form no-print">
        <label className="field">Period
          <select value={ranges.find(r => r[1] === from && r[2] === to)?.[0] ?? ''} onChange={e => {
            const r = ranges.find(x => x[0] === e.target.value)
            if (r) set({ from: r[1], to: r[2] })
          }}>
            <option value="">Custom</option>
            {ranges.map(r => <option key={r[0]}>{r[0]}</option>)}
          </select>
        </label>
        <label className="field">From<input type="date" value={from} onChange={e => e.target.value && set({ from: e.target.value })} /></label>
        <label className="field">To<input type="date" value={to} onChange={e => e.target.value && set({ to: e.target.value })} /></label>
        {limit == null && lookups.hotels.length > 1 && (
          <label className="field">Hotel
            <select value={hotelId} onChange={e => set({ hotelId: e.target.value })}>
              <option value="">All hotels</option>
              {lookups.hotels.map(h => <option key={h.id} value={h.id}>{h.code}</option>)}
            </select>
          </label>
        )}
        <div><a className="button secondary" href={`/api/reports/${kind}.csv?${query}`}>Export to Excel</a></div>
      </div>
      <p className="print-only">{formatLocal(from)} to {formatLocal(to)}</p>

      {error && <p className="alert">{error}</p>}
      {!data ? <p className="muted">Loading...</p> : kind === 'incidents' ? <IncidentCharts s={data as IncidentStats} /> : <EventCharts s={data as EventStats} />}
    </>
  )
}

function IncidentCharts({ s }: { s: IncidentStats }) {
  if (s.total === 0) return <p className="muted">No incident reports in this period.</p>
  return (
    <>
      <div className="stat-tiles">
        <Tile label="Incident reports" value={s.total} />
        {s.byStatus.filter(x => x.key !== 'Approved').map(x => <Tile key={x.key} label={STATUS_NAMES[x.key] ?? x.key} value={x.value} />)}
      </div>
      <Bars title="By month" data={s.byMonth.map(m => ({ key: monthName(m.key), value: m.value }))} keepOrder />
      <Bars title="By type" data={s.byType} />
      <div className="chart-pair">
        <Bars title="By shift" data={s.byShift} keepOrder />
        <Bars title="By form" data={s.byForm} />
      </div>
      <Bars title="By location" data={s.byLocation} />
      <Bars title="By officer (written by)" data={s.byOfficer} />
      {s.byHotel.length > 1 && <Bars title="By hotel" data={s.byHotel} />}
    </>
  )
}

function EventCharts({ s }: { s: EventStats }) {
  if (s.total === 0) return <p className="muted">No events logged in this period (or you cannot see the event log).</p>
  const avg = s.averageMinutesToComplete
  return (
    <>
      <div className="stat-tiles">
        <Tile label="Events" value={s.total} />
        <Tile label="Still open" value={s.open} />
        {avg != null && <Tile label="Average time to complete" value={avg < 120 ? `${avg} min` : `${(avg / 60).toFixed(1)} h`} />}
      </div>
      <Bars title="By month" data={s.byMonth.map(m => ({ key: monthName(m.key), value: m.value }))} keepOrder />
      <Bars title="By category" data={s.byCategory} />
      <div className="chart-pair">
        <Bars title="By shift" data={s.byShift} keepOrder />
        <Bars title="By priority" data={s.byPriority} keepOrder />
      </div>
      <Bars title="By officer (logged by)" data={s.byOfficer} />
    </>
  )
}

const monthName = (ym: string) => {
  const [y, m] = ym.split('-').map(Number)
  return new Date(y, m - 1, 1).toLocaleDateString('en-US', { month: 'short', year: 'numeric' })
}

function Tile({ label, value, note }: { label: string; value: number | string; note?: string }) {
  return <div className="stat-tile"><span className="muted small">{label}</span><b>{value}</b>{note && <span className="small warn">{note}</span>}</div>
}

// One series as a ranked list of thin bars: label, bar, count in text ink. Long lists
// show the top 12 with a "show all" toggle; the exact count is on hover.
function Bars({ title, data, keepOrder }: { title: string; data: Count[]; keepOrder?: boolean }) {
  const [all, setAll] = useState(false)
  const max = Math.max(1, ...data.map(d => d.value))
  const total = data.reduce((n, d) => n + d.value, 0)
  const rows = keepOrder || all ? data : data.slice(0, 12)
  return (
    <section className="card block chart">
      <h2>{title}</h2>
      <div className="bars" role="table" aria-label={title}>
        {rows.map(d => (
          <div key={d.key} className="bar-row" role="row" title={`${d.key}: ${d.value} (${total ? Math.round(d.value * 100 / total) : 0}%)`}>
            <span className="bar-label" role="cell">{d.key}</span>
            <span className="bar-track" role="presentation"><span className="bar" style={{ width: `${(d.value / max) * 100}%` }} /></span>
            <span className="bar-value" role="cell">{d.value}</span>
          </div>
        ))}
      </div>
      {!keepOrder && data.length > 12 && (
        <button className="link small no-print" onClick={() => setAll(a => !a)}>{all ? 'Show top 12' : `Show all ${data.length}`}</button>
      )}
    </section>
  )
}
