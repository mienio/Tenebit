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
const AdminPromoCodesPage = lazy(() => import('./admin/AdminPromoCodesPage').then(m => ({ default: m.AdminPromoCodesPage })));
const AdminAffiliatesPage = lazy(() => import('./admin/AdminAffiliatesPage').then(m => ({ default: m.AdminAffiliatesPage })));
const AdminAffiliateDetailPage = lazy(() => import('./admin/AdminAffiliateDetailPage').then(m => ({ default: m.AdminAffiliateDetailPage })));
const AdminAffiliateSettingsPage = lazy(() => import('./admin/AdminAffiliateSettingsPage').then(m => ({ default: m.AdminAffiliateSettingsPage })));
const AdminAffiliateMessagesPage = lazy(() => import('./admin/AdminAffiliateMessagesPage').then(m => ({ default: m.AdminAffiliateMessagesPage })));

const PartnerLoginPage = lazy(() => import('./partner/PartnerLoginPage').then(m => ({ default: m.PartnerLoginPage })));
const PartnerRegisterPage = lazy(() => import('./partner/PartnerRegisterPage').then(m => ({ default: m.PartnerRegisterPage })));
const PartnerVerifyEmailPage = lazy(() => import('./partner/PartnerVerifyEmailPage').then(m => ({ default: m.PartnerVerifyEmailPage })));
const PartnerForgotPasswordPage = lazy(() => import('./partner/PartnerForgotPasswordPage').then(m => ({ default: m.PartnerForgotPasswordPage })));
const PartnerResetPasswordPage = lazy(() => import('./partner/PartnerResetPasswordPage').then(m => ({ default: m.PartnerResetPasswordPage })));
const PartnerTermsPage = lazy(() => import('./partner/PartnerTermsPage').then(m => ({ default: m.PartnerTermsPage })));
const PartnerDashboardPage = lazy(() => import('./partner/PartnerDashboardPage').then(m => ({ default: m.PartnerDashboardPage })));
const PartnerCodesPage = lazy(() => import('./partner/PartnerCodesPage').then(m => ({ default: m.PartnerCodesPage })));
const PartnerConversionsPage = lazy(() => import('./partner/PartnerConversionsPage').then(m => ({ default: m.PartnerConversionsPage })));
const PartnerPayoutsPage = lazy(() => import('./partner/PartnerPayoutsPage').then(m => ({ default: m.PartnerPayoutsPage })));
const PartnerMessagesPage = lazy(() => import('./partner/PartnerMessagesPage').then(m => ({ default: m.PartnerMessagesPage })));
const PartnerProfilePage = lazy(() => import('./partner/PartnerProfilePage').then(m => ({ default: m.PartnerProfilePage })));

function HomeRoute() {
  const auth = useAuth();
  if (!auth.isAuthenticated) return <LandingPage />;
  const dashboard = nav.find(entry => entry.to === '/dashboard');
  return <Navigate to={dashboard && canSee(dashboard.roles, auth.roles) ? '/dashboard' : '/my'} replace />;
}

// Role bierzemy z `nav`, bo menu i dostep do trasy musza sie zgadzac. Trasy spoza menu (np. /pricing)
// podaja liste jawnie przez `roles`. Brak jednego i drugiego jest traktowany jak brak dostepu: wczesniej
// literowka w `path` cicho przepuszczala kazdego, bo `nav.find` zwracalo undefined.
function RequireRoles({ path, roles, children }: { path: string; roles?: string[]; children: ReactNode }) {
  const auth = useAuth();
  const required = roles ?? nav.find(entry => entry.to === path)?.roles;
  if (!required || !canSee(required, auth.roles)) return <ForbiddenPage />;
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
        <Route path="/s/:code" element={<PublicAssetScanPage />} />
        <Route path="/scan/:organizationId/:assetId" element={<PublicAssetScanPage />} />
        <Route path="/audit" element={<PublicAssetAuditPage />} />
        <Route path="/admin/login" element={<AdminLoginPage />} />
        <Route path="/admin" element={<AdminDashboardPage />} />
        <Route path="/admin/organizations" element={<AdminOrganizationsPage />} />
        <Route path="/admin/organizations/:id" element={<AdminOrganizationDetailPage />} />
        <Route path="/admin/users" element={<AdminUsersPage />} />
        <Route path="/admin/logins" element={<AdminLoginsPage />} />
        <Route path="/admin/audit" element={<AdminAuditPage />} />
        <Route path="/admin/promo-codes" element={<AdminPromoCodesPage />} />
        <Route path="/admin/affiliates" element={<AdminAffiliatesPage />} />
        <Route path="/admin/affiliates/:id" element={<AdminAffiliateDetailPage />} />
        <Route path="/admin/affiliate-settings" element={<AdminAffiliateSettingsPage />} />
        <Route path="/admin/affiliate-messages" element={<AdminAffiliateMessagesPage />} />

        {/* Hidden partner portal (spec §2): not linked from any public nav, noindex on every page,
            entirely separate auth/session from both the tenant app and the admin panel. */}
        <Route path="/partner/login" element={<PartnerLoginPage />} />
        <Route path="/partner/register" element={<PartnerRegisterPage />} />
        <Route path="/partner/verify-email" element={<PartnerVerifyEmailPage />} />
        <Route path="/partner/forgot-password" element={<PartnerForgotPasswordPage />} />
        <Route path="/partner/reset-password" element={<PartnerResetPasswordPage />} />
        <Route path="/partner/terms" element={<PartnerTermsPage />} />
        <Route path="/partner/dashboard" element={<PartnerDashboardPage />} />
        <Route path="/partner/codes" element={<PartnerCodesPage />} />
        <Route path="/partner/conversions" element={<PartnerConversionsPage />} />
        <Route path="/partner/payouts" element={<PartnerPayoutsPage />} />
        <Route path="/partner/messages" element={<PartnerMessagesPage />} />
        <Route path="/partner/profile" element={<PartnerProfilePage />} />
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
          {/* NIE `/audit` - ta sciezka nalezy do publicznej strony kampanii inwentaryzacyjnej (linia
              wyzej), ktorej adres jest juz rozeslany w mailach do pracownikow. Dopoki dziennik
              zdarzen tez siedzial na `/audit`, publiczna trasa wygrywala dopasowanie i caly modul
              byl nieosiagalny z poziomu aplikacji. */}
          <Route path="activity-log" element={<RequireRoles path="/activity-log"><AuditLogPage /></RequireRoles>} />
          <Route path="settings" element={<RequireRoles path="/settings"><SettingsPage /></RequireRoles>} />
          {/* Poza menu, wiec role jawnie. Backend i tak przepuszcza checkout/portal platnosci tylko
              wlascicielowi (SubscriptionService), a to zamyka slepy zaulek: reszta rol nie ogląda juz
              przyciskow, ktore skoncza sie bledem 403. */}
          <Route path="pricing" element={<RequireRoles path="/pricing" roles={['owner']}><PricingPage /></RequireRoles>} />
        </Route>
        <Route path="*" element={<NotFoundPage />} />
      </Routes>
    </Suspense>
  );
}
