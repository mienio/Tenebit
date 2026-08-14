export type AssetStatus = 'Draft' | 'InStock' | 'Reserved' | 'Assigned' | 'PendingReturn' | 'InTransit' | 'InService' | 'Damaged' | 'Lost' | 'Retired' | 'Disposed';
export type AssetCategoryType = 'Physical' | 'Digital' | 'License' | 'Account' | 'Document' | 'Location' | 'Vehicle' | 'Key' | 'Consumable' | 'Other';
export type PersonRelationType = string;
export type EmploymentStatus = 'Active' | 'Offboarding' | 'Inactive';
export type OffboardingCaseStatus = 'Draft' | 'Active' | 'WaitingForReturn' | 'ReadyToClose' | 'Completed' | 'Cancelled';
export type OffboardingItemType = 'AssetReturn' | 'LicenseRelease' | 'ManualTask';
export type OffboardingItemStatus = 'Pending' | 'EmployeeAcknowledged' | 'Received' | 'Inspecting' | 'Returned' | 'Released' | 'Missing' | 'Damaged' | 'Retained' | 'Waived';
export type OffboardingItemAutomationMode = 'Manual' | 'AtEmploymentEnd';
export type InspectionOutcome = 'ReadyForReuse' | 'Damaged' | 'Retired' | 'Disposed';

export function getEmploymentStatusPresentation(status: EmploymentStatus) {
  switch (status) {
    case 'Active': return { labelKey: 'people.active', badgeClass: 'status--InStock', action: 'deactivate' } as const;
    case 'Offboarding': return { labelKey: 'people.offboarding', badgeClass: 'status--AwaitingAcceptance', action: 'deactivate' } as const;
    case 'Inactive': return { labelKey: 'people.inactive', badgeClass: 'status--Draft', action: 'activate' } as const;
  }
}

export interface PersonRelationTypeOption {
  id: string;
  name: string;
}

export interface LicenseSeat {
  personId: string;
  personName: string;
  assignedAt: string;
}

export interface License {
  id: string;
  name: string;
  vendor?: string | null;
  licenseKey?: string | null;
  hasLicenseKey: boolean;
  canViewLicenseKey: boolean;
  seatsTotal: number;
  seatsAssigned: number;
  expiresAt?: string | null;
  notes?: string | null;
  seats: LicenseSeat[];
}

export interface RolePermission {
  roleKey: string;
  roleLabel: string;
  permissionKey: string;
  permissionLabel: string;
  permissionDescription: string;
  allowed: boolean;
}
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
  isReservable: boolean;
  maxReservationDays?: number | null;
  reservationInstructions?: string | null;
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
  isReservable: boolean;
  maxReservationDays?: number | null;
  reservationInstructions?: string | null;
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
  employmentStatus: EmploymentStatus;
  employmentEndsAt?: string | null;
  deactivatedAt?: string | null;
  preferredLanguage?: string | null;
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
  acceptanceLink: string;
  acceptedIp?: string | null;
  acceptanceHash?: string | null;
  isIntegrityVerified: boolean;
}

export interface PublicAssignmentAsset {
  name: string;
  assetTag: string;
  issueCondition: string;
  assetId: string;
  evidenceIds: string[];
}

export type EvidencePhase = 'Issue' | 'Return' | 'Audit' | 'Offboarding';

export interface AssetEvidence {
  id: string;
  assetId: string;
  assignmentId?: string | null;
  phase: EvidencePhase;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  sha256: string;
  caption?: string | null;
  uploadedAt: string;
  uploadedBy: string;
  lockedAt?: string | null;
  legalHold: boolean;
  redactedAt?: string | null;
}

export type AlertType =
  | 'AssetWarrantyExpiring'
  | 'LicenseExpiring'
  | 'ProcedureReviewDue'
  | 'AssignmentReturnDue'
  | 'AssignmentNotConfirmed'
  | 'OffboardingReturnDue'
  | 'AssetAuditNoResponse'
  | 'ReservationAwaitingApproval'
  | 'ReservationPickupUpcoming'
  | 'ReservationOverdue';

export type AlertDeliveryMode = 'Immediate' | 'Digest' | 'Both';
export type AlertRecipientMode = 'OwnersAndAdmins' | 'ResponsibleRoles' | 'ResponsiblePerson' | 'Custom';
export type AlertDigestFrequency = 'Off' | 'Daily' | 'Weekly';
export type SentAlertStatus = 'Pending' | 'Sent' | 'Failed' | 'IncludedInDigest';

export interface AlertRule {
  type: AlertType;
  isEnabled: boolean;
  thresholdDays: number[];
  deliveryMode: AlertDeliveryMode;
  recipientMode: AlertRecipientMode;
  customEmails: string | null;
  cooldownDays: number;
}

export interface AlertDigestSettings {
  frequency: AlertDigestFrequency;
  dayOfWeek: string | null;
  localTime: string;
  quietHoursStart: string | null;
  quietHoursEnd: string | null;
  businessDays: string | number;
  holidayCalendarCountryCode: string | null;
  includeEmptyDigest: boolean;
}

export interface SentAlertHistoryItem {
  id: string;
  type: AlertType;
  entityId: string;
  recipientEmail: string;
  status: SentAlertStatus;
  createdAt: string;
  sentAt: string | null;
  lastError: string | null;
}

export interface SaveAlertRuleRequest {
  isEnabled: boolean;
  thresholdDays: number[];
  deliveryMode: AlertDeliveryMode;
  recipientMode: AlertRecipientMode;
  customEmails: string | null;
  cooldownDays: number;
}

export interface SaveAlertDigestSettingsRequest {
  frequency: AlertDigestFrequency;
  dayOfWeek: string | null;
  localTime: string;
  quietHoursStart: string | null;
  quietHoursEnd: string | null;
  businessDays: number;
  holidayCalendarCountryCode: string | null;
  includeEmptyDigest: boolean;
}

export interface Paged<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
}

export interface PublicAssignmentDocument {
  id: string;
  fileName: string;
}

export interface PublicAssignmentProcedure {
  id: string;
  title: string;
  version: string;
  documents: PublicAssignmentDocument[];
}

export interface PublicAssignment {
  organizationName: string;
  protocolNumber: string;
  status: AssignmentStatus;
  personFirstName: string;
  assets: PublicAssignmentAsset[];
  proceduresRequiringAcceptance: PublicAssignmentProcedure[];
}

export interface PublicOffboardingItem {
  id: string;
  label: string;
  assetTag?: string | null;
  status: OffboardingItemStatus;
  employeeResponse?: string | null;
  employeeComment?: string | null;
  issuePhotoEvidenceId?: string | null;
}

export interface PublicOffboarding {
  organizationName: string;
  returnDueDate: string;
  defaultReturnLocation?: string | null;
  notes?: string | null;
  items: PublicOffboardingItem[];
}

export interface PublicOffboardingAnswer {
  itemId: string;
  response: string;
  comment?: string | null;
}

export interface CreateOffboardingCaseRequest {
  personId: string;
  employmentEndsAt: string;
  returnDueDate: string;
  defaultReturnLocation?: string | null;
  notes?: string | null;
  processOwnerId?: string | null;
  blockNewReservations: boolean;
  cancelFutureReservations: boolean;
  autoReleaseLicenses: boolean;
}

export interface UpdateOffboardingCaseRequest {
  employmentEndsAt: string;
  returnDueDate: string;
  defaultReturnLocation?: string | null;
  notes?: string | null;
  processOwnerId?: string | null;
  blockNewReservations: boolean;
  cancelFutureReservations: boolean;
  autoReleaseLicenses: boolean;
}

export interface OffboardingCaseSummary {
  id: string;
  personId: string;
  personName?: string | null;
  status: OffboardingCaseStatus;
  employmentEndsAt: string;
  returnDueDate: string;
  defaultReturnLocation?: string | null;
  notes?: string | null;
  processOwnerId?: string | null;
  blockNewReservations: boolean;
  cancelFutureReservations: boolean;
  autoReleaseLicenses: boolean;
  personDeactivatedAt?: string | null;
  scheduledActionsCompletedAt?: string | null;
  createdAt: string;
  createdBy: string;
  startedAt?: string | null;
  completedAt?: string | null;
  completedBy?: string | null;
  cancelledAt?: string | null;
  cancellationReason?: string | null;
  finalProtocolNumber?: string | null;
}

export interface OffboardingItem {
  id: string;
  type: OffboardingItemType;
  assetId?: string | null;
  assignmentId?: string | null;
  licenseId?: string | null;
  label: string;
  required: boolean;
  status: OffboardingItemStatus;
  employeeResponse?: string | null;
  employeeComment?: string | null;
  automationMode: OffboardingItemAutomationMode;
  automationLastAttemptAt?: string | null;
  automationError?: string | null;
  receivedAt?: string | null;
  receivedBy?: string | null;
  inspectionCompletedAt?: string | null;
  inspectionCompletedBy?: string | null;
  resolutionNotes?: string | null;
  completedAt?: string | null;
  completedBy?: string | null;
  sortOrder: number;
}

export interface OffboardingCaseDetails {
  case: OffboardingCaseSummary;
  items: OffboardingItem[];
}

export type AssetAuditCampaignStatus = 'Draft' | 'Active' | 'Reviewing' | 'Completed' | 'Cancelled';
export type AssetAuditParticipantStatus = 'Pending' | 'InProgress' | 'Submitted' | 'Reviewed';
export type AssetAuditResponse = 'Pending' | 'Confirmed' | 'Missing' | 'Damaged' | 'WrongOwner';
export type AssetAuditResolution = 'None' | 'Accepted' | 'AssetMarkedLost' | 'AssetMarkedDamaged' | 'OwnershipCorrected' | 'Dismissed';
export type AssetAuditScopeType = 'Organization' | 'Team' | 'Location' | 'AssetCategory' | 'Person';

export interface AssetAuditScope {
  type: AssetAuditScopeType;
  teamIds?: string[] | null;
  locations?: string[] | null;
  assetCategoryIds?: string[] | null;
  personIds?: string[] | null;
}

export interface CreateAssetAuditCampaignRequest {
  name: string;
  description?: string | null;
  dueDate: string;
  scope: AssetAuditScope;
}

export type UpdateAssetAuditCampaignRequest = CreateAssetAuditCampaignRequest;

export interface AssetAuditCampaignPreviewResponse {
  participantCount: number;
  assetCount: number;
  peopleWithoutEmail: string[];
}

export interface AssetAuditCampaignResponse {
  id: string;
  name: string;
  description?: string | null;
  status: AssetAuditCampaignStatus;
  dueDate: string;
  createdAt: string;
  createdBy: string;
  startedAt?: string | null;
  completedAt?: string | null;
  completedBy?: string | null;
}

export interface AssetAuditParticipantResponse {
  id: string;
  personId: string;
  personName?: string | null;
  email: string;
  status: AssetAuditParticipantStatus;
  submittedAt?: string | null;
  lastReminderAt?: string | null;
  itemCount: number;
}

export interface AssetAuditItemAdminResponse {
  id: string;
  participantId: string;
  participantName?: string | null;
  assetId: string;
  assetName: string;
  assetTag: string;
  expectedLocation?: string | null;
  response: AssetAuditResponse;
  comment?: string | null;
  respondedAt?: string | null;
  resolution: AssetAuditResolution;
  resolutionNotes?: string | null;
  resolvedBy?: string | null;
  resolvedAt?: string | null;
}

export interface AssetAuditCampaignDetailsResponse {
  campaign: AssetAuditCampaignResponse;
  participants: AssetAuditParticipantResponse[];
  items: AssetAuditItemAdminResponse[];
}

export interface RemindParticipantsResponse {
  remindedCount: number;
}

export interface ResolveAssetAuditItemRequest {
  resolution: AssetAuditResolution;
  notes?: string | null;
  newOwnerPersonId?: string | null;
}

export interface PublicAssetAuditItemResponse {
  id: string;
  assetName: string;
  assetTag: string;
  model?: string | null;
  response: AssetAuditResponse;
  comment?: string | null;
  photoEvidenceId?: string | null;
}

export interface PublicAssetAuditResponse {
  organizationName: string;
  campaignName: string;
  dueDate: string;
  readOnly: boolean;
  items: PublicAssetAuditItemResponse[];
}

export interface SubmitPublicAssetAuditItemRequest {
  response: AssetAuditResponse;
  comment?: string | null;
}

export interface DashboardComparison {
  comparedToDate: string;
  currentTotalAssets: number;
  previousTotalAssets: number;
  currentAssetsWithoutOwner: number;
  previousAssetsWithoutOwner: number;
  currentOpenAssignments: number;
  previousOpenAssignments: number;
  currentVisibleAssetValue: number;
  previousVisibleAssetValue: number;
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
  totalLicenses: number;
  licenseSeatsUsed: number;
  licenseSeatsTotal: number;
  assetsByStatus: { status: AssetStatus; count: number }[];
  warrantyExpiringSoon: { assetId: string; name: string; assetTag: string; warrantyUntil: string }[];
  recentActivity: { action: string; entityType: string; entityId?: string | null; displayName?: string | null; actor: string; createdAt: string }[];
  assetsByCategory: { categoryId: string; categoryName: string; count: number }[];
  assetsByLocation: { location: string; count: number }[];
  assetsByTeam: { teamId: string | null; teamName: string; count: number; totalValue: number }[];
  offboardingRequiringAttentionCount?: number;
}

export interface MyAsset {
  id: string;
  name: string;
  assetTag: string;
  categoryName?: string | null;
  categoryIcon?: string | null;
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
  assetConditions?: Record<string, string>;
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
  color: string;
  backgroundColor: string;
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

export type EquipmentReservationStatus = 'Draft' | 'PendingApproval' | 'Approved' | 'Rejected' | 'Cancelled' | 'ReadyForPickup' | 'CheckedOut' | 'Completed' | 'Expired';
export type EquipmentReservationItemStatus = 'Requested' | 'Allocated' | 'Approved' | 'Rejected' | 'Substituted' | 'CheckedOut' | 'Returned';
export type ReservationMode = 'RequestByCategory' | 'SelectExactAsset';

export interface ReservationCatalogCategory {
  id: string;
  name: string;
  description?: string | null;
  imageUrl?: string | null;
  reservationMode: ReservationMode;
  availableCount: number;
}

export interface ReservationCatalogKitItem {
  categoryId: string;
  categoryName: string;
  requiredQuantity: number;
}

export interface ReservationCatalogKit {
  id: string;
  name: string;
  description?: string | null;
  availableCount: number;
  items: ReservationCatalogKitItem[];
}

export interface ReservationCatalogResponse {
  hasPersonRecord: boolean;
  categories: ReservationCatalogCategory[];
  kits: ReservationCatalogKit[];
}

export interface ReservationItemRequest {
  categoryId?: string | null;
  kitDefinitionId?: string | null;
  quantity: number;
}

export interface CreateReservationRequest {
  startAt: string;
  endAt: string;
  purpose: string;
  pickupLocation?: string | null;
  notes?: string | null;
  items: ReservationItemRequest[];
}

export type UpdateReservationRequest = CreateReservationRequest;

export interface ReservationResponse {
  id: string;
  requesterPersonId: string;
  status: EquipmentReservationStatus;
  startAt: string;
  endAt: string;
  purpose: string;
  pickupLocation?: string | null;
  notes?: string | null;
  requestedAt?: string | null;
  approvedAt?: string | null;
  approvedBy?: string | null;
  rejectedAt?: string | null;
  rejectedBy?: string | null;
  decisionNotes?: string | null;
  cancelledAt?: string | null;
  cancelledBy?: string | null;
  cancellationReason?: string | null;
  createdAt: string;
}

export interface ReservationItemResponse {
  id: string;
  requestedCategoryId: string;
  requestedQuantity: number;
  kitDefinitionId?: string | null;
  assetId?: string | null;
  originalAssetId?: string | null;
  substitutionReason?: string | null;
  status: EquipmentReservationItemStatus;
}

export interface ReservationDetailsResponse {
  reservation: ReservationResponse;
  items: ReservationItemResponse[];
}

export interface ReservationAllocationRequest {
  itemId: string;
  assetId: string;
}

export interface ApproveReservationRequest {
  allocations: ReservationAllocationRequest[];
}

export interface ReservationCalendarItem {
  id: string;
  requesterPersonId: string;
  status: EquipmentReservationStatus;
  startAt: string;
  endAt: string;
  purpose: string;
  pickupLocation?: string | null;
  assetIds: string[];
  isConflicting: boolean;
  isDueToday: boolean;
  isOverdue: boolean;
}
