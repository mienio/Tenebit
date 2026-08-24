import { Suspense, lazy, type ReactNode } from 'react';
import { Navigate, Route, Routes } from 'react-router-dom';
import { useAuth } from './auth/AuthProvider';
import { RequireAuth } from './auth/RequireAuth';
import { Layout, canSee, nav } from './components/Layout';
import { LoadingState } from './components/StateViews';
import { ForbiddenPage, NotFoundPage } from './pages/ErrorPages';

const LandingPage = lazy(() => import('./pages/LandingPage').then(m => ({ default: m.LandingPage })));
const LoginPage = lazy(() => import('./pages/LoginPage').then(m => ({ default: m.LoginPage })));
const RegisterPage = lazy(() => import('./pages/RegisterPage').then(m => ({ default: m.RegisterPage })));
const SocialCallbackPage = lazy(() => import('./pages/SocialCallbackPage').then(m => ({ default: m.SocialCallbackPage })));
const ForgotPasswordPage = lazy(() => import('./pages/ForgotPasswordPage').then(m => ({ default: m.ForgotPasswordPage })));
const ResetPasswordPage = lazy(() => import('./pages/ResetPasswordPage').then(m => ({ default: m.ResetPasswordPage })));
const VerifyEmailPage = lazy(() => import('./pages/VerifyEmailPage').then(m => ({ default: m.VerifyEmailPage })));
const LegalPage = lazy(() => import('./pages/LegalPage').then(m => ({ default: m.LegalPage })));
const PublicAssignmentPage = lazy(() => import('./pages/PublicAssignmentPage').then(m => ({ default: m.PublicAssignmentPage })));
const PublicOffboardingPage = lazy(() => import('./pages/PublicOffboardingPage').then(m => ({ default: m.PublicOffboardingPage })));
const PublicAssetScanPage = lazy(() => import('./pages/PublicAssetScanPage').then(m => ({ default: m.PublicAssetScanPage })));
const PublicAssetAuditPage = lazy(() => import('./pages/PublicAssetAuditPage').then(m => ({ default: m.PublicAssetAuditPage })));
const DashboardPage = lazy(() => import('./pages/DashboardPage').then(m => ({ default: m.DashboardPage })));
const MyWorkspacePage = lazy(() => import('./pages/MyWorkspacePage').then(m => ({ default: m.MyWorkspacePage })));
const AssetsPage = lazy(() => import('./pages/AssetsPage').then(m => ({ default: m.AssetsPage })));
const PeoplePage = lazy(() => import('./pages/PeoplePage').then(m => ({ default: m.PeoplePage })));
const AssignmentsPage = lazy(() => import('./pages/AssignmentsPage').then(m => ({ default: m.AssignmentsPage })));
const ProceduresPage = lazy(() => import('./pages/ProceduresPage').then(m => ({ default: m.ProceduresPage })));
const OnboardingPage = lazy(() => import('./pages/OnboardingPage').then(m => ({ default: m.OnboardingPage })));
const OffboardingPage = lazy(() => import('./pages/OffboardingPage').then(m => ({ default: m.OffboardingPage })));
const AssetAuditsPage = lazy(() => import('./pages/AssetAuditsPage').then(m => ({ default: m.AssetAuditsPage })));
const ReportsPage = lazy(() => import('./pages/ReportsPage').then(m => ({ default: m.ReportsPage })));
const AuditLogPage = lazy(() => import('./pages/AuditLogPage').then(m => ({ default: m.AuditLogPage })));
const SettingsPage = lazy(() => import('./pages/SettingsPage').then(m => ({ default: m.SettingsPage })));
const PricingPage = lazy(() => import('./pages/PricingPage').then(m => ({ default: m.PricingPage })));
const LicensesPage = lazy(() => import('./pages/LicensesPage').then(m => ({ default: m.LicensesPage })));
const AdminLoginPage = lazy(() => import('./admin/AdminLoginPage').then(m => ({ default: m.AdminLoginPage })));
const AdminDashboardPage = lazy(() => import('./admin/AdminDashboardPage').then(m => ({ default: m.AdminDashboardPage })));
const AdminOrganizationsPage = lazy(() => import('./admin/AdminOrganizationsPage').then(m => ({ default: m.AdminOrganizationsPage })));
const AdminOrganizationDetailPage = lazy(() => import('./admin/AdminOrganizationDetailPage').then(m => ({ default: m.AdminOrganizationDetailPage })));
const AdminUsersPage = lazy(() => import('./admin/AdminUsersPage').then(m => ({ default: m.AdminUsersPage })));
const AdminLoginsPage = lazy(() => import('./admin/AdminLoginsPage').then(m => ({ default: m.AdminLoginsPage })));
const AdminAuditPage = lazy(() => import('./admin/AdminAuditPage').then(m => ({ default: m.AdminAuditPage })));

function HomeRoute() {
  const auth = useAuth();
  if (!auth.isAuthenticated) return <LandingPage />;
  const dashboard = nav.find(entry => entry.to === '/dashboard');
  return <Navigate to={dashboard && canSee(dashboard.roles, auth.roles) ? '/dashboard' : '/my'} replace />;
}

function RequireRoles({ path, children }: { path: string; children: ReactNode }) {
  const auth = useAuth();
  const item = nav.find(entry => entry.to === path);
  if (item && !canSee(item.roles, auth.roles)) return <ForbiddenPage />;
  return <>{children}</>;
}

export function App() {
  return (
    <Suspense fallback={<LoadingState />}>
      <Routes>
        <Route path="/" element={<HomeRoute />} />
        <Route path="/login" element={<LoginPage />} />
        <Route path="/register" element={<RegisterPage />} />
        <Route path="/auth/callback" element={<SocialCallbackPage />} />
        <Route path="/forgot-password" element={<ForgotPasswordPage />} />
        <Route path="/reset-password" element={<ResetPasswordPage />} />
        <Route path="/verify-email" element={<VerifyEmailPage />} />
        <Route path="/privacy" element={<LegalPage kind="privacy" />} />
        <Route path="/terms" element={<LegalPage kind="terms" />} />
        <Route path="/cookies" element={<LegalPage kind="cookies" />} />
        <Route path="/accept" element={<PublicAssignmentPage />} />
        <Route path="/exit" element={<PublicOffboardingPage />} />
        <Route path="/scan/:organizationId/:assetId" element={<PublicAssetScanPage />} />
        <Route path="/audit" element={<PublicAssetAuditPage />} />
        <Route path="/admin/login" element={<AdminLoginPage />} />
        <Route path="/admin" element={<AdminDashboardPage />} />
        <Route path="/admin/organizations" element={<AdminOrganizationsPage />} />
        <Route path="/admin/organizations/:id" element={<AdminOrganizationDetailPage />} />
        <Route path="/admin/users" element={<AdminUsersPage />} />
        <Route path="/admin/logins" element={<AdminLoginsPage />} />
        <Route path="/admin/audit" element={<AdminAuditPage />} />
        <Route element={<RequireAuth><Layout /></RequireAuth>}>
          <Route path="dashboard" element={<RequireRoles path="/dashboard"><DashboardPage /></RequireRoles>} />
          <Route path="my" element={<MyWorkspacePage />} />
          <Route path="assets" element={<RequireRoles path="/assets"><AssetsPage /></RequireRoles>} />
          <Route path="people" element={<RequireRoles path="/people"><PeoplePage /></RequireRoles>} />
          <Route path="assignments" element={<RequireRoles path="/assignments"><AssignmentsPage /></RequireRoles>} />
          <Route path="procedures" element={<RequireRoles path="/procedures"><ProceduresPage /></RequireRoles>} />
          <Route path="onboarding" element={<RequireRoles path="/onboarding"><OnboardingPage /></RequireRoles>} />
          <Route path="offboarding" element={<RequireRoles path="/offboarding"><OffboardingPage /></RequireRoles>} />
          <Route path="offboarding/:id" element={<RequireRoles path="/offboarding"><OffboardingPage /></RequireRoles>} />
          <Route path="asset-audits" element={<RequireRoles path="/asset-audits"><AssetAuditsPage /></RequireRoles>} />
          <Route path="asset-audits/:id" element={<RequireRoles path="/asset-audits"><AssetAuditsPage /></RequireRoles>} />
          <Route path="reports" element={<RequireRoles path="/reports"><ReportsPage /></RequireRoles>} />
          <Route path="licenses" element={<RequireRoles path="/licenses"><LicensesPage /></RequireRoles>} />
          <Route path="audit" element={<RequireRoles path="/audit"><AuditLogPage /></RequireRoles>} />
          <Route path="settings" element={<RequireRoles path="/settings"><SettingsPage /></RequireRoles>} />
          <Route path="pricing" element={<RequireRoles path="/pricing"><PricingPage /></RequireRoles>} />
        </Route>
        <Route path="*" element={<NotFoundPage />} />
      </Routes>
    </Suspense>
  );
}
