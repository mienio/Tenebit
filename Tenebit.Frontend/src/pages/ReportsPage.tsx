import { BarChart3, ClipboardList, Download, FileSpreadsheet, Minus, ShieldAlert, ShieldCheck, TrendingDown, TrendingUp } from 'lucide-react';
import { useMemo, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { api } from '../api/endpoints';
import { Button } from '../components/Button';
import { Card } from '../components/Card';
import { SelectInput } from '../components/FormFields';
import { PageHeader } from '../components/PageHeader';
import { ErrorState, LoadingState } from '../components/StateViews';
import { useAsyncData } from '../hooks/useAsyncData';
import { csvCell, formatDate, formatDateTime, formatMoney } from '../utils/format';
import { activityActionLabel, auditEntityLabel } from '../utils/labels';
import { useI18n } from '../i18n/I18nProvider';
import { DonutChart } from '../components/charts/DonutChart';
import { BarChart } from '../components/charts/BarChart';
import { statusColor } from '../utils/statusColors';
import type { DashboardSummary } from '../types/domain';

function TrendRow({ label, current, previous, format }: { label: string; current: number; previous: number; format: (value: number) => string }) {
  const delta = current - previous;
  const Icon = delta > 0 ? TrendingUp : delta < 0 ? TrendingDown : Minus;
  const tone = delta === 0 ? 'muted' : delta > 0 ? 'success' : 'error';
  return (
    <div className="listRow">
      <div><strong>{format(current)}</strong><small>{label}</small></div>
      <span className={`formMessage formMessage--${tone}`} style={{ display: 'flex', alignItems: 'center', gap: '4px', padding: 0, border: 'none', background: 'none' }}>
        <Icon size={14} /> {delta > 0 ? '+' : ''}{format(delta)}
      </span>
    </div>
  );
}

function downloadReportCsv(data: DashboardSummary, t: (key: string, params?: Record<string, string | number>) => string, statusLabel: (status: string) => string) {
  const lines: string[] = [];
  lines.push(csvCell(t('reports.exportGeneratedAt', { date: formatDateTime(new Date().toISOString()) })));
  lines.push('');

  lines.push(csvCell(t('reports.exportSummarySection')));
  lines.push([csvCell(t('reports.assetsWithoutOwner')), csvCell(data.assetsWithoutOwner)].join(','));
  lines.push([csvCell(t('reports.openAssignments')), csvCell(data.openAssignments)].join(','));
  lines.push([csvCell(t('reports.warrantyDeadlines')), csvCell(data.warrantyExpiringSoon.length)].join(','));
  lines.push([csvCell(t('reports.visibleValue')), csvCell(formatMoney(data.visibleAssetValue))].join(','));
  lines.push('');

  lines.push(csvCell(t('reports.byStatus')));
  for (const item of data.assetsByStatus) lines.push([csvCell(statusLabel(item.status)), csvCell(item.count)].join(','));
  lines.push('');

  lines.push(csvCell(t('dashboard.byCategory')));
  for (const item of data.assetsByCategory) lines.push([csvCell(item.categoryName), csvCell(item.count)].join(','));
  lines.push('');

  lines.push(csvCell(t('reports.byLocation')));
  for (const item of data.assetsByLocation) lines.push([csvCell(item.location), csvCell(item.count)].join(','));
  lines.push('');

  lines.push(csvCell(t('reports.valueByTeam')));
  for (const item of data.assetsByTeam) lines.push([csvCell(item.teamName), csvCell(item.count), csvCell(formatMoney(item.totalValue))].join(','));
  lines.push('');

  lines.push(csvCell(t('reports.warrantyToCheck')));
  for (const item of data.warrantyExpiringSoon) lines.push([csvCell(item.name), csvCell(item.assetTag), csvCell(formatDate(item.warrantyUntil))].join(','));

  const blob = new Blob(['﻿' + lines.join('\r\n')], { type: 'text/csv' });
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = `raport-${new Date().toISOString().slice(0, 10)}.csv`;
  document.body.appendChild(link);
  link.click();
  link.remove();
  URL.revokeObjectURL(url);
}

export function ReportsPage() {
  const { t } = useI18n();
  const navigate = useNavigate();
  const dashboard = useAsyncData(api.dashboard, []);
  const fleetValue = useAsyncData(api.fleetValue, []);
  const statusSettings = useAsyncData(api.assetStatuses, []);
  const statusSettingByKey = useMemo(() => {
    const map = new Map<string, { label: string; color: string }>();
    for (const item of statusSettings.data ?? []) map.set(item.statusKey, { label: item.label, color: item.color });
    return map;
  }, [statusSettings.data]);
  const [compareDays, setCompareDays] = useState(7);
  const comparisonLoader = useMemo(() => () => api.dashboardComparison(compareDays), [compareDays]);
  const comparison = useAsyncData(comparisonLoader, [comparisonLoader]);

  if (dashboard.isLoading && !dashboard.data) return <LoadingState title={t('reports.loadingTitle')} description={t('reports.loadingDesc')} />;
  if (dashboard.error || !dashboard.data) return <ErrorState message={dashboard.error ?? t('dashboard.errorFallback')} onRetry={dashboard.reload} />;

  const data = dashboard.data;

  return (
    <div className="pageStack">
      <PageHeader
        eyebrow={t('page.reports.eyebrow')}
        title={t('page.reports.title')}
        actions={<Button variant="secondary" onClick={() => downloadReportCsv(data, t, status => statusSettingByKey.get(status)?.label ?? t(`status.${status}`))} icon={<Download size={16} />}>{t('reports.exportCsv')}</Button>}
      />
      <p className="muted" style={{ marginTop: '-12px' }}>{t('reports.asOf', { date: formatDateTime(new Date().toISOString()) })}</p>

      <Card>
        <div className="sectionTitle">
          <div><h2>{t('reports.trendsTitle')}</h2></div>
          <SelectInput value={compareDays} onChange={event => setCompareDays(Number(event.target.value))} style={{ maxWidth: '220px' }}>
            <option value={7}>{t('reports.compareLast7')}</option>
            <option value={30}>{t('reports.compareLast30')}</option>
            <option value={90}>{t('reports.compareLast90')}</option>
          </SelectInput>
        </div>
        {comparison.isLoading ? <p className="muted">{t('common.loading')}</p> : comparison.error || !comparison.data ? (
          <p className="muted">{t('reports.notEnoughHistory')}</p>
        ) : (
          <>
            <p className="muted">{t('reports.comparedToDate', { date: formatDate(comparison.data.comparedToDate) })}</p>
            <div className="listRows">
              <TrendRow label={t('reports.trendTotalAssets')} current={comparison.data.currentTotalAssets} previous={comparison.data.previousTotalAssets} format={value => String(value)} />
              <TrendRow label={t('reports.assetsWithoutOwner')} current={comparison.data.currentAssetsWithoutOwner} previous={comparison.data.previousAssetsWithoutOwner} format={value => String(value)} />
              <TrendRow label={t('reports.openAssignments')} current={comparison.data.currentOpenAssignments} previous={comparison.data.previousOpenAssignments} format={value => String(value)} />
              <TrendRow label={t('reports.visibleValue')} current={comparison.data.currentVisibleAssetValue} previous={comparison.data.previousVisibleAssetValue} format={value => formatMoney(value)} />
            </div>
          </>
        )}
      </Card>

      <Card>
        <div className="sectionTitle">
          <div>
            <h2>{t('reports.fleetValueTitle')}</h2>
            <p>{t('reports.fleetValueHint')}</p>
          </div>
        </div>
        {fleetValue.isLoading ? <p className="muted">{t('common.loading')}</p> : fleetValue.error || !fleetValue.data ? (
          <p className="muted">{t('reports.fleetValueUnavailable')}</p>
        ) : fleetValue.data.assetsWithValue === 0 ? (
          <p className="muted">{t('reports.fleetValueNoPrices')}</p>
        ) : (
          <>
            <div className="listRows">
              <div className="listRow">
                <span>{t('reports.fleetPurchaseValue')}</span>
                <strong>{formatMoney(fleetValue.data.totalPurchaseValue)}</strong>
              </div>
              <div className="listRow">
                <span>{t('reports.fleetCurrentValue')}</span>
                <strong>{formatMoney(fleetValue.data.totalCurrentValue)}</strong>
              </div>
              <div className="listRow">
                <span>{t('reports.fleetDepreciated')}</span>
                <strong>{formatMoney(fleetValue.data.totalDepreciated)}</strong>
              </div>
            </div>

            <div className="tableWrap" style={{ marginTop: '16px' }}>
            <table>
              <thead>
                <tr>
                  <th>{t('reports.fleetCategory')}</th>
                  <th>{t('reports.fleetSchedule')}</th>
                  <th style={{ textAlign: 'right' }}>{t('reports.fleetCount')}</th>
                  <th style={{ textAlign: 'right' }}>{t('reports.fleetPurchaseValue')}</th>
                  <th style={{ textAlign: 'right' }}>{t('reports.fleetCurrentValue')}</th>
                </tr>
              </thead>
              <tbody>
                {fleetValue.data.byCategory.map(slice => (
                  <tr key={slice.categoryId}>
                    <td>{slice.categoryName}</td>
                    <td className="muted">
                      {slice.depreciationMonths
                        ? t('reports.fleetMonths', { months: String(slice.depreciationMonths) })
                        : t('reports.fleetNoSchedule')}
                    </td>
                    <td style={{ textAlign: 'right' }}>{slice.assetCount}</td>
                    <td style={{ textAlign: 'right' }}>{formatMoney(slice.purchaseValue)}</td>
                    <td style={{ textAlign: 'right' }}>{formatMoney(slice.currentValue)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
            </div>

            {fleetValue.data.assetsWithoutPrice > 0 ? (
              <p className="muted" style={{ marginTop: '12px' }}>
                {t('reports.fleetMissingPrices', { count: String(fleetValue.data.assetsWithoutPrice) })}
              </p>
            ) : null}
          </>
        )}
      </Card>

      <div className="reportGrid">
        <Link to="/assets?owner=none" className="card reportCard reportCard--action">
          <ShieldAlert size={22} />
          <span className="reportMetric">{data.assetsWithoutOwner}</span>
          <h2>{t('reports.assetsWithoutOwner')}</h2>
        </Link>
        <Link to="/assignments" className="card reportCard reportCard--action">
          <ClipboardList size={22} />
          <span className="reportMetric">{data.openAssignments}</span>
          <h2>{t('reports.openAssignments')}</h2>
        </Link>
        <Link to="/assets?warranty=expiring" className="card reportCard reportCard--action">
          <BarChart3 size={22} />
          <span className="reportMetric">{data.warrantyExpiringSoon.length}</span>
          <h2>{t('reports.warrantyDeadlines')}</h2>
        </Link>
      </div>

      <div className="twoColumns">
        <Card>
          <div className="sectionTitle"><div><h2>{t('reports.pendingAcceptances')}</h2></div></div>
          <div className="reportValue"><ShieldCheck size={24} /><strong>{data.pendingProcedureAcceptances}</strong><span>{t('reports.pendingAcceptancesDesc')}</span></div>
        </Card>

        <Card>
          <div className="sectionTitle"><div><h2>{t('reports.recentActivity')}</h2></div><Link className="inlineAction" to="/audit">{t('reports.viewAuditLog')}</Link></div>
          <div className="listRows">
            {!data.recentActivity.length ? <p className="muted">{t('reports.noActivity')}</p> : data.recentActivity.slice(0, 6).map((item, index) => (
              <div className="listRow" key={`${item.action}-${index}`}>
                <div><strong>{activityActionLabel(t, item.action)}</strong><small>{item.displayName ?? auditEntityLabel(t, item.entityType)}</small></div>
                <span>{formatDateTime(item.createdAt)}</span>
              </div>
            ))}
          </div>
        </Card>
      </div>

      <div className="twoColumns">
        <Card>
          <div className="sectionTitle"><div><h2>{t('reports.byStatus')}</h2></div></div>
          <DonutChart segments={data.assetsByStatus.map(item => ({ label: statusSettingByKey.get(item.status)?.label ?? t(`status.${item.status}`), value: item.count, color: statusSettingByKey.get(item.status)?.color ?? statusColor(item.status) }))} />
        </Card>

        <Card>
          <div className="sectionTitle"><div><h2>{t('reports.visibleValue')}</h2></div></div>
          <div className="reportValue"><FileSpreadsheet size={24} /><strong>{formatMoney(data.visibleAssetValue)}</strong><span>{t('reports.visibleValueDesc')}</span></div>
        </Card>
      </div>

      {data.assetsByCategory.length > 0 && (
        <Card>
          <div className="sectionTitle"><div><h2>{t('dashboard.byCategory')}</h2></div></div>
          <BarChart items={data.assetsByCategory.map(item => ({ label: item.categoryName, value: item.count }))} />
        </Card>
      )}

      <div className="twoColumns">
        {data.assetsByLocation.length > 0 && (
          <Card>
            <div className="sectionTitle"><div><h2>{t('reports.byLocation')}</h2></div></div>
            <BarChart
              items={data.assetsByLocation.map(item => ({ label: item.location, value: item.count }))}
              onItemClick={item => navigate(`/assets?location=${encodeURIComponent(item.label)}`)}
            />
          </Card>
        )}

        {data.assetsByTeam.length > 0 && (
          <Card>
            <div className="sectionTitle"><div><h2>{t('reports.byTeam')}</h2></div></div>
            <BarChart
              items={data.assetsByTeam.map(item => ({ label: item.teamName, value: item.count }))}
              onItemClick={item => {
                const match = data.assetsByTeam.find(team => team.teamName === item.label);
                if (match?.teamId) navigate(`/assets?team=${match.teamId}`);
              }}
            />
          </Card>
        )}
      </div>

      {data.assetsByTeam.length > 0 && (
        <Card>
          <div className="sectionTitle"><div><h2>{t('reports.valueByTeam')}</h2></div></div>
          <BarChart
            items={data.assetsByTeam.map(item => ({ label: item.teamName, value: item.totalValue }))}
            formatValue={value => formatMoney(value)}
            onItemClick={item => {
              const match = data.assetsByTeam.find(team => team.teamName === item.label);
              if (match?.teamId) navigate(`/assets?team=${match.teamId}`);
            }}
          />
        </Card>
      )}

      <Card>
        <div className="sectionTitle"><div><h2>{t('reports.warrantyToCheck')}</h2></div></div>
        {!data.warrantyExpiringSoon.length ? <p className="muted">{t('reports.noWarranty')}</p> : <div className="tableWrap"><table><thead><tr><th>{t('reports.colAsset')}</th><th>{t('reports.colTag')}</th><th>{t('reports.colDueDate')}</th><th></th></tr></thead><tbody>{data.warrantyExpiringSoon.map(item => <tr key={item.assetId}><td><strong>{item.name}</strong></td><td>{item.assetTag}</td><td>{formatDate(item.warrantyUntil)}</td><td><Link className="inlineAction" to={`/assets?search=${encodeURIComponent(item.assetTag)}`}>{t('reports.open')}</Link></td></tr>)}</tbody></table></div>}
      </Card>
    </div>
  );
}
