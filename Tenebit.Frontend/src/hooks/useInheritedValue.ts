import { useCallback, useRef, useState } from 'react';

/**
 * Pole, które podąża za innym polem, dopóki użytkownik go ręcznie nie zmieni: MPK za zespołem, termin
 * zwrotu za datą końca zatrudnienia, nabywca na fakturze za nazwą firmy. Jedna ręczna edycja wyłącza
 * auto-uzupełnianie do końca tej sesji formularza - kolejne zmiany pola źródłowego już jej nie nadpiszą.
 *
 * `inherit(value)` wołamy przy zmianie pola źródłowego, `edit(value)` przy ręcznej edycji tego pola.
 * `reset(initial, manual)` ustawia stan przy otwarciu formularza - `manual = true` dla wartości, które
 * już wcześniej świadomie odłączono (np. edycja rekordu, gdzie pola się różnią).
 */
export function useInheritedValue(initial = '') {
  const [value, setValue] = useState(initial);
  const manualRef = useRef(false);

  const inherit = useCallback((next: string | null | undefined) => {
    if (manualRef.current || next === null || next === undefined || next === '') return;
    setValue(next);
  }, []);

  const edit = useCallback((next: string) => {
    manualRef.current = true;
    setValue(next);
  }, []);

  const reset = useCallback((next: string, manual = false) => {
    manualRef.current = manual;
    setValue(next);
  }, []);

  return { value, inherit, edit, reset, isManual: () => manualRef.current };
}
