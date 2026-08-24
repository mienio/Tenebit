import { AlertTriangle, Building2, Boxes, Check, KeyRound, MapPin, ShieldCheck, UserCheck, Users } from 'lucide-react';
import { useEffect, useState, type ReactNode } from 'react';
import { Link } from 'react-router-dom';
import { LoadingState } from '../components/StateViews';
import { getAdminDashboard, type AdminDashboard } from './adminApi';
import { AdminDateRange, defaultRange, type DateRange } from './AdminDateRange';
import { AdminPageHeader, AdminShell } from './AdminShell';
import { AdminTimeSeriesChart } from './AdminTimeSeriesChart';

export function AdminDashboardPage() {
  const [range, setRange] = useState<DateRange>(() => defaultRange(30));
  const [data, setData] = useState<AdminDashboard | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    setData(null);
    getAdminDashboard(range.from, range.to)
      .then(result => { if (!cancelled) setData(result); })
      .catch(err => { if (!cancelled) setError(err instanceof Error ? err.message : 'Nie udało się pobrać danych.'); });
    return () => { cancelled = true; };
  }, [range]);

  return (
    <AdminShell>
      <AdminPageHeader
        title="Pulpit"
        description="Stan całej platformy w jednym miejscu."
        actions={<AdminDateRange value={range} onChange={setRange} />}
      />

      {error ? <p className="formMessage formMessage--error">{error}</p> : null}
      {!data ? <LoadingState /> : (
        <>
          <section className="adminStats">
            <Stat icon={<Building2 size={18} />} label="Organizacje" value={data.organizations} />
            <Stat icon={<Users size={18} />} label="Użytkownicy" value={data.users} hint={`${data.activeUsers} aktywnych`} />
            <Stat icon={<Boxes size={18} />} label="Aktywa" value={data.assets} />
            <Stat icon={<UserCheck size={18} />} label="Osoby" value={data.people} />
            <Stat icon={<MapPin size={18} />} label="Lokalizacje" value={data.locations} />
            <Stat icon={<KeyRound size={18} />} label="Licencje" value={data.licenses} />
            <Stat icon={<ShieldCheck size={18} />} label="Udane logowania" value={data.loginsInRange} hint="w zakresie" />
            <Stat
              icon={<AlertTriangle size={18} />}
              label="Nieudane logowania"
              value={data.failedLoginsInRange}
              tone={data.failedLoginsInRange > 20 ? 'warn' : undefined}
              hint={data.failedLoginsInRange > 20 ? 'Sprawdź logowania' : 'w zakresie'}
            />
          </section>

          {data.pendingReview > 0 ? (
            <p className="adminNotice">
              <Check size={16} />
              Nazw firm do sprawdzenia pod kątem regulaminu: <strong>{data.pendingReview}</strong>.{' '}
              <Link to="/admin/organizations">Przejdź do kolejki</Link>
            </p>
          ) : null}

          {data.suspendedOrganizations > 0 ? (
            <p className="adminNotice">
              <AlertTriangle size={16} />
              Zawieszone organizacje: <strong>{data.suspendedOrganizations}</strong>.{' '}
              <Link to="/admin/organizations">Zobacz listę</Link>
            </p>
          ) : null}

          <section className="adminCharts">
            <div className="card"><AdminTimeSeriesChart series={data.assetsCreated} /></div>
            <div className="card"><AdminTimeSeriesChart series={data.organizationsCreated} color="var(--success, #16a34a)" /></div>
            <div className="card"><AdminTimeSeriesChart series={data.logins} color="#8b5cf6" /></div>
            <div className="card"><AdminTimeSeriesChart series={data.failedLogins} color="var(--danger)" /></div>
          </section>

          <section className="adminSplit">
            <div className="card">
              <h2 className="adminCardTitle">Plany</h2>
              {data.plans.length === 0 ? <p className="adminMuted">Brak subskrypcji.</p> : (
                <ul className="adminBars">
                  {data.plans.map(plan => {
                    const max = Math.max(...data.plans.map(p => p.count), 1);
                    return (
                      <li key={plan.plan}>
                        <div><span>{plan.plan}</span><strong>{plan.count}</strong></div>
                        <span className="adminBars__track"><i style={{ width: `${(plan.count / max) * 100}%` }} /></span>
                      </li>
                    );
                  })}
                </ul>
              )}
            </div>

            <div className="card">
              <h2 className="adminCardTitle">Najnowsze organizacje</h2>
              <ul className="adminList">
                {data.newestOrganizations.map(org => (
                  <li key={org.id}>
                    <Link to={`/admin/organizations/${org.id}`}>{org.name}</Link>
                    <span className="adminMuted">
                      {new Date(org.createdAt).toLocaleDateString('pl-PL')} · {org.assetCount} aktywów
                      {org.isSuspended ? ' · zawieszona' : ''}
                    </span>
                  </li>
                ))}
              </ul>
            </div>
          </section>
        </>
      )}
    </AdminShell>
  );
}

function Stat({ icon, label, value, hint, tone }: { icon: ReactNode; label: string; value: number; hint?: string; tone?: 'warn' }) {
  return (
    <div className={`adminStat${tone === 'warn' ? ' adminStat--warn' : ''}`}>
      <span className="adminStat__icon">{icon}</span>
      <div>
        <span className="adminStat__label">{label}</span>
        <strong className="adminStat__value">{value.toLocaleString('pl-PL')}</strong>
        {hint ? <span className="adminStat__hint">{hint}</span> : null}
      </div>
    </div>
  );
}
