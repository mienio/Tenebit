export type AssetStatus = 'Draft' | 'InStock' | 'Reserved' | 'Assigned' | 'InTransit' | 'InService' | 'Damaged' | 'Lost' | 'Retired' | 'Disposed';
export type AssetCategoryType = 'Physical' | 'Digital' | 'License' | 'Account' | 'Document' | 'Location' | 'Vehicle' | 'Key' | 'Consumable' | 'Other';
export type PersonRelationType = 'Employee' | 'Contractor' | 'Vendor';
export type ProcedureStatus = 'Draft' | 'Published' | 'Archived';
export type AssignmentStatus = 'Draft' | 'AwaitingAcceptance' | 'Accepted' | 'Returned' | 'Cancelled' | 'Overdue';
export type AcceptanceStatus = 'Pending' | 'Accepted' | 'Declined' | 'Overdue';
export type LocationType = 'Address' | 'Building' | 'Floor' | 'Room' | 'Warehouse' | 'Zone' | 'Shelf' | 'Other';

export interface LocationNode {
  id: string;
  name: string;
  type: LocationType;
  parentId?: string | null;
  fullPath: string;
  assetCount: number;
  personCount: number;
  isActive: boolean;
}

export interface LocationInventory {
  location: LocationNode;
  assets: Asset[];
  people: Person[];
}

export type AssetFieldType = 'Text' | 'Number' | 'Date' | 'Boolean' | 'Select' | 'Sensitive';

export interface AssetFieldDefinition {
  id: string;
  key: string;
  label: string;
  fieldType: AssetFieldType;
  options: string[];
  required: boolean;
}

export interface SaveAssetFieldDefinitionRequest {
  key: string;
  label: string;
  fieldType: AssetFieldType;
  options?: string | null;
  required: boolean;
}

export interface AssetCategory {
  id: string;
  name: string;
  type: AssetCategoryType;
  description?: string | null;
  icon?: string | null;
  isSystem: boolean;
  fieldDefinitions: AssetFieldDefinition[];
}

export interface Asset {
  id: string;
  name: string;
  assetTag: string;
  serialNumber?: string | null;
  categoryId: string;
  categoryName?: string | null;
  status: AssetStatus;
  assignedPersonId?: string | null;
  assignedPersonName?: string | null;
  location?: string | null;
  manufacturer?: string | null;
  model?: string | null;
  purchasePrice?: number | null;
  currency?: string | null;
  purchaseDate?: string | null;
  warrantyUntil?: string | null;
  qrCodePayload: string;
  updatedAt: string;
  customFields: Record<string, string>;
  categoryFieldDefinitions: AssetFieldDefinition[];
  teamId?: string | null;
  teamName?: string | null;
}

export interface CreateAssetRequest {
  name: string;
  assetTag: string;
  serialNumber?: string | null;
  categoryId: string;
  location?: string | null;
  manufacturer?: string | null;
  model?: string | null;
  purchasePrice?: number | null;
  currency?: string | null;
  purchaseDate?: string | null;
  warrantyUntil?: string | null;
  teamId?: string | null;
  customFields?: Record<string, string> | null;
}

export interface Person {
  id: string;
  firstName: string;
  lastName: string;
  fullName: string;
  email: string;
  phone?: string | null;
  employeeNumber?: string | null;
  relationType: PersonRelationType;
  jobTitle?: string | null;
  teamId?: string | null;
  teamName?: string | null;
  managerId?: string | null;
  location?: string | null;
  costCenter?: string | null;
  isActive: boolean;
}

export interface Team {
  id: string;
  name: string;
  managerId?: string | null;
  costCenter?: string | null;
}

export interface ProcedureDocument {
  id: string;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  uploadedAt: string;
  uploadedBy: string;
}

export interface ProcedureAcceptanceStatus {
  personId: string;
  personName: string;
  status: AcceptanceStatus;
  sentAt: string;
  acceptedAt?: string | null;
  protocolNumber?: string | null;
  confirmedIp?: string | null;
  isIntegrityVerified: boolean;
}

export interface Procedure {
  id: string;
  title: string;
  version: string;
  owner: string;
  status: ProcedureStatus;
  appliesTo?: string | null;
  reviewDate?: string | null;
  requiresAcceptance: boolean;
  documents: ProcedureDocument[];
  createdAt: string;
  publishedAt?: string | null;
}

export interface AssignmentAssetRequest {
  assetId: string;
  issueCondition?: string | null;
}

export interface CreateAssignmentRequest {
  personId: string;
  assets: AssignmentAssetRequest[];
  procedureIds: string[];
  dueDate?: string | null;
  notes?: string | null;
}

export interface AssignmentAsset {
  assetId: string;
  assetName?: string | null;
  assetTag?: string | null;
  issueCondition: string;
  returnCondition?: string | null;
}

export interface ProcedureAcceptance {
  id: string;
  procedureId: string;
  procedureTitle?: string | null;
  status: AcceptanceStatus;
  sentAt: string;
  acceptedAt?: string | null;
  confirmedIp?: string | null;
  confirmationHash?: string | null;
  isIntegrityVerified: boolean;
}

export interface Assignment {
  id: string;
  personId: string;
  personName?: string | null;
  status: AssignmentStatus;
  issuedAt: string;
  dueDate?: string | null;
  acceptedAt?: string | null;
  returnedAt?: string | null;
  protocolNumber: string;
  notes?: string | null;
  assets: AssignmentAsset[];
  procedureAcceptances: ProcedureAcceptance[];
  acceptedIp?: string | null;
  acceptanceHash?: string | null;
  isIntegrityVerified: boolean;
}

export interface PublicAssignmentAsset {
  name: string;
  assetTag: string;
  issueCondition: string;
}

export interface PublicAssignment {
  organizationName: string;
  protocolNumber: string;
  status: AssignmentStatus;
  personFirstName: string;
  assets: PublicAssignmentAsset[];
  procedureTitlesRequiringAcceptance: string[];
}

export interface DashboardSummary {
  totalAssets: number;
  assetsInStock: number;
  assetsAssigned: number;
  assetsInService: number;
  assetsWithoutOwner: number;
  peopleCount: number;
  openAssignments: number;
  pendingProcedureAcceptances: number;
  visibleAssetValue: number;
  assetsByStatus: { status: AssetStatus; count: number }[];
  warrantyExpiringSoon: { assetId: string; name: string; assetTag: string; warrantyUntil: string }[];
  recentActivity: { action: string; entityType: string; entityId?: string | null; displayName?: string | null; actor: string; createdAt: string }[];
  assetsByCategory: { categoryId: string; categoryName: string; count: number }[];
  assetsByLocation: { location: string; count: number }[];
  assetsByTeam: { teamId: string | null; teamName: string; count: number; totalValue: number }[];
}

export interface MyAsset {
  id: string;
  name: string;
  assetTag: string;
  categoryName?: string | null;
  location?: string | null;
  warrantyUntil?: string | null;
}

export interface MyProcedure {
  procedureId: string;
  title?: string | null;
  status: AcceptanceStatus;
  documentId?: string | null;
  documentFileName?: string | null;
}

export interface MyAssignment {
  id: string;
  protocolNumber: string;
  status: AssignmentStatus;
  issuedAt: string;
  dueDate?: string | null;
  assetNames: string[];
  procedures: MyProcedure[];
}

export interface MyWorkspace {
  hasPersonRecord: boolean;
  personName?: string | null;
  assets: MyAsset[];
  assignments: MyAssignment[];
}

export interface OnboardingStatus {
  steps: { key: string; label: string; completed: boolean; nextAction: string }[];
  completionPercent: number;
}

export interface CreateStarterPackageRequest {
  teamName: string;
  employeeFirstName: string;
  employeeLastName: string;
  employeeEmail: string;
  jobTitle?: string | null;
  assetName: string;
  assetTag: string;
  serialNumber?: string | null;
  categoryName: string;
  location?: string | null;
  procedureTitle: string;
  procedureUrl?: string | null;
  returnDueDate?: string | null;
}

export interface StarterPackageResponse {
  personId: string;
  assetId: string;
  procedureId: string;
  assignmentId: string;
  protocolNumber: string;
  message: string;
}

export interface CreateEmployeePackageRequest {
  personId: string;
  jobProfileId?: string | null;
  assetIds: string[];
  procedureIds: string[];
  dueDate?: string | null;
  notes?: string | null;
}

export interface EmployeePackageResponse {
  assignmentId: string;
  protocolNumber: string;
  assignment: Assignment;
  warnings: string[];
}

export interface OnboardingChecklistItem {
  type: 'asset' | 'procedure';
  itemId: string;
  label: string;
  status: AssignmentStatus | AcceptanceStatus;
  completedAt?: string | null;
}

export interface OnboardingChecklist {
  personId: string;
  personName: string;
  items: OnboardingChecklistItem[];
  completedCount: number;
  totalCount: number;
}

export interface Organization {
  id: string;
  name: string;
  country: string;
  language: string;
  currency: string;
  timeZone: string;
  logoUrl?: string | null;
}


export interface AssetStatusSetting {
  statusKey: AssetStatus;
  label: string;
  sortOrder: number;
  isEnabled: boolean;
}

export interface JobProfile {
  id: string;
  name: string;
  description?: string | null;
  defaultManagerId?: string | null;
  assetCategoryIds: string[];
  procedureIds: string[];
  createdAt: string;
}

export interface OrganizationUser {
  id: string;
  email: string;
  displayName: string;
  isActive: boolean;
  roles: string[];
  createdAt: string;
}

export interface RoleInfo {
  key: string;
  label: string;
  description: string;
}

export interface ActivityLogEntry {
  id: string;
  action: string;
  entityType: string;
  entityId?: string | null;
  details?: string | null;
  actorDisplay: string;
  createdAt: string;
}

export interface PagedActivityLog {
  items: ActivityLogEntry[];
  total: number;
  page: number;
  pageSize: number;
}

export interface Subscription {
  id: string;
  planKey: string;
  planName: string;
  assetLimit: number;
  monthlyPrice: number;
  currency: string;
  currentAssetCount: number;
  status: string;
  currentPeriodEnd: string;
}
