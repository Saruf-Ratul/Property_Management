import { Routes, Route, Navigate, useLocation, Outlet } from 'react-router-dom'
import { AppLayout } from './components/layout/AppLayout'
import { ProtectedRoute } from './components/ProtectedRoute'
import { LoginPage } from './pages/LoginPage'
import { DashboardPage } from './pages/DashboardPage'
import { CasesListPage } from './pages/cases/CasesListPage'
import { CaseDetailPage } from './pages/cases/CaseDetailPage'
import { NewCasePage } from './pages/cases/NewCasePage'
import { CaseIntakeWizard } from './pages/cases/CaseIntakeWizard'
import { PropertiesPage } from './pages/PropertiesPage'
import { PropertyDetailsPage } from './pages/PropertyDetailsPage'
import { TenantsPage } from './pages/TenantsPage'
import { DelinquentPage } from './pages/DelinquentPage'
import { ClientsPage } from './pages/ClientsPage'
import { PmsIntegrationsPage } from './pages/PmsIntegrationsPage'
import { LtFormWizardPage } from './pages/forms/LtFormWizardPage'
import { FormsLandingPage } from './pages/forms/FormsLandingPage'
import { AuditLogsPage } from './pages/AuditLogsPage'
import { NotFoundPage } from './pages/NotFoundPage'
import { ClientPortalLayout } from './components/portal/ClientPortalLayout'
import { PortalDashboard } from './pages/portal/PortalDashboard'
import { PortalCasesListPage } from './pages/portal/PortalCasesListPage'
import { PortalCaseDetail } from './pages/portal/PortalCaseDetail'
import { PortalNotificationsPage } from './pages/portal/PortalNotificationsPage'
import { useAuth } from './lib/auth'

export default function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />

      <Route element={<ProtectedRoute />}>
        {/* ─── Client Portal (clients only) ────────────────────────────── */}
        <Route element={<ProtectedRoute roles={['ClientAdmin', 'ClientUser']} />}>
          <Route path="portal" element={<ClientPortalLayout />}>
            <Route index element={<PortalDashboard />} />
            <Route path="cases" element={<PortalCasesListPage />} />
            <Route path="cases/:id" element={<PortalCaseDetail />} />
            <Route path="notifications" element={<PortalNotificationsPage />} />
            <Route path="*" element={<Navigate to="/portal" replace />} />
          </Route>
        </Route>

        {/* ─── Firm admin UI (all firm-staff + Auditor) ───────────────── */}
        <Route element={<FirmRouteGuard />}>
          <Route element={<AppLayout />}>
            {/* Dashboard at "/" — wrapped in AppLayout so sidebar + topbar render. */}
            <Route index element={<DashboardPage />} />

            <Route path="cases" element={<CasesListPage />} />
            <Route path="cases/new" element={<NewCasePage />} />
            <Route path="cases/intake" element={<CaseIntakeWizard />} />
            <Route path="cases/:id" element={<CaseDetailPage />} />
            <Route path="properties" element={<PropertiesPage />} />
            <Route path="properties/:id" element={<PropertyDetailsPage />} />
            <Route path="tenants" element={<TenantsPage />} />
            <Route path="delinquent" element={<DelinquentPage />} />

            <Route element={<ProtectedRoute roles={['FirmAdmin', 'Lawyer', 'Paralegal']} />}>
              <Route path="clients" element={<ClientsPage />} />
              <Route path="pms-integrations" element={<PmsIntegrationsPage />} />
              <Route path="forms" element={<FormsLandingPage />} />
              <Route path="cases/:id/forms" element={<LtFormWizardPage />} />
            </Route>

            <Route element={<ProtectedRoute roles={['FirmAdmin', 'Auditor']} />}>
              <Route path="audit" element={<AuditLogsPage />} />
            </Route>

            <Route path="*" element={<NotFoundPage />} />
          </Route>
        </Route>
      </Route>

      <Route path="*" element={<Navigate to="/" />} />
    </Routes>
  )
}

/**
 * Forces client portal users away from firm admin routes (including "/").
 * Firm staff and Auditor are allowed through and see the AppLayout below.
 */
function FirmRouteGuard() {
  const { isClientUser } = useAuth()
  const loc = useLocation()
  if (isClientUser) return <Navigate to="/portal" replace state={{ from: loc.pathname }} />
  return <Outlet />
}
