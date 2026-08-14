import { useMemo, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { AlertTriangle, CalendarRange, CheckCircle2, ClipboardList, Eye, PackageCheck, RefreshCw, RotateCcw, XCircle } from 'lucide-react';
import { api } from '../api/endpoints';
import { Button } from '../components/Button';
import { Card } from '../components/Card';
import { DetailGrid, DetailItem } from '../components/DetailGrid';
import { Field, SelectInput, TextArea } from '../components/FormFields';
import { Modal } from '../components/Modal';
import { PageHeader } from '../components/PageHeader';
import { Pagination } from '../components/Pagination';
import { StatusBadge } from '../components/StatusBadge';
import { EmptyState, ErrorState, LoadingState } from '../components/StateViews';
import { useAsyncData } from '../hooks/useAsyncData';
import { useI18n } from '../i18n/I18nProvider';
import type { EquipmentReservationStatus, ReservationDetailsResponse } from '../types/domain';
import { formatDate, formatDateTime, toNullable } from '../utils/format';

const pageSize = 15;
const statusValues: EquipmentReservationStatus[] = ['Draft', 'PendingApproval', 'Approved', 'Rejected', 'Cancelled', 'ReadyForPickup', 'CheckedOut', 'Completed', 'Expired'];
const calendarRangeDays = 30;

function isoDate(daysFromNow: number) {
  const date = new Date();
  date.setDate(date.getDate() + daysFromNow);
  return date.toISOString();
}

export function ReservationsPage() {
  const { t } = useI18n();
  const navigate = useNavigate();
  const { id } = useParams<{ id?: string }>();
  const [tab, setTab] = useState<'queue' | 'calendar'>('queue');
  const [status, setStatus] = useState<EquipmentReservationStatus | ''>('');
  const [page, setPage] = useState(1);
  const [message, setMessage] = useState<{ type: 'success' | 'error'; text: string } | null>(null);
  const [activeAction, setActiveAction] = useState('');
  const [approveTarget, setApproveTarget] = useState<ReservationDetailsResponse | null>(null);
  const [rejectTarget, setRejectTarget] = useState<ReservationDetailsResponse | null>(null);
  const [cancelTarget, setCancelTarget] = useState<ReservationDetailsResponse | null>(null);
  const [substituteTarget, setSubstituteTarget] = useState<ReservationDetailsResponse | null>(null);

  const listLoader = useMemo(() => () => api.reservationsPaged({ status, page, pageSize }), [status, page]);
  const list = useAsyncData(listLoader, [listLoader]);
  const people = useAsyncData(() => api.people(), []);
  const categories = useAsyncData(() => api.categories(), []);
  const assets = useAsyncData(() => api.assets(), []);
  const calendarFrom = useMemo(() => isoDate(0), []);
  const calendarTo = useMemo(() => isoDate(calendarRangeDays), []);
  const calendar = useAsyncData(() => api.reservationsCalendar(calendarFrom, calendarTo), [calendarFrom, calendarTo]);
  const detailsLoader = useMemo(() => () => (id ? api.reservation(id) : Promise.resolve(null)), [id]);
  const details = useAsyncData(detailsLoader, [detailsLoader]);

  async function reloadAll() {
    await Promise.all([list.reload(), details.reload(), calendar.reload(), people.reload(), categories.reload(), assets.reload()]);
  }

  async function handleAction(actionKey: string, run: () => Promise<unknown>, successKey: string) {
    setActiveAction(actionKey);
    try {
      await run();
      setMessage({ type: 'success', text: t(successKey) });
      await reloadAll();
    } catch (error) {
      setMessage({ type: 'error', text: error instanceof Error ? error.message : t('reservations.actionFailed') });
    } finally {
      setActiveAction('');
    }
  }

  if (list.isLoading && !list.data) return <LoadingState title={t('reservations.loadingTitle')} description={t('reservations.loadingDesc')} />;
  if (list.error) return <ErrorState message={list.error} onRetry={list.reload} />;

  const items = list.data?.items ?? [];
  const personName = (personId: string) => people.data?.find(person => person.id === personId)?.fullName ?? '—';

  return (
    <div className="pageStack">
      <PageHeader
        eyebrow={t('page.reservations.eyebrow')}
        title={t('page.reservations.title')}
        actions={
          <div className="rowActions">
            <Button variant="secondary" onClick={() => void reloadAll()} icon={<RefreshCw size={16} />}>{t('common.refresh')}</Button>
          </div>
        }
      />

      {message ? <div className="toastStack" aria-live="polite"><div className={`toast toast--${message.type}`}>{message.text}</div></div> : null}

      <div className="tabBar" role="tablist" aria-label={t('reservations.tabsAria')}>
        <button type="button" role="tab" aria-selected={tab === 'queue'} className={tab === 'queue' ? 'tabBar__tab tabBar__tab--active' : 'tabBar__tab'} onClick={() => setTab('queue')}>
          <ClipboardList size={16} />{t('reservations.tabQueue')}
        </button>
        <button type="button" role="tab" aria-selected={tab === 'calendar'} className={tab === 'calendar' ? 'tabBar__tab tabBar__tab--active' : 'tabBar__tab'} onClick={() => setTab('calendar')}>
          <CalendarRange size={16} />{t('reservations.tabCalendar')}
        </button>
      </div>

      {tab === 'queue' ? (
        <>
          <Card className="toolbarCard">
            <div className="filters filters--three">
              <Field label={t('reservations.statusFilter')}>
                <SelectInput value={status} onChange={event => { setStatus(event.target.value as EquipmentReservationStatus | ''); setPage(1); }}>
                  <option value="">{t('reservations.allStatuses')}</option>
                  {statusValues.map(value => <option key={value} value={value}>{t(`status.${value}`)}</option>)}
                </SelectInput>
              </Field>
            </div>
          </Card>

          {!items.length ? (
            <EmptyState title={t('reservations.emptyTitle')} description={t('reservations.emptyDesc')} />
          ) : (
            <Card>
              <div className="tableWrap tableWrap--cards">
                <table>
                  <thead>
                    <tr>
                      <th>{t('reservations.colRequester')}</th>
                      <th>{t('reservations.colDates')}</th>
                      <th>{t('reservations.colPurpose')}</th>
                      <th>{t('reservations.colStatus')}</th>
                      <th></th>
                    </tr>
                  </thead>
                  <tbody>
                    {items.map(item => (
                      <tr key={item.id}>
                        <td data-label={t('reservations.colRequester')}><strong>{personName(item.requesterPersonId)}</strong></td>
                        <td data-label={t('reservations.colDates')}>{formatDate(item.startAt)} – {formatDate(item.endAt)}</td>
                        <td data-label={t('reservations.colPurpose')}>{item.purpose}</td>
                        <td data-label={t('reservations.colStatus')}><StatusBadge status={item.status} /></td>
                        <td>
                          <div className="tableActions">
                            <button type="button" className="iconButton" aria-label={t('reservations.detailsAria')} onClick={() => navigate(`/reservations/${item.id}`)}><Eye size={16} /></button>
                          </div>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
              <Pagination page={page} total={list.data?.total ?? 0} pageSize={pageSize} onPageChange={setPage} />
            </Card>
          )}
        </>
      ) : (
        <Card>
          <div className="sectionTitle"><div><h2>{t('reservations.calendarTitle')}</h2><p>{t('reservations.calendarRange', { from: formatDate(calendarFrom), to: formatDate(calendarTo) })}</p></div></div>
          {calendar.isLoading && !calendar.data ? <LoadingState title={t('reservations.calendarLoading')} /> : calendar.error ? <ErrorState message={calendar.error} onRetry={calendar.reload} /> : !(calendar.data ?? []).length ? <p className="muted">{t('reservations.calendarEmpty')}</p> : (
            <div className="listRows">
              {(calendar.data ?? []).slice().sort((a, b) => a.startAt.localeCompare(b.startAt)).map(item => (
                <div className="listRow" key={item.id}>
                  <div>
                    <strong>{personName(item.requesterPersonId)} · {item.purpose}</strong>
                    <small>{formatDateTime(item.startAt)} – {formatDateTime(item.endAt)}</small>
                  </div>
                  <div className="rowActions">
                    {item.isConflicting ? <span className="flag flag--danger">{t('reservations.isConflicting')}</span> : null}
                    {item.isDueToday ? <span className="flag flag--accent">{t('reservations.dueToday')}</span> : null}
                    {item.isOverdue ? <span className="flag flag--danger">{t('reservations.overdue')}</span> : null}
                    <StatusBadge status={item.status} />
                    <button type="button" className="iconButton" aria-label={t('reservations.detailsAria')} onClick={() => navigate(`/reservations/${item.id}`)}><Eye size={16} /></button>
                  </div>
                </div>
              ))}
            </div>
          )}
        </Card>
      )}

      {id ? (
        details.isLoading && !details.data ? <LoadingState title={t('reservations.detailsLoadingTitle')} /> :
          details.error || !details.data ? <ErrorState message={details.error ?? t('reservations.detailsLoadFailed')} onRetry={details.reload} /> :
            <ReservationDetailsView
              details={details.data}
              categories={categories.data ?? []}
              people={people.data ?? []}
              actionBusy={activeAction}
              onApprove={() => setApproveTarget(details.data)}
              onReject={() => setRejectTarget(details.data)}
              onSubstitute={() => setSubstituteTarget(details.data)}
              onCheckout={() => void handleAction('checkout', () => api.checkoutReservation(details.data!.reservation.id), 'reservations.checkedOut')}
              onCancel={() => setCancelTarget(details.data)}
            />
      ) : null}

      <ApproveModal target={approveTarget} assets={assets.data ?? []} categories={categories.data ?? []} onClose={() => setApproveTarget(null)} onSubmit={(allocations) => handleAction('approve', () => api.approveReservation(approveTarget!.reservation.id, { allocations }), 'reservations.approved')} />
      <RejectModal target={rejectTarget} onClose={() => setRejectTarget(null)} onSubmit={(reason) => handleAction('reject', () => api.rejectReservation(rejectTarget!.reservation.id, { reason }), 'reservations.rejected')} />
      <CancelModal target={cancelTarget} onClose={() => setCancelTarget(null)} onSubmit={(reason) => handleAction('cancel', () => api.cancelReservation(cancelTarget!.reservation.id, { reason }), 'reservations.cancelled')} />
      <SubstituteModal target={substituteTarget} assets={assets.data ?? []} categories={categories.data ?? []} onClose={() => setSubstituteTarget(null)} onSubmit={(body) => handleAction('substitute', () => api.substituteReservation(substituteTarget!.reservation.id, body), 'reservations.substituted')} />
    </div>
  );
}

function ReservationDetailsView({ details, categories, people, actionBusy, onApprove, onReject, onSubstitute, onCheckout, onCancel }: {
  details: ReservationDetailsResponse;
  categories: import('../types/domain').AssetCategory[];
  people: import('../types/domain').Person[];
  actionBusy: string;
  onApprove: () => void;
  onReject: () => void;
  onSubstitute: () => void;
  onCheckout: () => void;
  onCancel: () => void;
}) {
  const { t } = useI18n();
  const navigate = useNavigate();
  const reservation = details.reservation;
  const requesterName = people.find(person => person.id === reservation.requesterPersonId)?.fullName ?? '—';
  const categoryName = (categoryId: string) => categories.find(category => category.id === categoryId)?.name ?? '—';
  const canApprove = reservation.status === 'PendingApproval';
  const canCheckout = reservation.status === 'Approved' || reservation.status === 'ReadyForPickup';

  return (
    <div className="pageStack">
      <Card>
        <div className="sectionTitle">
          <div>
            <h2>{requesterName}</h2>
            <p>{reservation.purpose}</p>
          </div>
          <div className="rowActions">
            <Button variant="secondary" onClick={() => navigate('/reservations')} icon={<RotateCcw size={16} />}>{t('common.back')}</Button>
          </div>
        </div>
        <DetailGrid>
          <DetailItem label={t('reservations.statusLabel')} value={<StatusBadge status={reservation.status} />} />
          <DetailItem label={t('reservations.datesLabel')} value={`${formatDateTime(reservation.startAt)} – ${formatDateTime(reservation.endAt)}`} />
          <DetailItem label={t('reservations.locationLabel')} value={reservation.pickupLocation ?? t('common.none')} />
          <DetailItem label={t('reservations.purposeLabel')} value={reservation.purpose} />
          <DetailItem label={t('reservations.decisionLabel')} value={reservation.decisionNotes ?? t('common.none')} />
        </DetailGrid>
      </Card>

      <Card className="card--flat">
        <div className="sectionTitle"><div><h2>{t('reservations.itemsTitle')}</h2></div></div>
        {!details.items.length ? <p className="muted">{t('reservations.noItems')}</p> : (
          <div className="listRows">
            {details.items.map(item => (
              <div className="listRow" key={item.id}>
                <div>
                  <strong>{categoryName(item.requestedCategoryId)}</strong>
                  <small>{t('reservations.itemQuantity', { count: item.requestedQuantity })}</small>
                </div>
                <div className="rowActions">
                  {item.assetId ? <span className="mono">{t('reservations.assetIdLabel')}: {item.assetId.slice(0, 8)}…</span> : null}
                  <StatusBadge status={item.status} />
                </div>
              </div>
            ))}
          </div>
        )}
      </Card>

      <Card>
        <div className="formActions" style={{ flexWrap: 'wrap', justifyContent: 'flex-start' }}>
          {canApprove ? <Button onClick={onApprove} icon={<CheckCircle2 size={16} />}>{t('reservations.approve')}</Button> : null}
          {canApprove ? <Button variant="danger" onClick={onReject} icon={<XCircle size={16} />}>{t('reservations.reject')}</Button> : null}
          {reservation.status === 'Approved' ? <Button variant="secondary" onClick={onSubstitute} icon={<AlertTriangle size={16} />}>{t('reservations.substitute')}</Button> : null}
          {canCheckout ? <Button disabled={actionBusy === 'checkout'} onClick={onCheckout} icon={<PackageCheck size={16} />}>{t('reservations.checkout')}</Button> : null}
          {['Draft', 'PendingApproval', 'Approved', 'ReadyForPickup'].includes(reservation.status) ? <Button variant="ghost" onClick={onCancel} icon={<XCircle size={16} />}>{t('reservations.cancelAction')}</Button> : null}
        </div>
      </Card>
    </div>
  );
}

function ApproveModal({ target, assets, categories, onClose, onSubmit }: {
  target: ReservationDetailsResponse | null;
  assets: import('../types/domain').Asset[];
  categories: import('../types/domain').AssetCategory[];
  onClose: () => void;
  onSubmit: (allocations: { itemId: string; assetId: string }[]) => void;
}) {
  const { t } = useI18n();
  const [allocations, setAllocations] = useState<Record<string, string>>({});
  if (!target) return null;
  const items = target.items;
  const categoryName = (categoryId: string) => categories.find(category => category.id === categoryId)?.name ?? '—';
  const assetsFor = (categoryId: string) => assets.filter(asset => asset.categoryId === categoryId && asset.status === 'InStock');
  const complete = items.every(item => allocations[item.id]);

  return (
    <Modal open={!!target} title={t('reservations.approveTitle')} description={t('reservations.approveDesc')} onClose={onClose} width="wide">
      <div className="formGrid">
        {items.map(item => (
          <Field key={item.id} label={t('reservations.allocationLabel', { category: categoryName(item.requestedCategoryId) })}>
            <SelectInput value={allocations[item.id] ?? ''} onChange={event => setAllocations(prev => ({ ...prev, [item.id]: event.target.value }))}>
              <option value="">{t('reservations.chooseAsset')}</option>
              {assetsFor(item.requestedCategoryId).map(asset => <option key={asset.id} value={asset.id}>{asset.name} ({asset.assetTag})</option>)}
            </SelectInput>
          </Field>
        ))}
      </div>
      <p className="muted">{t('reservations.assetListNote')}</p>
      <div className="formActions formActions--split">
        <Button type="button" variant="ghost" onClick={onClose}>{t('common.cancel')}</Button>
        <Button
          type="button"
          disabled={!complete}
          onClick={() => {
            onSubmit(items.map(item => ({ itemId: item.id, assetId: allocations[item.id] })));
            setAllocations({});
            onClose();
          }}
        >
          {t('reservations.approve')}
        </Button>
      </div>
    </Modal>
  );
}

function RejectModal({ target, onClose, onSubmit }: {
  target: ReservationDetailsResponse | null;
  onClose: () => void;
  onSubmit: (reason: string) => void;
}) {
  const { t } = useI18n();
  const [reason, setReason] = useState('');
  return (
    <Modal open={!!target} title={t('reservations.rejectTitle')} onClose={onClose}>
      <div className="formGrid">
        <Field label={t('reservations.rejectReasonLabel')}><TextArea value={reason} onChange={event => setReason(event.target.value)} /></Field>
        <div className="formActions formActions--split">
          <Button type="button" variant="ghost" onClick={onClose}>{t('common.cancel')}</Button>
          <Button type="button" variant="danger" disabled={!reason.trim()} onClick={() => { onSubmit(reason.trim()); setReason(''); onClose(); }}>{t('reservations.reject')}</Button>
        </div>
      </div>
    </Modal>
  );
}

function CancelModal({ target, onClose, onSubmit }: {
  target: ReservationDetailsResponse | null;
  onClose: () => void;
  onSubmit: (reason: string | null) => void;
}) {
  const { t } = useI18n();
  const [reason, setReason] = useState('');
  return (
    <Modal open={!!target} title={t('reservations.cancelTitle')} onClose={onClose}>
      <div className="formGrid">
        <Field label={t('reservations.cancelReasonLabel')}><TextArea value={reason} onChange={event => setReason(event.target.value)} /></Field>
        <div className="formActions formActions--split">
          <Button type="button" variant="ghost" onClick={onClose}>{t('common.cancel')}</Button>
          <Button type="button" variant="danger" onClick={() => { onSubmit(toNullable(reason)); setReason(''); onClose(); }}>{t('reservations.cancelAction')}</Button>
        </div>
      </div>
    </Modal>
  );
}

function SubstituteModal({ target, assets, categories, onClose, onSubmit }: {
  target: ReservationDetailsResponse | null;
  assets: import('../types/domain').Asset[];
  categories: import('../types/domain').AssetCategory[];
  onClose: () => void;
  onSubmit: (body: { itemId: string; newAssetId: string; reason: string }) => void;
}) {
  const { t } = useI18n();
  const [itemId, setItemId] = useState('');
  const [newAssetId, setNewAssetId] = useState('');
  const [reason, setReason] = useState('');
  if (!target) return null;
  const item = target.items.find(x => x.id === itemId);
  const categoryName = (categoryId: string) => categories.find(category => category.id === categoryId)?.name ?? '—';
  const candidateAssets = item ? assets.filter(asset => asset.categoryId === item.requestedCategoryId && asset.status === 'InStock' && asset.id !== item.assetId) : [];
  const valid = itemId && newAssetId && reason.trim();

  return (
    <Modal open={!!target} title={t('reservations.substituteTitle')} onClose={onClose}>
      <div className="formGrid">
        <Field label={t('reservations.substituteItemLabel')}>
          <SelectInput value={itemId} onChange={event => { setItemId(event.target.value); setNewAssetId(''); }}>
            <option value="">{t('reservations.substituteItemChoose')}</option>
            {target.items.filter(x => x.assetId).map(x => <option key={x.id} value={x.id}>{categoryName(x.requestedCategoryId)}</option>)}
          </SelectInput>
        </Field>
        {item ? (
          <Field label={t('reservations.newAssetLabel')}>
            <SelectInput value={newAssetId} onChange={event => setNewAssetId(event.target.value)}>
              <option value="">{t('reservations.chooseAsset')}</option>
              {candidateAssets.map(asset => <option key={asset.id} value={asset.id}>{asset.name} ({asset.assetTag})</option>)}
            </SelectInput>
          </Field>
        ) : null}
        <Field label={t('reservations.substituteReasonLabel')}><TextArea value={reason} onChange={event => setReason(event.target.value)} /></Field>
        <div className="formActions formActions--split">
          <Button type="button" variant="ghost" onClick={onClose}>{t('common.cancel')}</Button>
          <Button type="button" disabled={!valid} onClick={() => { onSubmit({ itemId, newAssetId, reason: reason.trim() }); setItemId(''); setNewAssetId(''); setReason(''); onClose(); }}>{t('reservations.substitute')}</Button>
        </div>
      </div>
    </Modal>
  );
}
