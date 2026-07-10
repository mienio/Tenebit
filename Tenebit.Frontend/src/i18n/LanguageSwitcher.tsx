import { Languages } from 'lucide-react';
import { useI18n } from './I18nProvider';
import { languages } from './translations';

export function LanguageSwitcher({ className }: { className?: string }) {
  const { language, setLanguage } = useI18n();
  return (
    <label className={`languageSwitcher ${className ?? ''}`}>
      <Languages size={16} />
      <select value={language} onChange={event => setLanguage(event.target.value as typeof language)} aria-label="Język">
        {languages.map(item => <option key={item.value} value={item.value}>{item.label}</option>)}
      </select>
    </label>
  );
}
