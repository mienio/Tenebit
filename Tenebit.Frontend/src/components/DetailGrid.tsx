import type { ReactNode } from 'react';

type DetailGridItem = {
  label: string;
  value?: ReactNode;
};

type DetailGridProps = {
  items?: DetailGridItem[];
  children?: ReactNode;
};

export function DetailGrid({ items, children }: DetailGridProps) {
  if (items?.length) {
    return (
      <dl className="detailGrid">
        {items.map(item => <DetailItem key={item.label} label={item.label} value={item.value} />)}
      </dl>
    );
  }

  return <dl className="detailGrid">{children}</dl>;
}

export function DetailItem({ label, value }: DetailGridItem) {
  return (
    <div>
      <dt>{label}</dt>
      <dd>{value ?? '-'}</dd>
    </div>
  );
}
