import { Ban, Check, RotateCcw, Search } from 'lucide-react';
import { useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { Button } from '../components/Button';
import { TextInput } from '../components/FormFields';
import { LoadingState } from '../components/StateViews';
import {
  listAdminOrganizations,
  markOrganizationReviewed,
  restoreOrganization,
  suspendOrganization,
  type AdminOrganizationSummary,
} from './adminApi';
import { AdminActionDialog, type AdminActionRequest } from './AdminActionDialog';
import { AdminPageHeader, AdminShell } from './AdminShell';

type Filter = 'all' | 'pending' | 'active' | 'suspended';

export function AdminOrganizationsPage() {
  const [organizations, setOrganizations] = useState<AdminOrganizationSummary[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [search, setSearch] = useState('');
  const [filter, setFilter] = useState<Filter>('all');
  const [action, setAction] = useState<AdminActionRequest | null>(null);
  const [reloadKey, setReloadKey] = useState(0);
  const [reviewing, setReviewing] = useState<string | null>(null);

  async function handleReview(id: string) {
    setReviewing(id);
    try {
      await markOrganizationReviewed(id);
      setReloadKey(key => key + 1);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Nie udało się oznaczyć jako sprawdzonej.');
    } finally {
      setReviewing(null);
    }
  }

  useEffect(() => {
    let cancelled = false;
    setOrganizations(null);
    listAdminOrganizations()
      .then(result => { if (!cancelled) setOrganizations(result); })
      .catch(err => { if (!cancelled) setError(err instanceof Error ? err.message : 'Nie udało się pobrać organizacji.'); });
    return () => { cancelled = true; };
  }, [reloadKey]);

  const visible = useMemo(() => {
    if (!organizations) return [];
    const term = search.trim().toLowerCase();
    return organizations.filter(org => {
      if (filter === 'active' && org.isSuspended) return false;
      if (filter === 'suspended' && !org.isSuspended) return false;
      if (filter === 'pending' && org.reviewedAt) return false;
      return !term || org.name.toLowerCase().includes(term) || org.country.toLowerCase().includes(term);
    });
  }, [organizations, search, filter]);

  const pendingCount = organizations?.filter(org => !org.reviewedAt).length ?? 0;

  return (
    <AdminShell>
      <AdminPageHeader
        title="Organizacje"
        description="Wszystkie firmy w systemie. Filtr „Do sprawdzenia” pokazuje te, których nazwy jeszcze nie zweryfikowałeś pod kątem regulaminu."
      />

      {pendingCount > 0 ? (
        <p className="adminNotice">
          <Check size={16} />
          Nazw do sprawdzenia: <strong>{pendingCount}</strong>. Po weryfikacji kliknij „Sprawdzone”, żeby zniknęły z kolejki.
        </p>
      ) : null}

      <div className="adminToolbar">
        <label className="adminSearch">
          <Search size={16} />
          <TextInput placeholder="Szukaj nazwy lub kraju…" value={search} onChange={e => setSearch(e.target.value)} />
        </label>
        <div className="adminRange">
          {(['all', 'pending', 'active', 'suspended'] as Filter[]).map(value => (
            <button
              key={value}
              type="button"
              className={`adminRange__button${filter === value ? ' adminRange__button--active' : ''}`}
              onClick={() => setFilter(value)}
            >
              {value === 'all' ? 'Wszystkie'
                : value === 'pending' ? 'Do sprawdzenia'
                : value === 'active' ? 'Aktywne' : 'Zawieszone'}
            </button>
          ))}
        </div>
      </div>

      {error ? <p className="formMessage formMessage--error">{error}</p> : null}
      {!organizations ? <LoadingState /> : (
        <div className="card adminTableCard">
          <table className="adminTable">
            <thead>
              <tr>
                <th>Nazwa</th>
                <th>Kraj</th>
                <th>Plan</th>
                <th>Użytk.</th>
                <th>Aktywa</th>
                <th>Osoby</th>
                <th>Lokal.</th>
                <th>Utworzono</th>
                <th>Status</th>
                <th>Nazwa sprawdzona</th>
                <th aria-label="Akcje" />
              </tr>
            </thead>
            <tbody>
              {visible.map(org => (
                <tr key={org.id} className={org.isSuspended ? 'adminTable__row--muted' : undefined}>
                  <td><Link to={`/admin/organizations/${org.id}`}>{org.name}</Link></td>
                  <td>{org.country}</td>
                  <td>{org.planName}</td>
                  <td>{org.userCount}</td>
                  <td>{org.assetCount}</td>
                  <td>{org.peopleCount}</td>
                  <td>{org.locationCount}</td>
                  <td>{new Date(org.createdAt).toLocaleDateString('pl-PL')}</td>
                  <td>
                    {org.isSuspended
                      ? <span className="adminTag adminTag--danger" title={org.suspendedReason ?? undefined}>Zawieszona</span>
                      : <span className="adminTag">Aktywna</span>}
                  </td>
                  <td>
                    {org.reviewedAt
                      ? <span className="adminTag adminTag--ok" title={new Date(org.reviewedAt).toLocaleString('pl-PL')}>Tak</span>
                      : (
                        <Button
                          variant="secondary"
                          icon={<Check size={14} />}
                          disabled={reviewing === org.id}
                          onClick={() => handleReview(org.id)}
                        >{reviewing === org.id ? '…' : 'Sprawdzone'}</Button>
                      )}
                  </td>
                  <td className="adminTable__actions">
                    {org.isSuspended ? (
                      <Button
                        variant="secondary"
                        icon={<RotateCcw size={14} />}
                        onClick={() => setAction({
                          title: 'Przywrócić organizację?',
                          description: `Użytkownicy „${org.name}” znów będą mogli się zalogować.`,
                          confirmLabel: 'Przywróć',
                          requiresReason: false,
                          run: (_reason, totp) => restoreOrganization(org.id, totp),
                        })}
                      >Przywróć</Button>
                    ) : (
                      <Button
                        variant="danger"
                        icon={<Ban size={14} />}
                        onClick={() => setAction({
                          title: 'Zawiesić organizację?',
                          description: `Zablokuje to logowanie wszystkim użytkownikom „${org.name}”. Dane pozostaną nienaruszone, a operację można cofnąć.`,
                          confirmLabel: 'Zawieś',
                          requiresReason: true,
                          run: (reason, totp) => suspendOrganization(org.id, reason, totp),
                        })}
                      >Zawieś</Button>
                    )}
                  </td>
                </tr>
              ))}
              {visible.length === 0 ? <tr><td colSpan={11} className="adminMuted">Brak wyników.</td></tr> : null}
            </tbody>
          </table>
        </div>
      )}

      <AdminActionDialog request={action} onClose={() => setAction(null)} onDone={() => setReloadKey(key => key + 1)} />
    </AdminShell>
  );
}
