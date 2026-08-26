import { Copy, Eye, Pencil, Plus, QrCode, Trash2 } from 'lucide-react';
import { Avatar } from '../../components/Avatar';
import { Button } from '../../components/Button';
import { EvidenceGallery } from '../../components/Evidence';
import { SlidePanel } from '../../components/SlidePanel';
import { StatusBadge } from '../../components/StatusBadge';
import { api } from '../../api/endpoints';
import { useI18n } from '../../i18n/I18nProvider';
import type { ActivityLogEntry, Asset, AssetEvidence, ServiceTicket, ServiceTicketStatus } from '../../types/domain';
import { CategoryIcon } from '../../utils/categoryIcons';
import { activityLabel } from '../../utils/labels';
import { formatDate, formatDateTime, formatMoney } from '../../utils/format';
import { AssetMaintenanceSection } from './AssetMaintenanceSection';

interface AssetDetailPanelProps {
  selected: Asset | null;
  categoryIcon?: string | null;
  statusSettingByKey: Map<string, { label: string; color: string; backgroundColor: string }>;
  onClose(): void;
  onQr(asset: Asset): void;
  onEdit(asset: Asset): void;
  onDuplicate(asset: Asset): void;
  onDelete(asset: Asset): void;
  onViewPerson(id: string): void;
  onViewLocation(location: string): void;
  revealedFields: Record<string, string>;
  revealingKey: string | null;
  onRevealField(key: string): void;
  evidence: AssetEvidence[] | null | undefined;
  evidenceLoading: boolean;
  serviceTickets: ServiceTicket[] | null | undefined;
  serviceTicketsLoading: boolean;
  onOpenServiceTicket(): void;
  onCompleteServiceTicket(ticket: ServiceTicket): void;
  onCancelServiceTicket(ticket: ServiceTicket): void;
  history: ActivityLogEntry[] | null | undefined;
  historyLoading: boolean;
}

export function AssetDetailPanel(props: AssetDetailPanelProps) {
  const { t } = useI18n();
  const selected = props.selected;
  const serviceTicketStatusLabels: Record<ServiceTicketStatus, string> = {
    Open: t('serviceTickets.status.Open'),
    InProgress: t('serviceTickets.status.InProgress'),
    WaitingForParts: t('serviceTickets.status.WaitingForParts'),
    Completed: t('serviceTickets.status.Completed'),
    Cancelled: t('serviceTickets.status.Cancelled')
  };

  return (
    <SlidePanel open={!!selected} title={selected?.name ?? t('assets.colName')} onClose={props.onClose} width="wide">
      {selected && (
        <div className="modalDetails">
          <div className="modalToolbar">
            <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
              <div className="table-icon" style={{ width: '48px', height: '48px' }}><CategoryIcon icon={props.categoryIcon} size={24} /></div>
              <div><strong>{selected.assetTag}</strong><small>{selected.categoryName ?? t('assets.noCategory')}</small></div>
            </div>
            <div className="rowActions">
              <Button variant="secondary" onClick={() => props.onQr(selected)} icon={<QrCode size={16} />}>{t('assets.qrCode')}</Button>
              <Button variant="secondary" onClick={() => props.onEdit(selected)} icon={<Pencil size={16} />}>{t('assets.edit')}</Button>
              <Button variant="secondary" onClick={() => props.onDuplicate(selected)} icon={<Copy size={16} />}>{t('assets.duplicate')}</Button>
              <Button variant="secondary" onClick={() => props.onDelete(selected)} icon={<Trash2 size={16} />}>{t('assets.delete')}</Button>
            </div>
          </div>

          <dl className="detailGrid">
            <Detail label={t('assets.statusLabel')}><StatusBadge status={selected.status} {...statusProps(props.statusSettingByKey.get(selected.status))} /></Detail>
            <Detail label={t('assets.colPerson')}>{selected.assignedPersonId && selected.assignedPersonName ? (
              <span className="personChip">
                <Avatar name={selected.assignedPersonName} size={22} />
                <span className="personChip__sep">•</span>
                <button type="button" className="inlineAction" onClick={() => props.onViewPerson(selected.assignedPersonId!)}>{selected.assignedPersonName}</button>
              </span>
            ) : t('common.unassigned')}</Detail>
            <Detail label={t('assets.colLocation')}>{selected.location ? <button type="button" className="inlineAction" onClick={() => props.onViewLocation(selected.location!)}>{selected.location}</button> : t('common.noLocation')}</Detail>
            <Detail label={t('assets.teamLabel')}>{selected.teamName ?? t('common.none')}</Detail>
            <Detail label={t('assets.serialNumber')}>{selected.serialNumber ?? t('common.none')}</Detail>
            <Detail label={t('assets.manufacturerModel')}>{[selected.manufacturer, selected.model].filter(Boolean).join(' ') || t('common.none')}</Detail>
            <Detail label={t('assets.purchase')}>{selected.purchasePrice != null ? `${formatMoney(selected.purchasePrice, selected.currency ?? 'PLN')} · ${formatDate(selected.purchaseDate)}` : t('assets.noPurchaseData')}</Detail>
            <Detail label={t('assets.warranty')}>{formatDate(selected.warrantyUntil)}</Detail>
            {selected.categoryFieldDefinitions.map(field => (
              <Detail key={field.id} label={field.label}>
                {field.fieldType === 'Boolean' ? (
                  selected.customFields[field.key] === 'true' ? t('common.yes') : t('common.no')
                ) : field.fieldType === 'Sensitive' ? (
                  <span style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                    <span>{props.revealedFields[field.key] ?? selected.customFields[field.key] ?? t('common.none')}</span>
                    {props.revealedFields[field.key] === undefined && selected.customFields[field.key] && (
                      <button type="button" className="iconButton" aria-label={t('assets.revealField')} title={t('assets.revealField')} disabled={props.revealingKey === field.key} onClick={() => props.onRevealField(field.key)}><Eye size={14} /></button>
                    )}
                  </span>
                ) : selected.customFields[field.key] ?? t('common.none')}
              </Detail>
            ))}
          </dl>

          <div className="formSectionTitle">{t('evidence.photos')}</div>
          {props.evidenceLoading ? <p className="muted">{t('common.loading')}</p> : !props.evidence?.length ? (
            <p className="muted">{t('evidence.noPhotos')}</p>
          ) : (
            <div className="pageStack">
              {(['Issue', 'Return', 'Audit', 'Offboarding'] as const).map(phase => {
                const ids = props.evidence!.filter(item => item.phase === phase).map(item => item.id);
                if (!ids.length) return null;
                return <div key={phase}><strong>{t(`evidence.phase.${phase}`)}</strong><EvidenceGallery ids={ids} getBlob={api.evidenceBlob} /></div>;
              })}
            </div>
          )}

          <div className="formSectionTitle">{t('serviceTickets.title')}</div>
          <div style={{ display: 'flex', justifyContent: 'flex-end', marginBottom: '8px' }}>
            <Button variant="secondary" onClick={props.onOpenServiceTicket} icon={<Plus size={16} />} disabled={selected.status === 'Disposed'} title={selected.status === 'Disposed' ? t('serviceTickets.cannotOpenForDisposed') : undefined}>{t('serviceTickets.open')}</Button>
          </div>
          {props.serviceTicketsLoading ? <p className="muted">{t('common.loading')}</p> : !props.serviceTickets?.length ? (
            <p className="muted">{t('serviceTickets.none')}</p>
          ) : (
            <div className="listRows">
              {props.serviceTickets.map(ticket => {
                const cost = ticket.actualCost ?? ticket.estimatedCost;
                const isOpen = ticket.status === 'Open' || ticket.status === 'InProgress' || ticket.status === 'WaitingForParts';
                return (
                  <div className="listRow" key={ticket.id}>
                    <div><strong>{ticket.vendor}</strong><small><span className={ticketStatusClass(ticket.status)}>{serviceTicketStatusLabels[ticket.status]}</span>{' · '}{formatDate(ticket.openedAt)}{cost != null && ` · ${formatMoney(cost, ticket.currency ?? 'PLN')}`}</small></div>
                    {isOpen ? <div style={{ display: 'flex', gap: '8px' }}><Button variant="secondary" onClick={() => props.onCompleteServiceTicket(ticket)}>{t('serviceTickets.complete')}</Button><Button variant="ghost" onClick={() => props.onCancelServiceTicket(ticket)}>{t('serviceTickets.cancel')}</Button></div> : null}
                  </div>
                );
              })}
            </div>
          )}

          <AssetMaintenanceSection assetId={selected.id} />

          <div className="formSectionTitle">{t('assets.historyTitle')}</div>
          {props.historyLoading ? <p className="muted">{t('common.loading')}</p> : !props.history?.length ? (
            <p className="muted">{t('assets.noHistory')}</p>
          ) : (
            <div className="listRows">
              {props.history.map(entry => <div className="listRow" key={entry.id}><div><strong>{activityLabel(t, entry.action)}</strong><small>{entry.actorDisplay}</small></div><small>{formatDateTime(entry.createdAt)}</small></div>)}
            </div>
          )}
        </div>
      )}
    </SlidePanel>
  );
}

function Detail({ label, children }: { label: string; children: React.ReactNode }) {
  return <div><dt>{label}</dt><dd>{children}</dd></div>;
}

function statusProps(setting: { label: string; color: string; backgroundColor: string } | undefined) {
  return { label: setting?.label, color: setting?.color, backgroundColor: setting?.backgroundColor };
}

function ticketStatusClass(status: ServiceTicketStatus): string {
  if (status === 'Completed') return 'status status--InStock';
  if (status === 'Cancelled') return 'status status--Damaged';
  return 'status status--InService';
}
