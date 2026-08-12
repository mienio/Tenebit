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
const PublicAssignmentPage = lazy(() => import('./pages/PublicAssignmentPage').then(m => ({ default: m.PublicAssignmentPage })));
const PublicAssetScanPage = lazy(() => import('./pages/PublicAssetScanPage').then(m => ({ default: m.PublicAssetScanPage })));
const DashboardPage = lazy(() => import('./pages/DashboardPage').then(m => ({ default: m.DashboardPage })));
const MyWorkspacePage = lazy(() => import('./pages/MyWorkspacePage').then(m => ({ default: m.MyWorkspacePage })));
const AssetsPage = lazy(() => import('./pages/AssetsPage').then(m => ({ default: m.AssetsPage })));
const PeoplePage = lazy(() => import('./pages/PeoplePage').then(m => ({ default: m.PeoplePage })));
const AssignmentsPage = lazy(() => import('./pages/AssignmentsPage').then(m => ({ default: m.AssignmentsPage })));
const ProceduresPage = lazy(() => import('./pages/ProceduresPage').then(m => ({ default: m.ProceduresPage })));
const OnboardingPage = lazy(() => import('./pages/OnboardingPage').then(m => ({ default: m.OnboardingPage })));
const ReportsPage = lazy(() => import('./pages/ReportsPage').then(m => ({ default: m.ReportsPage })));
const AuditLogPage = lazy(() => import('./pages/AuditLogPage').then(m => ({ default: m.AuditLogPage })));
const SettingsPage = lazy(() => import('./pages/SettingsPage').then(m => ({ default: m.SettingsPage })));
const PricingPage = lazy(() => import('./pages/PricingPage').then(m => ({ default: m.PricingPage })));
const LicensesPage = lazy(() => import('./pages/LicensesPage').then(m => ({ default: m.LicensesPage })));

function HomeRoute() {
  const auth = useAuth();
  return auth.isAuthenticated ? <Navigate to="/dashboard" replace /> : <LandingPage />;
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
        <Route path="/accept/:organizationId/:assignmentId" element={<PublicAssignmentPage />} />
        <Route path="/scan/:organizationId/:assetId" element={<PublicAssetScanPage />} />
        <Route element={<RequireAuth><Layout /></RequireAuth>}>
          <Route path="dashboard" element={<DashboardPage />} />
          <Route path="my" element={<MyWorkspacePage />} />
          <Route path="assets" element={<RequireRoles path="/assets"><AssetsPage /></RequireRoles>} />
          <Route path="people" element={<RequireRoles path="/people"><PeoplePage /></RequireRoles>} />
          <Route path="assignments" element={<RequireRoles path="/assignments"><AssignmentsPage /></RequireRoles>} />
          <Route path="procedures" element={<RequireRoles path="/procedures"><ProceduresPage /></RequireRoles>} />
          <Route path="onboarding" element={<RequireRoles path="/onboarding"><OnboardingPage /></RequireRoles>} />
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
