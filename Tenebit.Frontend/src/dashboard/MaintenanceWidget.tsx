import { useEffect, useState } from 'react';
import { api } from '../api/endpoints';
import { MaintenanceDueRow, type MaintenanceScheduleItem } from '../components/MaintenanceDue';
import { useI18n } from '../i18n/I18nProvider';

/**
 * Upcoming and overdue maintenance on the dashboard.
 *
 * Loads its own data rather than reading the dashboard summary: maintenance is not part of that
 * payload, and giving this widget its own source avoids changing an endpoint every other widget
 * depends on. While the layout is being edited it renders placeholders instead of firing a request,
 * so dragging widgets around does not hammer the API.
 */
export function MaintenanceWidget({ editing }: { editing: boolean }) {
  const { t } = useI18n();
  const [items, setItems] = useState<MaintenanceScheduleItem[] | null>(null);
  const [error, setError] = useState(false);

  useEffect(() => {
    if (editing) return;
    let cancelled = false;
    api.maintenanceDue(365)
      .then(result => { if (!cancelled) setItems(result); })
      .catch(() => { if (!cancelled) setError(true); });
    return () => { cancelled = true; };
  }, [editing]);

  async function complete(item: MaintenanceScheduleItem) {
    try {
      await api.completeMaintenance(item.id, {});
      const refreshed = await api.maintenanceDue(365);
      setItems(refreshed);
    } catch {
      setError(true);
    }
  }

  return (
    <>
      <div className="sectionTitle"><div><h2>{t('maintenance.dueTitle')}</h2></div></div>
      {editing ? (
        <p className="muted">{t('maintenance.dueTitle')}</p>
      ) : error ? (
        <p className="muted">{t('common.error')}</p>
      ) : !items ? (
        <p className="muted">{t('common.loading')}</p>
      ) : items.length === 0 ? (
        <p className="dueList__empty">{t('maintenance.emptyDue')}</p>
      ) : (
        <div className="dueList">
          {items.slice(0, 6).map(item => (
            <MaintenanceDueRow key={item.id} item={item} onComplete={complete} />
          ))}
        </div>
      )}
    </>
  );
}
