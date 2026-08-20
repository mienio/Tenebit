import { AlertCircle, Check, ChevronDown, ChevronUp, GripVertical, Plus, RotateCcw, SlidersHorizontal, X, Zap } from 'lucide-react';
import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import GridLayout, { WidthProvider } from 'react-grid-layout';
import 'react-grid-layout/css/styles.css';
import 'react-resizable/css/styles.css';
import { api } from '../api/endpoints';
import { Button } from '../components/Button';
import { Card } from '../components/Card';
import { ConfirmDialog } from '../components/ConfirmDialog';
import { Modal } from '../components/Modal';
import { PageHeader } from '../components/PageHeader';
import { ErrorState, LoadingState } from '../components/StateViews';
import { useAsyncData } from '../hooks/useAsyncData';
import { useI18n } from '../i18n/I18nProvider';
import { DashboardWidgetContent } from '../dashboard/DashboardWidgetContent';
import { useDashboardLayout } from '../dashboard/useDashboardLayout';
import { GRID_COLS, WIDGET_ICONS, type WidgetType } from '../dashboard/widgetCatalog';

const AutoWidthGrid = WidthProvider(GridLayout);

const onboardingStepRoutes: Record<string, string> = {
  team: '/people',
  person: '/people',
  category: '/settings?tab=customFields',
  asset: '/assets',
  procedure: '/procedures',
  assignment: '/assignments'
};

const onboardingDismissKey = 'tenebit_onboarding_dismissed';

function useIsDesktop() {
  const [isDesktop, setIsDesktop] = useState(() => window.innerWidth >= 900);
  useEffect(() => {
    const onResize = () => setIsDesktop(window.innerWidth >= 900);
    window.addEventListener('resize', onResize);
    return () => window.removeEventListener('resize', onResize);
  }, []);
  return isDesktop;
}

export function DashboardPage() {
  const { t } = useI18n();
  const { data, error, isLoading, reload } = useAsyncData(api.dashboard, []);
  const subscription = useAsyncData(api.subscription, []);
  const onboarding = useAsyncData(api.onboardingStatus, []);
  const isDesktop = useIsDesktop();
  const layout = useDashboardLayout();
  const [stepsExpanded, setStepsExpanded] = useState(false);
  const [pickerOpen, setPickerOpen] = useState(false);
  const [resetConfirmOpen, setResetConfirmOpen] = useState(false);
  const [onboardingDismissed, setOnboardingDismissed] = useState(() => window.localStorage.getItem(onboardingDismissKey) === '1');

  function dismissOnboarding() {
    window.localStorage.setItem(onboardingDismissKey, '1');
    setOnboardingDismissed(true);
  }

  if (isLoading || !layout.widgets) return <LoadingState title={t('dashboard.loadingTitle')} description={t('dashboard.loadingDesc')} />;
  if (error || !data) return <ErrorState message={error ?? t('dashboard.errorFallback')} onRetry={reload} />;

  const actionCount = data.openAssignments + data.pendingProcedureAcceptances + data.warrantyExpiringSoon.length;
  const subData = subscription.data;
  const usagePercent = subData ? (subData.currentAssetCount / subData.assetLimit) * 100 : 0;
  const isNearLimit = usagePercent >= 90;
  const orderedForMobile = [...layout.widgets].sort((a, b) => (a.y - b.y) || (a.x - b.x));

  return (
    <div className="pageStack">
      <PageHeader
        eyebrow={t('page.dashboard.eyebrow')}
        title={t('page.dashboard.title')}
        actions={isDesktop ? (
          layout.editing ? (
            <>
              <Button variant="secondary" icon={<Plus size={16} />} onClick={() => setPickerOpen(true)}>{t('dashboard.addWidget')}</Button>
              <Button variant="ghost" icon={<RotateCcw size={16} />} onClick={() => setResetConfirmOpen(true)}>{t('dashboard.resetLayout')}</Button>
              <Button variant="ghost" onClick={layout.cancelEdit}>{t('common.cancel')}</Button>
              <Button disabled={layout.saving} icon={<Check size={16} />} onClick={layout.finishEdit}>{layout.saving ? t('common.saving') : t('dashboard.doneEditing')}</Button>
            </>
          ) : (
            <button type="button" className="iconButton" aria-label={t('dashboard.editLayout')} title={t('dashboard.editLayout')} onClick={layout.startEdit}>
              <SlidersHorizontal size={18} />
            </button>
          )
        ) : undefined}
      />

      {layout.saveError ? <p className="formMessage formMessage--error" aria-live="polite">{t('dashboard.layoutSaveFailed')}</p> : null}

      {actionCount > 0 && (
        <Card className="heroCard">
          <div>
            <p className="eyebrow">{t('dashboard.todayEyebrow')}</p>
            <h2>{t('dashboard.itemsNeedAttention', { count: actionCount })}</h2>
          </div>
          <div className="heroCard__actions">
            <Link className="inlineAction" to="/assignments">{t('dashboard.goToAssignments')}</Link>
            <Link className="inlineAction" to="/assets?status=InStock">{t('dashboard.stockLink')}</Link>
          </div>
        </Card>
      )}

      {(data.offboardingRequiringAttentionCount ?? 0) > 0 && (
        <Card>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: '16px', flexWrap: 'wrap' }}>
            <div>
              <strong>{t('dashboard.offboardingAttentionTitle')}</strong>
              <p className="muted">{t('dashboard.offboardingAttentionDesc', { count: data.offboardingRequiringAttentionCount ?? 0 })}</p>
            </div>
            <Link className="inlineAction" to="/offboarding">{t('dashboard.offboardingAttentionLink')}</Link>
          </div>
        </Card>
      )}

      {subscription.error && <Card><ErrorState message={subscription.error} onRetry={subscription.reload} /></Card>}

      {subData && isNearLimit && (
        <Card>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: '16px', flexWrap: 'wrap' }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
              <AlertCircle size={18} style={{ color: '#c08a1f', flexShrink: 0 }} />
              <span>{t('dashboard.nearLimit')}</span>
              <span className="status">{t('dashboard.assetsOf', { current: subData.currentAssetCount, limit: subData.assetLimit })}</span>
            </div>
            {subData.planKey.toLowerCase() !== 'enterprise' && (
              <Link to="/pricing">
                <Button icon={<Zap size={16} />}>{t('dashboard.upgradeToPro')}</Button>
              </Link>
            )}
          </div>
        </Card>
      )}

      {onboarding.error && <Card><ErrorState message={onboarding.error} onRetry={onboarding.reload} /></Card>}

      {onboarding.data && onboarding.data.completionPercent < 100 && !onboardingDismissed && (
        <Card>
          <div style={{ padding: '10px 16px' }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
              <strong style={{ fontSize: '14px', whiteSpace: 'nowrap' }}>{t('dashboard.gettingStarted')}</strong>
              <div style={{ flex: 1, background: 'var(--border)', borderRadius: 'var(--radius)', height: '6px', overflow: 'hidden' }}>
                <div style={{
                  width: `${onboarding.data.completionPercent}%`,
                  height: '100%',
                  background: 'var(--brand)',
                  borderRadius: 'var(--radius)',
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

      {isDesktop ? (
        <AutoWidthGrid
          className="dashboardGrid"
          layout={layout.widgets}
          cols={GRID_COLS}
          rowHeight={48}
          margin={[16, 16]}
          containerPadding={[0, 0]}
          isDraggable={layout.editing}
          isResizable={layout.editing}
          draggableHandle=".widgetHandle"
          onLayoutChange={next => { if (layout.editing) layout.setWidgets(next); }}
        >
          {layout.widgets.map(item => {
            const widgetType = item.i as WidgetType;
            const isMetric = widgetType.startsWith('metric-');
            return (
              <div key={item.i} className={['card', 'widgetCard', isMetric ? 'metricCard' : '', layout.editing ? 'widgetCard--editing' : ''].filter(Boolean).join(' ')}>
                {layout.editing && (
                  <>
                    <span className="widgetHandle" title={t('dashboard.dragWidget')}><GripVertical size={16} /></span>
                    <button type="button" className="widgetRemove" aria-label={t('dashboard.removeWidget')} title={t('dashboard.removeWidget')} onClick={() => layout.removeWidget(widgetType)}>
                      <X size={14} />
                    </button>
                  </>
                )}
                <DashboardWidgetContent type={widgetType} data={data} editing={layout.editing} t={t} />
              </div>
            );
          })}
        </AutoWidthGrid>
      ) : (
        <div className="pageStack">
          {orderedForMobile.map(item => (
            <Card key={item.i}>
              <DashboardWidgetContent type={item.i as WidgetType} data={data} editing={false} t={t} />
            </Card>
          ))}
        </div>
      )}

      <Modal open={pickerOpen} title={t('dashboard.addWidget')} onClose={() => setPickerOpen(false)}>
        {layout.availableToAdd.length === 0 ? (
          <p className="muted">{t('dashboard.allWidgetsAdded')}</p>
        ) : (
          <div className="choiceCards">
            {layout.availableToAdd.map(def => (
              <button type="button" key={def.type} className="choiceCard" onClick={() => layout.addWidget(def.type)}>
                {WIDGET_ICONS[def.type]}
                <span>{t(def.titleKey)}</span>
              </button>
            ))}
          </div>
        )}
        <div className="formActions">
          <Button type="button" variant="ghost" onClick={() => setPickerOpen(false)}>{t('common.close')}</Button>
        </div>
      </Modal>

      <ConfirmDialog
        open={resetConfirmOpen}
        title={t('dashboard.resetConfirmTitle')}
        description={t('dashboard.resetConfirmDesc')}
        confirmLabel={t('dashboard.resetLayout')}
        onConfirm={() => { layout.resetToDefault(); setResetConfirmOpen(false); }}
        onClose={() => setResetConfirmOpen(false)}
      />
    </div>
  );
}
