import { Boxes, KeyRound, MapPin, Search, Users } from 'lucide-react';
import { useCallback, useEffect, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { api, type SearchHit } from '../api/endpoints';
import { useDebouncedValue } from '../hooks/useDebouncedValue';
import { useI18n } from '../i18n/I18nProvider';
import './searchPalette.css';

export const OPEN_SEARCH_EVENT = 'tenebit:open-search';

export function openSearchPalette() {
  window.dispatchEvent(new CustomEvent(OPEN_SEARCH_EVENT));
}

const ICONS = {
  asset: Boxes,
  person: Users,
  location: MapPin,
  license: KeyRound,
} as const;

/**
 * Ctrl+K quick search across assets, people, locations and licenses.
 *
 * Results come from /api/search, which reuses each module's own service - so the palette shows exactly
 * what the signed-in user is allowed to see elsewhere, with no separate permission logic here.
 */
export function SearchPalette() {
  const navigate = useNavigate();
  const { t } = useI18n();
  const [open, setOpen] = useState(false);
  const [query, setQuery] = useState('');
  const [hits, setHits] = useState<SearchHit[]>([]);
  const [loading, setLoading] = useState(false);
  const [active, setActive] = useState(0);
  const inputRef = useRef<HTMLInputElement | null>(null);
  const debounced = useDebouncedValue(query, 220);

  const close = useCallback(() => {
    setOpen(false);
    setQuery('');
    setHits([]);
    setActive(0);
  }, []);

  // Ctrl+K / Cmd+K anywhere in the app, and "/" when not already typing in a field.
  useEffect(() => {
    function onKeyDown(event: KeyboardEvent) {
      const isShortcut = (event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'k';
      const target = event.target as HTMLElement | null;
      const typing = !!target && (target.tagName === 'INPUT' || target.tagName === 'TEXTAREA' || target.isContentEditable);

      if (isShortcut || (event.key === '/' && !typing)) {
        event.preventDefault();
        setOpen(current => !current);
      }
    }

    // Also opened by the sidebar button, which fires this event - avoids lifting palette state into
    // Layout just to expose one "open" call.
    function onRequestOpen() { setOpen(true); }

    document.addEventListener('keydown', onKeyDown);
    window.addEventListener(OPEN_SEARCH_EVENT, onRequestOpen);
    return () => {
      document.removeEventListener('keydown', onKeyDown);
      window.removeEventListener(OPEN_SEARCH_EVENT, onRequestOpen);
    };
  }, []);

  useEffect(() => {
    if (open) inputRef.current?.focus();
  }, [open]);

  useEffect(() => {
    if (!open) return;
    const term = debounced.trim();
    if (term.length < 2) {
      setHits([]);
      setLoading(false);
      return;
    }

    let cancelled = false;
    setLoading(true);
    api.search(term)
      .then(result => {
        if (cancelled) return;
        setHits(result.hits);
        setActive(0);
      })
      .catch(() => { if (!cancelled) setHits([]); })
      .finally(() => { if (!cancelled) setLoading(false); });

    return () => { cancelled = true; };
  }, [debounced, open]);

  function go(hit: SearchHit) {
    close();
    // The server already built a route that points at the record (asset detail panel, or the module
    // list pre-filtered), so it is followed as-is.
    navigate(hit.url);
  }

  function onKeyDown(event: React.KeyboardEvent<HTMLDivElement>) {
    if (event.key === 'Escape') { close(); return; }
    if (hits.length === 0) return;

    if (event.key === 'ArrowDown') {
      event.preventDefault();
      setActive(index => (index + 1) % hits.length);
    } else if (event.key === 'ArrowUp') {
      event.preventDefault();
      setActive(index => (index - 1 + hits.length) % hits.length);
    } else if (event.key === 'Enter') {
      event.preventDefault();
      go(hits[active]);
    }
  }

  if (!open) return null;

  const term = query.trim();

  return (
    <div className="palette" role="dialog" aria-modal="true" aria-label={t('search.title')} onClick={close}>
      <div className="palette__panel" onClick={event => event.stopPropagation()} onKeyDown={onKeyDown}>
        <div className="palette__inputRow">
          <Search size={18} />
          <input
            ref={inputRef}
            className="palette__input"
            value={query}
            onChange={event => setQuery(event.target.value)}
            placeholder={t('search.placeholder')}
            aria-label={t('search.placeholder')}
          />
          <kbd className="palette__kbd">esc</kbd>
        </div>

        <div className="palette__results" role="listbox">
          {term.length < 2 ? (
            <p className="palette__hint">{t('search.hintShort')}</p>
          ) : loading ? (
            <p className="palette__hint">{t('common.loading')}</p>
          ) : hits.length === 0 ? (
            <p className="palette__hint">{t('search.empty')}</p>
          ) : (
            hits.map((hit, index) => {
              const Icon = ICONS[hit.kind] ?? Boxes;
              return (
                <button
                  key={`${hit.kind}-${hit.id}`}
                  type="button"
                  role="option"
                  aria-selected={index === active}
                  className={`palette__hit${index === active ? ' palette__hit--active' : ''}`}
                  onMouseEnter={() => setActive(index)}
                  onClick={() => go(hit)}
                >
                  <span className="palette__icon"><Icon size={16} /></span>
                  <span className="palette__text">
                    <strong>{hit.title}</strong>
                    {hit.subtitle ? <span>{hit.subtitle}</span> : null}
                  </span>
                  {hit.badge ? <span className="palette__badge">{hit.badge}</span> : null}
                </button>
              );
            })
          )}
        </div>

        <div className="palette__footer">
          <span><kbd>↑</kbd><kbd>↓</kbd> {t('search.navigate')}</span>
          <span><kbd>↵</kbd> {t('search.open')}</span>
        </div>
      </div>
    </div>
  );
}
