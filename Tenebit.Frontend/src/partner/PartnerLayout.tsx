import { Gauge, HandCoins, LogOut, Mail, Receipt, Tag, UserCog } from 'lucide-react';
import { useEffect, type ReactNode } from 'react';
import { NavLink, useNavigate } from 'react-router-dom';
import { usePartnerAuth } from './PartnerAuthProvider';
import { PartnerLanguageSwitch, usePartnerLocale, type PartnerLocale } from './i18n';
import './partner.css';

const navContent: Record<PartnerLocale, { brand: string; pendingNotice: string; logout: string }> = {
  en: {
    brand: 'Tenebit Partners',
    pendingNotice: 'Your account is awaiting approval from Tenebit.',
    logout: 'Log out',
  },
  pl: {
    brand: 'Tenebit Partners',
    pendingNotice: 'Konto oczekuje na zatwierdzenie przez Tenebit.',
    logout: 'Wyloguj',
  },
};

const navItems: Record<PartnerLocale, { to: string; label: string; icon: typeof Gauge; end: boolean }[]> = {
  en: [
    { to: 'dashboard', label: 'Dashboard', icon: Gauge, end: true },
    { to: 'codes', label: 'Codes', icon: Tag, end: false },
    { to: 'conversions', label: 'Sales', icon: Receipt, end: false },
    { to: 'payouts', label: 'Payouts', icon: HandCoins, end: false },
    { to: 'messages', label: 'Messages', icon: Mail, end: false },
    { to: 'profile', label: 'Profile', icon: UserCog, end: false },
  ],
  pl: [
    { to: 'dashboard', label: 'Pulpit', icon: Gauge, end: true },
    { to: 'codes', label: 'Kody', icon: Tag, end: false },
    { to: 'conversions', label: 'Sprzedaże', icon: Receipt, end: false },
    { to: 'payouts', label: 'Wypłaty', icon: HandCoins, end: false },
    { to: 'messages', label: 'Wiadomości', icon: Mail, end: false },
    { to: 'profile', label: 'Profil', icon: UserCog, end: false },
  ],
};

/**
 * Hidden partner-program chrome (spec §2/§10) - noindex on every page under it, no link from any
 * public menu, own auth guard against PartnerAuthProvider (never the tenant or admin session state).
 */
export function PartnerLayout({ children }: { children: ReactNode }) {
  const navigate = useNavigate();
  const { isLoading, isAuthenticated, affiliate, logout } = usePartnerAuth();
  const { path, locale } = usePartnerLocale();
  const t = navContent[locale];
  const nav = navItems[locale];

  useEffect(() => {
    const meta = document.createElement('meta');
    meta.name = 'robots';
    meta.content = 'noindex, nofollow';
    document.head.appendChild(meta);
    return () => { document.head.removeChild(meta); };
  }, []);

  useEffect(() => {
    if (!isLoading && !isAuthenticated) navigate(path('login'), { replace: true });
  }, [isLoading, isAuthenticated, navigate, path]);

  async function handleLogout() {
    await logout();
    navigate(path('login'), { replace: true });
  }

  if (isLoading || !isAuthenticated) return null;

  return (
    <div className="partnerShell">
      <aside className="partnerShell__side">
        <div className="partnerShell__brand">
          <HandCoins size={18} />
          <span>{t.brand}</span>
        </div>
        {affiliate?.status === 'PendingApproval' && (
          <p className="partnerShell__notice">{t.pendingNotice}</p>
        )}
        <nav className="partnerShell__nav">
          {nav.map(item => (
            <NavLink key={item.to} to={path(item.to)} end={item.end}
              className={({ isActive }) => `partnerShell__link${isActive ? ' partnerShell__link--active' : ''}`}>
              <item.icon size={16} />
              <span>{item.label}</span>
            </NavLink>
          ))}
        </nav>
        <PartnerLanguageSwitch className="partnerShell__link" />
        <button type="button" className="partnerShell__logout" onClick={handleLogout}>
          <LogOut size={16} />
          <span>{t.logout}</span>
        </button>
      </aside>
      <main className="partnerShell__main">{children}</main>
    </div>
  );
}

export function PartnerPageHeader({ title, description, actions }: { title: string; description?: string; actions?: ReactNode }) {
  return (
    <header className="adminPageHeader">
      <div>
        <h1>{title}</h1>
        {description ? <p>{description}</p> : null}
      </div>
      {actions ? <div className="adminPageHeader__actions">{actions}</div> : null}
    </header>
  );
}
