const AVATAR_COLORS = ['#a63a2e', '#2f6f4f', '#2e5a88', '#7a4fa3', '#b8842e', '#3d7a7a', '#8a4a68', '#5a6b2e'];

function initialsFor(name: string): string {
  const parts = name.trim().split(/\s+/).filter(Boolean);
  if (parts.length === 0) return '?';
  if (parts.length === 1) return parts[0]!.slice(0, 2).toUpperCase();
  return (parts[0]![0]! + parts[parts.length - 1]![0]!).toUpperCase();
}

function colorFor(name: string): string {
  let hash = 0;
  for (let i = 0; i < name.length; i++) hash = (hash * 31 + name.charCodeAt(i)) >>> 0;
  return AVATAR_COLORS[hash % AVATAR_COLORS.length]!;
}

type AvatarProps = {
  name: string;
  size?: number;
  photoUrl?: string | null;
};

export function Avatar({ name, size = 32, photoUrl }: AvatarProps) {
  const style = { width: size, height: size, fontSize: Math.max(11, Math.round(size * 0.4)) };

  if (photoUrl) {
    return <img className="avatar" style={style} src={photoUrl} alt={name} />;
  }

  return (
    <span className="avatar" style={{ ...style, background: colorFor(name || '?') }} aria-hidden="true">
      {initialsFor(name)}
    </span>
  );
}
