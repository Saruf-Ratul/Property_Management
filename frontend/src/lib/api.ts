import axios, { AxiosError, AxiosInstance } from 'axios'
import toast from 'react-hot-toast'

const TOKEN_KEY = 'pm.access_token'
const USER_KEY = 'pm.user'

export function getStoredToken(): string | null {
  return localStorage.getItem(TOKEN_KEY)
}

export function setStoredToken(token: string | null) {
  if (token) localStorage.setItem(TOKEN_KEY, token)
  else localStorage.removeItem(TOKEN_KEY)
}

export function getStoredUser<T = unknown>(): T | null {
  const raw = localStorage.getItem(USER_KEY)
  return raw ? JSON.parse(raw) as T : null
}

export function setStoredUser(user: unknown | null) {
  if (user) localStorage.setItem(USER_KEY, JSON.stringify(user))
  else localStorage.removeItem(USER_KEY)
}

export const api: AxiosInstance = axios.create({
  baseURL: '/api',
  headers: { 'Content-Type': 'application/json' },
})

api.interceptors.request.use(config => {
  const token = getStoredToken()
  if (token) config.headers.Authorization = `Bearer ${token}`
  return config
})

api.interceptors.response.use(
  res => res,
  (error: AxiosError<any>) => {
    if (error.response?.status === 401 && !window.location.pathname.endsWith('/login')) {
      setStoredToken(null)
      setStoredUser(null)
      window.location.assign('/login')
      return Promise.reject(error)
    }
    const message =
      (error.response?.data as any)?.error ||
      (error.response?.data as any)?.title ||
      error.message
    if (error.response?.status && error.response.status >= 500) {
      toast.error(`Server error: ${message}`)
    }
    return Promise.reject(error)
  },
)

export function unwrapError(e: unknown): string {
  const ax = e as AxiosError<any>
  return (
    ax.response?.data?.error ||
    ax.response?.data?.title ||
    ax.message ||
    'Request failed'
  )
}

export const tokenStorageKey = TOKEN_KEY
export const userStorageKey = USER_KEY
