import type { ReactNode } from 'react'
import { useSession } from '../../App'
import type { Address, Person, PersonDescription, PersonEntry } from '../../types'
import { PERSON_TYPES } from './meta'

// Small form fields for the incident form. Empty inputs become undefined so the
// saved report only holds what was filled in.

const val = (s: string) => (s === '' ? undefined : s)

export function Text({ label, value, onChange, type = 'text', wide, required, placeholder, maxLength = 200 }: {
  label: string; value: string | null | undefined; onChange: (v: string | undefined) => void
  type?: string; wide?: boolean; required?: boolean; placeholder?: string; maxLength?: number
}) {
  return (
    <label className={wide ? 'field wide' : 'field'}>
      <span>{label}{required && <b className="req"> *</b>}</span>
      <input type={type} value={value ?? ''} placeholder={placeholder} maxLength={maxLength}
        onChange={e => onChange(val(e.target.value))} />
    </label>
  )
}

export function Note({ label, value, onChange, rows = 5, required, max = 50000, hint }: {
  label: string; value: string | null | undefined; onChange: (v: string | undefined) => void
  rows?: number; required?: boolean; max?: number; hint?: string
}) {
  return (
    <label className="field wide">
      <span>{label}{required && <b className="req"> *</b>}</span>
      {hint && <span className="muted small">{hint}</span>}
      <textarea rows={rows} value={value ?? ''} maxLength={max} onChange={e => onChange(val(e.target.value))} />
      <span className="muted small counter">{(value ?? '').length.toLocaleString()} characters</span>
    </label>
  )
}

export function When({ label, value, onChange, dateOnly, required }: {
  label: string; value: string | null | undefined; onChange: (v: string | undefined) => void
  dateOnly?: boolean; required?: boolean
}) {
  const shown = value ? (dateOnly ? value.slice(0, 10) : value.slice(0, 16)) : ''
  return (
    <label className="field">
      <span>{label}{required && <b className="req"> *</b>}</span>
      <input type={dateOnly ? 'date' : 'datetime-local'} value={shown} onChange={e => onChange(val(e.target.value))} />
    </label>
  )
}

// A pick from one of the admin-edited report lists. Switched-off entries are only
// offered when the report already uses them.
export function Pick({ label, list, value, onChange, required }: {
  label: string; list: string; value: number | null | undefined; onChange: (v: number | null) => void; required?: boolean
}) {
  const { lookups } = useSession()
  const items = lookups.listItems.filter(i => i.listKey === list && (i.isActive || i.id === value))
  return (
    <label className="field">
      <span>{label}{required && <b className="req"> *</b>}</span>
      <select value={value ?? ''} onChange={e => onChange(e.target.value ? Number(e.target.value) : null)}>
        <option value="">-</option>
        {items.map(i => <option key={i.id} value={i.id}>{i.name}</option>)}
      </select>
    </label>
  )
}

export function Choice({ label, options, value, onChange, required }: {
  label: string; options: string[]; value: string | null | undefined; onChange: (v: string | undefined) => void; required?: boolean
}) {
  return (
    <label className="field">
      <span>{label}{required && <b className="req"> *</b>}</span>
      <select value={value ?? ''} onChange={e => onChange(val(e.target.value))}>
        <option value="">-</option>
        {options.map(o => <option key={o} value={o}>{o}</option>)}
      </select>
    </label>
  )
}

export function DepartmentPick({ value, onChange, label = 'Department' }: {
  value: number | null | undefined; onChange: (v: number | null) => void; label?: string
}) {
  const { lookups } = useSession()
  return (
    <label className="field">
      <span>{label}</span>
      <select value={value ?? ''} onChange={e => onChange(e.target.value ? Number(e.target.value) : null)}>
        <option value="">-</option>
        {lookups.departments.filter(d => d.isActive || d.id === value).map(d => <option key={d.id} value={d.id}>{d.name}</option>)}
      </select>
    </label>
  )
}

export function UserPick({ label, people, value, onChange, required }: {
  label: string; people: Person[]; value: number | null | undefined; onChange: (v: number | null) => void; required?: boolean
}) {
  return (
    <label className="field">
      <span>{label}{required && <b className="req"> *</b>}</span>
      <select value={value ?? ''} onChange={e => onChange(e.target.value ? Number(e.target.value) : null)}>
        <option value="">-</option>
        {people.map(p => <option key={p.id} value={p.id}>{p.name}</option>)}
      </select>
    </label>
  )
}

// A Yes/No question that opens a block of fields, as on the MSShift forms.
export function Section({ title, on, onToggle, children, hint }: {
  title: string; on: boolean; onToggle?: (on: boolean) => void; children: ReactNode; hint?: string
}) {
  return (
    <section className={on ? 'card form-section open' : 'card form-section'}>
      <div className="spread">
        <h2>{title}</h2>
        {onToggle && (
          <div className="yesno" role="group" aria-label={title}>
            <button type="button" className={on ? 'tab' : 'tab active'} aria-pressed={!on} onClick={() => onToggle(false)}>No</button>
            <button type="button" className={on ? 'tab active' : 'tab'} aria-pressed={on} onClick={() => onToggle(true)}>Yes</button>
          </div>
        )}
      </div>
      {hint && <p className="muted small">{hint}</p>}
      {on && <div className="form">{children}</div>}
    </section>
  )
}

// Which parts of a person to ask for. Names and type are always asked.
export interface PersonShow {
  types?: string[]; employee?: boolean; stay?: boolean; contact?: boolean; home?: boolean; work?: boolean
  demographics?: boolean; description?: boolean
}

export function PersonFields({ value, onChange, show, people }: {
  value: PersonEntry; onChange: (p: PersonEntry) => void; show: PersonShow; people: Person[]
}) {
  const up = <K extends keyof PersonEntry>(k: K, v: PersonEntry[K]) => onChange({ ...value, [k]: v })
  const isEmployee = show.employee || value.type === 'Employee'
  return (
    <>
      {show.types && <Choice label="Type" options={show.types} value={value.type} onChange={v => up('type', v)} />}
      {isEmployee && (
        <UserPick label="Employee (if in Shore Desk)" people={people} value={value.userId} onChange={id => {
          const p = people.find(x => x.id === id)
          const [first, ...rest] = (p?.name ?? '').split(' ')
          onChange({ ...value, userId: id, firstName: p ? first : value.firstName, lastName: p ? rest.join(' ') : value.lastName,
            departmentId: p?.departmentId ?? value.departmentId })
        }} />
      )}
      <Text label="First name" value={value.firstName} onChange={v => up('firstName', v)} maxLength={100} />
      <Text label="Last name" value={value.lastName} onChange={v => up('lastName', v)} maxLength={100} />
      {value.type === 'Hotel Property' && <Text label="Hotel property" value={value.hotelProperty} onChange={v => up('hotelProperty', v)} />}
      {isEmployee && <>
        <DepartmentPick value={value.departmentId} onChange={v => up('departmentId', v)} />
        {show.employee && <>
          <Text label="Position" value={value.position} onChange={v => up('position', v)} />
          <Text label="Employee #" value={value.employeeNumber} onChange={v => up('employeeNumber', v)} maxLength={40} />
          <When label="Date of hire" dateOnly value={value.dateOfHire} onChange={v => up('dateOfHire', v)} />
        </>}
      </>}
      {show.stay && value.type !== 'Employee' && <>
        <Text label="Room #" value={value.room} onChange={v => up('room', v)} maxLength={40} />
        <When label="Arrival" dateOnly value={value.arrivalDate} onChange={v => up('arrivalDate', v)} />
        <When label="Departure" dateOnly value={value.departureDate} onChange={v => up('departureDate', v)} />
        <Text label="Group / company" value={value.company} onChange={v => up('company', v)} />
      </>}
      {show.demographics && <>
        <Choice label="Gender" options={['Female', 'Male', 'Other / not stated']} value={value.gender} onChange={v => up('gender', v)} />
        <When label="Date of birth" dateOnly value={value.dateOfBirth} onChange={v => up('dateOfBirth', v)} />
      </>}
      {show.contact && <>
        <Text label="Phone" type="tel" value={value.phone} onChange={v => up('phone', v)} maxLength={40} />
        <Text label="Email" type="email" value={value.email} onChange={v => up('email', v)} />
      </>}
      {show.home && <AddressFields label="Home address" value={value.home} onChange={a => up('home', a)} />}
      {show.work && <AddressFields label="Work address" value={value.work} onChange={a => up('work', a)} />}
      {show.description && <DescriptionFields value={value.description} onChange={d => up('description', d)} />}
    </>
  )
}

function AddressFields({ label, value, onChange }: { label: string; value?: Address; onChange: (a: Address) => void }) {
  const a = value ?? {}
  const up = (k: keyof Address, v: string | undefined) => onChange({ ...a, [k]: v })
  return (
    <fieldset className="wide sub">
      <legend>{label}</legend>
      <div className="form">
        <Text label="Street" value={a.street} onChange={v => up('street', v)} wide />
        <Text label="City" value={a.city} onChange={v => up('city', v)} />
        <Text label="State" value={a.state} onChange={v => up('state', v)} maxLength={60} />
        <Text label="Zip" value={a.zip} onChange={v => up('zip', v)} maxLength={20} />
        <Text label="Country" value={a.country} onChange={v => up('country', v)} maxLength={60} />
      </div>
    </fieldset>
  )
}

function DescriptionFields({ value, onChange }: { value?: PersonDescription; onChange: (d: PersonDescription) => void }) {
  const d = value ?? {}
  const up = (k: keyof PersonDescription, v: string | undefined) => onChange({ ...d, [k]: v })
  return (
    <fieldset className="wide sub">
      <legend>Description</legend>
      <div className="form">
        <Text label="Race" value={d.race} onChange={v => up('race', v)} maxLength={60} />
        <Text label="Height" value={d.height} onChange={v => up('height', v)} maxLength={20} />
        <Text label="Weight" value={d.weight} onChange={v => up('weight', v)} maxLength={20} />
        <Text label="Eyes" value={d.eyes} onChange={v => up('eyes', v)} maxLength={40} />
        <Text label="Hair" value={d.hair} onChange={v => up('hair', v)} maxLength={40} />
        <Text label="Driver's license #" value={d.licenseNumber} onChange={v => up('licenseNumber', v)} maxLength={40} />
        <Text label="License state" value={d.licenseState} onChange={v => up('licenseState', v)} maxLength={40} />
        <Text label="Clothing worn" value={d.clothing} onChange={v => up('clothing', v)} wide />
        <Text label="Scars, marks, tattoos" value={d.marks} onChange={v => up('marks', v)} wide />
      </div>
    </fieldset>
  )
}

// A list of people (subjects, witnesses, victims) with add / remove.
export function PeopleList({ noun, list, onChange, show, people, max = 10 }: {
  noun: string; list: PersonEntry[]; onChange: (l: PersonEntry[]) => void; show: PersonShow; people: Person[]; max?: number
}) {
  return (
    <div className="wide stack">
      {list.map((p, i) => (
        <fieldset key={i} className="sub">
          <legend>{noun} {list.length > 1 ? i + 1 : ''}</legend>
          <div className="form">
            <PersonFields value={p} people={people} show={{ types: PERSON_TYPES, ...show }}
              onChange={np => onChange(list.map((x, j) => (j === i ? np : x)))} />
          </div>
          <button type="button" className="link small" onClick={() => onChange(list.filter((_, j) => j !== i))}>Remove {noun.toLowerCase()}</button>
        </fieldset>
      ))}
      {list.length < max && (
        <div><button type="button" className="secondary" onClick={() => onChange([...list, {}])}>Add {noun.toLowerCase()}</button></div>
      )}
    </div>
  )
}
