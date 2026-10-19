// Times from the API are UTC; people read them in Pacific time.
export function formatDateTime(utc: string | null | undefined): string {
  if (!utc) return ''
  const d = new Date(utc.endsWith('Z') ? utc : utc + 'Z')
  return d.toLocaleString('en-US', {
    timeZone: 'America/Los_Angeles', month: 'short', day: 'numeric', year: 'numeric',
    hour: 'numeric', minute: '2-digit',
  })
}

// Report times are hotel (Pacific) wall-clock times with no zone: shown as typed.
export function formatLocal(local: string | null | undefined): string {
  if (!local) return ''
  const [d, t] = local.split('T')
  const [y, m, day] = d.split('-').map(Number)
  const date = new Date(y, m - 1, day).toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' })
  if (!t) return date
  const [h, min] = t.split(':').map(Number)
  const hour = h % 12 === 0 ? 12 : h % 12
  return `${date}, ${hour}:${String(min).padStart(2, '0')} ${h < 12 ? 'AM' : 'PM'}`
}

// Values for <input type="datetime-local"> and <input type="date"> from an API local time.
export const toInput = (local: string | null | undefined) => (local ? local.slice(0, 16) : '')
export const toDateInput = (local: string | null | undefined) => (local ? local.slice(0, 10) : '')

// Now in Pacific time as "YYYY-MM-DDTHH:mm", the default on new reports.
export function nowPacific(): string {
  const p = new Intl.DateTimeFormat('en-CA', {
    timeZone: 'America/Los_Angeles', year: 'numeric', month: '2-digit', day: '2-digit',
    hour: '2-digit', minute: '2-digit', hourCycle: 'h23',
  }).formatToParts(new Date())
  const v = (k: string) => p.find(x => x.type === k)!.value
  return `${v('year')}-${v('month')}-${v('day')}T${v('hour')}:${v('minute')}`
}

export function fileSize(bytes: number): string {
  return bytes < 1024 * 1024 ? `${Math.max(1, Math.round(bytes / 1024))} KB` : `${(bytes / 1024 / 1024).toFixed(1)} MB`
}

// Splits "SignIn" into "Sign in" for display.
export function words(s: string): string {
  const spaced = s.replace(/([a-z])([A-Z])/g, '$1 $2')
  return spaced.charAt(0) + spaced.slice(1).toLowerCase()
}
