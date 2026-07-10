import { Link } from 'react-router-dom';
import { api } from '../api/endpoints';
import { Modal } from './Modal';
import { useAsyncData } from '../hooks/useAsyncData';
import { useI18n } from '../i18n/I18nProvider';

export function LocationInventoryModal({ locationPath, onClose }: { locationPath: string; onClose: () => void }) {
  const { t } = useI18n();
  const locations = useAsyncData(api.locations, []);
  const matchedLocation = locations.data?.find(item => item.fullPath === locationPath) ?? null;
  const inventory = useAsyncData(
    () => (matchedLocation ? api.locationInventory(matchedLocation.id) : Promise.resolve(null)),
    [matchedLocation?.id]
  );

  return (
    <Modal open title={locationPath} onClose={onClose} width="wide">
      {locations.isLoading || inventory.isLoading ? (
        <p className="muted">{t('locations.loadingContent')}</p>
      ) : !matchedLocation ? (
        <p className="muted">{t('locations.noneSelected')}</p>
      ) : (
        <div className="twoColumns inventoryColumns">
          <div>
            <h3>{t('locations.assetsHeading')}</h3>
            {!inventory.data?.assets.length ? <p className="muted">{t('locations.noAssetsHere')}</p> : (
              <div className="listRows">{inventory.data.assets.map(asset => (
                <Link className="listRow" to={`/assets?search=${encodeURIComponent(asset.assetTag)}`} key={asset.id} onClick={onClose}>
                  <div><strong>{asset.name}</strong><small>{asset.assetTag}</small></div>
                  <span>{asset.assignedPersonName ?? t('common.unassigned')}</span>
                </Link>
              ))}</div>
            )}
          </div>
          <div>
            <h3>{t('locations.peopleHeading')}</h3>
            {!inventory.data?.people.length ? <p className="muted">{t('locations.noPeopleHere')}</p> : (
              <div className="listRows">{inventory.data.people.map(person => (
                <Link className="listRow" to={`/people?search=${encodeURIComponent(person.email)}`} key={person.id} onClick={onClose}>
                  <div><strong>{person.fullName}</strong><small>{person.email}</small></div>
                  <span>{person.jobTitle ?? t('locations.noJobTitle')}</span>
                </Link>
              ))}</div>
            )}
          </div>
        </div>
      )}
    </Modal>
  );
}
