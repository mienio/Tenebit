import { Link } from 'react-router-dom';
import { useI18n } from '../i18n/I18nProvider';

/// Zdanie o akceptacji dokumentów, z linkami do regulaminu i polityki prywatności.
///
/// Nazwy dokumentów mają tu własne klucze, oddzielone od nagłówków w <LegalPage>: w zdaniu stoją w
/// innym przypadku niż samodzielnie ("akceptujesz Politykę prywatności", nie "akceptujesz Polityka
/// prywatności" - QA: BUG-007). Spójnik zakończony apostrofem (włoskie "e l'") przykleja się do nazwy
/// bez odstępu, bo inaczej wychodziłoby "e l' Informativa".
export function LegalConsentText({ prefixKey }: { prefixKey: 'auth.socialTermsNotice' | 'auth.acceptTermsPrefix' }) {
  const { t } = useI18n();
  const prefix = t(prefixKey);
  const connector = t('auth.acceptTermsAnd');
  const space = (text: string) => (text.endsWith('’') || text.endsWith("'") ? '' : ' ');

  return (
    <>
      {prefix}{space(prefix)}
      <Link to="/terms">{t('auth.termsInlineLabel')}</Link>{' '}
      {connector}{space(connector)}
      <Link to="/privacy">{t('auth.privacyInlineLabel')}</Link>.
    </>
  );
}
