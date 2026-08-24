import { Ban, LogOut, RotateCcw, Search } from 'lucide-react';
import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { Button } from '../components/Button';
import { TextInput } from '../components/FormFields';
import { LoadingState } from '../components/StateViews';
import { blockUser, forceSignOut, listAdminUsers, unblockUser, type AdminPage, type AdminUserListItem } from './adminApi';
import { AdminActionDialog, type AdminActionRequest } from './AdminActionDialog';
import { AdminPageHeader, AdminShell } from './AdminShell';
import { useDebouncedValue } from '../hooks/useDebouncedValue';

const PAGE_SIZE = 50;

export function AdminUsersPage() {
  const [search, setSearch] = useState('');
  const debouncedSearch = useDebouncedValue(search, 300);
  const [page, setPage] = useState(1);
  const [data, setData] = useState<AdminPage<AdminUserListItem> | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [action, setAction] = useState<AdminActionRequest | null>(null);
  const [reloadKey, setReloadKey] = useState(0);

  useEffect(() => { setPage(1); }, [debouncedSearch]);

  useEffect(() => {
    let cancelled = false;
    setData(null);
    listAdminUsers(debouncedSearch, page, PAGE_SIZE)
      .then(result => { if (!cancelled) setData(result); })
      .catch(err => { if (!cancelled) setError(err instanceof Error ? err.message : 'Nie udało się pobrać użytkowników.'); });
    return () => { cancelled = true; };
  }, [debouncedSearch, page, reloadKey]);

  const totalPages = data ? Math.max(1, Math.ceil(data.total / data.pageSize)) : 1;

  return (
    <AdminShell>
      <AdminPageHeader title="Użytkownicy" description="Wszystkie konta na platformie, niezależnie od organizacji." />

      <div className="adminToolbar">
        <label className="adminSearch">
          <Search size={16} />
          <TextInput placeholder="Szukaj po pełnym e-mailu lub nazwie organizacji…" value={search} onChange={e => setSearch(e.target.value)} />
        </label>
        {data ? <span className="adminMuted">{data.total} kont</span> : null}
      </div>

      {error ? <p className="formMessage formMessage--error">{error}</p> : null}
      {!data ? <LoadingState /> : (
        <>
          <div className="card adminTableCard">
            <table className="adminTable">
              <thead>
                <tr>
                  <th>Użytkownik</th>
                  <th>Organizacja</th>
                  <th>Role</th>
                  <th>2FA</th>
                  <th>Ostatnie logowanie</th>
                  <th>Status</th>
                  <th aria-label="Akcje" />
                </tr>
              </thead>
              <tbody>
                {data.items.map(user => (
                  <tr key={user.id} className={!user.isActive ? 'adminTable__row--muted' : undefined}>
                    <td>
                      <strong>{user.initials}</strong>
                      <span className="adminMuted adminBlock">{user.maskedEmail}</span>
                    </td>
                    <td>
                      <Link to={`/admin/organizations/${user.organizationId}`}>{user.organizationName}</Link>
                      {user.organizationSuspended ? <span className="adminTag adminTag--danger adminBlock">org. zawieszona</span> : null}
                    </td>
                    <td>{user.roles.join(', ') || '—'}</td>
                    <td>{user.isTwoFactorEnabled ? 'tak' : 'nie'}</td>
                    <td>{user.lastLoginAt ? new Date(user.lastLoginAt).toLocaleString('pl-PL') : '—'}</td>
                    <td>
                      {user.isActive
                        ? <span className="adminTag">Aktywny</span>
                        : <span className="adminTag adminTag--danger">Zablokowany</span>}
                      {!user.isEmailVerified ? <span className="adminTag adminBlock">e-mail niepotwierdzony</span> : null}
                    </td>
                    <td className="adminTable__actions">
                      {user.isActive ? (
                        <>
                          <Button
                            variant="secondary"
                            icon={<LogOut size={14} />}
                            onClick={() => setAction({
                              title: 'Wylogować ze wszystkich urządzeń?',
                              description: `Unieważni to wszystkie aktywne sesje konta ${user.maskedEmail}. Konto pozostanie aktywne.`,
                              confirmLabel: 'Wyloguj',
                              requiresReason: false,
                              run: (_reason, totp) => forceSignOut(user.id, totp),
                            })}
                          >Wyloguj</Button>
                          <Button
                            variant="danger"
                            icon={<Ban size={14} />}
                            onClick={() => setAction({
                              title: 'Zablokować konto?',
                              description: `Konto ${user.maskedEmail} straci dostęp natychmiast. Operacja jest odwracalna.`,
                              confirmLabel: 'Zablokuj',
                              requiresReason: true,
                              run: (reason, totp) => blockUser(user.id, reason, totp),
                            })}
                          >Zablokuj</Button>
                        </>
                      ) : (
                        <Button
                          variant="secondary"
                          icon={<RotateCcw size={14} />}
                          onClick={() => setAction({
                            title: 'Odblokować konto?',
                            description: `Konto ${user.maskedEmail} odzyska dostęp do systemu.`,
                            confirmLabel: 'Odblokuj',
                            requiresReason: false,
                            run: (_reason, totp) => unblockUser(user.id, totp),
                          })}
                        >Odblokuj</Button>
                      )}
                    </td>
                  </tr>
                ))}
                {data.items.length === 0 ? <tr><td colSpan={7} className="adminMuted">Brak wyników.</td></tr> : null}
              </tbody>
            </table>
          </div>

          <AdminPager page={data.page} totalPages={totalPages} onChange={setPage} />
        </>
      )}

      <AdminActionDialog request={action} onClose={() => setAction(null)} onDone={() => setReloadKey(key => key + 1)} />
    </AdminShell>
  );
}

export function AdminPager({ page, totalPages, onChange }: { page: number; totalPages: number; onChange: (page: number) => void }) {
  if (totalPages <= 1) return null;
  return (
    <nav className="adminPager">
      <Button variant="secondary" disabled={page <= 1} onClick={() => onChange(page - 1)}>Poprzednia</Button>
      <span className="adminMuted">Strona {page} z {totalPages}</span>
      <Button variant="secondary" disabled={page >= totalPages} onClick={() => onChange(page + 1)}>Następna</Button>
    </nav>
  );
}
