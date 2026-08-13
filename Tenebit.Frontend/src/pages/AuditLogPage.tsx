import { useMemo, useState } from 'react';
import { Search, X } from 'lucide-react';
import { Link } from 'react-router-dom';
import { Button } from '../components/Button';
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
  const { t, tPlural } = useI18n();
  const [search, setSearch] = useState('');
  const [entityType, setEntityType] = useState('');
  const [dateFrom, setDateFrom] = useState('');
  const [dateTo, setDateTo] = useState('');
  const [actor, setActor] = useState('');
  const [action, setAction] = useState('');
  const [page, setPage] = useState(1);
  const debouncedSearch = useDebouncedValue(search.trim(), 320);
  const debouncedActor = useDebouncedValue(actor.trim(), 320);
  const debouncedAction = useDebouncedValue(action.trim(), 320);

  const loader = useMemo(
    () => () => api.activityLog({ page, pageSize, entityType: entityType || undefined, search: debouncedSearch || undefined, dateFrom: dateFrom || undefined, dateTo: dateTo || undefined, actor: debouncedActor || undefined, action: debouncedAction || undefined }),
    [page, entityType, debouncedSearch, dateFrom, dateTo, debouncedActor, debouncedAction]
  );
  const log = useAsyncData(loader, [loader]);

  if (log.isLoading && !log.data) return <LoadingState title={t('audit.loadingTitle')} description={t('audit.loadingDesc')} />;
  if (log.error) return <ErrorState message={log.error} onRetry={log.reload} />;

  const items = log.data?.items ?? [];

  return (
    <div className="pageStack">
      <PageHeader eyebrow={t('page.audit.eyebrow')} title={t('page.audit.title')} />

      <Card className="toolbarCard">
        <div className="filters filters--six">
          <Field label={t('common.search')}>
            <TextInput value={search} onChange={event => { setSearch(event.target.value); setPage(1); }} placeholder={t('audit.searchPlaceholder')} />
          </Field>
          <Field label={t('audit.entityTypeLabel')}>
            <SelectInput value={entityType} onChange={event => { setEntityType(event.target.value); setPage(1); }}>
              <option value="">{t('audit.allEntityTypes')}</option>
              {entityTypes.map(value => <option key={value} value={value}>{t(`audit.entityType.${value}`)}</option>)}
            </SelectInput>
          </Field>
          <Field label={t('audit.dateFromLabel')}>
            <TextInput type="date" value={dateFrom} max={dateTo || undefined} onChange={event => { setDateFrom(event.target.value); setPage(1); }} />
          </Field>
          <Field label={t('audit.dateToLabel')}>
            <TextInput type="date" value={dateTo} min={dateFrom || undefined} onChange={event => { setDateTo(event.target.value); setPage(1); }} />
          </Field>
          <Field label={t('audit.actorLabel')}>
            <TextInput value={actor} onChange={event => { setActor(event.target.value); setPage(1); }} placeholder={t('audit.actorPlaceholder')} />
          </Field>
          <Field label={t('audit.actionLabel')}>
            <TextInput value={action} onChange={event => { setAction(event.target.value); setPage(1); }} placeholder={t('audit.actionPlaceholder')} />
          </Field>
          <span className="toolbarHint"><Search size={16} /> {log.data?.total ?? 0} {tPlural('count.results', log.data?.total ?? 0)}</span>
        </div>
      </Card>

      {!items.length ? (
        <EmptyState
          title={t('audit.emptyTitle')}
          description={t('audit.emptyDesc')}
          action={(search || entityType || dateFrom || dateTo || actor || action) ? (
            <Button variant="secondary" icon={<X size={16} />} onClick={() => { setSearch(''); setEntityType(''); setDateFrom(''); setDateTo(''); setActor(''); setAction(''); setPage(1); }}>{t('common.clearFilters')}</Button>
          ) : undefined}
        />
      ) : (
        <Card>
          <div className="tableWrap tableWrap--cards">
            <table>
              <thead><tr><th>{t('audit.colDate')}</th><th>{t('audit.colActor')}</th><th>{t('audit.colAction')}</th><th>{t('audit.colEntity')}</th><th>{t('audit.colDetails')}</th></tr></thead>
              <tbody>
                {items.map(entry => (
                  <tr key={entry.id}>
                    <td data-label={t('audit.colDate')}><small>{formatDateTime(entry.createdAt)}</small></td>
                    <td data-label={t('audit.colActor')}>{entry.actorDisplay}</td>
                    <td data-label={t('audit.colAction')}>{activityActionLabel(t, entry.action)}</td>
                    <td data-label={t('audit.colEntity')}>
                      {auditEntityRoutes[entry.entityType]
                        ? <Link className="status" to={auditEntityRoutes[entry.entityType]}>{auditEntityLabel(t, entry.entityType)}</Link>
                        : <span className="status">{auditEntityLabel(t, entry.entityType)}</span>}
                    </td>
                    <td data-label={t('audit.colDetails')}>{entry.details ?? '—'}</td>
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
