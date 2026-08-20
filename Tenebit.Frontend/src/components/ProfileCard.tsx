import { Save } from 'lucide-react';
import { FormEvent, useState } from 'react';
import { useAuth } from '../auth/AuthProvider';
import { useI18n } from '../i18n/I18nProvider';
import { Avatar } from './Avatar';
import { Button } from './Button';
import { Card } from './Card';
import { Field, TextInput } from './FormFields';

type Message = { type: 'success' | 'error'; text: string } | null;

export function ProfileCard() {
  const auth = useAuth();
  const { t } = useI18n();
  const [name, setName] = useState(auth.userName);
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState<Message>(null);

  async function save(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setBusy(true);
    setMessage(null);
    try {
      await auth.updateDisplayName(name.trim());
      setMessage({ type: 'success', text: t('profile.saved') });
    } catch (error) {
      setMessage({ type: 'error', text: error instanceof Error ? error.message : t('profile.saveFailed') });
    } finally {
      setBusy(false);
    }
  }

  return (
    <Card>
      <div className="sectionTitle"><div><h2>{t('profile.title')}</h2><p>{t('profile.description')}</p></div></div>
      {message ? <p className={`formMessage formMessage--${message.type}`}>{message.text}</p> : null}
      <form className="formGrid" onSubmit={save}>
        <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
          <Avatar name={name || auth.userEmail} size={48} />
          <Field label={t('profile.nameLabel')}>
            <TextInput value={name} onChange={event => setName(event.target.value)} required />
          </Field>
        </div>
        <div className="formActions">
          <Button disabled={busy || !name.trim() || name.trim() === auth.userName} icon={<Save size={16} />}>{busy ? t('common.saving') : t('profile.save')}</Button>
        </div>
      </form>
    </Card>
  );
}
