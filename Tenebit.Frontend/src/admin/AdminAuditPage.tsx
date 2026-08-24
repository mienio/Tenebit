import { useEffect, useState } from 'react';
import { LoadingState } from '../components/StateViews';
import { listAdminAudit, type AdminAuditEntry } from './adminApi';
import { AdminPageHeader, AdminShell } from './AdminShell';

// Machine-readable verbs from the server, rendered in plain Polish.
const ACTIONS: Record<string, string> = {
  'admin.signed_in': 'Logowanie do panelu',
  'admin.sign_in_failed': 'Nieudane logowanie do panelu',
  'organization.suspended': 'Zawieszenie organizacji',
  'organization.restored': 'Przywrócenie organizacji',
  'user.blocked': 'Blokada konta',
  'user.unblocked': 'Odblokowanie konta',
  'user.forced_sign_out': 'Wymuszone wylogowanie',
};

const DANGEROUS = new Set(['organization.suspended', 'user.blocked', 'admin.sign_in_failed']);

export function AdminAuditPage() {
  const [entries, setEntries] = useState<AdminAuditEntry[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    listAdminAudit(200)
      .then(result => { if (!cancelled) setEntries(result); })
      .catch(err => { if (!cancelled) setError(err instanceof Error ? err.message : 'Nie udało się pobrać dziennika.'); });
    return () => { cancelled = true; };
  }, []);

  return (
    <AdminShell>
      <AdminPageHeader
        title="Dziennik administratora"
        description="Każde logowanie i każda akcja wykonana w tym panelu. Zapisu nie można zmienić ani usunąć — to ślad na wypadek przejęcia konta."
      />

      {error ? <p className="formMessage formMessage--error">{error}</p> : null}
      {!entries ? <LoadingState /> : (
        <div className="card adminTableCard">
          <table className="adminTable">
            <thead>
              <tr>
                <th>Kiedy</th>
                <th>Akcja</th>
                <th>Obiekt</th>
                <th>Szczegóły</th>
                <th>Adres IP</th>
              </tr>
            </thead>
            <tbody>
              {entries.map(entry => (
                <tr key={entry.id}>
                  <td>{new Date(entry.createdAt).toLocaleString('pl-PL')}</td>
                  <td>
                    <span className={`adminTag${DANGEROUS.has(entry.action) ? ' adminTag--danger' : ''}`}>
                      {ACTIONS[entry.action] ?? entry.action}
                    </span>
                  </td>
                  <td>{entry.targetLabel ?? <span className="adminMuted">—</span>}</td>
                  <td>{entry.details ?? <span className="adminMuted">—</span>}</td>
                  <td>{entry.ipAddress ?? <span className="adminMuted">—</span>}</td>
                </tr>
              ))}
              {entries.length === 0 ? <tr><td colSpan={5} className="adminMuted">Dziennik jest pusty.</td></tr> : null}
            </tbody>
          </table>
        </div>
      )}
    </AdminShell>
  );
}
