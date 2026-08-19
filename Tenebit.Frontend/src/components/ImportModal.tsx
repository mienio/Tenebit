import { useMemo, useState } from 'react';
import { Download, Upload } from 'lucide-react';
import { Button } from './Button';
import { Field, SelectInput } from './FormFields';
import { Modal } from './Modal';
import { api } from '../api/endpoints';
import type { AssetCategory, LocationNode, Team } from '../types/domain';
import { useI18n } from '../i18n/I18nProvider';
import type { Language } from '../i18n/translations';

type Entity = 'people' | 'assets';
type Step = 'pick' | 'preview' | 'importing' | 'result';

interface FieldDef {
  key: string;
  labelKey: string;
  required?: boolean;
  synonyms: string[];
}

const PEOPLE_FIELDS: FieldDef[] = [
  { key: 'firstName', labelKey: 'import.field.firstName', synonyms: ['imie', 'imię', 'first name', 'firstname'] },
  { key: 'lastName', labelKey: 'import.field.lastName', synonyms: ['nazwisko', 'last name', 'lastname'] },
  { key: 'fullName', labelKey: 'import.field.fullName', synonyms: ['imie i nazwisko', 'imię i nazwisko', 'fullname', 'full name', 'name', 'nazwa'] },
  { key: 'email', labelKey: 'import.field.email', required: true, synonyms: ['email', 'e-mail', 'mail'] },
  { key: 'phone', labelKey: 'import.field.phone', synonyms: ['telefon', 'phone'] },
  { key: 'jobTitle', labelKey: 'import.field.jobTitle', synonyms: ['stanowisko', 'job title', 'title'] },
  { key: 'team', labelKey: 'import.field.team', synonyms: ['zespol', 'zespół', 'team', 'dzial', 'dział'] },
  { key: 'employeeNumber', labelKey: 'import.field.employeeNumber', synonyms: ['nr pracownika', 'numer pracownika', 'employee number', 'emp id'] }
];

const ASSET_FIELDS: FieldDef[] = [
  { key: 'name', labelKey: 'import.field.name', required: true, synonyms: ['nazwa', 'name'] },
  { key: 'assetTag', labelKey: 'import.field.assetTag', required: true, synonyms: ['tag', 'assettag', 'numer inwentarzowy', 'nr inwentarzowy'] },
  { key: 'serialNumber', labelKey: 'import.field.serialNumber', synonyms: ['numer seryjny', 'serial', 'sn'] },
  { key: 'category', labelKey: 'import.field.category', required: true, synonyms: ['kategoria', 'category'] },
  { key: 'manufacturer', labelKey: 'import.field.manufacturer', synonyms: ['producent', 'manufacturer'] },
  { key: 'model', labelKey: 'import.field.model', synonyms: ['model'] },
  { key: 'location', labelKey: 'import.field.location', synonyms: ['lokalizacja', 'location'] }
];

interface RowResult {
  index: number;
  values: Record<string, string>;
  status: 'ok' | 'error' | 'duplicate';
  reason?: string;
}

const DIACRITICS_PATTERN = new RegExp('[\\u0300-\\u036f]', 'g');

function normalize(value: string): string {
  return value.normalize('NFD').replace(DIACRITICS_PATTERN, '').toLowerCase().trim();
}

function detectDelimiter(text: string): string {
  const sample = text.slice(0, 4000);
  const counts: Record<string, number> = { ',': 0, ';': 0, '\t': 0 };
  let inQuotes = false;
  for (const char of sample) {
    if (char === '"') { inQuotes = !inQuotes; continue; }
    if (!inQuotes && char in counts) counts[char]++;
  }
  return Object.entries(counts).sort((a, b) => b[1] - a[1])[0][0];
}

function parseCsv(text: string, delimiter: string): string[][] {
  const rows: string[][] = [];
  let row: string[] = [];
  let field = '';
  let inQuotes = false;
  for (let i = 0; i < text.length; i++) {
    const char = text[i];
    if (inQuotes) {
      if (char === '"') {
        if (text[i + 1] === '"') { field += '"'; i++; } else { inQuotes = false; }
      } else {
        field += char;
      }
      continue;
    }
    if (char === '"') { inQuotes = true; continue; }
    if (char === delimiter) { row.push(field); field = ''; continue; }
    if (char === '\r') continue;
    if (char === '\n') { row.push(field); rows.push(row); row = []; field = ''; continue; }
    field += char;
  }
  if (field.length > 0 || row.length > 0) { row.push(field); rows.push(row); }
  return rows.filter(cells => cells.some(cell => cell.trim() !== ''));
}

function autoMap(headers: string[], fields: FieldDef[]): Record<string, number | null> {
  const mapping: Record<string, number | null> = {};
  const used = new Set<number>();
  for (const fieldDef of fields) {
    const matchIndex = headers.findIndex((header, index) => !used.has(index) && fieldDef.synonyms.includes(normalize(header)));
    mapping[fieldDef.key] = matchIndex >= 0 ? matchIndex : null;
    if (matchIndex >= 0) used.add(matchIndex);
  }
  return mapping;
}

function buildTemplate(entity: Entity, language: Language): string {
  const templates: Record<Language, Record<Entity, string>> = {
    pl: {
      people: 'imie,nazwisko,email,telefon,stanowisko,zespol\nJan,Kowalski,jan.kowalski@firma.pl,600100200,Programista,IT',
      assets: 'nazwa,tag,numer seryjny,kategoria,producent,model,lokalizacja\nLaptop Dell,LAP-001,SN12345,Laptopy,Dell,Latitude 5440,Biuro Warszawa'
    },
    en: {
      people: 'firstname,lastname,email,phone,job title,team\nJohn,Smith,john.smith@company.com,600100200,Developer,IT',
      assets: 'name,tag,serial,category,manufacturer,model,location\nDell Laptop,LAP-001,SN12345,Laptops,Dell,Latitude 5440,New York Office'
    },
    es: {
      people: 'nombre,apellido,email,telefono,puesto,equipo\nJuan,Garcia,juan.garcia@empresa.com,600100200,Desarrollador,TI',
      assets: 'nombre,tag,numero de serie,categoria,fabricante,modelo,ubicacion\nPortatil Dell,LAP-001,SN12345,Portatiles,Dell,Latitude 5440,Oficina Madrid'
    },
    de: {
      people: 'vorname,nachname,email,telefon,position,team\nMax,Mueller,max.mueller@firma.de,600100200,Entwickler,IT',
      assets: 'name,tag,seriennummer,kategorie,hersteller,modell,standort\nDell Laptop,LAP-001,SN12345,Laptops,Dell,Latitude 5440,Buero Berlin'
    }
  };
  return templates[language][entity];
}

function downloadText(fileName: string, content: string, type: string) {
  const blob = new Blob(['\uFEFF' + content], { type });
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = fileName;
  document.body.appendChild(link);
  link.click();
  link.remove();
  URL.revokeObjectURL(url);
}

interface ImportModalProps {
  open: boolean;
  entity: Entity;
  existingKeys: string[];
  categories?: AssetCategory[];
  teams?: Team[];
  locations?: LocationNode[];
  onClose: () => void;
  onDone: () => void;
}

export function ImportModal({ open, entity, existingKeys, categories, teams, locations, onClose, onDone }: ImportModalProps) {
  const { t, language } = useI18n();
  const fields = entity === 'people' ? PEOPLE_FIELDS : ASSET_FIELDS;
  const [step, setStep] = useState<Step>('pick');
  const [headers, setHeaders] = useState<string[]>([]);
  const [rows, setRows] = useState<string[][]>([]);
  const [mapping, setMapping] = useState<Record<string, number | null>>({});
  const [progress, setProgress] = useState({ done: 0, total: 0 });
  const [summary, setSummary] = useState({ created: 0, skipped: 0, failed: [] as { row: number; reason: string }[] });
  const [parseError, setParseError] = useState<string | null>(null);
  const [fileInputKey, setFileInputKey] = useState(0);

  function reset() {
    setStep('pick');
    setHeaders([]);
    setRows([]);
    setMapping({});
    setParseError(null);
    setSummary({ created: 0, skipped: 0, failed: [] });
    setFileInputKey(current => current + 1);
  }

  function close() {
    if (step === 'importing') return;
    reset();
    onClose();
  }

  async function handleFile(file: File) {
    setParseError(null);
    if (file.size > 5 * 1024 * 1024) {
      setParseError(t('import.fileTooLarge'));
      return;
    }
    const rawText = await file.text();
    const text = rawText.replace(/^\uFEFF/, '');
    const delimiter = detectDelimiter(text);
    const parsed = parseCsv(text, delimiter);
    if (parsed.length < 2) {
      setParseError(t('import.emptyFile'));
      return;
    }
    if (parsed.length > 5001) {
      setParseError(t('import.tooManyRows', { max: 5000 }));
      return;
    }
    const [headerRow, ...dataRows] = parsed;
    setHeaders(headerRow);
    setRows(dataRows);
    setMapping(autoMap(headerRow, fields));
    setStep('preview');
  }

  const results = useMemo<RowResult[]>(() => {
    if (step === 'pick') return [];
    const seenKeys = new Set(existingKeys.map(key => key.toLowerCase()));
    return rows.map((cells, index) => {
      const values: Record<string, string> = {};
      for (const fieldDef of fields) {
        const columnIndex = mapping[fieldDef.key];
        values[fieldDef.key] = columnIndex != null ? (cells[columnIndex] ?? '').trim() : '';
      }

      if (entity === 'people') {
        if (!values.firstName && !values.lastName && values.fullName) {
          const parts = values.fullName.split(/\s+/);
          values.firstName = parts[0] ?? '';
          values.lastName = parts.slice(1).join(' ');
        }
        if (!values.email) return { index, values, status: 'error', reason: t('import.missingRequired', { field: t('import.field.email') }) } as RowResult;
        if (!values.firstName || !values.lastName) return { index, values, status: 'error', reason: t('import.missingRequired', { field: `${t('import.field.firstName')}/${t('import.field.lastName')}` }) } as RowResult;
        const key = values.email.toLowerCase();
        if (seenKeys.has(key)) return { index, values, status: 'duplicate', reason: t('import.duplicate', { value: values.email }) } as RowResult;
        seenKeys.add(key);
        if (values.team && !teams?.some(team => normalize(team.name) === normalize(values.team))) {
          return { index, values, status: 'ok', reason: t('import.teamNotFound', { name: values.team }) } as RowResult;
        }
        return { index, values, status: 'ok' } as RowResult;
      }

      if (!values.name) return { index, values, status: 'error', reason: t('import.missingRequired', { field: t('import.field.name') }) } as RowResult;
      if (!values.assetTag) return { index, values, status: 'error', reason: t('import.missingRequired', { field: t('import.field.assetTag') }) } as RowResult;
      const key = values.assetTag.toLowerCase();
      if (seenKeys.has(key)) return { index, values, status: 'duplicate', reason: t('import.duplicate', { value: values.assetTag }) } as RowResult;
      if (values.category && !categories?.some(category => normalize(category.name) === normalize(values.category))) {
        return { index, values, status: 'error', reason: t('import.categoryNotFound', { name: values.category }) } as RowResult;
      }
      if (!values.category) return { index, values, status: 'error', reason: t('import.missingRequired', { field: t('import.field.category') }) } as RowResult;
      seenKeys.add(key);
      if (values.location && !locations?.some(location => normalize(location.fullPath) === normalize(values.location))) {
        return { index, values, status: 'ok', reason: t('import.locationNotFound', { name: values.location }) } as RowResult;
      }
      return { index, values, status: 'ok' } as RowResult;
    });
  }, [rows, mapping, step, entity, existingKeys, categories, teams, locations, fields, t]);

  const okCount = results.filter(item => item.status === 'ok').length;
  const errorCount = results.filter(item => item.status === 'error').length;
  const duplicateCount = results.filter(item => item.status === 'duplicate').length;

  async function runImport() {
    const validRows = results.filter(item => item.status === 'ok');
    setStep('importing');
    setProgress({ done: 0, total: validRows.length });
    let created = 0;
    const failed: { row: number; reason: string }[] = [];

    for (const row of validRows) {
      try {
        if (entity === 'people') {
          const team = row.values.team ? teams?.find(item => normalize(item.name) === normalize(row.values.team)) : null;
          await api.createPerson({
            firstName: row.values.firstName,
            lastName: row.values.lastName,
            email: row.values.email,
            phone: row.values.phone || null,
            employeeNumber: row.values.employeeNumber || null,
            relationType: 'Employee',
            jobTitle: row.values.jobTitle || null,
            teamId: team?.id ?? null,
            managerId: null,
            location: null,
            costCenter: null
          });
        } else {
          const category = categories?.find(item => normalize(item.name) === normalize(row.values.category));
          await api.createAsset({
            name: row.values.name,
            assetTag: row.values.assetTag,
            serialNumber: row.values.serialNumber || null,
            categoryId: category!.id,
            location: row.values.location || null,
            manufacturer: row.values.manufacturer || null,
            model: row.values.model || null,
            purchasePrice: null,
            currency: null,
            purchaseDate: null,
            warrantyUntil: null
          });
        }
        created++;
      } catch (error) {
        failed.push({ row: row.index + 2, reason: error instanceof Error ? error.message : t('import.rowFailed') });
      }
      setProgress(current => ({ ...current, done: current.done + 1 }));
    }

    setSummary({ created, skipped: errorCount + duplicateCount, failed });
    setStep('result');
    onDone();
  }

  function updateMapping(fieldKey: string, columnIndex: number | null) {
    setMapping(current => ({ ...current, [fieldKey]: columnIndex }));
  }

  const title = entity === 'people' ? t('people.import') : t('assets.import');

  return (
    <Modal open={open} title={title} onClose={close} width="wide" closeDisabled={step === 'importing'}>
      <div className="importFlow">
        {step === 'pick' && (
          <div className="formGrid">
            <p className="muted">{t('import.pickFileDesc')}</p>
            <input key={fileInputKey} type="file" accept=".csv,.tsv,text/csv,text/tab-separated-values" onChange={event => { const file = event.target.files?.[0]; if (file) void handleFile(file); }} />
            {parseError && <p className="toast toast--error">{parseError}</p>}
            <Button type="button" variant="secondary" icon={<Download size={16} />} onClick={() => downloadText(`${language === 'pl' ? 'szablon' : 'template'}-${entity}.csv`, buildTemplate(entity, language), 'text/csv')}>
              {t('import.downloadTemplate')}
            </Button>
          </div>
        )}

        {step === 'preview' && (
          <div className="formGrid">
            <div className="sectionTitle"><div><h2>{t('import.mapColumns')}</h2></div></div>
            <div className="importMapGrid">
              {fields.map(fieldDef => (
                <Field key={fieldDef.key} label={t(fieldDef.labelKey) + (fieldDef.required ? ' *' : '')}>
                  <SelectInput value={mapping[fieldDef.key] ?? ''} onChange={event => updateMapping(fieldDef.key, event.target.value === '' ? null : Number(event.target.value))}>
                    <option value="">{t('import.skipColumn')}</option>
                    {headers.map((header, index) => <option key={index} value={index}>{header}</option>)}
                  </SelectInput>
                </Field>
              ))}
            </div>

            <div className="sectionTitle"><div><h2>{t('import.preview')}</h2><p>{t('import.previewSummary', { ok: okCount, errors: errorCount, duplicates: duplicateCount })}</p></div></div>
            <div className="tableWrap importPreviewTable">
              <table className="dense-table">
                <thead><tr><th></th><th>{t('import.colReason')}</th>{fields.map(f => <th key={f.key}>{t(f.labelKey)}</th>)}</tr></thead>
                <tbody>
                  {results.map(result => (
                    <tr key={result.index}>
                      <td>
                        <span className={`status status--${result.status === 'ok' ? 'InStock' : result.status === 'duplicate' ? 'InService' : 'Damaged'}`}>
                          {result.status === 'ok' ? t('import.statusOk') : result.status === 'duplicate' ? t('import.statusDuplicate') : t('import.statusError')}
                        </span>
                      </td>
                      <td>{result.reason ?? '-'}</td>
                      {fields.map(f => <td key={f.key}>{result.values[f.key] || '-'}</td>)}
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            <div className="formActions formActions--split">
              <Button type="button" variant="ghost" onClick={() => setStep('pick')}>{t('common.cancel')}</Button>
              <Button type="button" disabled={okCount === 0} icon={<Upload size={16} />} onClick={runImport}>
                {t('import.startImport', { count: okCount })}
              </Button>
            </div>
          </div>
        )}

        {step === 'importing' && (
          <div className="formGrid">
            <p>{t('import.progress', { done: progress.done, total: progress.total })}</p>
            <div style={{ background: 'var(--border)', borderRadius: 'var(--radius)', height: '8px', overflow: 'hidden' }}>
              <div style={{ width: `${progress.total ? (progress.done / progress.total) * 100 : 0}%`, height: '100%', background: 'var(--brand)', borderRadius: 'var(--radius)' }} />
            </div>
          </div>
        )}

        {step === 'result' && (
          <div className="formGrid">
            <p>{t('import.summary', { created: summary.created, skipped: summary.skipped, failed: summary.failed.length })}</p>
            {summary.failed.length > 0 && (
              <div className="listRows">
                {summary.failed.map(item => (
                  <div className="listRow" key={item.row}><div><strong>{t('import.rowLabel', { row: item.row })}</strong><small>{item.reason}</small></div></div>
                ))}
              </div>
            )}
            <div className="formActions formActions--split">
              <div />
              <Button type="button" onClick={close}>{t('import.close')}</Button>
            </div>
          </div>
        )}
      </div>
    </Modal>
  );
}
