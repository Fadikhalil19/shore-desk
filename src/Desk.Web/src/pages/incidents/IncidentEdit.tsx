import { useEffect, useMemo, useState } from 'react'
import { Link, useNavigate, useParams, useSearchParams } from 'react-router-dom'
import { api, ApiError } from '../../api'
import { useSession } from '../../App'
import { nowPacific } from '../../format'
import type {
  Incident, IncidentDetails, IncidentForm, IncidentSave, LockEntry, MedicalEntry, Person, PropertyItem, Shift, VehicleEntry,
} from '../../types'
import { Choice, DepartmentPick, Note, PeopleList, PersonFields, Pick, Section, Text, UserPick, When } from './fields'
import { emptyDetails, formInfo, kinds, levelFor, offers, typeList } from './meta'

// New report (/incidents/new?form=Incident) or edit (/incidents/12/edit).
// Officers can save a half-finished draft at any time and finish it later.
export default function IncidentEdit() {
  const { id } = useParams()
  const [params] = useSearchParams()
  const { me, lookups } = useSession()
  const navigate = useNavigate()

  const [r, setR] = useState<IncidentSave | null>(null)
  const [number, setNumber] = useState<string | null>(null)
  const [returnNote, setReturnNote] = useState<string | null>(null)
  const [people, setPeople] = useState<Person[]>([])
  const [reviewers, setReviewers] = useState<Person[]>([])
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  useEffect(() => { api.get<Person[]>('/api/people').then(setPeople).catch(() => setPeople([])) }, [])

  useEffect(() => {
    if (id) {
      api.get<Incident>(`/api/incidents/${id}`).then(i => {
        if (!i.can.edit) { navigate(`/incidents/${i.id}`, { replace: true }); return }
        setNumber(i.number)
        setReturnNote(i.status === 'Draft' ? i.reviewNote : null)
        setR({
          form: i.form, hotelId: i.hotelId, typeId: i.typeId, kind: i.kind, otherType: i.otherType, heading: i.heading,
          occurredLocal: i.occurredLocal, reportedLocal: i.reportedLocal, shift: i.shift, areaId: i.areaId,
          locationId: i.locationId, locationDetail: i.locationDetail, room: i.room, narrative: i.narrative,
          followUp: i.followUp, details: { ...emptyDetails(), ...i.details }, supervisorId: i.supervisorId,
          distributedTo: i.distributedTo,
        })
      }).catch(e => setError(e.message))
    } else {
      const form = (params.get('form') ?? 'Incident') as IncidentForm
      const limit = me.modules.find(m => m.key === formInfo(form).module)?.hotelId
      const now = nowPacific()
      setR({
        form, hotelId: limit ?? me.hotelId ?? lookups.hotels[0].id, typeId: null, kind: null, otherType: null, heading: '',
        occurredLocal: now, reportedLocal: now, shift: null, areaId: null, locationId: null, locationDetail: null,
        room: null, narrative: null, followUp: null,
        details: { ...emptyDetails(), subjects: form === 'EmployeeInjury' || form === 'GuestInjury' ? [{}] : [] },
        supervisorId: null, distributedTo: null,
      })
    }
  }, [id, params, me, lookups, navigate])

  const form = r?.form
  const hotelId = r?.hotelId
  useEffect(() => {
    if (!form || !hotelId) return
    api.get<Person[]>(`/api/incidents/reviewers?form=${form}&hotelId=${hotelId}`).then(setReviewers).catch(() => setReviewers([]))
  }, [form, hotelId])

  const hotels = useMemo(() => {
    if (!form) return []
    const limit = me.modules.find(m => m.key === formInfo(form).module)?.hotelId
    return lookups.hotels.filter(h => (h.isActive || h.id === hotelId) && (limit == null || h.id === limit))
  }, [form, hotelId, me, lookups])

  if (error && !r) return <p className="alert">{error}</p>
  if (!r) return <p className="muted">Loading...</p>
  if (!id && !['Edit', 'Manage'].includes(levelFor(me, r.form)))
    return <p className="alert">You cannot write {formInfo(r.form).name} reports. Ask an admin for access.</p>

  const up = <K extends keyof IncidentSave>(k: K, v: IncidentSave[K]) => setR({ ...r, [k]: v })
  const d = r.details
  const upD = <K extends keyof IncidentDetails>(k: K, v: IncidentDetails[K]) => setR({ ...r, details: { ...d, [k]: v } })
  const has = offers(r.form)
  const info = formInfo(r.form)
  const areas = lookups.areas.filter(a => (a.isActive || a.id === r.areaId) && (a.hotelId == null || a.hotelId === r.hotelId))
  const area = lookups.areas.find(a => a.id === r.areaId)

  const save = async (andSubmit: boolean) => {
    setBusy(true); setError(null)
    try {
      let reportId = id ? Number(id) : null
      if (reportId) await api.put(`/api/incidents/${reportId}`, r)
      else reportId = (await api.post<{ id: number }>('/api/incidents', r)).id
      if (andSubmit) {
        try { await api.post(`/api/incidents/${reportId}/submit`) }
        catch (e) {
          // Saved, but not complete: the report page lists what is missing.
          navigate(`/incidents/${reportId}`, { state: { message: (e as Error).message } })
          return
        }
      }
      navigate(`/incidents/${reportId}`)
    } catch (e) {
      setError(e instanceof ApiError ? e.message : (e as Error).message)
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="stack incident-form">
      <div className="spread">
        <h1>{number ? `Report ${number}` : 'New report'}: {info.name}</h1>
        <Link to={id ? `/incidents/${id}` : '/incidents'}>Cancel</Link>
      </div>
      {returnNote && <p className="notice"><b>Supervisor's note:</b> {returnNote}</p>}

      <Section title="Report" on>
        {hotels.length > 1 && (
          <label className="field">
            <span>Hotel<b className="req"> *</b></span>
            <select value={r.hotelId} onChange={e => setR({ ...r, hotelId: Number(e.target.value), areaId: null, locationId: null, supervisorId: null })}>
              {hotels.map(h => <option key={h.id} value={h.id}>{h.code} - {h.name}</option>)}
            </select>
          </label>
        )}
        {typeList(r.form) && <Pick label="Type" list={typeList(r.form)!} required value={r.typeId} onChange={v => up('typeId', v)} />}
        {kinds(r.form).length > 0 && <Choice label="Type" required options={kinds(r.form)} value={r.kind} onChange={v => up('kind', v ?? null)} />}
        <Text label="Other type" value={r.otherType} onChange={v => up('otherType', v ?? null)} />
        <Text label="Heading (short title)" required wide value={r.heading} onChange={v => up('heading', v ?? '')}
          placeholder="e.g. Noise complaint, room 214" />
      </Section>

      <Section title="When and where" on>
        <When label="Happened" required value={r.occurredLocal} onChange={v => up('occurredLocal', v ?? null)} />
        <When label="Reported to security" value={r.reportedLocal} onChange={v => up('reportedLocal', v ?? null)} />
        <Choice label="Shift" options={['Morning', 'Afternoon', 'Overnight']} value={r.shift}
          onChange={v => up('shift', (v as Shift) ?? null)} />
        <label className="field">
          <span>Location<b className="req"> *</b></span>
          <select value={r.areaId ?? ''} onChange={e => setR({ ...r, areaId: e.target.value ? Number(e.target.value) : null, locationId: null })}>
            <option value="">-</option>
            {areas.map(a => <option key={a.id} value={a.id}>{a.name}</option>)}
          </select>
        </label>
        {area && area.locations.length > 0 && (
          <label className="field">
            <span>Spot</span>
            <select value={r.locationId ?? ''} onChange={e => up('locationId', e.target.value ? Number(e.target.value) : null)}>
              <option value="">-</option>
              {area.locations.filter(l => l.isActive || l.id === r.locationId).map(l => <option key={l.id} value={l.id}>{l.name}</option>)}
            </select>
          </label>
        )}
        <Text label="Location detail" value={r.locationDetail} onChange={v => up('locationDetail', v ?? null)} placeholder="e.g. by the ice machine" />
        <Text label="Room #" value={r.room} onChange={v => up('room', v ?? null)} maxLength={40} />
      </Section>

      <Section title="Reported to security by" on={!!d.reportedBy} onToggle={on => upD('reportedBy', on ? {} : null)}>
        {d.reportedBy && <PersonFields value={d.reportedBy} people={people} onChange={p => upD('reportedBy', p)}
          show={{ types: ['Guest', 'Non Guest', 'Employee'], stay: true, contact: true }} />}
      </Section>

      {r.form === 'EmployeeInjury' ? (
        <Section title="Injured employee" on>
          <PersonFields value={d.subjects[0] ?? {}} people={people} onChange={p => upD('subjects', [p, ...d.subjects.slice(1)])}
            show={{ employee: true, demographics: true, contact: true, home: true }} />
        </Section>
      ) : r.form === 'GuestInjury' ? (
        <Section title="Injured or ill person" on>
          <PersonFields value={d.subjects[0] ?? {}} people={people} onChange={p => upD('subjects', [p, ...d.subjects.slice(1)])}
            show={{ types: ['Guest', 'Non Guest', 'Pedestrian'], stay: true, demographics: true, contact: true, home: true }} />
        </Section>
      ) : (
        <Section title="Subject" on={d.subjects.length > 0} onToggle={on => upD('subjects', on ? [{}] : [])}
          hint="The person (or hotel property) the report is about. Add more people if needed.">
          <PeopleList noun="Subject" list={d.subjects} onChange={l => upD('subjects', l)} people={people}
            show={r.form === 'HighLevel'
              ? { stay: true, contact: true, home: true, demographics: true, description: true }
              : { stay: true, contact: true, home: true, work: true }} />
        </Section>
      )}

      {has.victims && (
        <Section title="Victim" on={d.victims.length > 0} onToggle={on => upD('victims', on ? [{}] : [])}>
          <PeopleList noun="Victim" list={d.victims} onChange={l => upD('victims', l)} people={people}
            show={{ stay: true, contact: true, home: true, demographics: true, description: true }} />
        </Section>
      )}

      {has.medical && <MedicalSection value={d.medical ?? {}} employee={r.form === 'EmployeeInjury'} onChange={m => upD('medical', m)} />}

      {has.items && (
        <Section title={r.form === 'HighLevel' ? 'Hotel property involved' : 'Items'} on={r.form !== 'HighLevel' || d.propertyItems.length > 0}
          onToggle={r.form === 'HighLevel' ? on => upD('propertyItems', on ? [{}] : []) : undefined}>
          <ItemsEditor list={d.propertyItems} onChange={l => upD('propertyItems', l)} />
        </Section>
      )}

      <Section title="What happened" on>
        <Note label="Statement of facts" required rows={8} value={r.narrative} onChange={v => up('narrative', v ?? null)}
          hint="Who, what, when, where, how. Facts only." />
        <Note label="Investigation / follow up" rows={4} value={r.followUp} onChange={v => up('followUp', v ?? null)} />
      </Section>

      <Section title="Witnesses" on={d.witnesses.length > 0} onToggle={on => upD('witnesses', on ? [{}] : [])}>
        <PeopleList noun="Witness" list={d.witnesses} onChange={l => upD('witnesses', l)} people={people}
          show={{ stay: true, contact: true, home: true }} />
      </Section>

      <Section title="Vehicle" on={!!d.vehicle} onToggle={on => upD('vehicle', on ? {} : null)}>
        {d.vehicle && <VehicleFields value={d.vehicle} onChange={v => upD('vehicle', v)} />}
      </Section>

      {has.lock && (
        <Section title="Lock interrogation" on={d.lockInterrogation.length > 0} onToggle={on => upD('lockInterrogation', on ? [{}] : [])}
          hint="Who used a key on the door, from the lock's audit.">
          <LockRows list={d.lockInterrogation} people={people} onChange={l => upD('lockInterrogation', l)} />
        </Section>
      )}

      <Section title="Police" on={!!d.police} onToggle={on => upD('police', on ? {} : null)}>
        {d.police && <>
          <When label="Called / arrived" value={d.police.calledLocal} onChange={v => upD('police', { ...d.police, calledLocal: v })} />
          <Text label="Agency" value={d.police.agency} onChange={v => upD('police', { ...d.police, agency: v })} placeholder="e.g. Santa Monica PD" />
          <Text label="Officer name" value={d.police.officerName} onChange={v => upD('police', { ...d.police, officerName: v })} />
          <Text label="Shield / badge #" value={d.police.shield} onChange={v => upD('police', { ...d.police, shield: v })} maxLength={40} />
          <Text label="Case #" value={d.police.caseNumber} onChange={v => upD('police', { ...d.police, caseNumber: v })} maxLength={60} />
        </>}
      </Section>

      <Section title="Pictures / video" on={!!d.media} onToggle={on => upD('media', on ? {} : null)}
        hint="Add the photo and video files on the report page after saving.">
        {d.media && <>
          <Text label="Number of pictures" type="number" value={d.media.pictureCount?.toString()}
            onChange={v => upD('media', { ...d.media, pictureCount: v ? Number(v) : null })} />
          <When label="Pictures taken" value={d.media.picturesTakenLocal} onChange={v => upD('media', { ...d.media, picturesTakenLocal: v })} />
          <Text label="Taken by" value={d.media.takenBy} onChange={v => upD('media', { ...d.media, takenBy: v })} />
          <When label="Video date and time" value={d.media.videoLocal} onChange={v => upD('media', { ...d.media, videoLocal: v })} />
          <Text label="Video description" wide value={d.media.videoDescription} onChange={v => upD('media', { ...d.media, videoDescription: v })} maxLength={1000} />
        </>}
      </Section>

      <Section title="Evidence" on={!!d.evidence} onToggle={on => upD('evidence', on ? {} : null)}>
        {d.evidence && <>
          <Pick label="Status" list="evidence-status" value={d.evidence.statusId} onChange={v => upD('evidence', { ...d.evidence, statusId: v })} />
          <Text label="Items" wide value={d.evidence.items} onChange={v => upD('evidence', { ...d.evidence, items: v })} maxLength={1000} />
          <Text label="Where it is stored" value={d.evidence.storedLocation} onChange={v => upD('evidence', { ...d.evidence, storedLocation: v })} />
          <When label="Date" dateOnly value={d.evidence.date} onChange={v => upD('evidence', { ...d.evidence, date: v })} />
          <Text label="Disposition" value={d.evidence.disposition} onChange={v => upD('evidence', { ...d.evidence, disposition: v })} />
        </>}
      </Section>

      <Section title="Reported to claims" on={!!d.claim} onToggle={on => upD('claim', on ? {} : null)}>
        {d.claim && <>
          <When label="Reported" value={d.claim.reportedLocal} onChange={v => upD('claim', { ...d.claim, reportedLocal: v })} />
          <Text label="Claim #" value={d.claim.claimNumber} onChange={v => upD('claim', { ...d.claim, claimNumber: v })} maxLength={60} />
        </>}
      </Section>

      <Section title="Sign-off" on>
        <UserPick label="Supervisor (reviews this report)" required people={reviewers} value={r.supervisorId}
          onChange={v => up('supervisorId', v)} />
        {reviewers.length === 0 && <p className="warn small wide">Nobody manages {info.name} reports at this hotel yet. Ask an admin to give a supervisor Manage access.</p>}
        <Text label="Written by (if not you)" value={d.writtenByOther} onChange={v => upD('writtenByOther', v)} />
        <Text label="Report distributed to" wide value={r.distributedTo} onChange={v => up('distributedTo', v ?? null)} maxLength={400} />
      </Section>

      {error && <p className="alert">{error}</p>}
      <div className="row sticky-actions">
        <button disabled={busy || !r.heading} className="secondary" onClick={() => save(false)}>Save draft</button>
        <button disabled={busy || !r.heading} onClick={() => save(true)}>Save and send for review</button>
        {!r.heading && <span className="muted small">Type a heading to save.</span>}
      </div>
    </div>
  )
}

function MedicalSection({ value: m, employee, onChange }: { value: MedicalEntry; employee: boolean; onChange: (m: MedicalEntry) => void }) {
  const up = <K extends keyof MedicalEntry>(k: K, v: MedicalEntry[K]) => onChange({ ...m, [k]: v })
  return (
    <>
      <Section title="Injury or illness" on>
        <Choice label="Detail" options={['Incident only', 'Medical']} value={m.detail} onChange={v => up('detail', v)} />
        <Pick label="Injury type" list="injury-type" value={m.injuryTypeId} onChange={v => up('injuryTypeId', v)} />
        <Text label="Other injury" value={m.otherInjury} onChange={v => up('otherInjury', v)} />
        <Pick label="Body part" list="body-part" required value={m.bodyPartId} onChange={v => up('bodyPartId', v)} />
        <Pick label="Side / area" list="body-part-location" required value={m.bodyPartLocationId} onChange={v => up('bodyPartLocationId', v)} />
        <Pick label="Main contributing factor" list="contributing-factor" value={m.contributingFactorId} onChange={v => up('contributingFactorId', v)} />
        <Text label="Other factor" value={m.otherFactor} onChange={v => up('otherFactor', v)} />
        <When label="Food / drink consumed (illness)" value={m.consumedLocal} onChange={v => up('consumedLocal', v)} />
        <Text label="Symptoms" wide value={m.symptoms} onChange={v => up('symptoms', v)} maxLength={1000} />
        <label className="field check"><input type="checkbox" checked={!!m.refusedMedicalAttention}
          onChange={e => up('refusedMedicalAttention', e.target.checked)} /> Refused medical attention (RMA)</label>
      </Section>
      <Section title="Ambulance (EMS)" on={!!m.emsCalled} onToggle={on => up('emsCalled', on)}>
        <When label="EMS arrived" value={m.emsArrivedLocal} onChange={v => up('emsArrivedLocal', v)} />
        <Text label="EMS name" value={m.emsName} onChange={v => up('emsName', v)} />
        <Text label="EMS unit #" value={m.emsNumber} onChange={v => up('emsNumber', v)} maxLength={40} />
        <Text label="Taken to hospital" value={m.hospital} onChange={v => up('hospital', v)} />
      </Section>
      {employee && (
        <Section title="Safety shoes" on={m.safetyShoes === true} onToggle={on => up('safetyShoes', on)}>
          <Text label="Brand" value={m.safetyShoesBrand} onChange={v => up('safetyShoesBrand', v)} />
          <When label="Date purchased" dateOnly value={m.safetyShoesPurchased} onChange={v => up('safetyShoesPurchased', v)} />
        </Section>
      )}
    </>
  )
}

function ItemsEditor({ list, onChange }: { list: PropertyItem[]; onChange: (l: PropertyItem[]) => void }) {
  const total = list.reduce((s, i) => s + (i.claimValue ?? 0), 0)
  const set = (i: number, item: PropertyItem) => onChange(list.map((x, j) => (j === i ? item : x)))
  return (
    <div className="wide stack">
      {list.map((it, i) => (
        <fieldset key={i} className="sub">
          <legend>Item {i + 1}</legend>
          <div className="form">
            <Pick label="Category" list="property-category" value={it.categoryId} onChange={v => set(i, { ...it, categoryId: v })} />
            <Text label="Detail" value={it.detail} onChange={v => set(i, { ...it, detail: v })} />
            <Pick label="Color" list="color" value={it.colorId} onChange={v => set(i, { ...it, colorId: v })} />
            <Text label="Brand" value={it.brand} onChange={v => set(i, { ...it, brand: v })} />
            <Text label="Model / serial #" value={it.modelSerial} onChange={v => set(i, { ...it, modelSerial: v })} />
            <Text label="Claim value ($)" type="number" value={it.claimValue?.toString()}
              onChange={v => set(i, { ...it, claimValue: v ? Number(v) : null })} />
            <Text label="Description" wide value={it.description} onChange={v => set(i, { ...it, description: v })} maxLength={1000} />
          </div>
          <button type="button" className="link small" onClick={() => onChange(list.filter((_, j) => j !== i))}>Remove item</button>
        </fieldset>
      ))}
      <div className="spread">
        {list.length < 20 && <button type="button" className="secondary" onClick={() => onChange([...list, {}])}>Add item</button>}
        {total > 0 && <b>Total claim value: ${total.toLocaleString('en-US', { minimumFractionDigits: 2 })}</b>}
      </div>
    </div>
  )
}

function VehicleFields({ value: v, onChange }: { value: VehicleEntry; onChange: (v: VehicleEntry) => void }) {
  const up = <K extends keyof VehicleEntry>(k: K, x: VehicleEntry[K]) => onChange({ ...v, [k]: x })
  return (
    <>
      <Pick label="Circumstance" list="vehicle-circumstance" value={v.circumstanceId} onChange={x => up('circumstanceId', x)} />
      <Text label="Other circumstance" value={v.otherCircumstance} onChange={x => up('otherCircumstance', x)} />
      <Text label="Make" value={v.make} onChange={x => up('make', x)} maxLength={60} />
      <Text label="Model" value={v.model} onChange={x => up('model', x)} maxLength={60} />
      <Text label="Year" value={v.year} onChange={x => up('year', x)} maxLength={10} />
      <Pick label="Color" list="color" value={v.colorId} onChange={x => up('colorId', x)} />
      <Text label="Plate" value={v.plate} onChange={x => up('plate', x)} maxLength={20} />
      <Text label="Plate state" value={v.plateState} onChange={x => up('plateState', x)} maxLength={40} />
      <Text label="VIN" value={v.vin} onChange={x => up('vin', x)} maxLength={40} />
      <Text label="Valet ticket #" value={v.valetTicket} onChange={x => up('valetTicket', x)} maxLength={40} />
      <Text label="Driver name" value={v.operatorName} onChange={x => up('operatorName', x)} />
      <When label="Driver date of birth" dateOnly value={v.operatorDateOfBirth} onChange={x => up('operatorDateOfBirth', x)} />
      <Text label="Driver's license #" value={v.operatorLicense} onChange={x => up('operatorLicense', x)} maxLength={40} />
      <Text label="Driver phone" type="tel" value={v.operatorPhone} onChange={x => up('operatorPhone', x)} maxLength={40} />
      <Text label="Owner name" value={v.ownerName} onChange={x => up('ownerName', x)} />
      <Text label="Owner phone" type="tel" value={v.ownerPhone} onChange={x => up('ownerPhone', x)} maxLength={40} />
      <Text label="Owner address" wide value={v.ownerAddress} onChange={x => up('ownerAddress', x)} maxLength={400} />
      <Text label="Insurance company" value={v.insuranceCompany} onChange={x => up('insuranceCompany', x)} />
      <Text label="Rental company" value={v.rentalCompany} onChange={x => up('rentalCompany', x)} />
      <Text label="Damage (nature and extent)" wide value={v.damage} onChange={x => up('damage', x)} maxLength={1000} />
      <Text label="Description" wide value={v.description} onChange={x => up('description', x)} maxLength={1000} />
    </>
  )
}

function LockRows({ list, people, onChange }: { list: LockEntry[]; people: Person[]; onChange: (l: LockEntry[]) => void }) {
  const set = (i: number, e: LockEntry) => onChange(list.map((x, j) => (j === i ? e : x)))
  return (
    <div className="wide stack">
      {list.map((e, i) => (
        <fieldset key={i} className="sub">
          <legend>Key use {i + 1}</legend>
          <div className="form">
            <Choice label="Used by" options={['Guest', 'Employee']} value={e.usedByType} onChange={v => set(i, { ...e, usedByType: v })} />
            <Text label="Key name" value={e.keyName} onChange={v => set(i, { ...e, keyName: v })} />
            <When label="Date and time used" value={e.usedLocal} onChange={v => set(i, { ...e, usedLocal: v })} />
            {e.usedByType === 'Employee' && <>
              <DepartmentPick value={e.departmentId} onChange={v => set(i, { ...e, departmentId: v })} />
              <UserPick label="Employee" people={people} value={e.userId} onChange={v => set(i, { ...e, userId: v })} />
            </>}
            <Text label="Name" value={e.name} onChange={v => set(i, { ...e, name: v })} />
          </div>
          <button type="button" className="link small" onClick={() => onChange(list.filter((_, j) => j !== i))}>Remove row</button>
        </fieldset>
      ))}
      {list.length < 10 && <div><button type="button" className="secondary" onClick={() => onChange([...list, {}])}>Add key use</button></div>}
    </div>
  )
}
