import { useEffect, useRef, useState, type InputHTMLAttributes, type SelectHTMLAttributes, type TextareaHTMLAttributes } from 'react';
import { useI18n } from '../i18n/I18nProvider';

type ValidatableControl = HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement;
type Translate = (key: string, params?: Record<string, string | number>) => string;

/// Przekłada stan walidacji kontrolki na jedno zdanie w języku interfejsu. Kolejność ma znaczenie:
/// puste pole zgłasza tylko valueMissing, ale pole z błędną wartością potrafi zapalić kilka flag naraz
/// i wtedy pokazujemy tę najbliższą temu, co użytkownik właśnie zrobił.
export function validationMessage(control: ValidatableControl, t: Translate): string {
  const validity = control.validity;
  const input = control as HTMLInputElement;

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
export function Field({ label, info, children }: { label: string; info?: string; children: React.ReactNode }) {
  const { t } = useI18n();
  const ref = useRef<HTMLLabelElement>(null);
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

  return (
    <label ref={ref} className={error ? 'field field--invalid' : 'field'} title={info ?? undefined}>
      <span>{label}</span>
      {children}
      {error ? <small className="fieldError" role="alert">{error}</small> : null}
    </label>
  );
}

export function TextInput(props: InputHTMLAttributes<HTMLInputElement>) {
  return <input className="input" {...props} />;
}

export function SelectInput(props: SelectHTMLAttributes<HTMLSelectElement>) {
  return <select className="input" {...props} />;
}

export function TextArea(props: TextareaHTMLAttributes<HTMLTextAreaElement>) {
  return <textarea className="input textarea" {...props} />;
}
