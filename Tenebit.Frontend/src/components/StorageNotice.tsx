import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useI18n } from '../i18n/I18nProvider';
import { legalContentFor } from '../legal/legalContent';
import { getStoredConsent, storeConsent, type ConsentChoice } from '../analytics/consent';

export function StorageNotice() {
  const { language } = useI18n();
  const [choice, setChoice] = useState<ConsentChoice | null>(() => getStoredConsent());
  const ui = legalContentFor(language).ui;

  if (choice) return null;

  function decide(next: ConsentChoice) {
    storeConsent(next);
    setChoice(next);
  }

  return (
    <aside className="storageNotice" aria-label={ui.cookies}>
      <div>
        <strong>{ui.storageNotice}</strong>
        <p>{ui.storageNoticeDetails} <Link to="/cookies">{ui.cookies}</Link></p>
      </div>
      <div className="storageNotice__actions">
        <button type="button" className="storageNotice__reject" onClick={() => decide('rejected')}>{ui.consentReject}</button>
        <button type="button" onClick={() => decide('accepted')}>{ui.consentAccept}</button>
      </div>
    </aside>
  );
}
