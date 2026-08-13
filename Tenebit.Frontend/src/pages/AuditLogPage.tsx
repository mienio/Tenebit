import { useMemo, useState } from 'react';
import { Search } from 'lucide-react';
import { Link } from 'react-router-dom';
import { api } from '../api/endpoints';
import { Card } from '../components/Card';
import { Field, SelectInput, TextInput } from '../components/FormFields';
import { PageHeader } from '../components/PageHeader';
import { Pagination } from '../components/Pagination';
import { EmptyState, ErrorState, LoadingState } from '../components/StateViews';
import { useAsyncData } from '../hooks/useAsyncData';
import { useDebouncedValue } from '../hooks/useDebouncedValue';
import { formatDateTime } from '../utils/format';
import { activityActionLabel, auditEntityLabel, auditEntityRoutes } from '../utils/labels';
import { useI18n } from '../i18n/I18nProvider';

const pageSize = 25;
const entityTypes = ['asset', 'asset_category', 'person', 'team', 'assignment', 'procedure', 'job_profile', 'organization', 'organization_user', 'settings'];

export function AuditLogPage() {
  const { t } = useI18n();
  const [search, setSearch] = useState('');
  const [entityType, setEntityType] = useState('');
  const [page, setPage] = useState(1);
  const debouncedSearch = useDebouncedValue(search.trim(), 320);

  const loader = useMemo(
    () => () => api.activityLog({ page, pageSize, entityType: entityType || undefined, search: debouncedSearch || undefined }),
    [page, entityType, debouncedSearch]
  );
  const log = useAsyncData(loader, [loader]);

  if (log.isLoading && !log.data) return <LoadingState title={t('audit.loadingTitle')} description={t('audit.loadingDesc')} />;
  if (log.error) return <ErrorState message={log.error} onRetry={log.reload} />;

  const items = log.data?.items ?? [];

  return (
    <div className="pageStack">
      <PageHeader eyebrow={t('page.audit.eyebrow')} title={t('page.audit.title')} />

      <Card className="toolbarCard">
        <div className="filters filters--three">
          <Field label={t('common.search')}>
            <TextInput value={search} onChange={event => { setSearch(event.target.value); setPage(1); }} placeholder={t('audit.searchPlaceholder')} />
          </Field>
          <Field label={t('audit.entityTypeLabel')}>
            <SelectInput value={entityType} onChange={event => { setEntityType(event.target.value); setPage(1); }}>
              <option value="">{t('audit.allEntityTypes')}</option>
              {entityTypes.map(value => <option key={value} value={value}>{t(`audit.entityType.${value}`)}</option>)}
            </SelectInput>
          </Field>
          <span className="toolbarHint"><Search size={16} /> {log.data?.total ?? 0} {t('audit.results')}</span>
        </div>
      </Card>

      {!items.length ? <EmptyState title={t('audit.emptyTitle')} description={t('audit.emptyDesc')} /> : (
        <Card>
          <div className="tableWrap">
            <table>
              <thead><tr><th>{t('audit.colDate')}</th><th>{t('audit.colActor')}</th><th>{t('audit.colAction')}</th><th>{t('audit.colEntity')}</th><th>{t('audit.colDetails')}</th></tr></thead>
              <tbody>
                {items.map(entry => (
                  <tr key={entry.id}>
                    <td><small>{formatDateTime(entry.createdAt)}</small></td>
                    <td>{entry.actorDisplay}</td>
                    <td>{activityActionLabel(t, entry.action)}</td>
                    <td>
                      {auditEntityRoutes[entry.entityType]
                        ? <Link className="status" to={auditEntityRoutes[entry.entityType]}>{auditEntityLabel(t, entry.entityType)}</Link>
                        : <span className="status">{auditEntityLabel(t, entry.entityType)}</span>}
                    </td>
                    <td>{entry.details ?? '—'}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <Pagination page={log.data?.page ?? 1} total={log.data?.total ?? 0} pageSize={pageSize} onPageChange={setPage} />
        </Card>
      )}
    </div>
  );
}
