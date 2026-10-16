// Times from the API are UTC; people read them in Pacific time.
export function formatDateTime(utc: string | null | undefined): string {
  if (!utc) return ''
  const d = new Date(utc.endsWith('Z') ? utc : utc + 'Z')
  return d.toLocaleString('en-US', {
    timeZone: 'America/Los_Angeles', month: 'short', day: 'numeric', year: 'numeric',
    hour: 'numeric', minute: '2-digit',
  })
}

// Splits "SignIn" into "Sign in" for display.
export function words(s: string): string {
  const spaced = s.replace(/([a-z])([A-Z])/g, '$1 $2')
  return spaced.charAt(0) + spaced.slice(1).toLowerCase()
}
