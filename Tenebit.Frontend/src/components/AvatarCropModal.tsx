import { useEffect, useRef, useState, type PointerEvent as ReactPointerEvent, type WheelEvent as ReactWheelEvent } from 'react';
import { Modal } from './Modal';
import { Button } from './Button';
import { useI18n } from '../i18n/I18nProvider';

const VIEWPORT = 280;
const OUTPUT_SIZE = 512;
const MIN_ZOOM = 1;
const MAX_ZOOM = 3;
const MAX_OUTPUT_BYTES = 1024 * 1024;

interface AvatarCropModalProps {
  open: boolean;
  file: File | null;
  onCancel: () => void;
  onCropped: (blob: Blob) => void;
}

function loadImage(url: string): Promise<HTMLImageElement> {
  return new Promise((resolve, reject) => {
    const img = new Image();
    img.onload = () => resolve(img);
    img.onerror = reject;
    img.src = url;
  });
}

// Photo upload + crop: the browser never sends the original file to the server, only what this
// produces - a square JPEG squeezed under the server's 1 MB cap. The visible circle is just the
// .avatar CSS class's own border-radius; cropping to a plain square is enough to match it.
export function AvatarCropModal({ open, file, onCancel, onCropped }: AvatarCropModalProps) {
  const { t } = useI18n();
  const [imageUrl, setImageUrl] = useState<string | null>(null);
  const [naturalSize, setNaturalSize] = useState<{ width: number; height: number } | null>(null);
  const [zoom, setZoom] = useState(MIN_ZOOM);
  const [offset, setOffset] = useState({ x: 0, y: 0 });
  const [saving, setSaving] = useState(false);
  const dragRef = useRef<{ pointerId: number; startX: number; startY: number; originX: number; originY: number } | null>(null);

  useEffect(() => {
    if (!file) {
      setImageUrl(null);
      setNaturalSize(null);
      return;
    }
    const url = URL.createObjectURL(file);
    setImageUrl(url);
    setZoom(MIN_ZOOM);
    setOffset({ x: 0, y: 0 });
    loadImage(url).then(img => setNaturalSize({ width: img.naturalWidth, height: img.naturalHeight })).catch(() => setNaturalSize(null));
    return () => URL.revokeObjectURL(url);
  }, [file]);

  function baseScale() {
    if (!naturalSize) return 1;
    return VIEWPORT / Math.min(naturalSize.width, naturalSize.height);
  }

  // Keeps the image covering the whole viewport at all times, so a drag or zoom can never expose an
  // empty corner inside the crop circle.
  function clampOffset(next: { x: number; y: number }, currentZoom: number) {
    if (!naturalSize) return next;
    const scale = baseScale() * currentZoom;
    const maxX = Math.max(0, (naturalSize.width * scale - VIEWPORT) / 2);
    const maxY = Math.max(0, (naturalSize.height * scale - VIEWPORT) / 2);
    return { x: Math.min(maxX, Math.max(-maxX, next.x)), y: Math.min(maxY, Math.max(-maxY, next.y)) };
  }

  function handleZoomChange(nextZoom: number) {
    const clampedZoom = Math.min(MAX_ZOOM, Math.max(MIN_ZOOM, nextZoom));
    setZoom(clampedZoom);
    setOffset(current => clampOffset(current, clampedZoom));
  }

  function handlePointerDown(event: ReactPointerEvent<HTMLDivElement>) {
    event.currentTarget.setPointerCapture(event.pointerId);
    dragRef.current = { pointerId: event.pointerId, startX: event.clientX, startY: event.clientY, originX: offset.x, originY: offset.y };
  }

  function handlePointerMove(event: ReactPointerEvent<HTMLDivElement>) {
    const drag = dragRef.current;
    if (!drag || drag.pointerId !== event.pointerId) return;
    setOffset(clampOffset({ x: drag.originX + (event.clientX - drag.startX), y: drag.originY + (event.clientY - drag.startY) }, zoom));
  }

  function handlePointerUp(event: ReactPointerEvent<HTMLDivElement>) {
    if (dragRef.current?.pointerId === event.pointerId) dragRef.current = null;
  }

  function handleWheel(event: ReactWheelEvent<HTMLDivElement>) {
    event.preventDefault();
    handleZoomChange(zoom + (event.deltaY > 0 ? -0.08 : 0.08));
  }

  async function handleSave() {
    if (!imageUrl || !naturalSize) return;
    setSaving(true);
    try {
      const img = await loadImage(imageUrl);
      const scale = baseScale() * zoom;
      const cropSize = VIEWPORT / scale;
      const sx = naturalSize.width / 2 - offset.x / scale - cropSize / 2;
      const sy = naturalSize.height / 2 - offset.y / scale - cropSize / 2;

      const canvas = document.createElement('canvas');
      canvas.width = OUTPUT_SIZE;
      canvas.height = OUTPUT_SIZE;
      const ctx = canvas.getContext('2d');
      if (!ctx) return;
      ctx.drawImage(img, sx, sy, cropSize, cropSize, 0, 0, OUTPUT_SIZE, OUTPUT_SIZE);

      let quality = 0.92;
      let blob = await new Promise<Blob | null>(resolve => canvas.toBlob(resolve, 'image/jpeg', quality));
      while (blob && blob.size > MAX_OUTPUT_BYTES && quality > 0.35) {
        quality -= 0.15;
        blob = await new Promise<Blob | null>(resolve => canvas.toBlob(resolve, 'image/jpeg', quality));
      }
      if (blob) onCropped(blob);
    } finally {
      setSaving(false);
    }
  }

  return (
    <Modal open={open} title={t('profile.cropTitle')} description={t('profile.cropDescription')} onClose={onCancel}>
      <div className="avatarCrop">
        <div
          className="avatarCrop__viewport"
          style={{ width: VIEWPORT, height: VIEWPORT }}
          onPointerDown={handlePointerDown}
          onPointerMove={handlePointerMove}
          onPointerUp={handlePointerUp}
          onPointerCancel={handlePointerUp}
          onWheel={handleWheel}
        >
          {imageUrl && naturalSize ? (
            <img
              src={imageUrl}
              alt=""
              draggable={false}
              className="avatarCrop__image"
              style={{
                width: naturalSize.width * baseScale() * zoom,
                height: naturalSize.height * baseScale() * zoom,
                transform: `translate(calc(-50% + ${offset.x}px), calc(-50% + ${offset.y}px))`
              }}
            />
          ) : null}
          <div className="avatarCrop__mask" aria-hidden="true" />
        </div>

        <input
          className="avatarCrop__zoom"
          type="range"
          min={MIN_ZOOM}
          max={MAX_ZOOM}
          step={0.01}
          value={zoom}
          onChange={event => handleZoomChange(Number(event.target.value))}
          aria-label={t('profile.cropZoomLabel')}
          disabled={!naturalSize}
        />

        <div className="formActions formActions--split">
          <Button type="button" variant="ghost" onClick={onCancel}>{t('common.cancel')}</Button>
          <Button type="button" disabled={!naturalSize || saving} onClick={handleSave}>{saving ? t('common.saving') : t('profile.cropSave')}</Button>
        </div>
      </div>
    </Modal>
  );
}
