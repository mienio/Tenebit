import { Camera, Save, Trash2 } from 'lucide-react';
import { FormEvent, useRef, useState } from 'react';
import { useAuth } from '../auth/AuthProvider';
import { useOwnAvatarUrl } from '../hooks/useOwnAvatarUrl';
import { useI18n } from '../i18n/I18nProvider';
import { Avatar } from './Avatar';
import { AvatarCropModal } from './AvatarCropModal';
import { Button } from './Button';
import { Card } from './Card';
import { Field, TextInput } from './FormFields';

type Message = { type: 'success' | 'error'; text: string } | null;

const acceptedTypes = ['image/jpeg', 'image/png', 'image/webp', 'image/gif'];
const maxPickedFileBytes = 12 * 1024 * 1024;

export function ProfileCard() {
  const auth = useAuth();
  const { t } = useI18n();
  const avatarUrl = useOwnAvatarUrl();
  const [name, setName] = useState(auth.userName);
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState<Message>(null);
  const [avatarBusy, setAvatarBusy] = useState(false);
  const [pickedFile, setPickedFile] = useState<File | null>(null);
  const fileInput = useRef<HTMLInputElement>(null);

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

  function pickFile(file: File | undefined) {
    if (!file) return;
    setMessage(null);
    if (!acceptedTypes.includes(file.type)) {
      setMessage({ type: 'error', text: t('profile.avatarInvalidType') });
      return;
    }
    if (file.size > maxPickedFileBytes) {
      setMessage({ type: 'error', text: t('profile.avatarTooLarge') });
      return;
    }
    setPickedFile(file);
  }

  async function saveAvatar(blob: Blob) {
    setAvatarBusy(true);
    try {
      await auth.uploadAvatar(blob);
      setPickedFile(null);
      setMessage({ type: 'success', text: t('profile.avatarUploaded') });
    } catch (error) {
      setMessage({ type: 'error', text: error instanceof Error ? error.message : t('profile.avatarUploadFailed') });
    } finally {
      setAvatarBusy(false);
    }
  }

  async function removeAvatar() {
    setAvatarBusy(true);
    setMessage(null);
    try {
      await auth.removeAvatar();
      setMessage({ type: 'success', text: t('profile.avatarRemoved') });
    } catch (error) {
      setMessage({ type: 'error', text: error instanceof Error ? error.message : t('profile.avatarRemoveFailed') });
    } finally {
      setAvatarBusy(false);
    }
  }

  return (
    <Card>
      <div className="sectionTitle"><div><h2>{t('profile.title')}</h2><p>{t('profile.description')}</p></div></div>
      {message ? <p className={`formMessage formMessage--${message.type}`}>{message.text}</p> : null}
      <div className="avatarPicker">
        <Avatar name={name || auth.userEmail} size={64} photoUrl={avatarUrl} />
        <div className="avatarPicker__actions">
          <input
            ref={fileInput}
            type="file"
            accept={acceptedTypes.join(',')}
            hidden
            onChange={event => { pickFile(event.target.files?.[0]); event.target.value = ''; }}
          />
          <Button type="button" variant="secondary" disabled={avatarBusy} onClick={() => fileInput.current?.click()} icon={<Camera size={16} />}>
            {avatarUrl ? t('profile.changeAvatar') : t('profile.uploadAvatar')}
          </Button>
          {avatarUrl ? (
            <Button type="button" variant="ghost" disabled={avatarBusy} onClick={removeAvatar} icon={<Trash2 size={16} />}>{t('profile.removeAvatar')}</Button>
          ) : null}
        </div>
      </div>
      <form className="formGrid" onSubmit={save}>
        <Field label={t('profile.nameLabel')}>
          <TextInput value={name} onChange={event => setName(event.target.value)} required />
        </Field>
        <div className="formActions">
          <Button disabled={busy || !name.trim() || name.trim() === auth.userName} icon={<Save size={16} />}>{busy ? t('common.saving') : t('profile.save')}</Button>
        </div>
      </form>

      <AvatarCropModal
        open={!!pickedFile}
        file={pickedFile}
        onCancel={() => setPickedFile(null)}
        onCropped={blob => void saveAvatar(blob)}
      />
    </Card>
  );
}
