import type { AssetCategoryType, AssetStatus, AssignmentStatus, LocationType } from '../types/domain';

export const assetStatusValues: AssetStatus[] = ['Draft', 'InStock', 'Reserved', 'Assigned', 'InTransit', 'InService', 'Damaged', 'Lost', 'Retired', 'Disposed'];

export const assignmentStatusValues: AssignmentStatus[] = ['Draft', 'AwaitingAcceptance', 'Accepted', 'Returned', 'Cancelled', 'Overdue'];

export const categoryTypeValues: AssetCategoryType[] = ['Physical', 'Digital', 'License', 'Account', 'Document', 'Location', 'Vehicle', 'Key', 'Consumable', 'Other'];

export const locationTypeValues: LocationType[] = ['Address', 'Building', 'Floor', 'Room', 'Warehouse', 'Zone', 'Shelf', 'Other'];

type Translate = (key: string) => string;

export function translateOr(t: Translate, key: string, fallback: string) {
  const label = t(key);
  return label === key ? fallback : label;
}

export function activityLabel(t: Translate, action: string) {
  return translateOr(t, `activity.${action}`, action);
}
