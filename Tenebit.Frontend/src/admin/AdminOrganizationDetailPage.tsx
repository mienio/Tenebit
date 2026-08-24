import { ArrowLeft, Ban, RotateCcw, ShieldCheck } from 'lucide-react';
import { useEffect, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { Button } from '../components/Button';
import { LoadingState } from '../components/StateViews';
import {
  AdminApiError,
  getAdminOrganization,
  restoreOrganization,
  suspendOrganization,
  type AdminCountSlice,
  type AdminOrganizationDetail,
} from './adminApi';
import { AdminActionDialog, type AdminActionRequest } from './AdminActionDialog';
import { AdminDateRange, defaultRange, type DateRange } from './AdminDateRange';
import { AdminPageHeader, AdminShell } from './AdminShell';
import { AdminTimeSeriesChart } from './AdminTimeSeriesChart';

export function AdminOrganizationDetailPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [range, setRange] = useState<DateRange>(() => defaultRange(30));
  const [detail, setDetail] = useState<AdminOrganizationDetail | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [action, setAction] = useState<AdminActionRequest | null>(null);
  const [reloadKey, setReloadKey] = useState(0);

  useEffect(() => {
    if (!id) return;
    let cancelled = false;
    setDetail(null);
    getAdminOrganization(id, range.from, range.to)
      .then(result => { if (!cancelled) setDetail(result); })
      .catch(err => {
        if (cancelled) return;
        setError(err instanceof Error ? err.message : 'Nie udało się pobrać danych organizacji.');
        if (err instanceof AdminApiError && err.status === 401) navigate('/admin/login', { replace: true });
      });
    return () => { cancelled = true; };
  }, [id, navigate, range, reloadKey]);

  if (error) {
    return <AdminShell><p className="formMessage formMessage--error">{error}</p></AdminShell>;
  }
  if (!detail) {
    return <AdminShell><LoadingState /></AdminShell>;
  }

  const { summary, users, assetsByStatus, assetsByCategory, peopleByStatus, locationCount, assetsCreated } = detail;

  return (
    <AdminShell>
      <Link to="/admin/organizations" className="adminBack"><ArrowLeft size={16} /> Wróć do organizacji</Link>

      <AdminPageHeader
        title={summary.name}
        description={`${summary.country} · plan ${summary.planName} (${summary.subscriptionStatus}) · utworzono ${new Date(summary.createdAt).toLocaleDateString('pl-PL')}`}
        actions={summary.isSuspended ? (
          <Button
            variant="secondary"
            icon={<RotateCcw size={16} />}
            onClick={() => setAction({
              title: 'Przywrócić organizację?',
              description: `Użytkownicy „${summary.name}” znów będą mogli się zalogować.`,
              confirmLabel: 'Przywróć',
              requiresReason: false,
              run: (_reason, totp) => restoreOrganization(summary.id, totp),
            })}
          >Przywróć dostęp</Button>
        ) : (
          <Button
            variant="danger"
            icon={<Ban size={16} />}
            onClick={() => setAction({
              title: 'Zawiesić organizację?',
              description: `Zablokuje to logowanie wszystkim użytkownikom „${summary.name}”. Dane pozostaną nienaruszone, a operację można cofnąć.`,
              confirmLabel: 'Zawieś',
              requiresReason: true,
              run: (reason, totp) => suspendOrganization(summary.id, reason, totp),
            })}
          >Zawieś organizację</Button>
        )}
      />

      {summary.isSuspended ? (
        <p className="adminNotice adminNotice--danger">
          Organizacja zawieszona {summary.suspendedAt ? new Date(summary.suspendedAt).toLocaleString('pl-PL') : ''}
          {summary.suspendedReason ? ` — powód: ${summary.suspendedReason}` : ''}
        </p>
      ) : null}

      <p className="adminNotice">
        <ShieldCheck size={16} />
        Panel pokazuje wyłącznie liczby i statystyki. Nazwy aktywów, dane osób i pełne adresy e-mail nigdy nie opuszczają serwera.
      </p>

      <section className="adminStats adminStats--compact">
        <MiniStat label="Użytkownicy" value={summary.userCount} />
        <MiniStat label="Aktywa" value={summary.assetCount} />
        <MiniStat label="Osoby" value={summary.peopleCount} />
        <MiniStat label="Lokalizacje" value={locationCount} />
      </section>

      <div className="adminToolbar">
        <AdminDateRange value={range} onChange={setRange} />
      </div>

      <section className="adminSection">
        <div className="card"><AdminTimeSeriesChart series={assetsCreated} /></div>
      </section>

      <section className="adminSplit">
        <Breakdown title="Aktywa wg statusu" slices={assetsByStatus} />
        <Breakdown title="Aktywa wg kategorii" slices={assetsByCategory} />
        <Breakdown title="Osoby wg statusu" slices={peopleByStatus} />
      </section>

      <section className="adminSection">
        <h2 className="adminCardTitle">Konta użytkowników ({users.length})</h2>
        <div className="card adminTableCard">
          <table className="adminTable">
            <thead>
              <tr><th>Konto</th><th>Role</th><th>2FA</th><th>Ostatnie logowanie</th><th>Status</th></tr>
            </thead>
            <tbody>
              {users.map(user => (
                <tr key={user.id} className={!user.isActive ? 'adminTable__row--muted' : undefined}>
                  <td>
                    <strong>{user.initials}</strong>
                    <span className="adminMuted adminBlock">{user.maskedEmail}</span>
                  </td>
                  <td>{user.roles.join(', ') || '—'}</td>
                  <td>{user.isTwoFactorEnabled ? 'tak' : 'nie'}</td>
                  <td>{user.lastLoginAt ? new Date(user.lastLoginAt).toLocaleString('pl-PL') : '—'}</td>
                  <td>
                    {user.isActive
                      ? <span className="adminTag">Aktywny</span>
                      : <span className="adminTag adminTag--danger">Zablokowany</span>}
                  </td>
                </tr>
              ))}
              {users.length === 0 ? <tr><td colSpan={5} className="adminMuted">Brak kont.</td></tr> : null}
            </tbody>
          </table>
        </div>
      </section>

      <AdminActionDialog request={action} onClose={() => setAction(null)} onDone={() => setReloadKey(key => key + 1)} />
    </AdminShell>
  );
}

function Breakdown({ title, slices }: { title: string; slices: AdminCountSlice[] }) {
  const max = Math.max(...slices.map(s => s.count), 1);
  return (
    <div className="card">
      <h2 className="adminCardTitle">{title}</h2>
      {slices.length === 0 ? <p className="adminMuted">Brak danych.</p> : (
        <ul className="adminBars">
          {slices.map(slice => (
            <li key={slice.label}>
              <div><span>{slice.label}</span><strong>{slice.count}</strong></div>
              <span className="adminBars__track"><i style={{ width: `${(slice.count / max) * 100}%` }} /></span>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

function MiniStat({ label, value }: { label: string; value: number }) {
  return (
    <div className="adminStat">
      <div>
        <span className="adminStat__label">{label}</span>
        <strong className="adminStat__value">{value.toLocaleString('pl-PL')}</strong>
      </div>
    </div>
  );
}
