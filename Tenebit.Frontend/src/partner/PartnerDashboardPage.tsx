import { useEffect, useState } from 'react';
import { LoadingState } from '../components/StateViews';
import { PartnerLayout, PartnerPageHeader } from './PartnerLayout';
import { getMyDashboard, type AffiliateDashboard } from './partnerApi';
import { usePartnerLocale, type PartnerLocale } from './i18n';

function money(value: number): string {
  return `${value.toFixed(2)} €`;
}

function ordinal(day: number): string {
  if (day % 10 === 1 && day % 100 !== 11) return `${day}st`;
  if (day % 10 === 2 && day % 100 !== 12) return `${day}nd`;
  if (day % 10 === 3 && day % 100 !== 13) return `${day}rd`;
  return `${day}th`;
}

const content: Record<PartnerLocale, {
  title: string; description: string; fetchError: string;
  commissionThisMonth: string; commissionThisMonthHint: string;
  awaitingPayout: string; nextPayout: (date: string, days: number) => string;
  paidLifetime: string; activeCodes: string;
  howItWorksTitle: string; howItWorksText: (day: number, days: number, termsHref: string) => JSX.Element;
  dateLocale: string;
}> = {
  en: {
    title: 'Dashboard', description: 'A summary of your stats in the partner program.',
    fetchError: 'Could not fetch data.',
    commissionThisMonth: 'Commission this month', commissionThisMonthHint: 'Current, still-open period',
    awaitingPayout: 'Awaiting payout now',
    nextPayout: (date, days) => `Planned payout: ${date} (± ${days} days)`,
    paidLifetime: 'Total paid out', activeCodes: 'Active codes',
    howItWorksTitle: 'How settlement works',
    howItWorksText: (day, days, termsHref) => (
      <>
        A sale counts toward the calendar month in which it happened. The period closes at the end of
        the month, and the payout reaches your Revolut account by the {ordinal(day)} of the following
        month (up to {days} days of slack) - per the <a href={termsHref} target="_blank" rel="noreferrer">program terms</a>.
      </>
    ),
    dateLocale: 'en-GB',
  },
  pl: {
    title: 'Pulpit', description: 'Skrót Twoich statystyk w programie partnerskim.',
    fetchError: 'Nie udało się pobrać danych.',
    commissionThisMonth: 'Prowizja w tym miesiącu', commissionThisMonthHint: 'Bieżący, jeszcze otwarty okres',
    awaitingPayout: 'Do wypłaty teraz',
    nextPayout: (date, days) => `Planowana wypłata: ${date} (± ${days} dni)`,
    paidLifetime: 'Wypłacono łącznie', activeCodes: 'Aktywne kody',
    howItWorksTitle: 'Jak działa rozliczenie',
    howItWorksText: (day, days, termsHref) => (
      <>
        Sprzedaż liczy się do okresu miesiąca kalendarzowego, w którym nastąpiła. Okres zamyka się z
        końcem miesiąca, a wypłata trafia na Twoje konto Revolut docelowo {day}. dnia kolejnego miesiąca
        (maksymalnie {days} dni poślizgu) - zgodnie z <a href={termsHref} target="_blank" rel="noreferrer">regulaminem programu</a>.
      </>
    ),
    dateLocale: 'pl-PL',
  },
};

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
  const { locale, path } = usePartnerLocale();
  const t = content[locale];
  const [dashboard, setDashboard] = useState<AffiliateDashboard | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    getMyDashboard()
      .then(result => { if (!cancelled) setDashboard(result); })
      .catch(err => { if (!cancelled) setError(err instanceof Error ? err.message : t.fetchError); });
    return () => { cancelled = true; };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  return (
    <PartnerLayout>
      <PartnerPageHeader title={t.title} description={t.description} />
      {error ? <p className="formMessage formMessage--error">{error}</p> : null}
      {!dashboard ? <LoadingState /> : (
        <>
          <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap', marginBottom: 16 }}>
            <Stat label={t.commissionThisMonth} value={money(dashboard.commissionThisOpenPeriod)} hint={t.commissionThisMonthHint} />
            <Stat label={t.awaitingPayout} value={money(dashboard.totalAwaitingPayout)}
              hint={dashboard.nextPayoutTargetDate ? t.nextPayout(new Date(dashboard.nextPayoutTargetDate).toLocaleDateString(t.dateLocale), dashboard.payoutGraceDays) : undefined} />
            <Stat label={t.paidLifetime} value={money(dashboard.totalPaidLifetime)} />
            <Stat label={t.activeCodes} value={`${dashboard.activeCodeCount} / ${dashboard.maxActiveCodeCount}`} />
          </div>
          <div className="card">
            <h3 style={{ marginTop: 0 }}>{t.howItWorksTitle}</h3>
            <p className="adminMuted" style={{ lineHeight: 1.6 }}>
              {t.howItWorksText(dashboard.payoutDayOfMonth, dashboard.payoutGraceDays, path('terms'))}
            </p>
          </div>
        </>
      )}
    </PartnerLayout>
  );
}
