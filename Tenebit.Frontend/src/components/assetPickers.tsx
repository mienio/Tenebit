import { useEffect, useMemo } from 'react';
import { api } from '../api/endpoints';
import { useAsyncData } from '../hooks/useAsyncData';
import { useInheritedValue } from '../hooks/useInheritedValue';
import { useI18n } from '../i18n/I18nProvider';
import type { AssetCategory } from '../types/domain';
import { currencyOptions } from '../utils/referenceData';
import { OptionPicker, type PickerOption } from './OptionPicker';

/// Kategorie aktywów jako opcje pogrupowane wg typu ("Sprzęt fizyczny", "Licencja"...) - ten sam podział,
/// co w Ustawieniach → Pola niestandardowe.
export function useCategoryOptions(categories: AssetCategory[] | null | undefined): PickerOption[] {
  const { t, language } = useI18n();
  return useMemo(() => (categories ?? [])
    .map(category => ({ value: category.id, label: category.name, group: t(`categoryType.${category.type}`) }))
    .sort((a, b) => a.group.localeCompare(b.group, language) || a.label.localeCompare(b.label, language)),
  [categories, t, language]);
}

/// Waluta organizacji z Ustawień → Firma - domyślna wartość każdego pola waluty (aktywo, serwis).
export function useOrganizationCurrency(): string | null {
  const organization = useAsyncData(api.organization, []);
  return organization.data?.currency ?? null;
}

/**
 * Wybór waluty z listy ISO 4217 zamiast wolnego tekstu (US-11). Bez `defaultValue` pole przyjmuje walutę
 * organizacji, gdy tylko ta się wczyta - chyba że ktoś zdążył już wybrać inną.
 */
export function CurrencyPicker({ name = 'currency', defaultValue }: { name?: string; defaultValue?: string | null }) {
  const { language } = useI18n();
  const organizationCurrency = useOrganizationCurrency();
  const currency = useInheritedValue(defaultValue ?? '');
  const options = useMemo(
    () => currencyOptions(language, [organizationCurrency, defaultValue, 'EUR', 'USD']),
    [language, organizationCurrency, defaultValue]
  );

  useEffect(() => {
    if (!defaultValue) currency.inherit(organizationCurrency ?? 'PLN');
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [organizationCurrency, defaultValue]);

  return <OptionPicker name={name} options={options} value={currency.value} onChange={currency.edit} required />;
}
