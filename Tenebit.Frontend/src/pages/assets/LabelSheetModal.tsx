import { Printer } from 'lucide-react';
import { useEffect, useState } from 'react';
import { Button } from '../../components/Button';
import { Field, SelectInput } from '../../components/FormFields';
import { Modal } from '../../components/Modal';
import { useI18n } from '../../i18n/I18nProvider';
import type { Asset } from '../../types/domain';

/**
 * Sheet formats are given in millimetres and match the label stock people actually buy, so a printed
 * page lines up with the die-cut sheet in the tray instead of needing a scaling guess at the printer.
 */
export const labelSizes = {
  Square38: { width: 38, height: 38, gap: 2 },
  Medium63: { width: 63.5, height: 38.1, gap: 2 },
  Large99: { width: 99.1, height: 67.7, gap: 2 }
} as const;

export type LabelSize = keyof typeof labelSizes;

interface LabelSheetModalProps {
  labels: { asset: Asset; svg: string }[] | null;
  /** Stock configured in settings; the sheet opens on it and only deviates if someone changes it here. */
  defaultSize?: LabelSize;
  onClose: () => void;
}

export function LabelSheetModal({ labels, defaultSize, onClose }: LabelSheetModalProps) {
  const { t } = useI18n();
  const [size, setSize] = useState<LabelSize>(defaultSize ?? 'Medium63');
  useEffect(() => { if (defaultSize) setSize(defaultSize); }, [defaultSize]);
  const [bordered, setBordered] = useState(true);
  const format = labelSizes[size];

  return (
    <Modal open={!!labels} title={t('assets.labelSheetTitle')} onClose={onClose} width="wide">
      {labels && (
        <>
          <div className="filters">
            <Field label={t('assets.labelSize')}>
              <SelectInput value={size} onChange={event => setSize(event.target.value as LabelSize)}>
                <option value="Square38">{t('assets.labelSizeSmall', { w: labelSizes.Square38.width, h: labelSizes.Square38.height })}</option>
                <option value="Medium63">{t('assets.labelSizeMedium', { w: labelSizes.Medium63.width, h: labelSizes.Medium63.height })}</option>
                <option value="Large99">{t('assets.labelSizeLarge', { w: labelSizes.Large99.width, h: labelSizes.Large99.height })}</option>
              </SelectInput>
            </Field>
            <label className="checkField">
              <input type="checkbox" checked={bordered} onChange={event => setBordered(event.target.checked)} />
              {t('assets.labelCutBorder')}
            </label>
          </div>
          <p className="muted">{t('assets.labelSheetHint', { count: labels.length })}</p>

          <div
            className="qrPrintSheet"
            style={{
              ['--labelW' as string]: `${format.width}mm`,
              ['--labelH' as string]: `${format.height}mm`,
              ['--labelGap' as string]: `${format.gap}mm`
            }}
          >
            {labels.map(item => (
              <div className={`qrPrintCard${bordered ? ' qrPrintCard--bordered' : ''}`} key={item.asset.id}>
                <img src={`data:image/svg+xml;charset=utf-8,${encodeURIComponent(item.svg)}`} alt={item.asset.assetTag} />
              </div>
            ))}
          </div>

          <div className="formActions formActions--split">
            <Button type="button" variant="ghost" onClick={onClose}>{t('common.close')}</Button>
            <Button type="button" onClick={() => window.print()} icon={<Printer size={16} />}>{t('assets.bulkPrintQr')}</Button>
          </div>
        </>
      )}
    </Modal>
  );
}
