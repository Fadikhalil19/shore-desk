import { useCallback, useEffect, useState, type ReactNode } from 'react'
import { Link, useLocation, useParams } from 'react-router-dom'
import { api } from '../../api'
import { useSession } from '../../App'
import { fileSize, formatDateTime, formatLocal, words } from '../../format'
import type { Address, HistoryEntry, Incident, PersonEntry } from '../../types'
import { STATUS } from './meta'

// One report, read-only and printable, with the actions this person may take.
export default function IncidentView() {
  const { id } = useParams()
  const location = useLocation()
  const { lookups } = useSession()
  const [r, setR] = useState<Incident | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [message, setMessage] = useState<string | null>((location.state as { message?: string } | null)?.message ?? null)

  const load = useCallback(() => {
    api.get<Incident>(`/api/incidents/${id}`).then(setR).catch(e => setError(e.message))
  }, [id])
  useEffect(load, [load])

  if (error && !r) return <p className="alert">{error}</p>
  if (!r) return <p className="muted">Loading...</p>

  const hotel = lookups.hotels.find(h => h.id === r.hotelId)
  const item = (listId: number | null | undefined) => lookups.listItems.find(i => i.id === listId)?.name
  const dept = (deptId: number | null | undefined) => lookups.departments.find(x => x.id === deptId)?.name
  const d = r.details

  const act = async (fn: () => Promise<unknown>, done?: string) => {
    setError(null); setMessage(null)
    try { await fn(); if (done) setMessage(done); load() } catch (e) { setError((e as Error).message) }
  }

  return (
    <div className="stack report">
      <div className="print-only report-head">
        <b>{hotel?.name}</b>{hotel?.address && <> - {hotel.address}</>}
      </div>

      <div className="spread">
        <div>
          <p className="muted small no-print"><Link to="/incidents">Incident reports</Link></p>
          <h1>{r.number}: {r.heading || '(no heading)'}</h1>
          <p className="row">
            <span className={`pill st-${r.status}`}>{STATUS[r.status]}</span>
            <span>{r.formName}</span>
            <span className="muted">{hotel?.code}</span>
          </p>
        </div>
        <div className="row no-print">
          {r.can.edit && <Link className="button" to={`/incidents/${r.id}/edit`}>Edit</Link>}
          <button className="secondary" onClick={() => window.print()}>Print</button>
        </div>
      </div>

      {message && <p className="notice">{message}</p>}
      {error && <p className="alert">{error}</p>}
      {r.status === 'Draft' && r.reviewNote && <p className="notice"><b>Supervisor's note:</b> {r.reviewNote}</p>}
      {r.status === 'Voided' && <p className="alert"><b>Voided</b> {formatDateTime(r.voidedUtc)}: {r.voidReason}</p>}
      {r.missing.length > 0 && r.can.edit && (
        <p className="notice no-print"><b>Still to fill in before sending:</b> {r.missing.join(', ')}.</p>
      )}

      <Actions r={r} act={act} />

      <Block title="Report">
        <F k="Type" v={[r.typeName ?? r.kind, r.otherType].filter(Boolean).join(' - ')} />
        <F k="Happened" v={formatLocal(r.occurredLocal)} />
        <F k="Reported to security" v={formatLocal(r.reportedLocal)} />
        <F k="Shift" v={r.shift} />
        <F k="Location" v={[r.areaName, r.locationName, r.locationDetail].filter(Boolean).join(' - ')} />
        <F k="Room #" v={r.room} />
      </Block>

      {d.reportedBy && <Block title="Reported to security by"><PersonView p={d.reportedBy} dept={dept} /></Block>}
      {d.subjects.map((p, i) => (
        <Block key={`s${i}`} title={r.form === 'EmployeeInjury' ? 'Injured employee' : r.form === 'GuestInjury' ? 'Injured or ill person'
          : d.subjects.length > 1 ? `Subject ${i + 1}` : 'Subject'}>
          <PersonView p={p} dept={dept} />
        </Block>
      ))}
      {d.victims.map((p, i) => <Block key={`v${i}`} title={d.victims.length > 1 ? `Victim ${i + 1}` : 'Victim'}><PersonView p={p} dept={dept} /></Block>)}

      {d.medical && (r.form === 'GuestInjury' || r.form === 'EmployeeInjury') && (
        <Block title="Injury or illness">
          <F k="Detail" v={d.medical.detail} />
          <F k="Injury type" v={[item(d.medical.injuryTypeId), d.medical.otherInjury].filter(Boolean).join(' - ')} />
          <F k="Body part" v={[item(d.medical.bodyPartId), item(d.medical.bodyPartLocationId)].filter(Boolean).join(' - ')} />
          <F k="Contributing factor" v={[item(d.medical.contributingFactorId), d.medical.otherFactor].filter(Boolean).join(' - ')} />
          <F k="Consumed" v={formatLocal(d.medical.consumedLocal)} />
          <F k="Symptoms" v={d.medical.symptoms} />
          <F k="Refused medical attention" v={d.medical.refusedMedicalAttention ? 'Yes' : null} />
          <F k="Ambulance (EMS)" v={d.medical.emsCalled
            ? [formatLocal(d.medical.emsArrivedLocal), d.medical.emsName, d.medical.emsNumber && `unit ${d.medical.emsNumber}`,
              d.medical.hospital && `to ${d.medical.hospital}`].filter(Boolean).join(' - ') || 'Yes' : null} />
          <F k="Safety shoes" v={d.medical.safetyShoes ? [d.medical.safetyShoesBrand, formatLocal(d.medical.safetyShoesPurchased)].filter(Boolean).join(', ') || 'Yes' : null} />
        </Block>
      )}

      {d.propertyItems.length > 0 && (
        <Block title="Items">
          <div className="table-wrap wide">
            <table className="grid">
              <thead><tr><th>#</th><th>Item</th><th>Color</th><th>Brand / model</th><th>Description</th><th>Value</th></tr></thead>
              <tbody>
                {d.propertyItems.map((it, i) => (
                  <tr key={i}>
                    <td>{i + 1}</td>
                    <td>{[item(it.categoryId), it.detail].filter(Boolean).join(' - ')}</td>
                    <td>{item(it.colorId)}</td>
                    <td>{[it.brand, it.modelSerial].filter(Boolean).join(' / ')}</td>
                    <td>{it.description}</td>
                    <td className="nowrap">{it.claimValue != null ? `$${it.claimValue.toLocaleString('en-US', { minimumFractionDigits: 2 })}` : ''}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <F k="Total claim value" v={`$${d.propertyItems.reduce((s, i) => s + (i.claimValue ?? 0), 0).toLocaleString('en-US', { minimumFractionDigits: 2 })}`} />
        </Block>
      )}

      <Block title="Statement of facts"><p className="wide narrative">{r.narrative}</p></Block>
      {r.followUp && <Block title="Investigation / follow up"><p className="wide narrative">{r.followUp}</p></Block>}

      {d.witnesses.map((p, i) => <Block key={`w${i}`} title={d.witnesses.length > 1 ? `Witness ${i + 1}` : 'Witness'}><PersonView p={p} dept={dept} /></Block>)}

      {d.vehicle && (
        <Block title="Vehicle">
          <F k="Circumstance" v={[item(d.vehicle.circumstanceId), d.vehicle.otherCircumstance].filter(Boolean).join(' - ')} />
          <F k="Vehicle" v={[d.vehicle.year, item(d.vehicle.colorId), d.vehicle.make, d.vehicle.model].filter(Boolean).join(' ')} />
          <F k="Plate" v={[d.vehicle.plate, d.vehicle.plateState].filter(Boolean).join(' ')} />
          <F k="VIN" v={d.vehicle.vin} />
          <F k="Valet ticket #" v={d.vehicle.valetTicket} />
          <F k="Driver" v={[d.vehicle.operatorName, d.vehicle.operatorDateOfBirth && `born ${formatLocal(d.vehicle.operatorDateOfBirth)}`,
            d.vehicle.operatorLicense && `DL ${d.vehicle.operatorLicense}`, d.vehicle.operatorPhone].filter(Boolean).join(', ')} />
          <F k="Owner" v={[d.vehicle.ownerName, d.vehicle.ownerAddress, d.vehicle.ownerPhone].filter(Boolean).join(', ')} />
          <F k="Insurance / rental" v={[d.vehicle.insuranceCompany, d.vehicle.rentalCompany].filter(Boolean).join(' / ')} />
          <F k="Damage" v={d.vehicle.damage} />
          <F k="Description" v={d.vehicle.description} />
        </Block>
      )}

      {d.lockInterrogation.length > 0 && (
        <Block title="Lock interrogation">
          <div className="table-wrap wide">
            <table className="grid">
              <thead><tr><th>Used by</th><th>Key</th><th>When</th><th>Who</th></tr></thead>
              <tbody>
                {d.lockInterrogation.map((l, i) => (
                  <tr key={i}><td>{l.usedByType}</td><td>{l.keyName}</td><td>{formatLocal(l.usedLocal)}</td>
                    <td>{[l.name, dept(l.departmentId)].filter(Boolean).join(', ')}</td></tr>
                ))}
              </tbody>
            </table>
          </div>
        </Block>
      )}

      {d.police && (
        <Block title="Police">
          <F k="Called / arrived" v={formatLocal(d.police.calledLocal)} />
          <F k="Agency" v={d.police.agency} />
          <F k="Officer" v={[d.police.officerName, d.police.shield && `#${d.police.shield}`].filter(Boolean).join(' ')} />
          <F k="Case #" v={d.police.caseNumber} />
        </Block>
      )}
      {d.media && (
        <Block title="Pictures / video">
          <F k="Pictures" v={[d.media.pictureCount, formatLocal(d.media.picturesTakenLocal), d.media.takenBy && `by ${d.media.takenBy}`].filter(Boolean).join(', ')} />
          <F k="Video" v={[formatLocal(d.media.videoLocal), d.media.videoDescription].filter(Boolean).join(' - ')} />
        </Block>
      )}
      {d.evidence && (
        <Block title="Evidence">
          <F k="Status" v={item(d.evidence.statusId)} />
          <F k="Items" v={d.evidence.items} />
          <F k="Stored" v={[d.evidence.storedLocation, formatLocal(d.evidence.date)].filter(Boolean).join(', ')} />
          <F k="Disposition" v={d.evidence.disposition} />
        </Block>
      )}
      {d.claim && (
        <Block title="Reported to claims">
          <F k="Reported" v={formatLocal(d.claim.reportedLocal)} />
          <F k="Claim #" v={d.claim.claimNumber} />
        </Block>
      )}

      <Block title="Sign-off">
        <F k="Written by" v={[r.writtenByName, d.writtenByOther && `(for ${d.writtenByOther})`].filter(Boolean).join(' ')} />
        <F k="Supervisor" v={r.supervisorName} />
        <F k="Sent for review" v={formatDateTime(r.submittedUtc)} />
        <F k={r.status === 'Approved' ? 'Approved by' : 'Last reviewed by'} v={r.reviewedByName && `${r.reviewedByName}, ${formatDateTime(r.reviewedUtc)}`} />
        <F k="Distributed to" v={r.distributedTo} />
      </Block>

      <Files r={r} onChange={load} setError={setError} />
      {r.can.history && <History id={r.id} />}
    </div>
  )
}

function Block({ title, children }: { title: string; children: ReactNode }) {
  return <section className="card block"><h2>{title}</h2><dl className="facts">{children}</dl></section>
}

// One fact; left out entirely when empty.
function F({ k, v }: { k: string; v: ReactNode }) {
  if (v === null || v === undefined || v === '') return null
  return <><dt>{k}</dt><dd>{v}</dd></>
}

const addr = (a?: Address) => (a ? [a.street, a.city, a.state, a.zip, a.country].filter(Boolean).join(', ') : '')

function PersonView({ p, dept }: { p: PersonEntry; dept: (id: number | null | undefined) => string | undefined }) {
  const ds = p.description
  return (
    <>
      <F k="Name" v={[p.firstName, p.lastName].filter(Boolean).join(' ') || p.hotelProperty} />
      <F k="Type" v={p.type} />
      <F k="Department" v={[dept(p.departmentId), p.position].filter(Boolean).join(' - ')} />
      <F k="Employee #" v={p.employeeNumber} />
      <F k="Date of hire" v={formatLocal(p.dateOfHire)} />
      <F k="Room" v={p.room} />
      <F k="Stay" v={[formatLocal(p.arrivalDate), formatLocal(p.departureDate)].filter(Boolean).join(' to ')} />
      <F k="Group / company" v={p.company} />
      <F k="Gender" v={p.gender} />
      <F k="Date of birth" v={formatLocal(p.dateOfBirth)} />
      <F k="Phone" v={p.phone} />
      <F k="Email" v={p.email} />
      <F k="Home address" v={addr(p.home)} />
      <F k="Work address" v={addr(p.work)} />
      {ds && <F k="Description" v={[ds.race, ds.height, ds.weight && `${ds.weight}`, ds.eyes && `${ds.eyes} eyes`,
        ds.hair && `${ds.hair} hair`, ds.clothing && `wearing ${ds.clothing}`, ds.marks,
        ds.licenseNumber && `DL ${ds.licenseNumber} ${ds.licenseState ?? ''}`].filter(Boolean).join(', ')} />}
    </>
  )
}

function Actions({ r, act }: { r: Incident; act: (fn: () => Promise<unknown>, done?: string) => Promise<void> }) {
  const [mode, setMode] = useState<'return' | 'reopen' | 'void' | null>(null)
  const [text, setText] = useState('')
  const [confirm, setConfirm] = useState('')
  if (!r.can.submit && !r.can.review && !r.can.reopen && !r.can.void) return null

  const close = () => { setMode(null); setText(''); setConfirm('') }
  return (
    <div className="card no-print stack">
      <div className="row">
        {r.can.submit && <button onClick={() => act(() => api.post(`/api/incidents/${r.id}/submit`), 'Sent to the supervisor.')}>Send for review</button>}
        {r.can.review && <>
          <button onClick={() => act(() => api.post(`/api/incidents/${r.id}/approve`, { note: null }), 'Approved.')}>Approve</button>
          <button className="secondary" onClick={() => setMode('return')}>Send back</button>
        </>}
        {r.can.reopen && <button className="secondary" onClick={() => setMode('reopen')}>Reopen</button>}
        {r.can.void && <button className="link" onClick={() => setMode('void')}>Void report</button>}
      </div>
      {mode && (
        <div className="form">
          <label className="field wide">
            <span>{mode === 'return' ? 'What needs to change?' : mode === 'reopen' ? 'Why reopen it? (kept in the history)' : 'Why void it? (kept on the report)'}</span>
            <textarea rows={3} value={text} onChange={e => setText(e.target.value)} maxLength={1000} />
          </label>
          {mode === 'void' && (
            <label className="field">
              <span>Type the report number {r.number} to confirm</span>
              <input value={confirm} onChange={e => setConfirm(e.target.value)} />
            </label>
          )}
          <div className="row wide">
            <button className={mode === 'void' ? 'danger' : ''} disabled={!text.trim() || (mode === 'void' && confirm.trim() !== r.number)}
              onClick={async () => {
                const url = `/api/incidents/${r.id}/${mode}`
                await act(() => api.post(url, mode === 'void' ? { reason: text, confirmNumber: confirm } : { note: text }),
                  mode === 'return' ? 'Sent back to the writer.' : mode === 'reopen' ? 'Reopened.' : 'Voided.')
                close()
              }}>
              {mode === 'return' ? 'Send back' : mode === 'reopen' ? 'Reopen' : 'Void report'}
            </button>
            <button className="secondary" onClick={close}>Cancel</button>
          </div>
        </div>
      )}
    </div>
  )
}

function Files({ r, onChange, setError }: { r: Incident; onChange: () => void; setError: (e: string | null) => void }) {
  const [busy, setBusy] = useState(false)
  const [caption, setCaption] = useState('')
  if (r.attachments.length === 0 && !r.can.edit) return null
  const url = (aid: number, download = false) => `/api/incidents/${r.id}/attachments/${aid}${download ? '?download=true' : ''}`

  const upload = async (files: FileList | null) => {
    if (!files || files.length === 0) return
    const body = new FormData()
    for (const f of Array.from(files)) body.append('files', f)
    if (caption.trim()) body.append('caption', caption.trim())
    setBusy(true); setError(null)
    try { await api.post(`/api/incidents/${r.id}/attachments`, body); setCaption(''); onChange() }
    catch (e) { setError((e as Error).message) }
    finally { setBusy(false) }
  }

  return (
    <section className="card block">
      <h2>Photos and files</h2>
      <div className="thumbs">
        {r.attachments.map(a => (
          <figure key={a.id} className="thumb">
            {a.contentType.startsWith('image/') && a.inline
              ? <a href={url(a.id)} target="_blank" rel="noreferrer"><img src={url(a.id)} alt={a.caption ?? a.fileName} loading="lazy" /></a>
              : <a href={url(a.id, !a.inline)} target="_blank" rel="noreferrer" className="file">{a.fileName}</a>}
            <figcaption className="small">
              {a.caption && <>{a.caption}<br /></>}
              <span className="muted">{fileSize(a.sizeBytes)} - {a.uploadedBy}, {formatDateTime(a.uploadedUtc)}</span>
              {r.can.edit && <> <button className="link small no-print" onClick={async () => {
                try { await api.del(url(a.id)); onChange() } catch (e) { setError((e as Error).message) }
              }}>Remove</button></>}
            </figcaption>
          </figure>
        ))}
      </div>
      {r.can.edit && (
        <div className="form no-print">
          <label className="field">Caption (optional)<input value={caption} onChange={e => setCaption(e.target.value)} maxLength={400} /></label>
          <label className="field">
            <span>Add photos, video or documents</span>
            <input type="file" multiple disabled={busy} accept="image/*,video/*,.pdf,.txt,.docx,.xlsx,.heic,.m4a"
              onChange={e => { upload(e.target.files); e.target.value = '' }} />
          </label>
          {busy && <p className="muted small">Uploading...</p>}
        </div>
      )}
    </section>
  )
}

function History({ id }: { id: number }) {
  const [rows, setRows] = useState<HistoryEntry[] | null>(null)
  if (!rows) return <p className="no-print"><button className="link" onClick={() => api.get<HistoryEntry[]>(`/api/incidents/${id}/history`).then(setRows)}>Show history</button></p>
  return (
    <section className="card block no-print">
      <h2>History</h2>
      <div className="table-wrap">
        <table className="grid">
          <thead><tr><th>When</th><th>Who</th><th>What</th><th>Details</th></tr></thead>
          <tbody>
            {rows.map((h, i) => (
              <tr key={i}><td className="nowrap">{formatDateTime(h.utc)}</td><td>{h.userName}</td><td>{words(h.action)}</td>
                <td className="details">{h.details}</td></tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  )
}
