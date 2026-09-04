import { Building2, ClipboardList, LayoutDashboard, LogOut, ShieldCheck, Tag, Users } from 'lucide-react';
import { useEffect, type ReactNode } from 'react';
import { NavLink, useNavigate } from 'react-router-dom';
import { adminLogout, getAdminToken } from './adminApi';
import './admin.css';

const nav = [
  { to: '/admin', label: 'Pulpit', icon: LayoutDashboard, end: true },
  { to: '/admin/organizations', label: 'Organizacje', icon: Building2, end: false },
  { to: '/admin/users', label: 'Użytkownicy', icon: Users, end: false },
  { to: '/admin/logins', label: 'Logowania', icon: ShieldCheck, end: false },
  { to: '/admin/audit', label: 'Dziennik admina', icon: ClipboardList, end: false },
  { to: '/admin/promo-codes', label: 'Kody promocyjne', icon: Tag, end: false },
];

/**
 * Chrome shared by every admin screen. Also acts as the client-side gate: without a token it redirects
 * to the login form rather than rendering a page that would only fail its first request. That is a
 * convenience, not a security boundary - the server authorises every call independently.
 */
export function AdminShell({ children }: { children: ReactNode }) {
  const navigate = useNavigate();

  useEffect(() => {
    if (!getAdminToken()) navigate('/admin/login', { replace: true });
  }, [navigate]);

  function handleLogout() {
    adminLogout();
    navigate('/admin/login', { replace: true });
  }

  return (
    <div className="adminShell">
      <aside className="adminShell__side">
        <div className="adminShell__brand">
          <ShieldCheck size={18} />
          <span>Tenebit Admin</span>
        </div>
        <nav className="adminShell__nav">
          {nav.map(item => (
            <NavLink
              key={item.to}
              to={item.to}
              end={item.end}
              className={({ isActive }) => `adminShell__link${isActive ? ' adminShell__link--active' : ''}`}
            >
              <item.icon size={16} />
              <span>{item.label}</span>
            </NavLink>
          ))}
        </nav>
        <button type="button" className="adminShell__logout" onClick={handleLogout}>
          <LogOut size={16} />
          <span>Wyloguj</span>
        </button>
      </aside>
      <main className="adminShell__main">{children}</main>
    </div>
  );
}

export function AdminPageHeader({ title, description, actions }: { title: string; description?: string; actions?: ReactNode }) {
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
