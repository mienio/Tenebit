import { Navigate, Route, Routes } from 'react-router-dom';
import { useAuth } from './auth/AuthProvider';
import { RequireAuth } from './auth/RequireAuth';
import { Layout } from './components/Layout';
import { AssetsPage } from './pages/AssetsPage';
import { AssignmentsPage } from './pages/AssignmentsPage';
import { AuditLogPage } from './pages/AuditLogPage';
import { DashboardPage } from './pages/DashboardPage';
import { LandingPage } from './pages/LandingPage';
import { OnboardingPage } from './pages/OnboardingPage';
import { LoginPage } from './pages/LoginPage';
import { RegisterPage } from './pages/RegisterPage';
import { MyWorkspacePage } from './pages/MyWorkspacePage';
import { PeoplePage } from './pages/PeoplePage';
import { ProceduresPage } from './pages/ProceduresPage';
import { PublicAssetScanPage } from './pages/PublicAssetScanPage';
import { PublicAssignmentPage } from './pages/PublicAssignmentPage';
import { ReportsPage } from './pages/ReportsPage';
import { SettingsPage } from './pages/SettingsPage';
import { PricingPage } from './pages/PricingPage';
import { SocialCallbackPage } from './pages/SocialCallbackPage';
import { ForgotPasswordPage } from './pages/ForgotPasswordPage';
import { ResetPasswordPage } from './pages/ResetPasswordPage';
import { VerifyEmailPage } from './pages/VerifyEmailPage';

function HomeRoute() {
  const auth = useAuth();
  return auth.isAuthenticated ? <Navigate to="/dashboard" replace /> : <LandingPage />;
}

export function App() {
  return (
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
        <Route path="assets" element={<AssetsPage />} />
        <Route path="people" element={<PeoplePage />} />
        <Route path="assignments" element={<AssignmentsPage />} />
        <Route path="procedures" element={<ProceduresPage />} />
        <Route path="onboarding" element={<OnboardingPage />} />
        <Route path="reports" element={<ReportsPage />} />
        <Route path="audit" element={<AuditLogPage />} />
        <Route path="settings" element={<SettingsPage />} />
        <Route path="pricing" element={<PricingPage />} />
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}
