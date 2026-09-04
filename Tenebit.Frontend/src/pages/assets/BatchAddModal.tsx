import { Plus } from 'lucide-react';
import { FormEvent, useMemo, useState } from 'react';
import { api } from '../../api/endpoints';
import { Button } from '../../components/Button';
import { Field, SelectInput, TextInput } from '../../components/FormFields';
import { Modal } from '../../components/Modal';
import { useI18n } from '../../i18n/I18nProvider';
import type { Asset, AssetCategory, LocationNode, Team } from '../../types/domain';
import { toNullable } from '../../utils/format';

const maxQuantity = 100;

function buildBatchTags(prefix: string, startNumber: number, padding: number, quantity: number): string[] {
  const tags: string[] = [];
  for (let i = 0; i < quantity; i++) {
    const number = String(startNumber + i);
    tags.push(prefix + (padding > 0 ? number.padStart(padding, '0') : number));
  }
  return tags;
}

interface BatchAddModalProps {
  open: boolean;
  onClose: () => void;
  categories: AssetCategory[];
  locations: LocationNode[];
  teams: Team[];
  onCreated: (assets: Asset[]) => void;
  onError: (message: string) => void;
}

/**
 * One delivery of identical equipment entered once.
 *
 * The tag preview is the point of the top section: an operator about to create twenty assets needs to
 * see the first and last tag before committing, because those are the numbers going onto the boxes.
 */
export function BatchAddModal({ open, onClose, categories, locations, teams, onCreated, onError }: BatchAddModalProps) {
  const { t } = useI18n();
  const [quantity, setQuantity] = useState(5);
  const [tagPrefix, setTagPrefix] = useState('');
  const [startNumber, setStartNumber] = useState(1);
  const [padding, setPadding] = useState(4);
  const [categoryId, setCategoryId] = useState('');
  const [saving, setSaving] = useState(false);

  const categoryFields = useMemo(
    () => categories.find(category => category.id === categoryId)?.fieldDefinitions ?? [],
    [categories, categoryId]
  );

  const clampedQuantity = Math.min(Math.max(quantity, 0), maxQuantity);

  const tags = useMemo(() => {
    if (!tagPrefix.trim() || clampedQuantity < 1) return [];
    return buildBatchTags(tagPrefix.trim(), startNumber, padding, clampedQuantity);
  }, [tagPrefix, startNumber, padding, clampedQuantity]);

  const preview = tags.length === 0 ? null : tags.length <= 3 ? tags.join(', ') : `${tags[0]}, ${tags[1]} … ${tags[tags.length - 1]}`;

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = new FormData(event.currentTarget);

    if (!categoryId) return onError(t('assets.categoryRequired'));
    if (quantity < 1 || quantity > maxQuantity) return onError(t('assets.batchQuantityRange', { max: maxQuantity }));
    if (!tagPrefix.trim()) return onError(t('assets.batchPrefixRequired'));

    const serialNumbers = Array.from({ length: clampedQuantity }, (_, i) => String(form.get(`serial__${i}`) ?? '').trim());

    const customFields: Record<string, string> = {};
    for (const field of categoryFields) {
      if (field.fieldType === 'Boolean') {
        customFields[field.key] = form.get(`custom__${field.key}`) === 'on' ? 'true' : 'false';
      } else {
        const value = String(form.get(`custom__${field.key}`) ?? '').trim();
        if (value) customFields[field.key] = value;
      }
    }

    const rawPrice = String(form.get('purchasePrice') ?? '').trim().replace(',', '.');
    const purchasePrice = rawPrice ? Number(rawPrice) : null;
    if (rawPrice && !Number.isFinite(purchasePrice)) return onError(t('assets.invalidPrice'));

    setSaving(true);
    try {
      const result = await api.createAssetBatch({
        name: String(form.get('name') ?? '').trim(),
        categoryId,
        quantity,
        tagPrefix: tagPrefix.trim(),
        tagStartNumber: startNumber,
        tagPadding: padding,
        serialNumbers: serialNumbers.some(serial => serial.length > 0) ? serialNumbers : null,
        location: toNullable(String(form.get('location') ?? '')),
        manufacturer: toNullable(String(form.get('manufacturer') ?? '')),
        model: toNullable(String(form.get('model') ?? '')),
        purchasePrice,
        currency: toNullable(String(form.get('currency') ?? 'PLN')) ?? 'PLN',
        purchaseDate: toNullable(String(form.get('purchaseDate') ?? '')),
        warrantyUntil: toNullable(String(form.get('warrantyUntil') ?? '')),
        teamId: toNullable(String(form.get('teamId') ?? '')),
        customFields
      });
      onCreated(result.assets);
    } catch (error) {
      onError(error instanceof Error ? error.message : t('assets.batchFailed'));
    } finally {
      setSaving(false);
    }
  }

  return (
    <Modal open={open} title={t('assets.batchTitle')} onClose={onClose} width="wide">
      <form className="formGrid" onSubmit={submit}>
        <div className="formSectionTitle">{t('assets.batchNumberingSection')}</div>
        <Field label={t('assets.batchQuantity')}>
          <TextInput type="number" min={1} max={maxQuantity} value={quantity} onChange={event => setQuantity(Number(event.target.value))} required />
        </Field>
        <Field label={t('assets.batchTagPrefix')}>
          <TextInput value={tagPrefix} onChange={event => setTagPrefix(event.target.value)} placeholder="LAP-" required />
        </Field>
        <Field label={t('assets.batchStartNumber')}>
          <TextInput type="number" min={0} max={999999} value={startNumber} onChange={event => setStartNumber(Number(event.target.value))} required />
        </Field>
        <Field label={t('assets.batchPadding')}>
          <SelectInput value={padding} onChange={event => setPadding(Number(event.target.value))}>
            {[0, 2, 3, 4, 5, 6].map(option => <option key={option} value={option}>{option === 0 ? t('assets.batchPaddingNone') : option}</option>)}
          </SelectInput>
        </Field>
        <div className="formFullWidth">
          <p className="muted">{preview ? t('assets.batchPreview', { tags: preview }) : t('assets.batchPreviewEmpty')}</p>
        </div>

        <div className="formSectionTitle">{t('assets.identification')}</div>
        <Field label={t('assets.nameLabel')}><TextInput name="name" required /></Field>
        <Field label={t('assets.categoryLabel')}>
          <SelectInput value={categoryId} onChange={event => setCategoryId(event.target.value)} required>
            <option value="">{t('assets.chooseCategory')}</option>
            {categories.map(category => <option key={category.id} value={category.id}>{category.name}</option>)}
          </SelectInput>
        </Field>
        <Field label={t('assets.locationLabel')}>
          <SelectInput name="location" defaultValue="">
            <option value="">{t('assets.noLocationOption')}</option>
            {locations.map(item => <option key={item.id} value={item.fullPath}>{item.fullPath}</option>)}
          </SelectInput>
        </Field>
        <Field label={t('assets.teamLabel')}>
          <SelectInput name="teamId" defaultValue="">
            <option value="">{t('assets.noTeamOption')}</option>
            {teams.map(team => <option key={team.id} value={team.id}>{team.name}</option>)}
          </SelectInput>
        </Field>

        <div className="formSectionTitle">{t('assets.descAndDates')}</div>
        <Field label={t('assets.manufacturerLabel')}><TextInput name="manufacturer" /></Field>
        <Field label={t('assets.modelLabel')}><TextInput name="model" /></Field>
        <Field label={t('assets.purchasePriceLabel')}><TextInput name="purchasePrice" inputMode="decimal" /></Field>
        <Field label={t('assets.currencyLabel')}><TextInput name="currency" defaultValue="PLN" maxLength={3} /></Field>
        <Field label={t('assets.purchaseDateLabel')}><TextInput name="purchaseDate" type="date" /></Field>
        <Field label={t('assets.warrantyUntilLabel')}><TextInput name="warrantyUntil" type="date" /></Field>

        {categoryFields.length > 0 && (
          <>
            <div className="formSectionTitle">{t('assets.customFieldsSection')}</div>
            {categoryFields.map(field => (
              <Field key={field.id} label={field.required ? `${field.label} *` : field.label}>
                {field.fieldType === 'Boolean' ? (
                  <label style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                    <input type="checkbox" name={`custom__${field.key}`} />
                  </label>
                ) : field.fieldType === 'Select' ? (
                  <SelectInput name={`custom__${field.key}`} defaultValue="" required={field.required}>
                    <option value="">{t('assets.customFieldChoose')}</option>
                    {field.options.map(option => <option key={option} value={option}>{option}</option>)}
                  </SelectInput>
                ) : (
                  <TextInput
                    name={`custom__${field.key}`}
                    type={field.fieldType === 'Number' ? 'number' : field.fieldType === 'Date' ? 'date' : field.fieldType === 'Sensitive' ? 'password' : 'text'}
                    required={field.required}
                  />
                )}
              </Field>
            ))}
          </>
        )}

        {clampedQuantity > 0 && (
          <>
            <div className="formSectionTitle">{t('assets.batchSerialsSection')}</div>
            <div className="formFullWidth">
              <p className="muted">{t('assets.batchSerialsHint')}</p>
            </div>
            {Array.from({ length: clampedQuantity }, (_, i) => (
              <Field key={i} label={tags[i] ?? `#${i + 1}`}>
                <TextInput name={`serial__${i}`} />
              </Field>
            ))}
          </>
        )}

        <div className="formActions formActions--split">
          <Button type="button" variant="ghost" onClick={onClose}>{t('common.cancel')}</Button>
          <Button disabled={saving} icon={<Plus size={16} />}>
            {saving ? t('common.saving') : t('assets.batchSubmit', { count: quantity })}
          </Button>
        </div>
      </form>
    </Modal>
  );
}
