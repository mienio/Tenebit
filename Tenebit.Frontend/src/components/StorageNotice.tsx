import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useI18n } from '../i18n/I18nProvider';
import { legalContent } from '../legal/legalContent';

const STORAGE_KEY = 'tenebit_storage_notice_dismissed';

function wasDismissed() {
  try { return window.localStorage.getItem(STORAGE_KEY) === '1'; } catch { return false; }
}

export function StorageNotice() {
  const { language } = useI18n();
  const [visible, setVisible] = useState(() => !wasDismissed());
  const ui = legalContent[language].ui;

  if (!visible) return null;

  function dismiss() {
    try { window.localStorage.setItem(STORAGE_KEY, '1'); } catch { /* storage can be unavailable */ }
    setVisible(false);
  }

  return (
    <aside className="storageNotice" aria-label={ui.cookies}>
      <div>
        <strong>{ui.storageNotice}</strong>
        <p>{ui.storageNoticeDetails} <Link to="/cookies">{ui.cookies}</Link></p>
      </div>
      <button type="button" onClick={dismiss}>{ui.understand}</button>
    </aside>
  );
}
