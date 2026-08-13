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

export function activityActionLabel(t: (key: string) => string, action: string): string {
  const key = `activity.${action}`;
  const label = t(key);
  return label === key ? action.replace(/[._]/g, ' ') : label;
}

export function auditEntityLabel(t: (key: string) => string, entityType: string): string {
  const key = `audit.entityType.${entityType}`;
  const label = t(key);
  return label === key ? entityType.replace(/[._]/g, ' ') : label;
}

export const auditEntityRoutes: Record<string, string> = {
  asset: '/assets',
  asset_category: '/settings?tab=customFields',
  person: '/people',
  team: '/people',
  assignment: '/assignments',
  procedure: '/procedures',
  job_profile: '/settings?tab=profiles',
  organization: '/settings?tab=company',
  organization_user: '/settings?tab=users',
  settings: '/settings?tab=company'
};
