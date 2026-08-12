export function BrandMark() {
  return (
    <svg width="100%" height="100%" viewBox="0 0 44 44" aria-hidden="true" style={{ filter: 'drop-shadow(3px 3px 0 var(--brand))', overflow: 'visible' }}>
      <path
        d="M10 8 L26 8 L38 22 L26 36 L10 36 Q6 36 6 32 L6 12 Q6 8 10 8 Z"
        fill="var(--accent-soft)"
        stroke="var(--brand)"
        strokeWidth="2.5"
        strokeLinejoin="round"
      />
      <circle cx="14" cy="22" r="3.2" fill="none" stroke="var(--accent)" strokeWidth="2" />
    </svg>
  );
}
