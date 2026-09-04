import { Boxes, KeyRound, MapPin, PackageCheck, Plus, QrCode, Search, UserPlus, Users } from 'lucide-react';
import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { api, type SearchHit } from '../api/endpoints';
import { useAuth } from '../auth/AuthProvider';
import { useDebouncedValue } from '../hooks/useDebouncedValue';
import { useI18n } from '../i18n/I18nProvider';
import { canSee, nav } from './Layout';
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

interface Command {
  id: string;
  labelKey: string;
  to: string;
  icon: typeof Plus;
  /** Ścieżka nawigacji, której uprawnienia decydują o widoczności komendy. */
  guardPath: string;
  /** Dodatkowe role, gdy sama strona docelowa jest dostępna szerzej niż ta komenda. */
  roles?: string[];
}

// Skróty do najczęstszych czynności. Każda prowadzi do istniejącego ekranu z parametrem ?new=1, który
// otwiera tam formularz - paleta nie ma własnych formularzy ani własnej logiki uprawnień.
const COMMANDS: Command[] = [
  { id: 'new-asset', labelKey: 'search.command.newAsset', to: '/assets?new=1', icon: Plus, guardPath: '/assets' },
  { id: 'new-assignment', labelKey: 'search.command.newAssignment', to: '/assignments?new=1', icon: PackageCheck, guardPath: '/assignments' },
  { id: 'new-person', labelKey: 'search.command.newPerson', to: '/people?new=1', icon: UserPlus, guardPath: '/people' },
  { id: 'qr-label', labelKey: 'search.command.qrLabel', to: '/settings?tab=qrLabel', icon: QrCode, guardPath: '/settings', roles: ['owner', 'admin'] },
];

/**
 * Ctrl+K quick search across assets, people, locations and licenses.
 *
 * Results come from /api/search, which reuses each module's own service - so the palette shows exactly
 * what the signed-in user is allowed to see elsewhere, with no separate permission logic here.
 */
export function SearchPalette() {
  const navigate = useNavigate();
  const { t } = useI18n();
  const auth = useAuth();
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

  // Filtrowanie zmienia długość listy - bez tego zaznaczenie mogłoby wskazywać poza nią.
  useEffect(() => { setActive(0); }, [query]);

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

  // Komendy i skoki do modułów. Widoczne są tylko te, do których użytkownik ma dostęp - filtr jest ten
  // sam, którym Layout buduje menu, więc paleta nigdy nie proponuje ekranu kończącego się błędem 403.
  const commands = useMemo(() => {
    const term = query.trim().toLowerCase();
    const entries = [
      ...COMMANDS
        .filter(command => {
          const guard = nav.find(item => item.to === command.guardPath);
          if (guard && !canSee(guard.roles, auth.roles)) return false;
          return !command.roles || canSee(command.roles, auth.roles);
        })
        .map(command => ({ id: command.id, label: t(command.labelKey), to: command.to, icon: command.icon })),
      ...nav
        .filter(item => canSee(item.roles, auth.roles))
        .map(item => ({ id: `nav-${item.to}`, label: t(item.labelKey), to: item.to, icon: item.icon })),
    ];
    return term ? entries.filter(entry => entry.label.toLowerCase().includes(term)) : entries;
  }, [query, auth.roles, t]);

  // Jedna lista dla klawiatury: komendy na górze, wyniki z serwera pod nimi.
  const entries = useMemo(
    () => [
      ...commands.map(command => ({ kind: 'command' as const, key: command.id, command })),
      ...hits.map(hit => ({ kind: 'hit' as const, key: `${hit.kind}-${hit.id}`, hit })),
    ],
    [commands, hits]
  );

  const go = useCallback((target: string) => {
    close();
    // Dla wyników serwer zbudował już trasę wskazującą rekord (panel aktywa albo lista z filtrem),
    // więc jest używana bez zmian.
    navigate(target);
  }, [close, navigate]);

  function onKeyDown(event: React.KeyboardEvent<HTMLDivElement>) {
    if (event.key === 'Escape') { close(); return; }
    if (entries.length === 0) return;

    if (event.key === 'ArrowDown') {
      event.preventDefault();
      setActive(index => (index + 1) % entries.length);
    } else if (event.key === 'ArrowUp') {
      event.preventDefault();
      setActive(index => (index - 1 + entries.length) % entries.length);
    } else if (event.key === 'Enter') {
      event.preventDefault();
      const entry = entries[active];
      if (entry) go(entry.kind === 'command' ? entry.command.to : entry.hit.url);
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
          {entries.map((entry, index) => {
            const active_ = index === active;
            if (entry.kind === 'command') {
              const Icon = entry.command.icon;
              return (
                <button
                  key={entry.key}
                  type="button"
                  role="option"
                  aria-selected={active_}
                  className={`palette__hit${active_ ? ' palette__hit--active' : ''}`}
                  onMouseEnter={() => setActive(index)}
                  onClick={() => go(entry.command.to)}
                >
                  <span className="palette__icon"><Icon size={16} /></span>
                  <span className="palette__text"><strong>{entry.command.label}</strong></span>
                </button>
              );
            }

            const Icon = ICONS[entry.hit.kind] ?? Boxes;
            return (
              <button
                key={entry.key}
                type="button"
                role="option"
                aria-selected={active_}
                className={`palette__hit${active_ ? ' palette__hit--active' : ''}`}
                onMouseEnter={() => setActive(index)}
                onClick={() => go(entry.hit.url)}
              >
                <span className="palette__icon"><Icon size={16} /></span>
                <span className="palette__text">
                  <strong>{entry.hit.title}</strong>
                  {entry.hit.subtitle ? <span>{entry.hit.subtitle}</span> : null}
                </span>
                {entry.hit.badge ? <span className="palette__badge">{entry.hit.badge}</span> : null}
              </button>
            );
          })}

          {/* Podpowiedzi pokazują się pod komendami: paleta nigdy nie jest pusta, ale trzeba powiedzieć,
              co się dzieje z częścią wyszukiwarki. */}
          {term.length > 0 && term.length < 2 ? (
            <p className="palette__hint">{t('search.hintShort')}</p>
          ) : loading ? (
            <p className="palette__hint">{t('common.loading')}</p>
          ) : term.length >= 2 && hits.length === 0 ? (
            <p className="palette__hint">{t('search.empty')}</p>
          ) : null}
        </div>

        <div className="palette__footer">
          <span><kbd>↑</kbd><kbd>↓</kbd> {t('search.navigate')}</span>
          <span><kbd>↵</kbd> {t('search.open')}</span>
        </div>
      </div>
    </div>
  );
}
