import { Link } from 'react-router-dom';
import { BarChart } from '../components/charts/BarChart';
import { DonutChart } from '../components/charts/DonutChart';
import type { DashboardSummary } from '../types/domain';
import { activityLabel } from '../utils/labels';
import { formatDate, formatMoney } from '../utils/format';
import { statusColor } from '../utils/statusColors';
import { WIDGET_CATALOG_MAP, WIDGET_ICONS, type WidgetType } from './widgetCatalog';
import { MaintenanceWidget } from './MaintenanceWidget';

type Translate = (key: string, params?: Record<string, string | number>) => string;

const metricTypes = new Set<WidgetType>(['metric-assets', 'metric-inStock', 'metric-assigned', 'metric-people', 'metric-openAssignments', 'metric-value', 'metric-licenses']);

const metricLinks: Partial<Record<WidgetType, string>> = {
  'metric-assets': '/assets',
  'metric-inStock': '/assets?status=InStock',
  'metric-assigned': '/assets?status=Assigned',
  'metric-people': '/people',
  'metric-openAssignments': '/assignments',
  'metric-value': '/reports',
  'metric-licenses': '/licenses'
};

function metricValue(type: WidgetType, data: DashboardSummary): string | number {
  switch (type) {
    case 'metric-assets': return data.totalAssets;
    case 'metric-inStock': return data.assetsInStock;
    case 'metric-assigned': return data.assetsAssigned;
    case 'metric-people': return data.peopleCount;
    case 'metric-openAssignments': return data.openAssignments;
    case 'metric-value': return formatMoney(data.visibleAssetValue);
    case 'metric-licenses': return `${data.licenseSeatsUsed}/${data.licenseSeatsTotal}`;
    default: return '';
  }
}

function metricHint(type: WidgetType, data: DashboardSummary, t: Translate): string {
  switch (type) {
    case 'metric-assets': return t('metric.assetsHint', { count: data.assetsWithoutOwner });
    case 'metric-inStock': return t('metric.inStockHint');
    case 'metric-assigned': return t('metric.assignedHint');
    case 'metric-people': return t('metric.peopleHint');
    case 'metric-openAssignments': return t('metric.openAssignmentsHint');
    case 'metric-value': return t('metric.visibleValueHint');
    case 'metric-licenses': return t('metric.licensesHint', { count: data.totalLicenses });
    default: return '';
  }
}

export function DashboardWidgetContent({ type, data, editing, t }: { type: WidgetType; data: DashboardSummary; editing: boolean; t: Translate }) {
  if (metricTypes.has(type)) {
    const label = t(WIDGET_CATALOG_MAP[type].titleKey);
    const body = (
      <>
        <div className="metricCard__top"><span>{label}</span><span className="metricCard__icon">{WIDGET_ICONS[type]}</span></div>
        <div className="metricCard__body"><strong>{metricValue(type, data)}</strong><small>{metricHint(type, data, t)}</small></div>
      </>
    );
    const to = metricLinks[type];
    if (!editing && to) return <Link to={to} style={{ display: 'contents' }}>{body}</Link>;
    return <>{body}</>;
  }

  if (type === 'chart-byStatus') {
    return (
      <>
        <div className="sectionTitle"><div><h2>{t('dashboard.byStatus')}</h2></div></div>
        <DonutChart segments={data.assetsByStatus.map(item => ({ label: t(`status.${item.status}`), value: item.count, color: statusColor(item.status) }))} />
      </>
    );
  }

  if (type === 'chart-byCategory') {
    const sorted = data.assetsByCategory;
    const bars = sorted.length <= 8
      ? sorted.map(item => ({ label: item.categoryName, value: item.count }))
      : [...sorted.slice(0, 8).map(item => ({ label: item.categoryName, value: item.count })), { label: t('dashboard.otherCategories'), value: sorted.slice(8).reduce((sum, item) => sum + item.count, 0) }];
    return (
      <>
        <div className="sectionTitle"><div><h2>{t('dashboard.byCategory')}</h2></div></div>
        {bars.length > 0 ? <BarChart items={bars} /> : <p className="muted">{t('dashboard.noActivity')}</p>}
      </>
    );
  }

  if (type === 'list-maintenance') {
    // Fetches its own data: maintenance is not part of the dashboard summary payload, and giving the
    // widget its own source keeps that endpoint unchanged for every other widget.
    return <MaintenanceWidget editing={editing} />;
  }

  if (type === 'list-warranty') {
    return (
      <>
        <div className="sectionTitle"><div><h2>{t('dashboard.warrantyDeadlines')}</h2></div></div>
        <div className="listRows">
          {data.warrantyExpiringSoon.length === 0 ? <p className="muted">{t('dashboard.noWarranty')}</p> : data.warrantyExpiringSoon.map(item => (
            editing ? (
              <div className="listRow" key={item.assetId}><div><strong>{item.name}</strong><small>{item.assetTag}</small></div><span>{formatDate(item.warrantyUntil)}</span></div>
            ) : (
              <Link className="listRow" to={`/assets?search=${encodeURIComponent(item.assetTag)}`} key={item.assetId}>
                <div><strong>{item.name}</strong><small>{item.assetTag}</small></div>
                <span>{formatDate(item.warrantyUntil)}</span>
              </Link>
            )
          ))}
        </div>
      </>
    );
  }

  if (type === 'list-activity') {
    return (
      <>
        <div className="sectionTitle"><div><h2>{t('dashboard.recentActivity')}</h2></div></div>
        <div className="listRows">
          {data.recentActivity.length === 0 ? <p className="muted">{t('dashboard.noActivity')}</p> : data.recentActivity.map((item, index) => (
            <div className="listRow" key={`${item.action}-${index}`}>
              <div><strong>{activityLabel(t, item.action)}</strong><small>{item.displayName ?? item.entityType}</small></div>
              <span>{formatDate(item.createdAt)}</span>
            </div>
          ))}
        </div>
      </>
    );
  }

  return null;
}
