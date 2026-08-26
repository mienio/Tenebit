import { Eraser } from 'lucide-react';
import { useCallback, useEffect, useRef, useState } from 'react';
import { useI18n } from '../i18n/I18nProvider';
import './signaturePad.css';

interface SignaturePadProps {
  /** Wywoływane po każdej zmianie: data URL PNG albo null, gdy pole jest puste. */
  onChange: (dataUrl: string | null) => void;
  disabled?: boolean;
}

/**
 * Pole podpisu odręcznego dla protokołu przekazania.
 *
 * Rysunek trafia do backendu jako PNG w data URL - tam jest sanityzowany i zapieczętowany hashem
 * potwierdzenia. To potwierdzenie elektroniczne, nie kwalifikowany podpis elektroniczny.
 */
export function SignaturePad({ onChange, disabled }: SignaturePadProps) {
  const { t } = useI18n();
  const canvasRef = useRef<HTMLCanvasElement | null>(null);
  const drawing = useRef(false);
  const [hasInk, setHasInk] = useState(false);

  // Canvas rysuje w pikselach urządzenia, inaczej na telefonie linia jest rozmyta i gruba.
  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas) return;
    const ratio = window.devicePixelRatio || 1;
    const rect = canvas.getBoundingClientRect();
    canvas.width = rect.width * ratio;
    canvas.height = rect.height * ratio;
    const context = canvas.getContext('2d');
    if (!context) return;
    context.scale(ratio, ratio);
    context.lineWidth = 2;
    context.lineCap = 'round';
    context.lineJoin = 'round';
    context.strokeStyle = '#111827';
  }, []);

  const positionOf = useCallback((event: React.PointerEvent<HTMLCanvasElement>) => {
    const rect = event.currentTarget.getBoundingClientRect();
    return { x: event.clientX - rect.left, y: event.clientY - rect.top };
  }, []);

  function start(event: React.PointerEvent<HTMLCanvasElement>) {
    if (disabled) return;
    const context = canvasRef.current?.getContext('2d');
    if (!context) return;
    event.currentTarget.setPointerCapture(event.pointerId);
    drawing.current = true;
    const { x, y } = positionOf(event);
    context.beginPath();
    context.moveTo(x, y);
  }

  function move(event: React.PointerEvent<HTMLCanvasElement>) {
    if (!drawing.current) return;
    const context = canvasRef.current?.getContext('2d');
    if (!context) return;
    const { x, y } = positionOf(event);
    context.lineTo(x, y);
    context.stroke();
    if (!hasInk) setHasInk(true);
  }

  function end() {
    if (!drawing.current) return;
    drawing.current = false;
    const canvas = canvasRef.current;
    if (!canvas) return;
    onChange(hasInk ? canvas.toDataURL('image/png') : null);
  }

  function clear() {
    const canvas = canvasRef.current;
    const context = canvas?.getContext('2d');
    if (!canvas || !context) return;
    context.clearRect(0, 0, canvas.width, canvas.height);
    setHasInk(false);
    onChange(null);
  }

  return (
    <div className="signaturePad">
      <canvas
        ref={canvasRef}
        className="signaturePad__canvas"
        aria-label={t('signature.ariaLabel')}
        onPointerDown={start}
        onPointerMove={move}
        onPointerUp={end}
        onPointerLeave={end}
        onPointerCancel={end}
      />
      <div className="signaturePad__footer">
        <small className="muted">{hasInk ? t('signature.drawn') : t('signature.hint')}</small>
        <button type="button" className="inlineAction" onClick={clear} disabled={disabled || !hasInk}>
          <Eraser size={13} /> {t('signature.clear')}
        </button>
      </div>
    </div>
  );
}
