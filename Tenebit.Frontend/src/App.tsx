import { Suspense, useEffect, type ReactNode } from 'react';
import { Navigate, Route, Routes, useLocation } from 'react-router-dom';
import { useAuth } from './auth/AuthProvider';
import { RequireAuth } from './auth/RequireAuth';
import { Layout, canSee, canSeeModule, nav } from './components/Layout';
import { LoadingState } from './components/StateViews';
import { ForbiddenPage, NotFoundPage } from './pages/ErrorPages';
import { PartnerLocaleProvider, type PartnerLocale } from './partner/i18n';
import { lazyRoute } from './utils/lazyRoute';

const LandingPage = lazyRoute(() => import('./pages/LandingPage').then(m => ({ default: m.LandingPage })));
const LoginPage = lazyRoute(() => import('./pages/LoginPage').then(m => ({ default: m.LoginPage })));
const RegisterPage = lazyRoute(() => import('./pages/RegisterPage').then(m => ({ default: m.RegisterPage })));
const SocialCallbackPage = lazyRoute(() => import('./pages/SocialCallbackPage').then(m => ({ default: m.SocialCallbackPage })));
const ForgotPasswordPage = lazyRoute(() => import('./pages/ForgotPasswordPage').then(m => ({ default: m.ForgotPasswordPage })));
const ResetPasswordPage = lazyRoute(() => import('./pages/ResetPasswordPage').then(m => ({ default: m.ResetPasswordPage })));
const VerifyEmailPage = lazyRoute(() => import('./pages/VerifyEmailPage').then(m => ({ default: m.VerifyEmailPage })));
const LegalPage = lazyRoute(() => import('./pages/LegalPage').then(m => ({ default: m.LegalPage })));
const PublicPricingPage = lazyRoute(() => import('./pages/PublicPricingPage').then(m => ({ default: m.PublicPricingPage })));
const CheckoutPage = lazyRoute(() => import('./pages/CheckoutPage').then(m => ({ default: m.CheckoutPage })));
const CheckoutSuccessPage = lazyRoute(() => import('./pages/CheckoutSuccessPage').then(m => ({ default: m.CheckoutSuccessPage })));
const PublicAssignmentPage = lazyRoute(() => import('./pages/PublicAssignmentPage').then(m => ({ default: m.PublicAssignmentPage })));
const PublicOffboardingPage = lazyRoute(() => import('./pages/PublicOffboardingPage').then(m => ({ default: m.PublicOffboardingPage })));
const PublicAssetScanPage = lazyRoute(() => import('./pages/PublicAssetScanPage').then(m => ({ default: m.PublicAssetScanPage })));
const PublicAssetAuditPage = lazyRoute(() => import('./pages/PublicAssetAuditPage').then(m => ({ default: m.PublicAssetAuditPage })));
const DashboardPage = lazyRoute(() => import('./pages/DashboardPage').then(m => ({ default: m.DashboardPage })));
const MyWorkspacePage = lazyRoute(() => import('./pages/MyWorkspacePage').then(m => ({ default: m.MyWorkspacePage })));
const AssetsPage = lazyRoute(() => import('./pages/AssetsPage').then(m => ({ default: m.AssetsPage })));
const PeoplePage = lazyRoute(() => import('./pages/PeoplePage').then(m => ({ default: m.PeoplePage })));
const AssignmentsPage = lazyRoute(() => import('./pages/AssignmentsPage').then(m => ({ default: m.AssignmentsPage })));
const ProceduresPage = lazyRoute(() => import('./pages/ProceduresPage').then(m => ({ default: m.ProceduresPage })));
const OnboardingPage = lazyRoute(() => import('./pages/OnboardingPage').then(m => ({ default: m.OnboardingPage })));
const OffboardingPage = lazyRoute(() => import('./pages/OffboardingPage').then(m => ({ default: m.OffboardingPage })));
const AssetAuditsPage = lazyRoute(() => import('./pages/AssetAuditsPage').then(m => ({ default: m.AssetAuditsPage })));
const ReportsPage = lazyRoute(() => import('./pages/ReportsPage').then(m => ({ default: m.ReportsPage })));
const AuditLogPage = lazyRoute(() => import('./pages/AuditLogPage').then(m => ({ default: m.AuditLogPage })));
const SettingsPage = lazyRoute(() => import('./pages/SettingsPage').then(m => ({ default: m.SettingsPage })));
const PricingPage = lazyRoute(() => import('./pages/PricingPage').then(m => ({ default: m.PricingPage })));
const LicensesPage = lazyRoute(() => import('./pages/LicensesPage').then(m => ({ default: m.LicensesPage })));
const AdminLoginPage = lazyRoute(() => import('./admin/AdminLoginPage').then(m => ({ default: m.AdminLoginPage })));
const AdminDashboardPage = lazyRoute(() => import('./admin/AdminDashboardPage').then(m => ({ default: m.AdminDashboardPage })));
const AdminOrganizationsPage = lazyRoute(() => import('./admin/AdminOrganizationsPage').then(m => ({ default: m.AdminOrganizationsPage })));
const AdminOrganizationDetailPage = lazyRoute(() => import('./admin/AdminOrganizationDetailPage').then(m => ({ default: m.AdminOrganizationDetailPage })));
const AdminUsersPage = lazyRoute(() => import('./admin/AdminUsersPage').then(m => ({ default: m.AdminUsersPage })));
const AdminLoginsPage = lazyRoute(() => import('./admin/AdminLoginsPage').then(m => ({ default: m.AdminLoginsPage })));
const AdminAuditPage = lazyRoute(() => import('./admin/AdminAuditPage').then(m => ({ default: m.AdminAuditPage })));
const AdminPromoCodesPage = lazyRoute(() => import('./admin/AdminPromoCodesPage').then(m => ({ default: m.AdminPromoCodesPage })));
const AdminAffiliatesPage = lazyRoute(() => import('./admin/AdminAffiliatesPage').then(m => ({ default: m.AdminAffiliatesPage })));
const AdminAffiliateDetailPage = lazyRoute(() => import('./admin/AdminAffiliateDetailPage').then(m => ({ default: m.AdminAffiliateDetailPage })));
const AdminAffiliateSettingsPage = lazyRoute(() => import('./admin/AdminAffiliateSettingsPage').then(m => ({ default: m.AdminAffiliateSettingsPage })));
const AdminAffiliateMessagesPage = lazyRoute(() => import('./admin/AdminAffiliateMessagesPage').then(m => ({ default: m.AdminAffiliateMessagesPage })));

const PartnerLandingPage = lazyRoute(() => import('./partner/PartnerLandingPage').then(m => ({ default: m.PartnerLandingPage })));
const PartnerLoginPage = lazyRoute(() => import('./partner/PartnerLoginPage').then(m => ({ default: m.PartnerLoginPage })));
const PartnerRegisterPage = lazyRoute(() => import('./partner/PartnerRegisterPage').then(m => ({ default: m.PartnerRegisterPage })));
const PartnerVerifyEmailPage = lazyRoute(() => import('./partner/PartnerVerifyEmailPage').then(m => ({ default: m.PartnerVerifyEmailPage })));
const PartnerForgotPasswordPage = lazyRoute(() => import('./partner/PartnerForgotPasswordPage').then(m => ({ default: m.PartnerForgotPasswordPage })));
const PartnerResetPasswordPage = lazyRoute(() => import('./partner/PartnerResetPasswordPage').then(m => ({ default: m.PartnerResetPasswordPage })));
const PartnerTermsPage = lazyRoute(() => import('./partner/PartnerTermsPage').then(m => ({ default: m.PartnerTermsPage })));
const PartnerDashboardPage = lazyRoute(() => import('./partner/PartnerDashboardPage').then(m => ({ default: m.PartnerDashboardPage })));
const PartnerCodesPage = lazyRoute(() => import('./partner/PartnerCodesPage').then(m => ({ default: m.PartnerCodesPage })));
const PartnerConversionsPage = lazyRoute(() => import('./partner/PartnerConversionsPage').then(m => ({ default: m.PartnerConversionsPage })));
const PartnerPayoutsPage = lazyRoute(() => import('./partner/PartnerPayoutsPage').then(m => ({ default: m.PartnerPayoutsPage })));
const PartnerMessagesPage = lazyRoute(() => import('./partner/PartnerMessagesPage').then(m => ({ default: m.PartnerMessagesPage })));
const PartnerProfilePage = lazyRoute(() => import('./partner/PartnerProfilePage').then(m => ({ default: m.PartnerProfilePage })));

const partnerRoutes: { subpath: string; element: ReactNode }[] = [
  { subpath: '', element: <PartnerLandingPage /> },
  { subpath: 'login', element: <PartnerLoginPage /> },
  { subpath: 'register', element: <PartnerRegisterPage /> },
  { subpath: 'verify-email', element: <PartnerVerifyEmailPage /> },
  { subpath: 'forgot-password', element: <PartnerForgotPasswordPage /> },
  { subpath: 'reset-password', element: <PartnerResetPasswordPage /> },
  { subpath: 'terms', element: <PartnerTermsPage /> },
  { subpath: 'dashboard', element: <PartnerDashboardPage /> },
  { subpath: 'codes', element: <PartnerCodesPage /> },
  { subpath: 'conversions', element: <PartnerConversionsPage /> },
  { subpath: 'payouts', element: <PartnerPayoutsPage /> },
  { subpath: 'messages', element: <PartnerMessagesPage /> },
  { subpath: 'profile', element: <PartnerProfilePage /> },
];

function HomeRoute() {
  const auth = useAuth();
  if (!auth.isAuthenticated) return <LandingPage />;
  const dashboard = nav.find(entry => entry.to === '/dashboard');
  return <Navigate to={dashboard && canSeeModule(dashboard.module, auth.can) ? '/dashboard' : '/my'} replace />;
}

// Modul (permission matrix) bierzemy z `nav`, bo menu i dostep do trasy musza sie zgadzac. Trasy spoza
// menu bez modulu (np. /pricing) podaja liste rol jawnie przez `roles` - ten jeden przypadek zostaje
// poza edytowalna macierza uprawnien (subskrypcje sa celowo wlasciciel-only, patrz SubscriptionService).
// Brak jednego i drugiego jest traktowany jak brak dostepu: wczesniej literowka w `path` cicho
// przepuszczala kazdego, bo `nav.find` zwracalo undefined.
function RequireRoles({ path, roles, children }: { path: string; roles?: string[]; children: ReactNode }) {
  const auth = useAuth();
  if (roles) {
    return canSee(roles, auth.roles) ? <>{children}</> : <ForbiddenPage />;
  }

  const entry = nav.find(item => item.to === path);
  if (!entry) return <ForbiddenPage />;
  return canSeeModule(entry.module, auth.can) ? <>{children}</> : <ForbiddenPage />;
}

// The dashboard's own Layout already does this for logged-in routes; public pages (landing, legal,
// the partner site) render outside Layout, so without this a link like the footer's Affiliate Program
// one lands on the new page still scrolled to wherever the click happened on the old one.
function ScrollToTop() {
  const { pathname } = useLocation();
  useEffect(() => {
    window.scrollTo(0, 0);
  }, [pathname]);
  return null;
}

export function App() {
  return (
    <Suspense fallback={<LoadingState />}>
      <ScrollToTop />
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
        <Route path="/refund" element={<LegalPage kind="refund" />} />
        {/* Public pricing. The in-app /pricing below stays behind the auth guard - it drives real
            checkouts; this one only shows the price list, and is the URL Paddle domain review opens. */}
        <Route path="/plans" element={<PublicPricingPage />} />
        <Route path="/cookies" element={<LegalPage kind="cookies" />} />
        {/* Paddle's Default payment link. Public on purpose: the shopper arrives from a Paddle mail,
            often not logged in, and a bounce through /login would drop the ?_ptxn transaction id. */}
        <Route path="/checkout" element={<CheckoutPage />} />
        <Route path="/checkout/success" element={<CheckoutSuccessPage />} />
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

        {/* Partner portal: public landing page at /partner invites sign-ups; not linked from the main
            site nav, but discoverable on its own. Entirely separate auth/session from both the tenant
            app and the admin panel. English is the canonical language (bare /partner/*); a full Polish
            mirror lives under /partner/pl/* - both trees render the same page components, switched via
            PartnerLocaleProvider rather than duplicated routes. */}
        {(['en', 'pl'] as PartnerLocale[]).flatMap(locale => partnerRoutes.map(({ subpath, element }) => (
          <Route
            key={`partner-${locale}-${subpath}`}
            path={locale === 'en' ? `/partner${subpath ? `/${subpath}` : ''}` : `/partner/pl${subpath ? `/${subpath}` : ''}`}
            element={<PartnerLocaleProvider locale={locale}>{element}</PartnerLocaleProvider>}
          />
        )))}
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
