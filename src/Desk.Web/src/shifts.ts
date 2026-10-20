import type { Shift } from './types'

// Shift helpers shared by the handover and on-duty pages (mirrors HandoverRules.cs).

export const SHIFTS: Shift[] = ['Morning', 'Afternoon', 'Overnight']
export const HOURS: Record<Shift, string> = { Morning: '5 am - 1 pm', Afternoon: '1 pm - 9 pm', Overnight: '9 pm - 5 am' }
const NEXT: Record<Shift, [number, Shift]> = { Morning: [0, 'Afternoon'], Afternoon: [0, 'Overnight'], Overnight: [1, 'Morning'] }

export function addDays(date: string, n: number) {
  const d = new Date(`${date}T12:00:00`)
  d.setDate(d.getDate() + n)
  return d.toISOString().slice(0, 10)
}

export function nextShift(date: string, shift: Shift): [string, Shift] {
  const [days, s] = NEXT[shift]
  return [addDays(date, days), s]
}

// Default start and end ("YYYY-MM-DDTHH:mm") of a shift on a date.
export function shiftTimes(date: string, shift: Shift): [string, string] {
  if (shift === 'Morning') return [`${date}T05:00`, `${date}T13:00`]
  if (shift === 'Afternoon') return [`${date}T13:00`, `${date}T21:00`]
  return [`${date}T21:00`, `${addDays(date, 1)}T05:00`]
}
