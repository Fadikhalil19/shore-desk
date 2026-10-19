import type { IncidentDetails, IncidentForm, IncidentStatus, Me } from '../../types'

// Mirrors src/Desk.Core/Incidents/IncidentRules.cs (the server re-checks everything).

export const FORMS: { form: IncidentForm; name: string; short: string; module: string; hint: string }[] = [
  { form: 'Incident', name: 'Incident', short: 'IR', module: 'incidents', hint: 'Disturbances, alarms, evictions, hazards and everything else.' },
  { form: 'GuestInjury', name: 'Guest Accident/Illness', short: 'GAI', module: 'incidents', hint: 'A guest or visitor is hurt or ill.' },
  { form: 'PropertyMissingDamaged', name: 'Property Missing/Damaged', short: 'MD', module: 'incidents', hint: 'Items missing or damaged, with claim values.' },
  { form: 'EmployeeInjury', name: 'Employee Accident/Illness', short: 'EAI', module: 'incidents-employee', hint: "An employee is hurt or ill (workers' comp)." },
  { form: 'HighLevel', name: 'High Level', short: 'HI', module: 'incidents-highlevel', hint: 'Assault, robbery, arrest, threats and other serious events.' },
]

export const formInfo = (f: IncidentForm) => FORMS.find(x => x.form === f)!

export const STATUS: Record<IncidentStatus, string> = {
  Draft: 'Draft', AwaitingReview: 'Waiting for review', Approved: 'Approved', Voided: 'Voided',
}

export const typeList = (f: IncidentForm) =>
  f === 'Incident' ? 'incident-type' : f === 'HighLevel' ? 'highlevel-type' : null

export const kinds = (f: IncidentForm) =>
  f === 'GuestInjury' || f === 'EmployeeInjury' ? ['Accident', 'Illness'] : f === 'PropertyMissingDamaged' ? ['Missing', 'Damaged'] : []

export const isInjury = (f: IncidentForm) => f === 'GuestInjury' || f === 'EmployeeInjury'

// Which optional sections a form offers (as on the MSShift forms).
export const offers = (f: IncidentForm) => ({
  lock: f === 'Incident' || f === 'PropertyMissingDamaged',
  items: f === 'PropertyMissingDamaged' || f === 'HighLevel',
  victims: f === 'HighLevel',
  medical: isInjury(f),
})

export const PERSON_TYPES = ['Guest', 'Non Guest', 'Employee', 'Hotel Property', 'Pedestrian', 'N/A']

export function emptyDetails(): IncidentDetails {
  return { subjects: [], victims: [], witnesses: [], lockInterrogation: [], propertyItems: [] }
}

// The person's level in the module behind a form ('None' when they have no access).
export function levelFor(me: Me, f: IncidentForm) {
  return me.modules.find(m => m.key === formInfo(f).module)?.level ?? 'None'
}
