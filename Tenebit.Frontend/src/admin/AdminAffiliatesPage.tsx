import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { Field, SelectInput } from '../components/FormFields';
import { LoadingState } from '../components/StateViews';
import { AdminPageHeader, AdminShell } from './AdminShell';
import { listAffiliates, type AffiliateAdminListItem, type AffiliateStatus } from './adminApi';

const STATUS_LABELS: Record<AffiliateStatus, string> = {
  PendingApproval: 'Oczekuje na zatwierdzenie',
  Active: 'Aktywny',
  Blocked: 'Zablokowany',
};

function money(value: number): string {
  return `${value.toFixed(2)} €`;
}

export function AdminAffiliatesPage() {
  const [items, setItems] = useState<AffiliateAdminListItem[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [statusFilter, setStatusFilter] = useState<AffiliateStatus | ''>('');

  useEffect(() => {
    let cancelled = false;
    setItems(null);
    listAffiliates(statusFilter || undefined)
      .then(result => { if (!cancelled) setItems(result); })
      .catch(err => { if (!cancelled) setError(err instanceof Error ? err.message : 'Nie udało się pobrać listy afiliantów.'); });
    return () => { cancelled = true; };
  }, [statusFilter]);

  const sorted = items ? [...items].sort((a, b) => {
    // Pending approval first (needs action), then by amount currently due.
    if (a.status === 'PendingApproval' && b.status !== 'PendingApproval') return -1;
    if (b.status === 'PendingApproval' && a.status !== 'PendingApproval') return 1;
    return b.totalDueNow - a.totalDueNow;
  }) : null;

  return (
    <AdminShell>
      <AdminPageHeader
        title="Program partnerski"
        description="Afilianci promujący Tenebit własnymi kodami. Kliknij wiersz, żeby zobaczyć kto i kiedy im zapłacił oraz statystyki kodów."
      />

      <div style={{ marginBottom: 16, maxWidth: 320 }}>
        <Field label="Status">
          <SelectInput value={statusFilter} onChange={e => setStatusFilter(e.target.value as AffiliateStatus | '')}>
            <option value="">Wszyscy</option>
            <option value="PendingApproval">Oczekują na zatwierdzenie</option>
            <option value="Active">Aktywni</option>
            <option value="Blocked">Zablokowani</option>
          </SelectInput>
        </Field>
      </div>

      {error ? <p className="formMessage formMessage--error">{error}</p> : null}

      {!sorted ? <LoadingState /> : (
        <div className="card adminTableCard">
          <table className="adminTable">
            <thead>
              <tr>
                <th>Afiliant</th>
                <th>Status</th>
                <th>Aktywne kody</th>
                <th>Sprzedaże</th>
                <th>Należne teraz</th>
                <th>Wypłacono łącznie</th>
              </tr>
            </thead>
            <tbody>
              {sorted.map(item => (
                <tr key={item.id}>
                  <td>
                    <Link to={`/admin/affiliates/${item.id}`} style={{ fontWeight: 600 }}>{item.fullName}</Link>
                    <div className="adminMuted">{item.email}</div>
                  </td>
                  <td>
                    {item.status === 'PendingApproval' ? <span className="adminTag adminTag--danger">{STATUS_LABELS[item.status]}</span>
                      : item.status === 'Blocked' ? <span className="adminTag adminTag--danger">{STATUS_LABELS[item.status]}</span>
                      : <span className="adminTag adminTag--ok">{STATUS_LABELS[item.status]}</span>}
                  </td>
                  <td>{item.activeCodeCount}</td>
                  <td>{item.lifetimeConversionCount}</td>
                  <td>{money(item.totalDueNow)}</td>
                  <td>{money(item.totalPaidLifetime)}</td>
                </tr>
              ))}
              {sorted.length === 0 ? <tr><td colSpan={6} className="adminMuted">Brak afiliantów.</td></tr> : null}
            </tbody>
          </table>
        </div>
      )}
    </AdminShell>
  );
}
