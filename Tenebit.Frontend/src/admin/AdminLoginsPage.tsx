import { Search } from 'lucide-react';
import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { TextInput } from '../components/FormFields';
import { LoadingState } from '../components/StateViews';
import { useDebouncedValue } from '../hooks/useDebouncedValue';
import { listAdminLogins, type AdminLoginEntry, type AdminPage } from './adminApi';
import { AdminPageHeader, AdminShell } from './AdminShell';
import { AdminPager } from './AdminUsersPage';

const PAGE_SIZE = 50;

// Server-side reason codes rendered in plain Polish.
const REASONS: Record<string, string> = {
  unknown_account: 'nieznane konto',
  bad_password: 'błędne hasło',
  account_blocked: 'konto zablokowane',
  email_unverified: 'e-mail niepotwierdzony',
  organization_suspended: 'organizacja zawieszona',
  bad_two_factor: 'błędny kod 2FA',
  no_organization: 'brak organizacji',
};

type Filter = 'all' | 'success' | 'failed';

export function AdminLoginsPage() {
  const [search, setSearch] = useState('');
  const debouncedSearch = useDebouncedValue(search, 300);
  const [filter, setFilter] = useState<Filter>('all');
  const [page, setPage] = useState(1);
  const [data, setData] = useState<AdminPage<AdminLoginEntry> | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => { setPage(1); }, [debouncedSearch, filter]);

  useEffect(() => {
    let cancelled = false;
    setData(null);
    const succeeded = filter === 'all' ? null : filter === 'success';
    listAdminLogins(debouncedSearch, succeeded, page, PAGE_SIZE)
      .then(result => { if (!cancelled) setData(result); })
      .catch(err => { if (!cancelled) setError(err instanceof Error ? err.message : 'Nie udało się pobrać historii logowań.'); });
    return () => { cancelled = true; };
  }, [debouncedSearch, filter, page]);

  const totalPages = data ? Math.max(1, Math.ceil(data.total / data.pageSize)) : 1;

  return (
    <AdminShell>
      <AdminPageHeader
        title="Logowania"
        description="Historia prób logowania do systemu. Rejestrowana od wdrożenia panelu — wcześniejsze logowania nie były zapisywane."
      />

      <div className="adminToolbar">
        <label className="adminSearch">
          <Search size={16} />
          <TextInput placeholder="Szukaj e-maila lub adresu IP…" value={search} onChange={e => setSearch(e.target.value)} />
        </label>
        <div className="adminRange">
          {(['all', 'success', 'failed'] as Filter[]).map(value => (
            <button
              key={value}
              type="button"
              className={`adminRange__button${filter === value ? ' adminRange__button--active' : ''}`}
              onClick={() => setFilter(value)}
            >
              {value === 'all' ? 'Wszystkie' : value === 'success' ? 'Udane' : 'Nieudane'}
            </button>
          ))}
        </div>
        {data ? <span className="adminMuted">{data.total} zdarzeń</span> : null}
      </div>

      {error ? <p className="formMessage formMessage--error">{error}</p> : null}
      {!data ? <LoadingState /> : (
        <>
          <div className="card adminTableCard">
            <table className="adminTable">
              <thead>
                <tr>
                  <th>Kiedy</th>
                  <th>E-mail</th>
                  <th>Organizacja</th>
                  <th>Wynik</th>
                  <th>Adres IP</th>
                  <th>Przeglądarka</th>
                </tr>
              </thead>
              <tbody>
                {data.items.map(entry => (
                  <tr key={entry.id}>
                    <td>{new Date(entry.createdAt).toLocaleString('pl-PL')}</td>
                    <td>{entry.maskedEmail}</td>
                    <td>
                      {entry.organizationId
                        ? <Link to={`/admin/organizations/${entry.organizationId}`}>{entry.organizationName ?? '—'}</Link>
                        : <span className="adminMuted">—</span>}
                    </td>
                    <td>
                      {entry.succeeded
                        ? <span className="adminTag adminTag--ok">Udane</span>
                        : <span className="adminTag adminTag--danger">{REASONS[entry.failureReason ?? ''] ?? 'nieudane'}</span>}
                    </td>
                    <td>{entry.ipAddress ?? <span className="adminMuted">—</span>}</td>
                    <td className="adminTable__agent" title={entry.userAgent ?? undefined}>{entry.userAgent ?? '—'}</td>
                  </tr>
                ))}
                {data.items.length === 0 ? <tr><td colSpan={6} className="adminMuted">Brak zdarzeń.</td></tr> : null}
              </tbody>
            </table>
          </div>
          <AdminPager page={data.page} totalPages={totalPages} onChange={setPage} />
        </>
      )}
    </AdminShell>
  );
}
