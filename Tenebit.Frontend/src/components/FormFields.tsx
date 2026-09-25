import { useEffect, useId, useRef, useState, type InputHTMLAttributes, type SelectHTMLAttributes, type TextareaHTMLAttributes } from 'react';
import { CalendarCheck } from 'lucide-react';
import { useI18n } from '../i18n/I18nProvider';

type ValidatableControl = HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement;
type Translate = (key: string, params?: Record<string, string | number>) => string;

/// Przekłada stan walidacji kontrolki na jedno zdanie w języku interfejsu. Kolejność ma znaczenie:
/// puste pole zgłasza tylko valueMissing, ale pole z błędną wartością potrafi zapalić kilka flag naraz
/// i wtedy pokazujemy tę najbliższą temu, co użytkownik właśnie zrobił.
export function validationMessage(control: ValidatableControl, t: Translate): string {
  const validity = control.validity;
  const input = control as HTMLInputElement;

  if (validity.customError) return control.validationMessage;
  if (validity.valueMissing) return t('validation.required');
  if (validity.typeMismatch) {
    if (input.type === 'email') return t('validation.email');
    if (input.type === 'url') return t('validation.url');
    return t('validation.invalid');
  }
  if (validity.badInput) return t('validation.badInput');
  if (validity.rangeUnderflow) return t('validation.min', { min: input.min });
  if (validity.rangeOverflow) return t('validation.max', { max: input.max });
  if (validity.tooShort) return t('validation.tooShort', { min: input.minLength });
  if (validity.tooLong) return t('validation.tooLong', { max: input.maxLength });
  if (validity.stepMismatch) return t('validation.step');
  if (validity.patternMismatch) return t('validation.pattern');
  return t('validation.invalid');
}

/// Pole formularza razem z komunikatem walidacji.
///
/// Walidacja w całej aplikacji opiera się na atrybutach HTML (required, type, min, maxLength), a sama
/// przeglądarka pokazywała przy nich tylko własny dymek: znikał po chwili, był w języku przeglądarki,
/// a czytnikom ekranu zostawiał wyłącznie czerwoną obwódkę (QA: BUG-003). Zdarzenie `invalid` nie
/// bąbelkuje, ale faza przechwytywania i tak przechodzi przez przodków - dlatego jeden nasłuch na
/// etykiecie wystarczy, żeby przejąć każdą kontrolkę w środku, wyciszyć dymek i pokazać komunikat
/// tekstowy pod polem. Działa to w każdym formularzu korzystającym z <Field>, bez zmian w nich samych.
/// `group` renderuje pole jako <div role="group"> zamiast <label> - dla kontrolek złożonych z kilku
/// elementów (grupa przycisków, tagi z przyciskami usuwania). <label> "klika" swoją pierwszą kontrolkę przy
/// kliknięciu w tekst etykiety, co zaznaczałoby pierwszą opcję albo usuwało pierwszy tag.
export function Field({ label, info, group, children }: { label: string; info?: string; group?: boolean; children: React.ReactNode }) {
  const { t } = useI18n();
  const ref = useRef<HTMLLabelElement & HTMLDivElement>(null);
  const labelId = useId();
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const node = ref.current;
    if (!node) return;

    const onInvalid = (event: Event) => {
      const control = event.target as ValidatableControl;
      event.preventDefault();
      control.setAttribute('aria-invalid', 'true');
      setError(validationMessage(control, t));
    };
    const onEdit = (event: Event) => {
      (event.target as Element | null)?.removeAttribute('aria-invalid');
      setError(current => (current === null ? current : null));
    };

    node.addEventListener('invalid', onInvalid, true);
    node.addEventListener('input', onEdit);
    node.addEventListener('change', onEdit);
    return () => {
      node.removeEventListener('invalid', onInvalid, true);
      node.removeEventListener('input', onEdit);
      node.removeEventListener('change', onEdit);
    };
  }, [t]);

  const className = error ? 'field field--invalid' : 'field';
  const errorNode = error ? <small className="fieldError" role="alert">{error}</small> : null;
  if (group) {
    return (
      <div ref={ref} className={className} title={info ?? undefined} role="group" aria-labelledby={labelId}>
        <span id={labelId}>{label}</span>
        {children}
        {errorNode}
      </div>
    );
  }
  return (
    <label ref={ref} className={className} title={info ?? undefined}>
      <span>{label}</span>
      {children}
      {errorNode}
    </label>
  );
}

export function TextInput(props: InputHTMLAttributes<HTMLInputElement>) {
  if (props.type === 'date' || props.type === 'datetime-local') return <DateInput {...props} />;
  return <input className="input" {...props} />;
}

const pad = (value: number) => String(value).padStart(2, '0');

/// Dzisiejsza data w strefie przeglądarki. toISOString() dawałby datę UTC, czyli w Polsce między
/// północą a 2:00 jeszcze wczorajszą.
export function localTodayIso(now = new Date()): string {
  return `${now.getFullYear()}-${pad(now.getMonth() + 1)}-${pad(now.getDate())}`;
}

/// Wartość, którą wpisuje przycisk "Dzisiaj". Dla datetime-local zostawia godzinę już wpisaną w pole,
/// a gdy jej nie ma - bierze bieżącą, żeby pole od razu było kompletne.
export function todayInputValue(type: string, current: string, now = new Date()): string {
  const date = localTodayIso(now);
  if (type !== 'datetime-local') return date;
  const time = /T(\d{2}:\d{2})/.exec(current)?.[1] ?? `${pad(now.getHours())}:${pad(now.getMinutes())}`;
  return `${date}T${time}`;
}

const MIN_YEAR = 1900;
const MAX_YEAR = new Date().getFullYear() + 50;

/// Rok poza rozsądnym zakresem, wpisany bez żadnego ostrzeżenia przeglądarki. Natywny <input type="date">
/// dzieli się na trzy segmenty sterowane pozycją kursora, nie parsowaniem tekstu - wpisanie "po ludzku"
/// ciągu cyfr ze slashami (np. wklejone `09/25/2026`) rozjeżdża się z tym, czego oczekuje kontrolka,
/// i cicho produkuje datę w stylu "09.02.0005" zamiast błędu walidacji (QA zgłoszenie 25.09.2026).
/// Sama wartość jest przy tym syntaktycznie poprawną datą, więc validity API przeglądarki jej nie łapie -
/// stąd ręczny setCustomValidity, włączony w ten sam mechanizm co reszta walidacji w <Field>.
export function yearRangeError(value: string, t: Translate): string {
  const year = Number(value.slice(0, 4));
  if (value.length < 4 || !Number.isFinite(year)) return '';
  return year < MIN_YEAR || year > MAX_YEAR ? t('validation.yearRange', { min: MIN_YEAR, max: MAX_YEAR }) : '';
}

/// Pole daty z przyciskiem "Dzisiaj". Natywny kalendarz wymaga otwarcia i szukania dnia, a puste pole
/// straszy tylko maską dd.mm.rrrr - jedno kliknięcie w ikonę wpisuje dzisiejszą datę. Wartość ustawiamy
/// przez natywny setter i zdarzenie `input`, więc działa to tak samo dla pól sterowanych (onChange
/// dostaje zdarzenie jak od użytkownika) i niesterowanych (FormData czyta wartość z DOM), a <Field>
/// czyści przy tym komunikat walidacji.
function DateInput(props: InputHTMLAttributes<HTMLInputElement>) {
  const { t } = useI18n();
  const ref = useRef<HTMLInputElement>(null);
  const today = localTodayIso();
  const outOfRange = (typeof props.min === 'string' && props.min.slice(0, 10) > today)
    || (typeof props.max === 'string' && props.max !== '' && props.max.slice(0, 10) < today);
  const locked = props.disabled || props.readOnly;

  // Rok poza zakresem ustawia customValidity na każdą zmianę (żeby złapać ją także przy zapisie formularza
  // bez opuszczania pola), a dopiero na blur wywołuje reportValidity() - to ono odpala zdarzenie `invalid`,
  // które <Field> już przechwytuje i pokazuje jako zwykły komunikat pod polem, bez czekania na submit.
  useEffect(() => {
    const input = ref.current;
    if (!input) return;
    const sync = () => input.setCustomValidity(yearRangeError(input.value, t));
    const onBlur = () => { sync(); if (!input.validity.valid) input.reportValidity(); };
    sync();
    input.addEventListener('input', sync);
    input.addEventListener('change', sync);
    input.addEventListener('blur', onBlur);
    return () => {
      input.removeEventListener('input', sync);
      input.removeEventListener('change', sync);
      input.removeEventListener('blur', onBlur);
    };
  }, [t]);

  const setToday = () => {
    const input = ref.current;
    if (!input) return;
    const setter = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value')?.set;
    setter?.call(input, todayInputValue(input.type, input.value));
    input.dispatchEvent(new Event('input', { bubbles: true }));
    input.dispatchEvent(new Event('change', { bubbles: true }));
  };

  return (
    <span className="dateInput">
      <input className="input" {...props} ref={ref} />
      {locked ? null : (
        <button
          type="button"
          className="dateInput__today"
          onClick={setToday}
          disabled={outOfRange}
          title={t('common.today')}
          aria-label={t('common.today')}
        >
          <CalendarCheck size={16} />
        </button>
      )}
    </span>
  );
}

export function SelectInput(props: SelectHTMLAttributes<HTMLSelectElement>) {
  return <select className="input" {...props} />;
}

export function TextArea(props: TextareaHTMLAttributes<HTMLTextAreaElement>) {
  return <textarea className="input textarea" {...props} />;
}
