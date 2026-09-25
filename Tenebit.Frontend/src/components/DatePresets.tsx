import type { ReactNode } from 'react';

export interface DatePreset {
  label: string;
  /** Wyliczona data (YYYY-MM-DD) albo null, gdy brakuje daty bazowej (np. daty zakupu). */
  compute(): string | null;
}

/**
 * Rząd przycisków wstawiających typową datę jednym kliknięciem ("+7 dni", "+24 mies."). Wartość zawsze
 * da się potem poprawić ręcznie w samym polu - presety tylko je wypełniają.
 */
export function DatePresets({ presets, onPick, onMissingBase, missingBaseHint }: {
  presets: DatePreset[];
  onPick(value: string): void;
  /** Wywoływane zamiast liczenia daty, gdy preset nie ma od czego liczyć - np. przeniesienie fokusu na brakujące pole. */
  onMissingBase?(): void;
  missingBaseHint?: string;
}) {
  return (
    <div className="presetRow">
      {presets.map(preset => {
        const value = preset.compute();
        return (
          <button
            key={preset.label}
            type="button"
            className="presetChip"
            aria-disabled={value === null}
            title={value === null ? missingBaseHint : undefined}
            onClick={() => (value === null ? onMissingBase?.() : onPick(value))}
          >
            {preset.label}
          </button>
        );
      })}
    </div>
  );
}

/** Przełącznik on/off z opisem - ten sam wygląd co przełączniki reguł alertów. */
export function Switch({ checked, onChange, label, hint, name }: { checked: boolean; onChange(checked: boolean): void; label: string; hint?: ReactNode; name?: string }) {
  return (
    <label className="switchField">
      <span className="toggleSwitch">
        <input type="checkbox" role="switch" name={name} checked={checked} onChange={event => onChange(event.target.checked)} />
        <span className="toggleSwitch__track" />
        <span className="toggleSwitch__thumb" />
      </span>
      <span className="switchField__text">
        <span>{label}</span>
        {hint ? <small>{hint}</small> : null}
      </span>
    </label>
  );
}
