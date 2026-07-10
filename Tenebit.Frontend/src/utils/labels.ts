import type { AssetCategoryType, AssetStatus, AssignmentStatus, LocationType, PersonRelationType } from '../types/domain';

export const assetStatusValues: AssetStatus[] = ['Draft', 'InStock', 'Reserved', 'Assigned', 'InTransit', 'InService', 'Damaged', 'Lost', 'Retired', 'Disposed'];

export const assignmentStatusValues: AssignmentStatus[] = ['Draft', 'AwaitingAcceptance', 'Accepted', 'Returned', 'Cancelled', 'Overdue'];

export const categoryTypeValues: AssetCategoryType[] = ['Physical', 'Digital', 'License', 'Account', 'Document', 'Location', 'Vehicle', 'Key', 'Consumable', 'Other'];

export const personRelationValues: PersonRelationType[] = ['Employee', 'Contractor', 'Vendor'];

export const locationTypeValues: LocationType[] = ['Address', 'Building', 'Floor', 'Room', 'Warehouse', 'Zone', 'Shelf', 'Other'];
