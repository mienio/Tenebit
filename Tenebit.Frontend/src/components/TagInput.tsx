import { useId, useMemo, useState, type KeyboardEvent } from 'react';
import { X } from 'lucide-react';
import { useI18n } from '../i18n/I18nProvider';
import { matchSuggestions } from './Autocomplete';

interface TagInputProps {
  tags: string[];
  onChange(tags: string[]): void;
  /** Gdy podane, każdy tag trafia do FormData jako osobna wartość pod tą nazwą. */
  name?: string;
  suggestions?: string[];
  /** Zamienia wpisany tekst na tag albo zwraca null, gdy wartość jest niedozwolona (np. nie liczba). */
  parse?(text: string): string | null;
  /** Szybkie przyciski z typowymi wartościami - pokazywane tylko te, których jeszcze nie ma na liście. */
  quickValues?: string[];
  max?: number;
  placeholder?: string;
  inputMode?: 'text' | 'numeric';
  /** Sortowanie tagów po dodaniu (np. progi dni malejąco). */
  sort?(a: string, b: string): number;
  labelledBy?: string;
}

/**
 * Lista wartości jako osobne, usuwalne tagi. Nowy tag: wpisanie + Enter (albo przecinek), Backspace w pustym
 * polu usuwa ostatni. Zastępuje pola typu "30, 7", w których jedna literówka psuła cały zapis.
 * Wymaga <Field group> - przyciski usuwania nie mogą leżeć w <label>.
 */
export function TagInput({ tags, onChange, name, suggestions = [], parse, quickValues, max, placeholder, inputMode, sort, labelledBy }: TagInputProps) {
  const { t } = useI18n();
  const listId = useId();
  const [text, setText] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [open, setOpen] = useState(false);
  const [active, setActive] = useState(-1);
  const full = max !== undefined && tags.length >= max;
  const has = (value: string) => tags.some(tag => tag.toLocaleLowerCase() === value.toLocaleLowerCase());
  const visible = useMemo(
    () => (open ? matchSuggestions(suggestions.filter(item => !has(item)), text) : []),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [open, suggestions, text, tags]
  );

  function add(raw: string) {
    const trimmed = raw.trim();
    if (!trimmed) return;
    const value = parse ? parse(trimmed) : trimmed;
    if (value === null) { setError(t('tags.invalidValue', { value: trimmed })); return; }
    if (full) { setError(t('tags.limitReached', { max: max ?? 0 })); return; }
    setError(null);
    setText('');
    setActive(-1);
    if (has(value)) return;
    const next = [...tags, value];
    onChange(sort ? next.sort(sort) : next);
  }

  function remove(value: string) {
    setError(null);
    onChange(tags.filter(tag => tag !== value));
  }

  function onKeyDown(event: KeyboardEvent<HTMLInputElement>) {
    if (event.key === 'Enter' || event.key === ',') {
      event.preventDefault();
      if (active >= 0 && visible[active]) add(visible[active]);
      else add(text);
    } else if (event.key === 'Backspace' && !text && tags.length) {
      remove(tags[tags.length - 1]);
    } else if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
      if (!suggestions.length) return;
      event.preventDefault();
      setOpen(true);
      const step = event.key === 'ArrowDown' ? 1 : -1;
      setActive(index => (visible.length ? (index + step + visible.length) % visible.length : -1));
    } else if (event.key === 'Escape' && open && visible.length) {
      event.preventDefault();
      event.stopPropagation();
      setOpen(false);
    }
  }

  const quick = (quickValues ?? []).filter(value => !has(value));

  return (
    <div className="tagInput">
      <div className="tagInput__box picker">
        {tags.map(tag => (
          <span key={tag} className="tagInput__tag">
            {tag}
            <button type="button" onClick={() => remove(tag)} aria-label={t('tags.remove', { value: tag })}><X size={12} /></button>
            {name ? <input type="hidden" name={name} value={tag} /> : null}
          </span>
        ))}
        <input
          className="tagInput__input"
          value={text}
          inputMode={inputMode}
          placeholder={full ? undefined : placeholder ?? t('tags.addPlaceholder')}
          disabled={full}
          aria-labelledby={labelledBy}
          role={suggestions.length ? 'combobox' : undefined}
          aria-expanded={suggestions.length ? open && visible.length > 0 : undefined}
          aria-controls={suggestions.length ? listId : undefined}
          aria-activedescendant={open && active >= 0 && visible[active] ? `${listId}-${active}` : undefined}
          onChange={event => { setText(event.target.value); setError(null); setOpen(true); setActive(-1); }}
          onFocus={() => setOpen(true)}
          onBlur={() => { setOpen(false); if (text.trim()) add(text); }}
          onKeyDown={onKeyDown}
        />
        {open && visible.length ? (
          <ul className="picker__list" id={listId} role="listbox">
            {visible.map((item, index) => (
              <li
                key={item}
                id={`${listId}-${index}`}
                role="option"
                aria-selected={index === active}
                className={index === active ? 'picker__option picker__option--active' : 'picker__option'}
                onMouseDown={event => { event.preventDefault(); add(item); }}
                onMouseEnter={() => setActive(index)}
              >
                {item}
              </li>
            ))}
          </ul>
        ) : null}
      </div>
      {quick.length && !full ? (
        <div className="presetRow" aria-label={t('tags.quickAdd')}>
          {quick.map(value => <button key={value} type="button" className="presetChip" onClick={() => add(value)}>+ {value}</button>)}
        </div>
      ) : null}
      {error ? <small className="fieldError" role="alert">{error}</small> : null}
    </div>
  );
}
