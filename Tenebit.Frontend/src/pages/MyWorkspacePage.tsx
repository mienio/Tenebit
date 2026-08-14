import { CheckCircle2, Download, FileText, Plus, Users, XCircle } from 'lucide-react';
import { useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { api } from '../api/endpoints';
import { Button } from '../components/Button';
import { Card } from '../components/Card';
import { canSee, nav } from '../components/Layout';
import { LocationInventoryModal } from '../components/LocationInventoryModal';
import { PageHeader } from '../components/PageHeader';
import { Field, TextArea, TextInput } from '../components/FormFields';
import { StatusBadge } from '../components/StatusBadge';
import { EmptyState, ErrorState, LoadingState } from '../components/StateViews';
import { useAuth } from '../auth/AuthProvider';
import { useAsyncData } from '../hooks/useAsyncData';
import { formatDate, formatDateTime, toNullable } from '../utils/format';
import { CategoryIcon } from '../utils/categoryIcons';
import type { CreateReservationRequest, MyAssignment, MyProcedure, ReservationItemRequest } from '../types/domain';
import { useI18n } from '../i18n/I18nProvider';

const peopleNavRoles = nav.find(item => item.to === '/people')?.roles ?? [];

type CartItem = { categoryId?: string; kitDefinitionId?: string; quantity: number; label: string };

function toLocalInput(date: Date) {
  const tz = date.getTimezoneOffset() * 60000;
  return new Date(date.getTime() - tz).toISOString().slice(0, 16);
}

function toIso(value: string): string | undefined {
  if (!value) return undefined;
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? undefined : date.toISOString();
}

export function MyWorkspacePage() {
  const { t } = useI18n();
  const auth = useAuth();
  const workspace = useAsyncData(api.myWorkspace, []);
  const [activeTab, setActiveTab] = useState<'equipment' | 'order'>('equipment');
  const [message, setMessage] = useState<{ type: 'success' | 'error'; text: string } | null>(null);
  const [accepting, setAccepting] = useState<string | null>(null);
  const [viewLocation, setViewLocation] = useState<string | null>(null);

  useEffect(() => {
    if (!message) return;
    const timeout = window.setTimeout(() => setMessage(null), message.type === 'success' ? 3500 : 6500);
    return () => window.clearTimeout(timeout);
  }, [message]);

  async function accept(assignment: MyAssignment) {
    setAccepting(assignment.id);
    setMessage(null);
    try {
      await api.acceptAssignment(assignment.id);
      setMessage({ type: 'success', text: t('myWorkspace.confirmed') });
      await workspace.reload();
    } catch (error) {
      setMessage({ type: 'error', text: error instanceof Error ? error.message : t('myWorkspace.confirmFailed') });
    } finally {
      setAccepting(null);
    }
  }

  async function downloadProtocol(assignment: MyAssignment) {
    try {
      const blob = await api.downloadAssignmentProtocol(assignment.id);
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `${assignment.protocolNumber}.pdf`;
      document.body.appendChild(link);
      link.click();
      link.remove();
      URL.revokeObjectURL(url);
    } catch (error) {
      setMessage({ type: 'error', text: error instanceof Error ? error.message : t('assignments.downloadProtocolFailed') });
    }
  }

  async function download(procedure: MyProcedure) {
    if (!procedure.documentId) return;
    try {
      const blob = await api.downloadProcedureDocument(procedure.procedureId, procedure.documentId);
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = procedure.documentFileName ?? `${procedure.title ?? 'procedura'}.pdf`;
      document.body.appendChild(link);
      link.click();
      link.remove();
      URL.revokeObjectURL(url);
    } catch (error) {
      setMessage({ type: 'error', text: error instanceof Error ? error.message : t('myWorkspace.downloadFailed') });
    }
  }

  if (workspace.isLoading && !workspace.data) return <LoadingState title={t('myWorkspace.loadingTitle')} description={t('myWorkspace.loadingDesc')} />;
  if (workspace.error || !workspace.data) return <ErrorState message={workspace.error ?? t('myWorkspace.noData')} onRetry={workspace.reload} />;

  const data = workspace.data;

  if (!data.hasPersonRecord) {
    const canManagePeople = canSee(peopleNavRoles, auth.roles);
    const description = !auth.userEmail
      ? t('myWorkspace.noPersonDesc')
      : canManagePeople
        ? t('myWorkspace.noPersonDescSelfWithEmail', { email: auth.userEmail })
        : t('myWorkspace.noPersonDescWithEmail', { email: auth.userEmail });
    return (
      <div className="pageStack pageStack--narrow">
        <PageHeader eyebrow={t('page.my.eyebrow')} title={t('nav.my')} />
        <Card>
          <EmptyState
            title={t('myWorkspace.noPersonTitle')}
            description={description}
            action={canManagePeople ? (
              <Link to="/people?addSelf=1" className="button button--primary">
                <span className="button__icon"><Users size={16} /></span>
                <span>{t('myWorkspace.addSelf')}</span>
              </Link>
            ) : undefined}
          />
        </Card>
      </div>
    );
  }

  const pendingAssignments = data.assignments.filter(item => item.status === 'AwaitingAcceptance');
  const pastAssignments = data.assignments.filter(item => item.status !== 'AwaitingAcceptance');

  const procedureMap = new Map<string, MyProcedure>();
  for (const assignment of data.assignments) {
    for (const procedure of assignment.procedures) {
      procedureMap.set(procedure.procedureId, procedure);
    }
  }
  const procedures = Array.from(procedureMap.values());

  return (
    <div className="pageStack pageStack--narrow">
      <PageHeader eyebrow={t('page.my.eyebrow')} title={`${t('page.my.greeting')}, ${data.personName ?? ''}`} />

      {message ? <div className="toastStack" aria-live="polite"><div className={`toast toast--${message.type}`}>{message.text}</div></div> : null}

      <div className="tabBar" role="tablist" aria-label={t('myWorkspace.tabsAria')}>
        <button type="button" role="tab" aria-selected={activeTab === 'equipment'} className={activeTab === 'equipment' ? 'tabBar__tab tabBar__tab--active' : 'tabBar__tab'} onClick={() => setActiveTab('equipment')}>
          {t('myWorkspace.myEquipment')}
        </button>
        <button type="button" role="tab" aria-selected={activeTab === 'order'} className={activeTab === 'order' ? 'tabBar__tab tabBar__tab--active' : 'tabBar__tab'} onClick={() => setActiveTab('order')}>
          {t('myWorkspace.orderEquipment')}
        </button>
      </div>

      {activeTab === 'equipment' ? (
        <>
          {pendingAssignments.length > 0 && (
            <Card>
              <div className="sectionTitle"><div><h2>{t('myWorkspace.pending')}</h2></div></div>
              <div className="listRows">
                {pendingAssignments.map(assignment => (
                  <div className="listRow" key={assignment.id}>
                    <div>
                      <strong>{assignment.protocolNumber}</strong>
                      <small>{assignment.assetNames.join(', ')}</small>
                    </div>
                    <div className="rowActions">
                      <button className="iconButton" aria-label={t('assignments.downloadProtocol')} title={t('assignments.downloadProtocol')} onClick={() => downloadProtocol(assignment)}><Download size={16} /></button>
                      <Button disabled={accepting === assignment.id} onClick={() => accept(assignment)} icon={<CheckCircle2 size={16} />}>
                        {accepting === assignment.id ? t('myWorkspace.confirming') : t('myWorkspace.confirmReceipt')}
                      </Button>
                    </div>
                  </div>
                ))}
              </div>
            </Card>
          )}

          <Card>
            <div className="sectionTitle"><div><h2>{t('myWorkspace.yourEquipment')}</h2></div></div>
            {!data.assets.length ? <p className="muted">{t('myWorkspace.noEquipment')}</p> : (
              <div className="listRows">
                {data.assets.map(asset => (
                  <div className="listRow" key={asset.id}>
                    <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
                      <div className="table-icon"><CategoryIcon icon={asset.categoryIcon} size={16} /></div>
                      <div><strong>{asset.name}</strong><small>{asset.assetTag} · {asset.categoryName ?? t('myWorkspace.noCategory')}</small></div>
                    </div>
                    {asset.location ? (
                      <button type="button" className="inlineAction" onClick={() => setViewLocation(asset.location ?? null)}>{asset.location}</button>
                    ) : <span>{t('common.noLocation')}</span>}
                  </div>
                ))}
              </div>
            )}
          </Card>

          <Card>
            <div className="sectionTitle"><div><h2>{t('myWorkspace.yourProcedures')}</h2></div></div>
            {!procedures.length ? <p className="muted">{t('myWorkspace.noProcedures')}</p> : (
              <div className="listRows">
                {procedures.map(procedure => (
                  <div className="listRow" key={procedure.procedureId}>
                    <div><FileText size={16} /> <strong>{procedure.title ?? t('myWorkspace.unnamedProcedure')}</strong></div>
                    <div className="rowActions">
                      <StatusBadge status={procedure.status} />
                      {procedure.documentId ? (
                        <button className="iconButton" aria-label={t('myWorkspace.downloadAria', { file: procedure.title ?? '' })} onClick={() => download(procedure)}><Download size={16} /></button>
                      ) : null}
                    </div>
                  </div>
                ))}
              </div>
            )}
          </Card>

          {pastAssignments.length > 0 && (
            <Card>
              <div className="sectionTitle"><div><h2>{t('myWorkspace.history')}</h2></div></div>
              <div className="listRows">
                {pastAssignments.map(assignment => (
                  <div className="listRow" key={assignment.id}>
                    <div><strong>{assignment.protocolNumber}</strong><small>{formatDateTime(assignment.issuedAt)} · {formatDate(assignment.dueDate)}</small></div>
                    <div className="rowActions">
                      <button className="iconButton" aria-label={t('assignments.downloadProtocol')} title={t('assignments.downloadProtocol')} onClick={() => downloadProtocol(assignment)}><Download size={16} /></button>
                      <StatusBadge status={assignment.status} />
                    </div>
                  </div>
                ))}
              </div>
            </Card>
          )}
        </>
      ) : (
        <OrderEquipmentTab onMessage={setMessage} />
      )}

      {viewLocation && <LocationInventoryModal locationPath={viewLocation} onClose={() => setViewLocation(null)} />}
    </div>
  );
}

function OrderEquipmentTab({ onMessage }: { onMessage: (message: { type: 'success' | 'error'; text: string }) => void }) {
  const { t } = useI18n();
  const [from, setFrom] = useState(toLocalInput(new Date()));
  const [to, setTo] = useState(toLocalInput(new Date(Date.now() + 7 * 86400000)));
  const [purpose, setPurpose] = useState('');
  const [pickupLocation, setPickupLocation] = useState('');
  const [notes, setNotes] = useState('');
  const [cart, setCart] = useState<CartItem[]>([]);
  const [submitting, setSubmitting] = useState(false);

  const catalogLoader = useMemo(() => () => api.reservationCatalog({ from: toIso(from), to: toIso(to) }), [from, to]);
  const catalog = useAsyncData(catalogLoader, [catalogLoader]);
  const myReservations = useAsyncData(api.myReservations, []);

  function addCategory(id: string, name: string) {
    setCart(prev => {
      const existing = prev.find(item => item.categoryId === id);
      if (existing) return prev.map(item => item.categoryId === id ? { ...item, quantity: item.quantity + 1 } : item);
      return [...prev, { categoryId: id, quantity: 1, label: name }];
    });
  }

  function addKit(id: string, name: string) {
    setCart(prev => {
      const existing = prev.find(item => item.kitDefinitionId === id);
      if (existing) return prev.map(item => item.kitDefinitionId === id ? { ...item, quantity: item.quantity + 1 } : item);
      return [...prev, { kitDefinitionId: id, quantity: 1, label: name }];
    });
  }

  function setQuantity(index: number, quantity: number) {
    setCart(prev => prev.map((item, i) => i === index ? { ...item, quantity: Math.max(1, quantity) } : item));
  }

  function removeItem(index: number) {
    setCart(prev => prev.filter((_, i) => i !== index));
  }

  async function submit() {
    if (!from || !to || !purpose.trim() || !cart.length) {
      onMessage({ type: 'error', text: t('myWorkspace.reservationFormRequired') });
      return;
    }
    setSubmitting(true);
    try {
      const body: CreateReservationRequest = {
        startAt: new Date(from).toISOString(),
        endAt: new Date(to).toISOString(),
        purpose: purpose.trim(),
        pickupLocation: toNullable(pickupLocation),
        notes: toNullable(notes),
        items: cart.map(item => ({ categoryId: item.categoryId ?? null, kitDefinitionId: item.kitDefinitionId ?? null, quantity: item.quantity }))
      };
      await api.createReservation(body);
      onMessage({ type: 'success', text: t('myWorkspace.reservationSubmitted') });
      setCart([]);
      setPurpose('');
      setPickupLocation('');
      setNotes('');
      await myReservations.reload();
    } catch (error) {
      onMessage({ type: 'error', text: error instanceof Error ? error.message : t('myWorkspace.reservationSubmitFailed') });
    } finally {
      setSubmitting(false);
    }
  }

  async function cancelReservation(id: string) {
    try {
      await api.cancelMyReservation(id, { reason: null });
      onMessage({ type: 'success', text: t('myWorkspace.reservationCancelled') });
      await myReservations.reload();
    } catch (error) {
      onMessage({ type: 'error', text: error instanceof Error ? error.message : t('myWorkspace.reservationCancelFailed') });
    }
  }

  if (catalog.isLoading && !catalog.data) return <LoadingState title={t('myWorkspace.reservationLoading')} />;
  if (catalog.error || !catalog.data) return <ErrorState message={catalog.error ?? t('myWorkspace.reservationLoadFailed')} onRetry={catalog.reload} />;

  if (!catalog.data.hasPersonRecord) {
    return (
      <Card>
        <EmptyState title={t('myWorkspace.reservationNoPersonTitle')} description={t('myWorkspace.reservationNoPersonDesc')} />
      </Card>
    );
  }

  return (
    <>
      <Card>
        <div className="sectionTitle"><div><h2>{t('myWorkspace.reservationCatalogTitle')}</h2><p>{t('myWorkspace.reservationCatalogDesc')}</p></div></div>
        <div className="formGrid">
          <Field label={t('myWorkspace.reservationFrom')}><TextInput type="datetime-local" value={from} onChange={event => setFrom(event.target.value)} /></Field>
          <Field label={t('myWorkspace.reservationTo')}><TextInput type="datetime-local" value={to} onChange={event => setTo(event.target.value)} /></Field>
        </div>
        <div className="listRows" style={{ marginTop: '12px' }}>
          {catalog.data.categories.map(category => (
            <div className="listRow" key={category.id}>
              <div>
                <strong>{category.name}</strong>
                {category.description ? <small>{category.description}</small> : null}
              </div>
              <div className="rowActions">
                <span>{t('myWorkspace.reservationAvailable', { count: category.availableCount })}</span>
                <Button variant="secondary" onClick={() => addCategory(category.id, category.name)} icon={<Plus size={16} />} disabled={category.availableCount <= 0}>{t('myWorkspace.reservationAdd')}</Button>
              </div>
            </div>
          ))}
          {catalog.data.kits.map(kit => (
            <div className="listRow" key={kit.id}>
              <div>
                <strong>{kit.name}</strong>
                {kit.description ? <small>{kit.description}</small> : null}
              </div>
              <div className="rowActions">
                <span>{t('myWorkspace.reservationAvailable', { count: kit.availableCount })}</span>
                <Button variant="secondary" onClick={() => addKit(kit.id, kit.name)} icon={<Plus size={16} />} disabled={kit.availableCount <= 0}>{t('myWorkspace.reservationAdd')}</Button>
              </div>
            </div>
          ))}
        </div>
      </Card>

      <Card>
        <div className="sectionTitle"><div><h2>{t('myWorkspace.reservationSummaryTitle')}</h2></div></div>
        {!cart.length ? <p className="muted">{t('myWorkspace.reservationCartEmpty')}</p> : (
          <div className="listRows">
            {cart.map((item, index) => (
              <div className="listRow" key={item.categoryId ?? item.kitDefinitionId ?? index}>
                <div><strong>{item.label}</strong><small>{t('myWorkspace.reservationQuantity')}: {item.quantity}</small></div>
                <div className="rowActions">
                  <input type="number" min={1} className="input" style={{ width: '72px' }} value={item.quantity} onChange={event => setQuantity(index, Number(event.target.value))} aria-label={t('myWorkspace.reservationQuantity')} />
                  <button className="iconButton" type="button" aria-label={t('common.delete')} onClick={() => removeItem(index)}><XCircle size={16} /></button>
                </div>
              </div>
            ))}
          </div>
        )}
        <div className="formGrid" style={{ marginTop: '12px' }}>
          <Field label={t('myWorkspace.reservationPurpose')}><TextInput value={purpose} onChange={event => setPurpose(event.target.value)} /></Field>
          <Field label={t('myWorkspace.reservationPickupLocation')}><TextInput value={pickupLocation} onChange={event => setPickupLocation(event.target.value)} /></Field>
          <Field label={t('myWorkspace.reservationNotes')}><TextArea value={notes} onChange={event => setNotes(event.target.value)} /></Field>
        </div>
        <div className="formActions" style={{ marginTop: '12px' }}>
          <Button disabled={submitting || !cart.length} onClick={submit}>{submitting ? t('common.saving') : t('myWorkspace.reservationSubmit')}</Button>
        </div>
      </Card>

      <Card>
        <div className="sectionTitle"><div><h2>{t('myWorkspace.myReservationsTitle')}</h2></div></div>
        {myReservations.isLoading && !myReservations.data ? <LoadingState /> : !(myReservations.data ?? []).length ? <p className="muted">{t('myWorkspace.reservationEmpty')}</p> : (
          <div className="listRows">
            {(myReservations.data ?? []).map(reservation => (
              <div className="listRow" key={reservation.id}>
                <div>
                  <strong>{reservation.purpose}</strong>
                  <small>{formatDateTime(reservation.startAt)} – {formatDateTime(reservation.endAt)}</small>
                  {reservation.decisionNotes ? <small>{t('myWorkspace.reservationDecision')}: {reservation.decisionNotes}</small> : null}
                  {reservation.status === 'ReadyForPickup' && reservation.pickupLocation ? <small>{t('myWorkspace.reservationPickup')}: {reservation.pickupLocation}</small> : null}
                </div>
                <div className="rowActions">
                  <StatusBadge status={reservation.status} />
                  {['Draft', 'PendingApproval', 'Approved', 'ReadyForPickup'].includes(reservation.status) ? <Button variant="ghost" onClick={() => cancelReservation(reservation.id)}>{t('myWorkspace.reservationCancel')}</Button> : null}
                </div>
              </div>
            ))}
          </div>
        )}
      </Card>
    </>
  );
}
