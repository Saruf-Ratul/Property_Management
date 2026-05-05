import { createContext, useCallback, useContext, useEffect, useMemo, useState, ReactNode } from 'react'
import { api, getStoredToken, getStoredUser, setStoredToken, setStoredUser, unwrapError } from './api'
import type { AuthResponse, Role, User } from '@/types'
import toast from 'react-hot-toast'

interface AuthContextValue {
  user: User | null
  isAuthenticated: boolean
  isLoading: boolean
  login: (email: string, password: string) => Promise<User>
  logout: () => void
  hasAnyRole: (roles: Role[]) => boolean
  isClientUser: boolean
  isFirmStaff: boolean
}

const AuthContext = createContext<AuthContextValue | null>(null)

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<User | null>(() => getStoredUser<User>())
  const [isLoading, setIsLoading] = useState(false)

  useEffect(() => {
    const token = getStoredToken()
    if (token && !user) {
      setIsLoading(true)
      api.get<User>('/auth/me')
        .then(r => { setUser(r.data); setStoredUser(r.data) })
        .catch(() => { setStoredToken(null); setStoredUser(null) })
        .finally(() => setIsLoading(false))
    }
  }, [])

  const login = useCallback(async (email: string, password: string) => {
    setIsLoading(true)
    try {
      const r = await api.post<AuthResponse>('/auth/login', { email, password })
      setStoredToken(r.data.accessToken)
      setStoredUser(r.data.user)
      setUser(r.data.user)
      return r.data.user
    } catch (e) {
      toast.error(unwrapError(e))
      throw e
    } finally {
      setIsLoading(false)
    }
  }, [])

  const logout = useCallback(() => {
    setStoredToken(null); setStoredUser(null); setUser(null)
  }, [])

  const hasAnyRole = useCallback((roles: Role[]) =>
    !!user && user.roles.some(r => roles.includes(r)), [user])

  const value = useMemo<AuthContextValue>(() => ({
    user,
    isAuthenticated: !!user,
    isLoading,
    login,
    logout,
    hasAnyRole,
    isClientUser: !!user?.roles.some(r => r === 'ClientAdmin' || r === 'ClientUser'),
    isFirmStaff: !!user?.roles.some(r => r === 'FirmAdmin' || r === 'Lawyer' || r === 'Paralegal'),
  }), [user, isLoading, login, logout, hasAnyRole])

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuth() {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('useAuth must be used within AuthProvider')
  return ctx
}
