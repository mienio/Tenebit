import { useId, useMemo, useRef, useState, type KeyboardEvent } from 'react';
import { Search } from 'lucide-react';
import { useI18n } from '../i18n/I18nProvider';

export interface PickerOption {
  value: string;
  label: string;
  /** Nagłówek grupy w liście z wyszukiwarką (np. typ kategorii). Segmented i zwykły select go pomijają. */
  group?: string;
}

export type PickerKind = 'segmented' | 'select' | 'search';

// Jedno miejsce, w którym zapada decyzja "jaki komponent do ilu opcji". Przy 2-4 opcjach wszystko mieści
// się w jednym rzędzie przycisków i wybór to jedno kliknięcie; od 5 przyciski przestają się czytelnie
// mieścić, więc zwykły select; od 9 przewijanie listy jest wolniejsze niż wpisanie kilku liter.
export const SEGMENTED_MAX_OPTIONS = 4;
export const SELECT_MAX_OPTIONS = 8;

export function pickerKindFor(optionCount: number): PickerKind {
  if (optionCount <= SEGMENTED_MAX_OPTIONS) return 'segmented';
  if (optionCount <= SELECT_MAX_OPTIONS) return 'select';
  return 'search';
}

/// Porównanie bez wielkości liter i polskich znaków - "lodz" ma znaleźć "Łódź", "zolw" - "żółw".
export function normalizeForSearch(text: string): string {
  return text.normalize('NFD').replace(/[\u0300-\u036f]/g, '').replace(/ł/g, 'l').replace(/Ł/g, 'L').toLowerCase();
}

export function filterOptions<T extends { label: string }>(options: T[], query: string): T[] {
  const needle = normalizeForSearch(query.trim());
  if (!needle) return options;
  return options.filter(option => normalizeForSearch(option.label).includes(needle));
}

interface OptionPickerProps {
  options: PickerOption[];
  /** Nazwa pola w FormData. Formularze w aplikacji czytają wartości z FormData, więc wybór zawsze trafia do DOM. */
  name?: string;
  value?: string;
  defaultValue?: string;
  onChange?(value: string): void;
  required?: boolean;
  disabled?: boolean;
  /** Opcja "brak wyboru" (wartość ''), np. "Bez lokalizacji". Liczy się do progu jak każda inna. */
  emptyOption?: string;
  /** Tekst w pustym select/wyszukiwarce, gdy nic nie wybrano i nie ma emptyOption. */
  placeholder?: string;
  /** Wymusza wariant - tylko dla wyjątków; domyślnie decyduje liczba opcji. */
  kind?: PickerKind;
  'aria-label'?: string;
  'aria-labelledby'?: string;
}

/**
 * Pole wyboru, które samo dobiera kontrolkę do liczby opcji (patrz pickerKindFor). Niezależnie od wariantu
 * wartość trafia do formularza pod tym samym `name`, a `required` działa przez natywną walidację -
 * <Field> pokazuje wtedy ten sam komunikat co przy każdym innym polu.
 *
 * Wariant segmented to grupa radio, więc <Field> musi mieć `group` (etykieta nie może obejmować kilku
 * kontrolek naraz - kliknięcie w jej tekst zaznaczałoby pierwszą opcję).
 */
export function OptionPicker(props: OptionPickerProps) {
  const { options, emptyOption } = props;
  const allOptions = useMemo(
    () => (emptyOption !== undefined ? [{ value: '', label: emptyOption }, ...options] : options),
    [options, emptyOption]
  );
  const [inner, setInner] = useState(props.defaultValue ?? '');
  const current = props.value ?? inner;
  const kind = props.kind ?? pickerKindFor(allOptions.length);

  const change = (next: string) => {
    if (props.value === undefined) setInner(next);
    props.onChange?.(next);
  };

  if (kind === 'segmented') return <SegmentedPicker {...props} options={allOptions} current={current} onPick={change} />;
  if (kind === 'select') {
    return (
      <select
        className="input"
        name={props.name}
        value={current}
        onChange={event => change(event.target.value)}
        required={props.required}
        disabled={props.disabled}
        aria-label={props['aria-label']}
        aria-labelledby={props['aria-labelledby']}
      >
        {emptyOption === undefined ? <option value="" disabled={props.required}>{props.placeholder ?? ''}</option> : null}
        {allOptions.map(option => <option key={option.value} value={option.value}>{option.label}</option>)}
      </select>
    );
  }
  return <SearchablePicker {...props} options={allOptions} current={current} onPick={change} />;
}

type InnerProps = OptionPickerProps & { current: string; onPick(value: string): void };

function SegmentedPicker({ options, name, current, onPick, required, disabled, ...aria }: InnerProps) {
  const fallbackName = useId();
  return (
    <div className="segmented" role="radiogroup" aria-label={aria['aria-label']} aria-labelledby={aria['aria-labelledby']}>
      {options.map(option => (
        <label key={option.value} className={current === option.value ? 'segmented__option segmented__option--active' : 'segmented__option'}>
          <input
            type="radio"
            name={name ?? fallbackName}
            value={option.value}
            checked={current === option.value}
            onChange={() => onPick(option.value)}
            required={required}
            disabled={disabled}
          />
          <span>{option.label}</span>
        </label>
      ))}
    </div>
  );
}

function SearchablePicker({ options, name, current, onPick, required, disabled, placeholder, ...aria }: InnerProps) {
  const { t } = useI18n();
  const listId = useId();
  const inputRef = useRef<HTMLInputElement>(null);
  const [open, setOpen] = useState(false);
  const [query, setQuery] = useState('');
  const [active, setActive] = useState(0);
  const selected = options.find(option => option.value === current);
  const visible = useMemo(() => filterOptions(options, query), [options, query]);

  function openList() {
    if (disabled) return;
    setQuery('');
    const selectedIndex = options.findIndex(option => option.value === current);
    setActive(Math.max(selectedIndex, 0));
    setOpen(true);
  }

  function close() {
    setOpen(false);
    setQuery('');
  }

  function pick(option: PickerOption) {
    onPick(option.value);
    close();
  }

  function onKeyDown(event: KeyboardEvent<HTMLInputElement>) {
    if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
      event.preventDefault();
      if (!open) return openList();
      const step = event.key === 'ArrowDown' ? 1 : -1;
      setActive(index => (visible.length ? (index + step + visible.length) % visible.length : 0));
    } else if (event.key === 'Enter') {
      if (!open) return;
      // Enter w otwartej liście wybiera pozycję - nie może przy okazji wysłać formularza.
      event.preventDefault();
      const option = visible[active];
      if (option) pick(option);
    } else if (event.key === 'Escape') {
      if (!open) return;
      // Escape zamyka tylko listę; modal nasłuchuje na document, więc zatrzymujemy zdarzenie tutaj.
      event.preventDefault();
      event.stopPropagation();
      close();
    } else if (event.key === 'Tab') {
      if (open) close();
    }
  }

  // Wiersze listy z nagłówkami grup - nagłówek pojawia się, gdy grupa zmienia się względem poprzedniej pozycji.
  let lastGroup: string | undefined;

  return (
    <div className="picker">
      <input
        ref={inputRef}
        className="input picker__input"
        role="combobox"
        aria-expanded={open}
        aria-controls={listId}
        aria-autocomplete="list"
        aria-activedescendant={open && visible[active] ? `${listId}-${active}` : undefined}
        aria-label={aria['aria-label']}
        aria-labelledby={aria['aria-labelledby']}
        autoComplete="off"
        disabled={disabled}
        value={open ? query : selected?.label ?? ''}
        placeholder={open ? selected?.label ?? t('picker.searchPlaceholder') : placeholder ?? t('picker.choose')}
        onChange={event => { if (!open) setOpen(true); setQuery(event.target.value); setActive(0); }}
        onFocus={openList}
        onClick={() => { if (!open) openList(); }}
        onBlur={close}
        onKeyDown={onKeyDown}
      />
      {/* Lupa zamiast strzałki: przy 9+ opcjach pole jest polem tekstowym z wyszukiwaniem, nie zwykłym
          selectem, a sama strzałka w dół nie mówiła tego wystarczająco wyraźnie (QA 25.09.2026). */}
      <Search className="picker__chevron" size={16} aria-hidden />
      {/* Nośnik wartości dla FormData i walidacji `required`. Nie `type="hidden"` i nie readOnly - takie
          pola przeglądarka pomija przy walidacji, a pusty wymagany wybór ma zatrzymać zapis. */}
      <input className="picker__value" tabIndex={-1} aria-hidden name={name} value={current} required={required} onChange={() => undefined} />
      {open ? (
        // preventDefault na click: lista siedzi w <label> pola, a klik w etykietę "klika" też jej kontrolkę,
        // co otwierałoby listę ponownie tuż po wyborze.
        <ul className="picker__list" id={listId} role="listbox" onClick={event => event.preventDefault()}>
          {visible.length === 0 ? <li className="picker__empty">{t('picker.noResults')}</li> : visible.map((option, index) => {
            const header = option.group && option.group !== lastGroup ? option.group : null;
            lastGroup = option.group;
            return (
              <li key={option.value || '__empty'} role="presentation">
                {header ? <div className="picker__group">{header}</div> : null}
                <div
                  id={`${listId}-${index}`}
                  role="option"
                  aria-selected={option.value === current}
                  className={index === active ? 'picker__option picker__option--active' : 'picker__option'}
                  // mousedown zamiast click: blur pola zamknąłby listę, zanim click by doszedł.
                  onMouseDown={event => { event.preventDefault(); pick(option); }}
                  onMouseEnter={() => setActive(index)}
                >
                  {option.label}
                </div>
              </li>
            );
          })}
        </ul>
      ) : null}
    </div>
  );
}
