import { Navigate, Outlet, useLocation } from 'react-router-dom'
import { useAuth } from '@/lib/auth'
import type { Role } from '@/types'
import { PageLoader } from './ui/Spinner'

interface Props {
  roles?: Role[]
}

export function ProtectedRoute({ roles }: Props) {
  const { isAuthenticated, isLoading, hasAnyRole } = useAuth()
  const location = useLocation()

  if (isLoading) return <PageLoader />
  if (!isAuthenticated) {
    return <Navigate to="/login" state={{ from: location }} replace />
  }
  if (roles && !hasAnyRole(roles)) {
    return <Navigate to="/" replace />
  }
  return <Outlet />
}
