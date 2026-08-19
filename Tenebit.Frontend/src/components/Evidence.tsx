import { Camera, Trash2 } from 'lucide-react';
import { useEffect, useRef, useState } from 'react';
import { useI18n } from '../i18n/I18nProvider';

const MAX_PHOTOS = 5;

function FileThumb({ file, onRemove }: { file: File; onRemove: () => void }) {
  const { t } = useI18n();
  const [url, setUrl] = useState<string | null>(null);
  useEffect(() => {
    const objectUrl = URL.createObjectURL(file);
    setUrl(objectUrl);
    return () => URL.revokeObjectURL(objectUrl);
  }, [file]);
  return (
    <div className="evidenceThumb">
      {url ? <img src={url} alt={file.name} /> : null}
      <button type="button" className="evidenceThumb__remove" aria-label={t('evidence.removePhoto')} onClick={onRemove}><Trash2 size={14} /></button>
    </div>
  );
}

// Pole przesyłania zdjęć (kilka plików per aktywo, maks. 5). Zawsze pokazuje linię instrukcji
// prywatności ze spec 6.9. Samo zbiera pliki do stanu rodzica - wysyłka następuje razem z formularzem.
export function EvidencePhotoPicker({ files, onChange }: { files: File[]; onChange: (files: File[]) => void }) {
  const { t } = useI18n();
  function addFiles(list: FileList | null) {
    if (!list) return;
    onChange([...files, ...Array.from(list)].slice(0, MAX_PHOTOS));
  }
  return (
    <div className="evidencePicker">
      <p className="evidencePicker__notice">{t('evidence.privacyInstruction')}</p>
      {files.length > 0 ? (
        <div className="evidenceThumbs">
          {files.map((file, index) => (
            <FileThumb key={index} file={file} onRemove={() => onChange(files.filter((_, i) => i !== index))} />
          ))}
        </div>
      ) : null}
      {files.length < MAX_PHOTOS ? (
        <label className="button button--secondary">
          <span className="button__icon"><Camera size={16} /></span>
          <span>{t('evidence.addPhoto')}</span>
          <input type="file" accept="image/*" multiple hidden onChange={event => { addFiles(event.target.files); event.target.value = ''; }} />
        </label>
      ) : null}
    </div>
  );
}

function EvidenceImage({ id, getBlob }: { id: string; getBlob: (id: string) => Promise<Blob> }) {
  const [url, setUrl] = useState<string | null>(null);
  const [failed, setFailed] = useState(false);
  const fetcher = useRef(getBlob);
  fetcher.current = getBlob;
  useEffect(() => {
    let objectUrl: string | null = null;
    let cancelled = false;
    setUrl(null);
    setFailed(false);
    fetcher.current(id)
      .then(blob => {
        if (cancelled) return;
        objectUrl = URL.createObjectURL(blob);
        setUrl(objectUrl);
      })
      .catch(() => { if (!cancelled) setFailed(true); });
    return () => {
      cancelled = true;
      if (objectUrl) URL.revokeObjectURL(objectUrl);
    };
  }, [id]);
  if (failed) return null;
  return <div className="evidenceThumb">{url ? <img src={url} alt="" /> : <div className="evidenceThumb__placeholder" />}</div>;
}

// Galeria zdjęć (tylko do odczytu): pobiera oryginały przez przekazany fetcher i pokazuje miniatury.
export function EvidenceGallery({ ids, getBlob }: { ids: string[]; getBlob: (id: string) => Promise<Blob> }) {
  const { t } = useI18n();
  if (!ids.length) return <p className="muted">{t('evidence.noPhotos')}</p>;
  return (
    <div className="evidenceThumbs">
      {ids.map(id => <EvidenceImage key={id} id={id} getBlob={getBlob} />)}
    </div>
  );
}
