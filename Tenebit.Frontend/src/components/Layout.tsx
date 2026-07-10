import { BarChart3, Boxes, ClipboardCheck, FileText, History, LayoutDashboard, LogOut, Menu, PackageCheck, QrCode, Settings, User, Users, X, Zap } from 'lucide-react';
import { useState } from 'react';
import { NavLink, Outlet } from 'react-router-dom';
import { Button } from './Button';
import { useAuth } from '../auth/AuthProvider';
import { useI18n } from '../i18n/I18nProvider';

export const nav = [
  { to: '/dashboard', labelKey: 'nav.dashboard', icon: LayoutDashboard, roles: [] },
  { to: '/my', labelKey: 'nav.my', icon: User, roles: [] },
  { to: '/assets', labelKey: 'nav.assets', icon: Boxes, roles: ['owner', 'admin', 'asset_operator', 'manager', 'hr', 'finance', 'auditor'] },
  { to: '/people', labelKey: 'nav.people', icon: Users, roles: ['owner', 'admin', 'manager', 'hr', 'asset_operator', 'auditor'] },
  { to: '/assignments', labelKey: 'nav.assignments', icon: PackageCheck, roles: ['owner', 'admin', 'asset_operator', 'hr', 'manager'] },
  { to: '/procedures', labelKey: 'nav.procedures', icon: ClipboardCheck, roles: ['owner', 'admin', 'hr', 'manager', 'asset_operator', 'auditor'] },
  { to: '/onboarding', labelKey: 'nav.onboarding', icon: FileText, roles: ['owner', 'admin', 'hr', 'asset_operator'] },
  { to: '/reports', labelKey: 'nav.reports', icon: BarChart3, roles: ['owner', 'admin', 'manager', 'finance', 'auditor', 'asset_operator'] },
  { to: '/audit', labelKey: 'nav.audit', icon: History, roles: ['owner', 'admin', 'auditor'] },
  { to: '/pricing', labelKey: 'nav.pricing', icon: Zap, roles: ['owner', 'admin'] },
  { to: '/settings', labelKey: 'nav.settings', icon: Settings, roles: ['owner', 'admin'] }
];

export function canSee(requiredRoles: string[], userRoles: string[]) {
  if (!requiredRoles.length) return true;
  return requiredRoles.some(role => userRoles.includes(role));
}

export function Layout() {
  const auth = useAuth();
  const { t } = useI18n();
  const [mobileOpen, setMobileOpen] = useState(false);
  const visibleNav = nav.filter(item => canSee(item.roles, auth.roles));

  return (
    <div className={mobileOpen ? 'appShell appShell--menuOpen' : 'appShell'}>
      <header className="mobileTopbar">
        <div className="brand brand--compact">
          <div className="brand__mark"><QrCode size={20} /></div>
          <div><strong>Tenebit</strong><small>{t('nav.tagline')}</small></div>
        </div>
        <button className="iconButton" type="button" aria-label={mobileOpen ? t('nav.closeMenu') : t('nav.openMenu')} aria-expanded={mobileOpen} onClick={() => setMobileOpen(open => !open)}>{mobileOpen ? <X size={18} /> : <Menu size={18} />}</button>
      </header>

      {mobileOpen ? <button className="sidebarScrim" type="button" aria-label={t('nav.closeMenu')} onClick={() => setMobileOpen(false)} /> : null}

      <aside className="sidebar">
        <div className="brand">
          <div className="brand__mark"><QrCode size={22} /></div>
          <div>
            <strong>Tenebit</strong>
            <small>{t('nav.sidebarTagline')}</small>
          </div>
        </div>
        <nav className="nav" aria-label={t('nav.mainNavAria')}>
          {visibleNav.map(item => {
            const Icon = item.icon;
            return <NavLink key={item.to} to={item.to} end={item.to === '/dashboard'} onClick={() => setMobileOpen(false)}><Icon size={18} />{t(item.labelKey)}</NavLink>;
          })}
        </nav>
        <div className="sidebarFooter">
          <span>{auth.userName}</span>
          <Button variant="ghost" onClick={auth.logout} icon={<LogOut size={16} />}>{t('nav.logout')}</Button>
        </div>
      </aside>

      <main className="content"><Outlet /></main>
    </div>
  );
}
