import { ChevronLeft, ChevronRight } from 'lucide-react';
import { Button } from './Button';
import { useI18n } from '../i18n/I18nProvider';

export const DEFAULT_PAGE_SIZE = 12;

export function paginate<T>(items: T[], page: number, pageSize = DEFAULT_PAGE_SIZE) {
  const totalPages = Math.max(1, Math.ceil(items.length / pageSize));
  const safePage = Math.min(Math.max(page, 1), totalPages);
  const start = (safePage - 1) * pageSize;
  return {
    page: safePage,
    totalPages,
    total: items.length,
    items: items.slice(start, start + pageSize)
  };
}

export function Pagination({ page, total, pageSize = DEFAULT_PAGE_SIZE, onPageChange }: { page: number; total: number; pageSize?: number; onPageChange: (page: number) => void }) {
  const { t } = useI18n();
  const totalPages = Math.max(1, Math.ceil(total / pageSize));
  if (total <= pageSize) return null;

  const from = (page - 1) * pageSize + 1;
  const to = Math.min(total, page * pageSize);

  return (
    <nav className="pagination" aria-label="Paginacja listy">
      <span>{from}-{to} {t('common.of')} {total}</span>
      <div className="rowActions">
        <Button type="button" variant="secondary" disabled={page <= 1} onClick={() => onPageChange(page - 1)} icon={<ChevronLeft size={16} />}>{t('common.back')}</Button>
        <Button type="button" variant="secondary" disabled={page >= totalPages} onClick={() => onPageChange(page + 1)} icon={<ChevronRight size={16} />}>{t('common.next')}</Button>
      </div>
    </nav>
  );
}
