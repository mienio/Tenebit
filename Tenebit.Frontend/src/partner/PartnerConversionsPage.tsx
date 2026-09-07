import { useEffect, useState } from 'react';
import { LoadingState } from '../components/StateViews';
import { PartnerLayout, PartnerPageHeader } from './PartnerLayout';
import { getMyConversions, type AffiliateConversionSummary } from './partnerApi';
import { usePartnerLocale, type PartnerLocale } from './i18n';

function money(value: number, currency: string): string {
  return `${value.toFixed(2)} ${currency === 'EUR' ? '€' : currency}`;
}

const content: Record<PartnerLocale, {
  title: string; description: string; fetchError: string;
  colDate: string; colCode: string; colType: string; colCommission: string; colStatus: string;
  initialSale: string; renewal: string; inReview: string; outsideWindow: string; accrued: string;
  empty: string; dateLocale: string;
}> = {
  en: {
    title: 'Sales', description: 'Anonymized list - you see the date, commission amount, and code used, never the buyer’s data.',
    fetchError: 'Could not fetch sales.',
    colDate: 'Date', colCode: 'Code', colType: 'Type', colCommission: 'Commission', colStatus: 'Status',
    initialSale: 'First sale', renewal: 'Renewal', inReview: 'In review', outsideWindow: 'Outside commission window', accrued: 'Accrued',
    empty: 'No sales recorded on your codes yet.', dateLocale: 'en-GB',
  },
  pl: {
    title: 'Sprzedaże', description: 'Zanonimizowana lista - widzisz datę, kwotę prowizji i użyty kod, nigdy dane kupującego.',
    fetchError: 'Nie udało się pobrać sprzedaży.',
    colDate: 'Data', colCode: 'Kod', colType: 'Typ', colCommission: 'Prowizja', colStatus: 'Status',
    initialSale: 'Pierwsza sprzedaż', renewal: 'Odnowienie', inReview: 'W weryfikacji', outsideWindow: 'Poza oknem prowizyjnym', accrued: 'Naliczono',
    empty: 'Brak sprzedaży jeszcze zarejestrowanej na Twoich kodach.', dateLocale: 'pl-PL',
  },
};

export function PartnerConversionsPage() {
  const { locale } = usePartnerLocale();
  const t = content[locale];
  const [items, setItems] = useState<AffiliateConversionSummary[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    getMyConversions(1, 100)
      .then(result => { if (!cancelled) setItems(result.items); })
      .catch(err => { if (!cancelled) setError(err instanceof Error ? err.message : t.fetchError); });
    return () => { cancelled = true; };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  return (
    <PartnerLayout>
      <PartnerPageHeader title={t.title} description={t.description} />
      {error ? <p className="formMessage formMessage--error">{error}</p> : null}
      {!items ? <LoadingState /> : (
        <div className="card adminTableCard">
          <table className="adminTable">
            <thead><tr><th>{t.colDate}</th><th>{t.colCode}</th><th>{t.colType}</th><th>{t.colCommission}</th><th>{t.colStatus}</th></tr></thead>
            <tbody>
              {items.map((c, i) => (
                <tr key={i}>
                  <td>{new Date(c.occurredAt).toLocaleDateString(t.dateLocale)}</td>
                  <td><code>{c.code}</code></td>
                  <td>{c.eventType === 'InitialSale' ? t.initialSale : t.renewal}</td>
                  <td>{money(c.commissionAmount, c.currency)}</td>
                  <td>
                    {c.requiresReview ? <span className="adminTag">{t.inReview}</span>
                      : !c.isWithinCommissionWindow ? <span className="adminTag">{t.outsideWindow}</span>
                      : <span className="adminTag adminTag--ok">{t.accrued}</span>}
                  </td>
                </tr>
              ))}
              {items.length === 0 ? <tr><td colSpan={5} className="adminMuted">{t.empty}</td></tr> : null}
            </tbody>
          </table>
        </div>
      )}
    </PartnerLayout>
  );
}
