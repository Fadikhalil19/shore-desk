// Small wrapper around fetch for the Shore Desk API.
// The sign-in cookie is sent automatically (same site). Every write carries the
// X-CSRF header the server requires.

export class ApiError extends Error {
  status: number
  errors?: Record<string, string[]>
  constructor(status: number, message: string, errors?: Record<string, string[]>) {
    super(message)
    this.status = status
    this.errors = errors
  }
}

async function request<T>(method: string, url: string, body?: unknown): Promise<T> {
  const headers: Record<string, string> = { Accept: 'application/json' }
  let payload: BodyInit | undefined
  if (method !== 'GET') headers['X-CSRF'] = '1'
  if (body instanceof FormData) {
    payload = body
  } else if (body !== undefined) {
    headers['Content-Type'] = 'application/json'
    payload = JSON.stringify(body)
  }

  const res = await fetch(url, { method, headers, body: payload, credentials: 'same-origin' })

  if (res.status === 401) {
    // Session expired: go through Microsoft sign-in again, then come back.
    window.location.href = `/auth/login?returnUrl=${encodeURIComponent(window.location.pathname + window.location.search)}`
    throw new ApiError(401, 'Signing in again...')
  }
  if (!res.ok) {
    let message = `Request failed (${res.status})`
    let errors: Record<string, string[]> | undefined
    try {
      const data = await res.json()
      message = data.message ?? data.title ?? message
      errors = data.errors
    } catch {
      /* not JSON */
    }
    if (res.status === 403) message = 'You do not have permission to do that.'
    if (res.status === 404) message = 'Not found, or you do not have access to it.'
    throw new ApiError(res.status, message, errors)
  }
  if (res.status === 204) return undefined as T
  const text = await res.text()
  return (text ? JSON.parse(text) : undefined) as T
}

export const api = {
  get: <T,>(url: string) => request<T>('GET', url),
  post: <T,>(url: string, body?: unknown) => request<T>('POST', url, body),
  put: <T,>(url: string, body?: unknown) => request<T>('PUT', url, body),
  del: <T,>(url: string) => request<T>('DELETE', url),
}
