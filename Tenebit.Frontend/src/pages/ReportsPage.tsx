import { BarChart3, ClipboardList, FileSpreadsheet, ShieldAlert } from 'lucide-react';
import { Link, useNavigate } from 'react-router-dom';
import { api } from '../api/endpoints';
import { Card } from '../components/Card';
import { PageHeader } from '../components/PageHeader';
import { ErrorState, LoadingState } from '../components/StateViews';
import { useAsyncData } from '../hooks/useAsyncData';
import { formatDate, formatMoney } from '../utils/format';
import { useI18n } from '../i18n/I18nProvider';
import { DonutChart } from '../components/charts/DonutChart';
import { BarChart } from '../components/charts/BarChart';
import { statusColor } from '../utils/statusColors';

export function ReportsPage() {
  const { t } = useI18n();
  const navigate = useNavigate();
  const dashboard = useAsyncData(api.dashboard, []);

  if (dashboard.isLoading && !dashboard.data) return <LoadingState title={t('reports.loadingTitle')} description={t('reports.loadingDesc')} />;
  if (dashboard.error || !dashboard.data) return <ErrorState message={dashboard.error ?? t('dashboard.errorFallback')} onRetry={dashboard.reload} />;

  const data = dashboard.data;

  return (
    <div className="pageStack">
      <PageHeader eyebrow={t('page.reports.eyebrow')} title={t('page.reports.title')} />

      <div className="reportGrid">
        <Link to="/assets" className="card reportCard reportCard--action">
          <ShieldAlert size={22} />
          <span className="reportMetric">{data.assetsWithoutOwner}</span>
          <h2>{t('reports.assetsWithoutOwner')}</h2>
        </Link>
        <Link to="/assignments" className="card reportCard reportCard--action">
          <ClipboardList size={22} />
          <span className="reportMetric">{data.openAssignments}</span>
          <h2>{t('reports.openAssignments')}</h2>
        </Link>
        <Link to="/assets" className="card reportCard reportCard--action">
          <BarChart3 size={22} />
          <span className="reportMetric">{data.warrantyExpiringSoon.length}</span>
          <h2>{t('reports.warrantyDeadlines')}</h2>
        </Link>
      </div>

      <div className="twoColumns">
        <Card>
          <div className="sectionTitle"><div><h2>{t('reports.byStatus')}</h2></div></div>
          <DonutChart segments={data.assetsByStatus.map(item => ({ label: t(`status.${item.status}`), value: item.count, color: statusColor(item.status) }))} />
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
