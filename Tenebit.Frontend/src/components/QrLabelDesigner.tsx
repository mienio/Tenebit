import { Image, Save, Trash2, Upload } from 'lucide-react';
import { useEffect, useRef, useState } from 'react';
import { api } from '../api/endpoints';
import { Button } from './Button';
import { Field, SelectInput, TextInput } from './FormFields';
import { useI18n } from '../i18n/I18nProvider';
import type { QrLabelCodeSize, QrLabelFormat, QrLabelLogoMode, QrLabelPreview, QrLabelSettings, SaveQrLabelSettings } from '../types/domain';

const maxLogoBytes = 512 * 1024;
const logoModes: QrLabelLogoMode[] = ['None', 'Tenebit', 'Custom'];
const codeSizes: QrLabelCodeSize[] = ['Small', 'Medium', 'Large'];
const formats: QrLabelFormat[] = ['Square38', 'Medium63', 'Large99'];

/**
 * Below this a printed module is smaller than a phone camera reliably resolves at arm's length, and the
 * code starts needing a second or third try. It is the number that makes the caption-versus-code
 * trade-off concrete, so it is shown rather than merely enforced.
 */
const comfortableMmPerModule = 0.4;

const formatMm = (value: number) => `${value.toFixed(1).replace(/\.0$/, '')} mm`;

function toDraft(settings: QrLabelSettings): SaveQrLabelSettings {
  return {
    showName: settings.showName,
    showTag: settings.showTag,
    showSerialNumber: settings.showSerialNumber,
    showOrganizationName: settings.showOrganizationName,
    customText: settings.customText ?? '',
    logo: settings.logo,
    codeSize: settings.codeSize,
    format: settings.format
  };
}

interface QrLabelDesignerProps {
  settings: QrLabelSettings;
  onSaved: (settings: QrLabelSettings) => void;
  onSuccess: (message: string) => void;
  onFailure: (error: unknown, fallback: string) => void;
}

/**
 * Editor for what is printed around the QR code.
 *
 * The preview comes from the server rather than being redrawn here, so the admin is looking at the exact
 * SVG the label sheet will print - the same composition, the same fonts, the same mark. A local imitation
 * would drift from it the moment either side changed.
 */
export function QrLabelDesigner({ settings, onSaved, onSuccess, onFailure }: QrLabelDesignerProps) {
  const { t } = useI18n();
  const [draft, setDraft] = useState<SaveQrLabelSettings>(() => toDraft(settings));
  const [hasCustomLogo, setHasCustomLogo] = useState(settings.hasCustomLogo);
  const [preview, setPreview] = useState<QrLabelPreview | null>(null);
  const [saving, setSaving] = useState(false);
  const [uploading, setUploading] = useState(false);
  const fileInput = useRef<HTMLInputElement>(null);
  const dirty = useRef(false);

  // Wgranie albo usunięcie logo zapisuje się od razu i odświeża ustawienia w rodzicu. Bez tej blokady
  // przychodzący obiekt settings nadpisywał formularz i kasował wszystkie niezapisane zmiany.
  useEffect(() => {
    if (dirty.current) return;
    setDraft(toDraft(settings));
    setHasCustomLogo(settings.hasCustomLogo);
  }, [settings]);

  // Preview requests are debounced because every keystroke in the free-text line would otherwise
  // re-render a QR code server-side.
  useEffect(() => {
    let cancelled = false;
    const timeout = window.setTimeout(() => {
      api.previewQrLabel({ ...draft, customText: draft.customText?.trim() || null })
        .then(result => { if (!cancelled) setPreview(result); })
        .catch(() => { if (!cancelled) setPreview(null); });
    }, 300);
    return () => { cancelled = true; window.clearTimeout(timeout); };
  }, [draft]);

  function update(patch: Partial<SaveQrLabelSettings>) {
    dirty.current = true;
    setDraft(current => ({ ...current, ...patch }));
  }

  async function save() {
    setSaving(true);
    try {
      const saved = await api.saveQrLabelSettings({ ...draft, customText: draft.customText?.trim() || null });
      dirty.current = false;
      onSaved(saved);
      onSuccess(t('settings.qrLabelSaved'));
    } catch (error) {
      onFailure(error, t('settings.qrLabelSaveFailed'));
    } finally {
      setSaving(false);
    }
  }

  async function uploadLogo(file: File) {
    if (file.size > maxLogoBytes) return onFailure(new Error(t('settings.qrLabelLogoTooLarge')), t('settings.qrLabelLogoUploadFailed'));
    setUploading(true);
    try {
      const saved = await api.uploadQrLabelLogo(file);
      setHasCustomLogo(saved.hasCustomLogo);
      update({ logo: saved.logo });
      onSaved(saved);
      onSuccess(t('settings.qrLabelLogoUploaded'));
    } catch (error) {
      onFailure(error, t('settings.qrLabelLogoUploadFailed'));
    } finally {
      setUploading(false);
      if (fileInput.current) fileInput.current.value = '';
    }
  }

  async function removeLogo() {
    setUploading(true);
    try {
      const saved = await api.removeQrLabelLogo();
      setHasCustomLogo(false);
      update({ logo: saved.logo });
      onSaved(saved);
      onSuccess(t('settings.qrLabelLogoRemoved'));
    } catch (error) {
      onFailure(error, t('settings.qrLabelLogoRemoveFailed'));
    } finally {
      setUploading(false);
    }
  }

  return (
    <div className="qrDesigner">
      <div className="qrDesigner__controls">
        <div className="formSectionTitle">{t('settings.qrLabelContentSection')}</div>
        <label className="checkField">
          <input type="checkbox" checked={draft.showOrganizationName} onChange={event => update({ showOrganizationName: event.target.checked })} />
          {t('settings.qrLabelShowOrganizationName', { name: settings.organizationName })}
        </label>
        <label className="checkField">
          <input type="checkbox" checked={draft.showTag} onChange={event => update({ showTag: event.target.checked })} />
          {t('settings.qrLabelShowTag')}
        </label>
        <label className="checkField">
          <input type="checkbox" checked={draft.showName} onChange={event => update({ showName: event.target.checked })} />
          {t('settings.qrLabelShowName')}
        </label>
        <label className="checkField">
          <input type="checkbox" checked={draft.showSerialNumber} onChange={event => update({ showSerialNumber: event.target.checked })} />
          {t('settings.qrLabelShowSerialNumber')}
        </label>
        <div className="formFullWidth">
          <Field label={t('settings.qrLabelCustomText')}>
            <TextInput
              value={draft.customText ?? ''}
              maxLength={60}
              placeholder={t('settings.qrLabelCustomTextPlaceholder')}
              onChange={event => update({ customText: event.target.value })}
            />
          </Field>
          <p className="muted">{t('settings.qrLabelCustomTextHint')}</p>
        </div>

        <div className="formSectionTitle">{t('settings.qrLabelSizeSection')}</div>
        <Field label={t('settings.qrLabelFormat')}>
          <SelectInput value={draft.format} onChange={event => update({ format: event.target.value as QrLabelFormat })}>
            {formats.map(format => <option key={format} value={format}>{t(`settings.qrLabelFormat${format}`)}</option>)}
          </SelectInput>
        </Field>
        <Field label={t('settings.qrLabelCodeSize')}>
          <SelectInput value={draft.codeSize} onChange={event => update({ codeSize: event.target.value as QrLabelCodeSize })}>
            {codeSizes.map(size => <option key={size} value={size}>{t(`settings.qrLabelCodeSize${size}`)}</option>)}
          </SelectInput>
        </Field>
        <div className="formFullWidth">
          <p className="muted">{t('settings.qrLabelCodeSizeHint')}</p>
        </div>

        <div className="formSectionTitle">{t('settings.qrLabelLogoSection')}</div>
        <div className="qrDesigner__logoModes">
          {logoModes.map(mode => (
            <label className="checkField" key={mode}>
              <input
                type="radio"
                name="qrLabelLogo"
                checked={draft.logo === mode}
                disabled={mode === 'Custom' && !hasCustomLogo}
                onChange={() => update({ logo: mode })}
              />
              {t(`settings.qrLabelLogo${mode}`)}
            </label>
          ))}
        </div>
        <p className="muted">{t('settings.qrLabelLogoHint')}</p>
        <div className="formActions formActions--split">
          <div style={{ display: 'flex', gap: '8px' }}>
            <input
              ref={fileInput}
              type="file"
              accept="image/png,image/jpeg,image/webp"
              style={{ display: 'none' }}
              onChange={event => { const file = event.target.files?.[0]; if (file) void uploadLogo(file); }}
            />
            <Button type="button" variant="secondary" disabled={uploading} onClick={() => fileInput.current?.click()} icon={<Upload size={16} />}>
              {hasCustomLogo ? t('settings.qrLabelReplaceLogo') : t('settings.qrLabelUploadLogo')}
            </Button>
            {hasCustomLogo && (
              <Button type="button" variant="ghost" disabled={uploading} onClick={() => void removeLogo()} icon={<Trash2 size={16} />}>
                {t('settings.qrLabelRemoveLogo')}
              </Button>
            )}
          </div>
          <Button type="button" disabled={saving} onClick={() => void save()} icon={<Save size={16} />}>
            {saving ? t('common.saving') : t('settings.save')}
          </Button>
        </div>
      </div>

      <div className="qrDesigner__preview">
        <span className="qrDesigner__previewLabel">{t('settings.qrLabelPreview')}</span>
        {preview ? (
          <>
            <img
              src={`data:image/svg+xml;charset=utf-8,${encodeURIComponent(preview.svg)}`}
              alt={t('settings.qrLabelPreview')}
              style={{ aspectRatio: `${preview.widthPx} / ${preview.heightPx}` }}
            />
            <p className="muted">
              {t('settings.qrLabelMeasured', {
                label: `${formatMm(preview.labelWidthMm)} × ${formatMm(preview.labelHeightMm)}`,
                code: formatMm(preview.codeMm),
                perModule: preview.millimetresPerModule.toFixed(2)
              })}
            </p>
            {preview.millimetresPerModule < comfortableMmPerModule && (
              <p className="qrDesigner__warning">{t('settings.qrLabelTooDense')}</p>
            )}
          </>
        ) : (
          <div className="qrDesigner__previewEmpty"><Image size={28} /></div>
        )}
        <p className="muted">{t('settings.qrLabelPreviewHint')}</p>
      </div>
    </div>
  );
}
