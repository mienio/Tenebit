import { Boxes, CheckCircle2, ChevronDown, ChevronUp, ClipboardList, PackageOpen, ShieldCheck, Users, X, Zap, AlertCircle } from 'lucide-react';
import { useState } from 'react';
import { Link } from 'react-router-dom';
import { api } from '../api/endpoints';
import { Card, MetricCard } from '../components/Card';
import { PageHeader } from '../components/PageHeader';
import { ErrorState, LoadingState } from '../components/StateViews';
import { useAsyncData } from '../hooks/useAsyncData';
import { formatDate, formatMoney } from '../utils/format';
import { Button } from '../components/Button';
import { useI18n } from '../i18n/I18nProvider';
import { DonutChart } from '../components/charts/DonutChart';
import { BarChart } from '../components/charts/BarChart';
import { statusColor } from '../utils/statusColors';

const onboardingStepRoutes: Record<string, string> = {
  team: '/people',
  person: '/people',
  category: '/settings?tab=customFields',
  asset: '/assets',
  procedure: '/procedures',
  assignment: '/assignments'
};

const onboardingDismissKey = 'tenebit_onboarding_dismissed';

export function DashboardPage() {
  const { t } = useI18n();
  const { data, error, isLoading, reload } = useAsyncData(api.dashboard, []);
  const subscription = useAsyncData(api.subscription, []);
  const onboarding = useAsyncData(api.onboardingStatus, []);
  const [stepsExpanded, setStepsExpanded] = useState(false);
  const [onboardingDismissed, setOnboardingDismissed] = useState(() => window.sessionStorage.getItem(onboardingDismissKey) === '1');

  function dismissOnboarding() {
    window.sessionStorage.setItem(onboardingDismissKey, '1');
    setOnboardingDismissed(true);
  }

  const activityLabels: Record<string, string> = {
    'asset.created': t('activity.asset.created'),
    'asset.updated': t('activity.asset.updated'),
    'asset.deleted': t('activity.asset.deleted'),
    'assignment.created': t('activity.assignment.created'),
    'assignment.accepted': t('activity.assignment.accepted'),
    'assignment.returned': t('activity.assignment.returned'),
    'person.created': t('activity.person.created'),
    'person.updated': t('activity.person.updated'),
    'procedure.created': t('activity.procedure.created'),
    'procedure.published': t('activity.procedure.published'),
    'seed.demo.created': t('activity.seed.demo.created')
  };

  if (isLoading) return <LoadingState title={t('dashboard.loadingTitle')} description={t('dashboard.loadingDesc')} />;
  if (error || !data) return <ErrorState message={error ?? t('dashboard.errorFallback')} onRetry={reload} />;

  const actionCount = data.openAssignments + data.pendingProcedureAcceptances + data.warrantyExpiringSoon.length;

  const categoryBars = (() => {
    const sorted = data.assetsByCategory;
    if (sorted.length <= 8) return sorted.map(item => ({ label: item.categoryName, value: item.count }));
    const top = sorted.slice(0, 8);
    const otherCount = sorted.slice(8).reduce((sum, item) => sum + item.count, 0);
    return [...top.map(item => ({ label: item.categoryName, value: item.count })), { label: t('dashboard.otherCategories'), value: otherCount }];
  })();

  const subData = subscription.data;
  const usagePercent = subData ? (subData.currentAssetCount / subData.assetLimit) * 100 : 0;
  const isNearLimit = usagePercent >= 80;

  return (
    <div className="pageStack">
      <PageHeader eyebrow={t('page.dashboard.eyebrow')} title={t('page.dashboard.title')} />

      {subData && (
        <Card>
          <div style={{
            padding: '20px',
            background: isNearLimit ? 'linear-gradient(135deg, #fff7ed 0%, #fff 100%)' : 'rgba(255,255,255,0.9)',
            border: isNearLimit ? '2px solid #fb923c' : '1px solid var(--border)',
            borderRadius: 'var(--radius)',
            display: 'flex',
            justifyContent: 'space-between',
            alignItems: 'center',
            gap: '24px'
          }}>
            <div style={{ flex: 1 }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: '8px', marginBottom: '8px' }}>
                {isNearLimit && <AlertCircle size={18} style={{ color: '#ea580c' }} />}
                <h3 style={{ margin: 0, fontSize: '16px', fontWeight: 600 }}>
                  {t('dashboard.plan', { name: subData.planName })}
                </h3>
                <span className="status">{t('dashboard.assetsOf', { current: subData.currentAssetCount, limit: subData.assetLimit })}</span>
              </div>
              <div style={{ background: 'var(--border)', borderRadius: '999px', height: '8px', overflow: 'hidden', marginTop: '12px' }}>
                <div style={{
                  width: `${Math.min(usagePercent, 100)}%`,
                  height: '100%',
                  background: isNearLimit ? 'linear-gradient(90deg, #f97316, #ea580c)' : 'linear-gradient(90deg, #111827, #334155)',
                  borderRadius: '999px',
                  transition: 'width 0.3s ease'
                }} />
              </div>
              {isNearLimit && (
                <p style={{ margin: '12px 0 0', color: '#9a3412', fontSize: '14px' }}>
                  {t('dashboard.nearLimit')}
                </p>
              )}
            </div>
            <div style={{ display: 'flex', gap: '12px', alignItems: 'center' }}>
              {subData.planKey === 'free' && (
                <Link to="/pricing">
                  <Button icon={<Zap size={16} />}>{t('dashboard.upgradeToPro')}</Button>
                </Link>
              )}
            </div>
          </div>
        </Card>
      )}

      {onboarding.data && onboarding.data.completionPercent < 100 && !onboardingDismissed && (
        <Card>
          <div style={{ padding: '10px 16px' }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
              <strong style={{ fontSize: '14px', whiteSpace: 'nowrap' }}>{t('dashboard.gettingStarted')}</strong>
              <div style={{ flex: 1, background: 'var(--border)', borderRadius: '999px', height: '6px', overflow: 'hidden' }}>
                <div style={{
                  width: `${onboarding.data.completionPercent}%`,
                  height: '100%',
                  background: 'linear-gradient(90deg, #111827, #334155)',
                  borderRadius: '999px',
                  transition: 'width 0.3s ease'
                }} />
              </div>
              <span className="status" style={{ whiteSpace: 'nowrap' }}>{t('dashboard.gettingStartedPercent', { percent: onboarding.data.completionPercent })}</span>
              <button type="button" className="iconButton" aria-label={stepsExpanded ? t('dashboard.gettingStartedHide') : t('dashboard.gettingStartedShow')} title={stepsExpanded ? t('dashboard.gettingStartedHide') : t('dashboard.gettingStartedShow')} onClick={() => setStepsExpanded(current => !current)}>
                {stepsExpanded ? <ChevronUp size={16} /> : <ChevronDown size={16} />}
              </button>
              <button type="button" className="iconButton" aria-label={t('dashboard.gettingStartedDismiss')} title={t('dashboard.gettingStartedDismiss')} onClick={dismissOnboarding}>
                <X size={16} />
              </button>
            </div>
            {stepsExpanded && (
              <div className="listRows" style={{ marginTop: '12px' }}>
                {onboarding.data.steps.filter(step => !step.completed).map(step => (
                  <Link className="listRow" to={onboardingStepRoutes[step.key] ?? '/dashboard'} key={step.key}>
                    <div><strong>{step.label}</strong><small>{step.nextAction}</small></div>
                  </Link>
                ))}
              </div>
            )}
          </div>
        </Card>
      )}

      <Card className="heroCard">
        <div>
          <p className="eyebrow">{t('dashboard.todayEyebrow')}</p>
          <h2>{actionCount === 0 ? t('dashboard.noUrgent') : t('dashboard.itemsNeedAttention', { count: actionCount })}</h2>
        </div>
        <div className="heroCard__actions">
          <Link className="inlineAction" to="/assignments">{t('dashboard.goToAssignments')}</Link>
          <Link className="inlineAction" to="/assets?status=InStock">{t('dashboard.stockLink')}</Link>
        </div>
      </Card>

      <div className="metricGrid">
        <MetricCard to="/assets" label={t('metric.assets')} value={data.totalAssets} hint={t('metric.assetsHint', { count: data.assetsWithoutOwner })} icon={<Boxes size={18} />} />
        <MetricCard to="/assets?status=InStock" label={t('metric.inStock')} value={data.assetsInStock} hint={t('metric.inStockHint')} icon={<PackageOpen size={18} />} />
        <MetricCard to="/assets?status=Assigned" label={t('metric.assigned')} value={data.assetsAssigned} hint={t('metric.assignedHint')} icon={<CheckCircle2 size={18} />} />
        <MetricCard to="/people" label={t('metric.people')} value={data.peopleCount} hint={t('metric.peopleHint')} icon={<Users size={18} />} />
        <MetricCard to="/assignments" label={t('metric.openAssignments')} value={data.openAssignments} hint={t('metric.openAssignmentsHint')} icon={<ClipboardList size={18} />} />
        <MetricCard to="/reports" label={t('metric.visibleValue')} value={formatMoney(data.visibleAssetValue)} hint={t('metric.visibleValueHint')} icon={<ShieldCheck size={18} />} />
      </div>

      <div className="twoColumns">
        <Card>
          <div className="sectionTitle"><div><h2>{t('dashboard.byStatus')}</h2></div></div>
          <DonutChart segments={data.assetsByStatus.map(item => ({ label: t(`status.${item.status}`), value: item.count, color: statusColor(item.status) }))} />
        </Card>

        <Card>
          <div className="sectionTitle"><div><h2>{t('dashboard.warrantyDeadlines')}</h2></div></div>
          <div className="listRows">
            {data.warrantyExpiringSoon.length === 0 ? <p className="muted">{t('dashboard.noWarranty')}</p> : data.warrantyExpiringSoon.map(item => (
              <Link className="listRow" to={`/assets?search=${encodeURIComponent(item.assetTag)}`} key={item.assetId}>
                <div><strong>{item.name}</strong><small>{item.assetTag}</small></div>
                <span>{formatDate(item.warrantyUntil)}</span>
              </Link>
            ))}
          </div>
        </Card>
      </div>

      {categoryBars.length > 0 && (
        <Card>
          <div className="sectionTitle"><div><h2>{t('dashboard.byCategory')}</h2></div></div>
          <BarChart items={categoryBars} />
        </Card>
      )}

      <Card>
        <div className="sectionTitle"><div><h2>{t('dashboard.recentActivity')}</h2></div></div>
        <div className="listRows">
          {data.recentActivity.length === 0 ? <p className="muted">{t('dashboard.noActivity')}</p> : data.recentActivity.map((item, index) => (
            <div className="listRow" key={`${item.action}-${index}`}>
              <div><strong>{activityLabels[item.action] ?? item.action}</strong><small>{item.displayName ?? item.entityType}</small></div>
              <span>{formatDate(item.createdAt)}</span>
            </div>
          ))}
        </div>
      </Card>
    </div>
  );
}
