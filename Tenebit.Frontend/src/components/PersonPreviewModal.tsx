import { Link } from 'react-router-dom';
import { api } from '../api/endpoints';
import { Avatar } from './Avatar';
import { DetailGrid, DetailItem } from './DetailGrid';
import { Modal } from './Modal';
import { StatusBadge } from './StatusBadge';
import { useAsyncData } from '../hooks/useAsyncData';
import { useI18n } from '../i18n/I18nProvider';

export function PersonPreviewModal({ personId, onClose }: { personId: string; onClose: () => void }) {
  const { t } = useI18n();
  const people = useAsyncData(() => api.people(), []);
  const person = people.data?.find(item => item.id === personId) ?? null;
  const workspace = useAsyncData(() => api.personWorkspace(personId), [personId]);

  return (
    <Modal open title={person?.fullName ?? t('common.loading')} onClose={onClose} width="wide">
      {people.isLoading || workspace.isLoading ? (
        <p className="muted">{t('people.loadingWorkspace')}</p>
      ) : !person ? (
        <p className="muted">{t('people.notFound')}</p>
      ) : (
        <div className="pageStack">
          <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
            <Avatar name={person.fullName} size={40} />
            <strong>{person.fullName}</strong>
          </div>
          <DetailGrid>
            <DetailItem label={t('settings.emailLabel')} value={person.email} />
            <DetailItem label={t('people.phoneLabel')} value={person.phone ?? t('common.none')} />
            <DetailItem label={t('people.jobTitleLabel')} value={person.jobTitle ?? t('common.none')} />
            <DetailItem label={t('people.teamLabel')} value={person.teamName ?? t('common.none')} />
            <DetailItem label={t('people.locationLabel')} value={person.location ?? t('common.none')} />
          </DetailGrid>

          <div className="formSectionTitle">{t('people.assignedAssetsTitle')}</div>
          {!workspace.data?.assets?.length ? (
            <p className="muted">{t('people.noAssignedAssets')}</p>
          ) : (
            <div className="listRows">
              {workspace.data.assets.map(asset => (
                <Link className="listRow" to={`/assets?search=${encodeURIComponent(asset.assetTag)}`} key={asset.id} onClick={onClose}>
                  <div><strong>{asset.name}</strong><small>{asset.assetTag}</small></div>
                  <span>{asset.categoryName ?? t('common.none')}</span>
                </Link>
              ))}
            </div>
          )}

          <div className="formSectionTitle">{t('people.assignmentsTitle')}</div>
          {!workspace.data?.assignments?.length ? (
            <p className="muted">{t('people.noAssignments')}</p>
          ) : (
            <div className="listRows">
              {workspace.data.assignments.map(assignment => (
                <div className="listRow" key={assignment.id}>
                  <div><strong>{assignment.protocolNumber}</strong><small>{assignment.assetNames.join(', ')}</small></div>
                  <StatusBadge status={assignment.status} />
                </div>
              ))}
            </div>
          )}
        </div>
      )}
    </Modal>
  );
}
