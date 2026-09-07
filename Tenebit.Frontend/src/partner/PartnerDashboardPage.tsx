import { useEffect, useState } from 'react';
import { LoadingState } from '../components/StateViews';
import { PartnerLayout, PartnerPageHeader } from './PartnerLayout';
import { getMyDashboard, type AffiliateDashboard } from './partnerApi';

function money(value: number): string {
  return `${value.toFixed(2)} €`;
}

function Stat({ label, value, hint }: { label: string; value: string; hint?: string }) {
  return (
    <div className="card" style={{ flex: '1 1 200px' }}>
      <div className="adminMuted">{label}</div>
      <div style={{ fontSize: 26, fontWeight: 700, marginTop: 4 }}>{value}</div>
      {hint ? <div className="adminMuted" style={{ marginTop: 4 }}>{hint}</div> : null}
    </div>
  );
}

export function PartnerDashboardPage() {
  const [dashboard, setDashboard] = useState<AffiliateDashboard | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    getMyDashboard()
      .then(result => { if (!cancelled) setDashboard(result); })
      .catch(err => { if (!cancelled) setError(err instanceof Error ? err.message : 'Nie udało się pobrać danych.'); });
    return () => { cancelled = true; };
  }, []);

  return (
    <PartnerLayout>
      <PartnerPageHeader title="Pulpit" description="Skrót Twoich statystyk w programie partnerskim." />
      {error ? <p className="formMessage formMessage--error">{error}</p> : null}
      {!dashboard ? <LoadingState /> : (
        <>
          <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap', marginBottom: 16 }}>
            <Stat label="Prowizja w tym miesiącu" value={money(dashboard.commissionThisOpenPeriod)} hint="Bieżący, jeszcze otwarty okres" />
            <Stat label="Do wypłaty teraz" value={money(dashboard.totalAwaitingPayout)}
              hint={dashboard.nextPayoutTargetDate ? `Planowana wypłata: ${new Date(dashboard.nextPayoutTargetDate).toLocaleDateString('pl-PL')} (± ${dashboard.payoutGraceDays} dni)` : undefined} />
            <Stat label="Wypłacono łącznie" value={money(dashboard.totalPaidLifetime)} />
            <Stat label="Aktywne kody" value={`${dashboard.activeCodeCount} / ${dashboard.maxActiveCodeCount}`} />
          </div>
          <div className="card">
            <h3 style={{ marginTop: 0 }}>Jak działa rozliczenie</h3>
            <p className="adminMuted" style={{ lineHeight: 1.6 }}>
              Sprzedaż liczy się do okresu miesiąca kalendarzowego, w którym nastąpiła. Okres zamyka się z końcem miesiąca,
              a wypłata trafia na Twoje konto Revolut docelowo {dashboard.payoutDayOfMonth}. dnia kolejnego miesiąca
              (maksymalnie {dashboard.payoutGraceDays} dni poślizgu) - zgodnie z <a href="/partner/terms" target="_blank" rel="noreferrer">regulaminem programu</a>.
            </p>
          </div>
        </>
      )}
    </PartnerLayout>
  );
}
