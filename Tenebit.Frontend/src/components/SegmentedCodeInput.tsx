import { ClipboardPaste } from 'lucide-react';
import { useRef } from 'react';

const digitsOnly = (value: string) => value.replace(/\D/g, '');

export function SegmentedCodeInput({
  value,
  onChange,
  length = 6,
  label,
  pasteLabel,
  disabled = false,
  autoFocus = false
}: {
  value: string;
  onChange: (value: string) => void;
  length?: number;
  label: string;
  pasteLabel: string;
  disabled?: boolean;
  autoFocus?: boolean;
}) {
  const refs = useRef<Array<HTMLInputElement | null>>([]);
  const normalized = digitsOnly(value).slice(0, length).padEnd(length, ' ');

  function setAt(index: number, input: string) {
    const incoming = digitsOnly(input);
    if (!incoming) return;
    const current = normalized.split('');
    incoming.slice(0, length - index).split('').forEach((digit, offset) => { current[index + offset] = digit; });
    const next = current.join('').replace(/\s/g, '').slice(0, length);
    onChange(next);
    refs.current[Math.min(index + incoming.length, length - 1)]?.focus();
  }

  function clearAt(index: number) {
    const current = normalized.split('');
    current[index] = ' ';
    onChange(current.join('').replace(/\s/g, ''));
  }

  async function pasteFromClipboard() {
    try {
      const text = await navigator.clipboard.readText();
      const code = digitsOnly(text).slice(0, length);
      if (!code) return;
      onChange(code);
      refs.current[Math.min(code.length, length) - 1]?.focus();
    } catch {
      refs.current[0]?.focus();
    }
  }

  return (
    <div className="codeField">
      <div className="codeField__head">
        <span>{label}</span>
        <button type="button" className="codeField__paste" onClick={() => void pasteFromClipboard()} disabled={disabled}>
          <ClipboardPaste size={15} /> {pasteLabel}
        </button>
      </div>
      <div
        className="segmentedCode"
        onPaste={event => {
          const code = digitsOnly(event.clipboardData.getData('text')).slice(0, length);
          if (!code) return;
          event.preventDefault();
          onChange(code);
          refs.current[Math.min(code.length, length) - 1]?.focus();
        }}
      >
        {Array.from({ length }, (_, index) => (
          <input
            key={index}
            ref={element => { refs.current[index] = element; }}
            value={normalized[index].trim()}
            onChange={event => {
              if (!event.target.value) clearAt(index);
              else setAt(index, event.target.value);
            }}
            onKeyDown={event => {
              if (event.key === 'Backspace' && !normalized[index].trim() && index > 0) {
                clearAt(index - 1);
                refs.current[index - 1]?.focus();
              } else if (event.key === 'ArrowLeft' && index > 0) {
                refs.current[index - 1]?.focus();
              } else if (event.key === 'ArrowRight' && index < length - 1) {
                refs.current[index + 1]?.focus();
              }
            }}
            inputMode="numeric"
            autoComplete={index === 0 ? 'one-time-code' : 'off'}
            pattern="[0-9]*"
            maxLength={length}
            aria-label={`${label} ${index + 1}`}
            disabled={disabled}
            autoFocus={autoFocus && index === 0}
          />
        ))}
      </div>
    </div>
  );
}
