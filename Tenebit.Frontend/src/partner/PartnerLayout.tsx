import { Gauge, HandCoins, LogOut, Mail, Receipt, Tag, UserCog } from 'lucide-react';
import { useEffect, type ReactNode } from 'react';
import { NavLink, useNavigate } from 'react-router-dom';
import { usePartnerAuth } from './PartnerAuthProvider';
import './partner.css';

const nav = [
  { to: '/partner/dashboard', label: 'Pulpit', icon: Gauge, end: true },
  { to: '/partner/codes', label: 'Kody', icon: Tag, end: false },
  { to: '/partner/conversions', label: 'Sprzedaże', icon: Receipt, end: false },
  { to: '/partner/payouts', label: 'Wypłaty', icon: HandCoins, end: false },
  { to: '/partner/messages', label: 'Wiadomości', icon: Mail, end: false },
  { to: '/partner/profile', label: 'Profil', icon: UserCog, end: false },
];

/**
 * Hidden partner-program chrome (spec §2/§10) - noindex on every page under it, no link from any
 * public menu, own auth guard against PartnerAuthProvider (never the tenant or admin session state).
 */
export function PartnerLayout({ children }: { children: ReactNode }) {
  const navigate = useNavigate();
  const { isLoading, isAuthenticated, affiliate, logout } = usePartnerAuth();

  useEffect(() => {
    const meta = document.createElement('meta');
    meta.name = 'robots';
    meta.content = 'noindex, nofollow';
    document.head.appendChild(meta);
    return () => { document.head.removeChild(meta); };
  }, []);

  useEffect(() => {
    if (!isLoading && !isAuthenticated) navigate('/partner/login', { replace: true });
  }, [isLoading, isAuthenticated, navigate]);

  async function handleLogout() {
    await logout();
    navigate('/partner/login', { replace: true });
  }

  if (isLoading || !isAuthenticated) return null;

  return (
    <div className="partnerShell">
      <aside className="partnerShell__side">
        <div className="partnerShell__brand">
          <HandCoins size={18} />
          <span>Tenebit Partners</span>
        </div>
        {affiliate?.status === 'PendingApproval' && (
          <p className="partnerShell__notice">Konto oczekuje na zatwierdzenie przez Tenebit.</p>
        )}
        <nav className="partnerShell__nav">
          {nav.map(item => (
            <NavLink key={item.to} to={item.to} end={item.end}
              className={({ isActive }) => `partnerShell__link${isActive ? ' partnerShell__link--active' : ''}`}>
              <item.icon size={16} />
              <span>{item.label}</span>
            </NavLink>
          ))}
        </nav>
        <button type="button" className="partnerShell__logout" onClick={handleLogout}>
          <LogOut size={16} />
          <span>Wyloguj</span>
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
