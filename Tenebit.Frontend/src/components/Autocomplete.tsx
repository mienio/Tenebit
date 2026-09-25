import { useId, useMemo, useState, type InputHTMLAttributes, type KeyboardEvent } from 'react';
import { normalizeForSearch } from './OptionPicker';

const MAX_SUGGESTIONS = 8;

/// Unikalne, niepuste wartości w kolejności alfabetycznej; warianty różniące się tylko wielkością liter
/// lub spacjami na brzegach liczą się jako jedna podpowiedź (zostaje pierwsza napotkana pisownia).
export function uniqueSuggestions(values: Array<string | null | undefined>): string[] {
  const seen = new Map<string, string>();
  for (const raw of values) {
    const value = raw?.trim();
    if (!value) continue;
    const key = value.toLocaleLowerCase();
    if (!seen.has(key)) seen.set(key, value);
  }
  return [...seen.values()].sort((a, b) => a.localeCompare(b));
}

export function matchSuggestions(suggestions: string[], query: string): string[] {
  const needle = normalizeForSearch(query.trim());
  if (!needle) return suggestions.slice(0, MAX_SUGGESTIONS);
  return suggestions
    .filter(item => normalizeForSearch(item).includes(needle) && normalizeForSearch(item) !== needle)
    .slice(0, MAX_SUGGESTIONS);
}

type AutocompleteProps = Omit<InputHTMLAttributes<HTMLInputElement>, 'value' | 'defaultValue' | 'onChange'> & {
  suggestions: string[];
  value?: string;
  defaultValue?: string;
  onChange?(value: string): void;
};

/**
 * Pole tekstowe z podpowiedziami z wartości już użytych w organizacji (stanowiska, dostawcy, serwisy).
 * To nie jest zamknięty słownik: podpowiedź można wybrać albo wpisać zupełnie nową wartość - dlatego
 * Enter bez podświetlonej podpowiedzi zachowuje się jak w zwykłym polu i wysyła formularz.
 */
export function Autocomplete({ suggestions, value, defaultValue, onChange, onBlur, onKeyDown, ...inputProps }: AutocompleteProps) {
  const listId = useId();
  const [inner, setInner] = useState(defaultValue ?? '');
  const current = value ?? inner;
  const [open, setOpen] = useState(false);
  const [active, setActive] = useState(-1);
  const visible = useMemo(() => (open ? matchSuggestions(suggestions, current) : []), [open, suggestions, current]);
  const expanded = open && visible.length > 0;

  function set(next: string) {
    if (value === undefined) setInner(next);
    onChange?.(next);
  }

  function pick(next: string) {
    set(next);
    setOpen(false);
    setActive(-1);
  }

  function handleKeyDown(event: KeyboardEvent<HTMLInputElement>) {
    onKeyDown?.(event);
    if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
      event.preventDefault();
      if (!open) { setOpen(true); setActive(0); return; }
      const step = event.key === 'ArrowDown' ? 1 : -1;
      setActive(index => (visible.length ? (index + step + visible.length) % visible.length : -1));
    } else if (event.key === 'Enter' && expanded && active >= 0 && visible[active]) {
      event.preventDefault();
      pick(visible[active]);
    } else if (event.key === 'Escape' && expanded) {
      event.preventDefault();
      event.stopPropagation();
      setOpen(false);
    } else if (event.key === 'Tab') {
      setOpen(false);
    }
  }

  return (
    <div className="picker">
      <input
        className="input"
        {...inputProps}
        role="combobox"
        aria-expanded={expanded}
        aria-controls={listId}
        aria-autocomplete="list"
        aria-activedescendant={expanded && active >= 0 ? `${listId}-${active}` : undefined}
        autoComplete="off"
        value={current}
        onChange={event => { set(event.target.value); setOpen(true); setActive(-1); }}
        onBlur={event => { setOpen(false); onBlur?.(event); }}
        onKeyDown={handleKeyDown}
      />
      {expanded ? (
        <ul className="picker__list" id={listId} role="listbox" onClick={event => event.preventDefault()}>
          {visible.map((item, index) => (
            <li
              key={item}
              id={`${listId}-${index}`}
              role="option"
              aria-selected={index === active}
              className={index === active ? 'picker__option picker__option--active' : 'picker__option'}
              onMouseDown={event => { event.preventDefault(); pick(item); }}
              onMouseEnter={() => setActive(index)}
            >
              {item}
            </li>
          ))}
        </ul>
      ) : null}
    </div>
  );
}
