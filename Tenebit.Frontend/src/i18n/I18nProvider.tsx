import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import { languages, translations, type Language } from './translations';
import { setLanguageProvider } from '../api/apiClient';

const STORAGE_KEY = 'tenebit_language';
const supportedLanguages = languages.map(item => item.value);

type I18nContextValue = {
  language: Language;
  setLanguage: (language: Language) => void;
  t: (key: string, params?: Record<string, string | number>) => string;
};

const I18nContext = createContext<I18nContextValue | null>(null);

function detectInitialLanguage(): Language {
  const stored = window.localStorage.getItem(STORAGE_KEY) as Language | null;
  return stored && supportedLanguages.includes(stored) ? stored : 'pl';
}

export function I18nProvider({ children }: { children: ReactNode }) {
  const [language, setLanguageState] = useState<Language>(detectInitialLanguage);

  useEffect(() => {
    setLanguageProvider(() => language);
  }, [language]);

  const value = useMemo<I18nContextValue>(() => ({
    language,
    setLanguage: next => {
      window.localStorage.setItem(STORAGE_KEY, next);
      setLanguageState(next);
    },
    t: (key, params) => {
      const template = translations[language][key] ?? translations.pl[key] ?? key;
      if (!params) return template;
      return Object.entries(params).reduce((text, [name, value]) => text.split(`{${name}}`).join(String(value)), template);
    }
  }), [language]);

  return <I18nContext.Provider value={value}>{children}</I18nContext.Provider>;
}

export function useI18n() {
  const context = useContext(I18nContext);
  if (!context) throw new Error('useI18n musi być użyty wewnątrz I18nProvider.');
  return context;
}
