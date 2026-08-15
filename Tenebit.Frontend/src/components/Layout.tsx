import { BarChart3, Boxes, ChevronDown, ClipboardCheck, ClipboardList, FileText, History, KeyRound, LayoutDashboard, LogOut, Menu, PackageCheck, Settings, User, UserRoundX, Users, X } from 'lucide-react';
import { useEffect, useRef, useState } from 'react';
import { NavLink, Outlet, useLocation } from 'react-router-dom';
import { BrandMark } from './BrandMark';
import { Button } from './Button';
import { keepFocusInside } from './Modal';
import { useAuth } from '../auth/AuthProvider';
import { useI18n } from '../i18n/I18nProvider';

export const navGroups = [
  { key: 'assets', labelKey: 'nav.group.assets' },
  { key: 'employment', labelKey: 'nav.group.employment' },
  { key: 'reports', labelKey: 'nav.group.reports' },
];

export const nav = [
  { to: '/dashboard', labelKey: 'nav.dashboard', icon: LayoutDashboard, roles: [] },
  { to: '/my', labelKey: 'nav.my', icon: User, roles: [] },
  { to: '/assets', labelKey: 'nav.assets', icon: Boxes, roles: ['owner', 'admin', 'asset_operator', 'technician', 'manager', 'hr', 'license_manager', 'finance', 'auditor'], group: 'assets' },
  { to: '/licenses', labelKey: 'nav.licenses', icon: KeyRound, roles: ['owner', 'admin', 'license_manager', 'finance', 'auditor'], group: 'assets' },
  { to: '/assignments', labelKey: 'nav.assignments', icon: PackageCheck, roles: ['owner', 'admin', 'asset_operator', 'hr', 'manager'], group: 'assets' },
  { to: '/asset-audits', labelKey: 'nav.assetAudits', icon: ClipboardList, roles: ['owner', 'admin', 'asset_operator', 'auditor'], group: 'assets' },
  { to: '/people', labelKey: 'nav.people', icon: Users, roles: ['owner', 'admin', 'manager', 'hr', 'asset_operator', 'auditor'], group: 'employment' },
  { to: '/onboarding', labelKey: 'nav.onboarding', icon: FileText, roles: ['owner', 'admin', 'hr', 'asset_operator'], group: 'employment' },
  { to: '/offboarding', labelKey: 'nav.offboarding', icon: UserRoundX, roles: ['owner', 'admin', 'hr', 'asset_operator'], group: 'employment' },
  { to: '/procedures', labelKey: 'nav.procedures', icon: ClipboardCheck, roles: ['owner', 'admin', 'hr', 'manager', 'asset_operator', 'auditor', 'procedure_manager'], group: 'employment' },
  { to: '/reports', labelKey: 'nav.reports', icon: BarChart3, roles: ['owner', 'admin', 'manager', 'finance', 'auditor', 'asset_operator'], group: 'reports' },
  { to: '/audit', labelKey: 'nav.audit', icon: History, roles: ['owner', 'admin', 'auditor'], group: 'reports' },
  { to: '/settings', labelKey: 'nav.settings', icon: Settings, roles: [] },
];

export function canSee(requiredRoles: string[], userRoles: string[]) {
  if (!requiredRoles.length) return true;
  return requiredRoles.some(role => userRoles.includes(role));
}

export function Layout() {
  const auth = useAuth();
  const { t } = useI18n();
  const location = useLocation();
  const [mobileOpen, setMobileOpen] = useState(false);
  const sidebarRef = useRef<HTMLElement | null>(null);
  const menuButtonRef = useRef<HTMLButtonElement | null>(null);
  const visibleNav = nav.filter(item => canSee(item.roles, auth.roles));
  const standaloneNav = visibleNav.filter(item => !item.group);
  const groupedNav = navGroups.map(group => ({ group, items: visibleNav.filter(item => item.group === group.key) })).filter(({ items }) => items.length > 0);
  const [expandedGroups, setExpandedGroups] = useState<Record<string, boolean>>({});

  useEffect(() => {
    window.scrollTo(0, 0);
  }, [location.pathname]);

  useEffect(() => {
    if (!mobileOpen) return;
    const focusTimer = window.setTimeout(() => {
      sidebarRef.current?.querySelector<HTMLElement>('a, button')?.focus();
    }, 0);
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') setMobileOpen(false);
      if (sidebarRef.current) keepFocusInside(event, sidebarRef.current);
    };
    document.addEventListener('keydown', onKeyDown);
    return () => {
      window.clearTimeout(focusTimer);
      document.removeEventListener('keydown', onKeyDown);
      menuButtonRef.current?.focus();
    };
  }, [mobileOpen]);

  return (
    <div className={mobileOpen ? 'appShell appShell--menuOpen' : 'appShell'}>
      <header className="mobileTopbar">
        <div className="brand brand--compact">
          <div className="brand__mark"><BrandMark /></div>
          <div><strong>Tenebit</strong><small>{t('nav.tagline')}</small></div>
        </div>
        <button ref={menuButtonRef} className="iconButton" type="button" aria-label={mobileOpen ? t('nav.closeMenu') : t('nav.openMenu')} aria-expanded={mobileOpen} onClick={() => setMobileOpen(open => !open)}>{mobileOpen ? <X size={18} /> : <Menu size={18} />}</button>
      </header>

      {mobileOpen ? <button className="sidebarScrim" type="button" aria-label={t('nav.closeMenu')} onClick={() => setMobileOpen(false)} /> : null}

      <aside className="sidebar" ref={sidebarRef}>
        <div className="brand">
          <div className="brand__mark"><BrandMark /></div>
          <div>
            <strong>Tenebit</strong>
            <small>{t('nav.sidebarTagline')}</small>
          </div>
        </div>
        <nav className="nav" aria-label={t('nav.mainNavAria')}>
          {standaloneNav.filter(item => item.to !== '/settings').map(item => {
            const Icon = item.icon;
            return <NavLink key={item.to} to={item.to} end={item.to === '/dashboard'} onClick={() => setMobileOpen(false)}><Icon size={18} />{t(item.labelKey)}</NavLink>;
          })}
          {groupedNav.map(({ group, items }) => {
            const isExpanded = expandedGroups[group.key] !== false;
            return (
              <div className="navGroup" key={group.key}>
                <button type="button" className="navGroup__header" aria-expanded={isExpanded} onClick={() => setExpandedGroups(current => ({ ...current, [group.key]: !isExpanded }))}>
                  <span className="navGroup__label">{t(group.labelKey)}</span>
                  <ChevronDown size={16} style={{ transform: isExpanded ? 'rotate(0)' : 'rotate(-90deg)' }} />
                </button>
                {isExpanded && (
                  <>
                    {items.map(item => {
                      const Icon = item.icon;
                      return <NavLink key={item.to} to={item.to} end={item.to === '/dashboard'} onClick={() => setMobileOpen(false)}><Icon size={18} />{t(item.labelKey)}</NavLink>;
                    })}
                  </>
                )}
              </div>
            );
          })}
          {standaloneNav.filter(item => item.to === '/settings').map(item => {
            const Icon = item.icon;
            return <NavLink key={item.to} to={item.to} end={item.to === '/dashboard'} onClick={() => setMobileOpen(false)}><Icon size={18} />{t(item.labelKey)}</NavLink>;
          })}
        </nav>
        <div className="sidebarFooter">
          <span>{auth.userName}</span>
          <Button variant="ghost" onClick={auth.logout} icon={<LogOut size={16} />}>{t('nav.logout')}</Button>
        </div>
      </aside>

      <main className="content">
        <Outlet />
      </main>
    </div>
  );
}
