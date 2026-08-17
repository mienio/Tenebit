import { apiBlob, apiRequest } from './apiClient';
import type {
  AlertDigestSettings,
  AlertRule,
  AlertType,
  Asset,
  AssetCategory,
  AssetCategoryType,
  AssetEvidence,
  AssetFieldDefinition,
  AssetGroupCounts,
  AssetStatus,
  AssetStatusSetting,
  QrLabelSettings,
  Assignment,
  AssetAuditCampaignDetailsResponse,
  AssetAuditCampaignPreviewResponse,
  AssetAuditCampaignResponse,
  AssetAuditCampaignStatus,
  CreateAssetAuditCampaignRequest,
  UpdateAssetAuditCampaignRequest,
  PublicAssetAuditResponse,
  RemindParticipantsResponse,
  ResolveAssetAuditItemRequest,
  SubmitPublicAssetAuditItemRequest,
  CreateOffboardingCaseRequest,
  CreateAssignmentRequest,
  CreateAssetRequest,
  CreateEmployeePackageRequest,
  DashboardComparison,
  DashboardSummary,
  EmployeePackageResponse,
  JobProfile,
  License,
  LocationInventory,
  LocationNode,
  LocationType,
  MyWorkspace,
  OnboardingChecklist,
  OnboardingStatus,
  Organization,
  Paged,
  OrganizationUser,
  Person,
  PersonRelationType,
  PersonRelationTypeOption,
  Procedure,
  PublicAssignment,
  PublicOffboarding,
  PublicOffboardingAnswer,
  RoleInfo,
  RolePermission,
  SaveAlertDigestSettingsRequest,
  SaveAlertRuleRequest,
  SentAlertHistoryItem,
  OffboardingCaseDetails,
  OffboardingCaseStatus,
  OffboardingCaseSummary,
  OffboardingPreview,
  SaveAssetFieldDefinitionRequest,
  ServiceTicket,
  OpenServiceTicketRequest,
  CompleteServiceTicketRequest,
  CancelServiceTicketRequest,
  Team
} from '../types/domain';

export interface EvidencePhoto {
  assetId: string;
  caption?: string | null;
  file: File;
}

// Buduje multipart dla endpointów "with-evidence": część `request` (JSON), `evidenceManifest`
// (JSON mapujący nazwę pliku → { assetId, caption }) i pliki. Nazwy części plików są generowane
// (`photo_0`, `photo_1`, ...) i muszą pokrywać się z kluczami manifestu.
function buildEvidenceForm(request: unknown, photos: EvidencePhoto[]): FormData {
  const form = new FormData();
  form.set('request', JSON.stringify(request));
  const manifest: Record<string, { assetId: string; caption?: string | null }> = {};
  photos.forEach((photo, index) => {
    const name = `photo_${index}`;
    manifest[name] = { assetId: photo.assetId, caption: photo.caption ?? null };
    form.append(name, photo.file);
  });
  form.set('evidenceManifest', JSON.stringify(manifest));
  return form;
}

export const api = {
  dashboard: () => apiRequest<DashboardSummary>('/api/dashboard'),
  dashboardComparison: (daysAgo: number) => apiRequest<DashboardComparison>(`/api/dashboard/comparison?daysAgo=${daysAgo}`),
  dashboardLayout: () => apiRequest<{ layoutJson: string | null }>('/api/dashboard/layout'),
  saveDashboardLayout: (layoutJson: string) => apiRequest<{ layoutJson: string | null }>('/api/dashboard/layout', { method: 'PUT', body: JSON.stringify({ layoutJson }) }),
  organization: () => apiRequest<Organization>('/api/organization'),
  updateOrganization: (body: Omit<Organization, 'id'>) => apiRequest<Organization>('/api/organization', { method: 'PUT', body: JSON.stringify(body) }),

  onboardingStatus: () => apiRequest<OnboardingStatus>('/api/onboarding/status'),
  createEmployeePackage: (body: CreateEmployeePackageRequest) => apiRequest<EmployeePackageResponse>('/api/onboarding/employee-package', { method: 'POST', body: JSON.stringify(body) }),
  createEmployeePackageWithEvidence: (body: CreateEmployeePackageRequest, photos: EvidencePhoto[]) => apiRequest<EmployeePackageResponse>('/api/onboarding/employee-package/with-evidence', { method: 'POST', body: buildEvidenceForm(body, photos) }),
  onboardingChecklist: (personId: string) => apiRequest<OnboardingChecklist>(`/api/onboarding/checklist/${personId}`),

  locations: () => apiRequest<LocationNode[]>('/api/locations'),
  createLocation: (body: { name: string; type: LocationType; parentId?: string | null }) => apiRequest<LocationNode>('/api/locations', { method: 'POST', body: JSON.stringify(body) }),
  updateLocation: (id: string, body: { name: string; type: LocationType; parentId?: string | null; isActive: boolean }) => apiRequest<LocationNode>(`/api/locations/${id}`, { method: 'PUT', body: JSON.stringify(body) }),
  deleteLocation: (id: string) => apiRequest<void>(`/api/locations/${id}`, { method: 'DELETE' }),
  locationInventory: (id: string) => apiRequest<LocationInventory>(`/api/locations/${id}/inventory`),

  categories: () => apiRequest<AssetCategory[]>('/api/asset-categories'),
  createCategory: (body: { name: string; type: AssetCategoryType; description?: string | null; icon?: string | null }) => apiRequest<AssetCategory>('/api/asset-categories', { method: 'POST', body: JSON.stringify(body) }),
  updateCategory: (id: string, body: { name: string; type: AssetCategoryType; description?: string | null; icon?: string | null }) => apiRequest<AssetCategory>(`/api/asset-categories/${id}`, { method: 'PUT', body: JSON.stringify(body) }),
  deleteCategory: (id: string) => apiRequest<void>(`/api/asset-categories/${id}`, { method: 'DELETE' }),
  saveCategoryFields: (categoryId: string, body: SaveAssetFieldDefinitionRequest[]) => apiRequest<AssetFieldDefinition[]>(`/api/asset-categories/${categoryId}/fields`, { method: 'PUT', body: JSON.stringify(body) }),

  assetStatuses: () => apiRequest<AssetStatusSetting[]>('/api/asset-statuses'),
  saveAssetStatuses: (body: AssetStatusSetting[]) => apiRequest<AssetStatusSetting[]>('/api/asset-statuses', { method: 'PUT', body: JSON.stringify(body) }),
  qrLabelSettings: () => apiRequest<QrLabelSettings>('/api/settings/qr-label'),
  saveQrLabelSettings: (body: QrLabelSettings) => apiRequest<QrLabelSettings>('/api/settings/qr-label', { method: 'PUT', body: JSON.stringify(body) }),

  jobProfiles: () => apiRequest<JobProfile[]>('/api/job-profiles'),
  createJobProfile: (body: { name: string; description?: string | null; defaultManagerId?: string | null; assetCategoryIds: string[]; procedureIds: string[] }) => apiRequest<JobProfile>('/api/job-profiles', { method: 'POST', body: JSON.stringify(body) }),
  updateJobProfile: (id: string, body: { name: string; description?: string | null; defaultManagerId?: string | null; assetCategoryIds: string[]; procedureIds: string[] }) => apiRequest<JobProfile>(`/api/job-profiles/${id}`, { method: 'PUT', body: JSON.stringify(body) }),
  deleteJobProfile: (id: string) => apiRequest<void>(`/api/job-profiles/${id}`, { method: 'DELETE' }),

  users: () => apiRequest<OrganizationUser[]>('/api/organization-users'),
  createUser: (body: { email: string; displayName: string; isActive: boolean; roles: string[] }) => apiRequest<OrganizationUser>('/api/organization-users', { method: 'POST', body: JSON.stringify(body) }),
  updateUser: (id: string, body: { email: string; displayName: string; isActive: boolean; roles: string[] }) => apiRequest<OrganizationUser>(`/api/organization-users/${id}`, { method: 'PUT', body: JSON.stringify(body) }),
  roles: () => apiRequest<RoleInfo[]>('/api/roles'),
  rolePermissions: () => apiRequest<RolePermission[]>('/api/role-permissions'),
  setRolePermission: (body: { roleKey: string; permissionKey: string; allowed: boolean }) => apiRequest<void>('/api/role-permissions', { method: 'PUT', body: JSON.stringify(body) }),

  alertRules: () => apiRequest<AlertRule[]>('/api/settings/alerts'),
  saveAlertRule: (type: AlertType, body: SaveAlertRuleRequest) => apiRequest<AlertRule>(`/api/settings/alerts/${type}`, { method: 'PUT', body: JSON.stringify(body) }),
  alertDigest: () => apiRequest<AlertDigestSettings>('/api/settings/alert-digest'),
  saveAlertDigest: (body: SaveAlertDigestSettingsRequest) => apiRequest<AlertDigestSettings>('/api/settings/alert-digest', { method: 'PUT', body: JSON.stringify(body) }),
  sendTestAlert: (alertType?: AlertType) => apiRequest<void>('/api/settings/alerts/test', { method: 'POST', body: JSON.stringify(alertType ? { alertType } : {}) }),
  alertHistory: (page: number, pageSize: number) => apiRequest<Paged<SentAlertHistoryItem>>(`/api/alerts/history?page=${page}&pageSize=${pageSize}`),

  licenses: () => apiRequest<License[]>('/api/licenses'),
  createLicense: (body: { name: string; vendor?: string | null; licenseKey?: string | null; seatsTotal: number; expiresAt?: string | null; notes?: string | null }) => apiRequest<License>('/api/licenses', { method: 'POST', body: JSON.stringify(body) }),
  updateLicense: (id: string, body: { name: string; vendor?: string | null; licenseKey?: string | null; seatsTotal: number; expiresAt?: string | null; notes?: string | null }) => apiRequest<License>(`/api/licenses/${id}`, { method: 'PUT', body: JSON.stringify(body) }),
  deleteLicense: (id: string) => apiRequest<void>(`/api/licenses/${id}`, { method: 'DELETE' }),
  assignLicenseSeat: (id: string, personId: string) => apiRequest<License>(`/api/licenses/${id}/seats`, { method: 'POST', body: JSON.stringify({ personId }) }),
  unassignLicenseSeat: (id: string, personId: string) => apiRequest<License>(`/api/licenses/${id}/seats/${personId}`, { method: 'DELETE' }),

  subscription: () => apiRequest<import('../types/domain').Subscription>('/api/subscription'),
  upgradeSubscription: (planKey: string) => apiRequest<import('../types/domain').Subscription>('/api/subscription/upgrade', { method: 'POST', body: JSON.stringify({ planKey }) }),
  // successPath/cancelPath/returnPath are relative paths (e.g. "/dashboard?checkout=success") — the
  // backend builds the actual absolute redirect URL from its own configured origin, never from a
  // client-supplied full URL (audit AUD3-010, open redirect).
  createCheckoutSession: (successPath: string, cancelPath: string) => apiRequest<string>('/api/subscription/checkout', { method: 'POST', body: JSON.stringify({ successUrl: successPath, cancelUrl: cancelPath }) }),
  createBillingPortalSession: (returnPath: string) => apiRequest<string>('/api/subscription/billing-portal', { method: 'POST', body: JSON.stringify({ returnUrl: returnPath }) }),

  assets: (params?: { search?: string; status?: AssetStatus | ''; location?: string | '' }) => {
    const query = new URLSearchParams();
    if (params?.search) query.set('search', params.search);
    if (params?.status) query.set('status', params.status);
    if (params?.location) query.set('location', params.location);
    const suffix = query.toString() ? `?${query.toString()}` : '';
    return apiRequest<Asset[]>(`/api/assets${suffix}`);
  },
  assetsPaged: (params: { search?: string; status?: AssetStatus | ''; location?: string; teamId?: string; categoryId?: string; owner?: string; warranty?: string; sort?: string; desc?: boolean; page: number; pageSize: number }) => {
    const query = new URLSearchParams();
    if (params.search) query.set('search', params.search);
    if (params.status) query.set('status', params.status);
    if (params.location) query.set('location', params.location);
    if (params.teamId) query.set('teamId', params.teamId);
    if (params.categoryId) query.set('categoryId', params.categoryId);
    if (params.owner) query.set('owner', params.owner);
    if (params.warranty) query.set('warranty', params.warranty);
    if (params.sort) query.set('sort', params.sort);
    if (params.desc) query.set('desc', 'true');
    query.set('page', String(params.page));
    query.set('pageSize', String(params.pageSize));
    return apiRequest<Paged<Asset>>(`/api/assets?${query.toString()}`);
  },
  assetGroupCounts: () => apiRequest<AssetGroupCounts>('/api/assets/group-counts'),
  createAsset: (body: CreateAssetRequest) => apiRequest<Asset>('/api/assets', { method: 'POST', body: JSON.stringify(body) }),
  updateAsset: (id: string, body: CreateAssetRequest & { status: AssetStatus }) => apiRequest<Asset>(`/api/assets/${id}`, { method: 'PUT', body: JSON.stringify(body) }),
  deleteAsset: (id: string) => apiRequest<void>(`/api/assets/${id}`, { method: 'DELETE' }),
  getAsset: (id: string) => apiRequest<Asset>(`/api/assets/${id}`),
  assetQr: (id: string) => apiRequest<string>(`/api/assets/${id}/qr`),
  revealAssetField: (id: string, fieldKey: string) => apiRequest<string>(`/api/assets/${id}/fields/${encodeURIComponent(fieldKey)}/reveal`, { method: 'POST' }),
  assetEvidence: (assetId: string) => apiRequest<AssetEvidence[]>(`/api/assets/${assetId}/evidence`),
  assetServiceTickets: (assetId: string) => apiRequest<ServiceTicket[]>(`/api/assets/${assetId}/service-tickets`),
  openServiceTicket: (body: OpenServiceTicketRequest) => apiRequest<ServiceTicket>('/api/service-tickets', { method: 'POST', body: JSON.stringify(body) }),
  completeServiceTicket: (id: string, body: CompleteServiceTicketRequest) => apiRequest<ServiceTicket>(`/api/service-tickets/${id}/complete`, { method: 'POST', body: JSON.stringify(body) }),
  cancelServiceTicket: (id: string, body: CancelServiceTicketRequest) => apiRequest<ServiceTicket>(`/api/service-tickets/${id}/cancel`, { method: 'POST', body: JSON.stringify(body) }),
  evidenceBlob: (id: string) => apiBlob(`/api/evidence/${id}`),
  publicAssetScan: (organizationId: string, assetId: string) => apiRequest<{ organizationName: string }>(`/api/public/assets/${organizationId}/${assetId}`),
  reportAssetIssue: (organizationId: string, assetId: string, message: string) => apiRequest<void>(`/api/public/assets/${organizationId}/${assetId}/report`, { method: 'POST', body: JSON.stringify({ message }) }),

  teams: () => apiRequest<Team[]>('/api/teams'),
  createTeam: (body: { name: string; managerId?: string | null; costCenter?: string | null }) => apiRequest<Team>('/api/teams', { method: 'POST', body: JSON.stringify(body) }),
  updateTeam: (id: string, body: { name: string; managerId?: string | null; costCenter?: string | null }) => apiRequest<Team>(`/api/teams/${id}`, { method: 'PUT', body: JSON.stringify(body) }),
  deleteTeam: (id: string) => apiRequest<void>(`/api/teams/${id}`, { method: 'DELETE' }),

  personRelationTypes: () => apiRequest<PersonRelationTypeOption[]>('/api/person-relation-types'),
  createPersonRelationType: (body: { name: string }) => apiRequest<PersonRelationTypeOption>('/api/person-relation-types', { method: 'POST', body: JSON.stringify(body) }),
  updatePersonRelationType: (id: string, body: { name: string }) => apiRequest<PersonRelationTypeOption>(`/api/person-relation-types/${id}`, { method: 'PUT', body: JSON.stringify(body) }),
  deletePersonRelationType: (id: string) => apiRequest<void>(`/api/person-relation-types/${id}`, { method: 'DELETE' }),

  people: (search?: string) => apiRequest<Person[]>(`/api/people${search ? `?search=${encodeURIComponent(search)}` : ''}`),
  peoplePaged: (params: { search?: string; page: number; pageSize: number }) => {
    const query = new URLSearchParams();
    if (params.search) query.set('search', params.search);
    query.set('page', String(params.page));
    query.set('pageSize', String(params.pageSize));
    return apiRequest<Paged<Person>>(`/api/people?${query.toString()}`);
  },
  createPerson: (body: {
    firstName: string;
    lastName: string;
    email: string;
    phone?: string | null;
    employeeNumber?: string | null;
    relationType: PersonRelationType;
    jobTitle?: string | null;
    teamId?: string | null;
    managerId?: string | null;
    location?: string | null;
    costCenter?: string | null;
    preferredLanguage?: string | null;
  }) => apiRequest<Person>('/api/people', { method: 'POST', body: JSON.stringify(body) }),
  updatePerson: (id: string, body: {
    firstName: string;
    lastName: string;
    email: string;
    phone?: string | null;
    employeeNumber?: string | null;
    relationType: PersonRelationType;
    jobTitle?: string | null;
    teamId?: string | null;
    managerId?: string | null;
    location?: string | null;
    costCenter?: string | null;
    isActive: boolean;
    preferredLanguage?: string | null;
  }) => apiRequest<Person>(`/api/people/${id}`, { method: 'PUT', body: JSON.stringify(body) }),
  deletePerson: (id: string) => apiRequest<void>(`/api/people/${id}`, { method: 'DELETE' }),
  personWorkspace: (id: string) => apiRequest<MyWorkspace>(`/api/people/${id}/workspace`),

  procedures: (search?: string) => apiRequest<Procedure[]>(`/api/procedures${search ? `?search=${encodeURIComponent(search)}` : ''}`),
  proceduresPaged: (params: { search?: string; page: number; pageSize: number }) => {
    const query = new URLSearchParams();
    if (params.search) query.set('search', params.search);
    query.set('page', String(params.page));
    query.set('pageSize', String(params.pageSize));
    return apiRequest<Paged<Procedure>>(`/api/procedures?${query.toString()}`);
  },
  createProcedure: (body: {
    title: string;
    version: string;
    owner: string;
    appliesTo?: string | null;
    reviewDate?: string | null;
    requiresAcceptance: boolean;
  }) => apiRequest<Procedure>('/api/procedures', { method: 'POST', body: JSON.stringify(body) }),
  updateProcedure: (id: string, body: {
    title: string;
    version: string;
    owner: string;
    appliesTo?: string | null;
    reviewDate?: string | null;
    requiresAcceptance: boolean;
  }) => apiRequest<Procedure>(`/api/procedures/${id}`, { method: 'PUT', body: JSON.stringify(body) }),
  uploadProcedureDocument: (id: string, file: File) => {
    const body = new FormData();
    body.set('file', file);
    return apiRequest<Procedure>(`/api/procedures/${id}/documents`, { method: 'POST', body });
  },
  downloadProcedureDocument: (procedureId: string, documentId: string) => apiBlob(`/api/procedures/${procedureId}/documents/${documentId}`),
  deleteProcedureDocument: (procedureId: string, documentId: string) => apiRequest<Procedure>(`/api/procedures/${procedureId}/documents/${documentId}`, { method: 'DELETE' }),
  publishProcedure: (id: string) => apiRequest<Procedure>(`/api/procedures/${id}/publish`, { method: 'POST' }),
  archiveProcedure: (id: string) => apiRequest<Procedure>(`/api/procedures/${id}/archive`, { method: 'POST' }),
  procedureAcceptances: (id: string) => apiRequest<import('../types/domain').ProcedureAcceptanceStatus[]>(`/api/procedures/${id}/acceptances`),

  assignments: () => apiRequest<Assignment[]>('/api/assignments'),
  assignmentsPaged: (params: { search?: string; status?: string; page: number; pageSize: number }) => {
    const query = new URLSearchParams();
    if (params.search) query.set('search', params.search);
    if (params.status) query.set('status', params.status);
    query.set('page', String(params.page));
    query.set('pageSize', String(params.pageSize));
    return apiRequest<Paged<Assignment>>(`/api/assignments?${query.toString()}`);
  },
  createAssignment: (body: CreateAssignmentRequest) => apiRequest<Assignment>('/api/assignments', { method: 'POST', body: JSON.stringify(body) }),
  createAssignmentWithEvidence: (body: CreateAssignmentRequest, photos: EvidencePhoto[]) => apiRequest<Assignment>('/api/assignments/with-evidence', { method: 'POST', body: buildEvidenceForm(body, photos) }),
  acceptAssignment: (id: string) => apiRequest<Assignment>(`/api/assignments/${id}/accept`, { method: 'POST' }),
  returnAssignment: (id: string, body: { returnCondition?: string | null; destinationLocation?: string | null; assets?: { assetId: string; returnCondition?: string | null }[] }) => apiRequest<Assignment>(`/api/assignments/${id}/return`, { method: 'POST', body: JSON.stringify(body) }),
  returnAssetWithEvidence: (assignmentId: string, assetId: string, body: { resolution: string; returnCondition?: string | null; returnLocation?: string | null; notes?: string | null }, photos: File[]) => {
    const form = new FormData();
    form.set('request', JSON.stringify(body));
    photos.forEach((file, index) => form.append(`photo_${index}`, file));
    return apiRequest<Assignment>(`/api/assignments/${assignmentId}/assets/${assetId}/return-with-evidence`, { method: 'POST', body: form });
  },
  downloadAssignmentProtocol: (id: string) => apiBlob(`/api/assignments/${id}/protocol`),

  offboardingPaged: (params: { status?: OffboardingCaseStatus | ''; page: number; pageSize: number }) => {
    const query = new URLSearchParams();
    if (params.status) query.set('status', params.status);
    query.set('page', String(params.page));
    query.set('pageSize', String(params.pageSize));
    return apiRequest<Paged<OffboardingCaseSummary>>(`/api/offboarding?${query.toString()}`);
  },
  offboarding: (id: string) => apiRequest<OffboardingCaseDetails>(`/api/offboarding/${id}`),
  offboardingPreview: (personId: string) => apiRequest<OffboardingPreview>(`/api/people/${personId}/offboarding-preview`),
  createOffboarding: (body: CreateOffboardingCaseRequest) => apiRequest<OffboardingCaseDetails>('/api/offboarding', { method: 'POST', body: JSON.stringify(body) }),
  updateOffboarding: (id: string, body: Omit<CreateOffboardingCaseRequest, 'personId'>) => apiRequest<OffboardingCaseDetails>(`/api/offboarding/${id}`, { method: 'PUT', body: JSON.stringify(body) }),
  startOffboarding: (id: string, body: { notifyEmployee: boolean }) => apiRequest<OffboardingCaseDetails>(`/api/offboarding/${id}/start`, { method: 'POST', body: JSON.stringify(body) }),
  resendOffboarding: (id: string) => apiRequest<OffboardingCaseDetails>(`/api/offboarding/${id}/resend`, { method: 'POST' }),
  regenerateOffboardingLink: (id: string) => apiRequest<string>(`/api/offboarding/${id}/regenerate-link`, { method: 'POST' }),
  executeOffboardingScheduledActions: (id: string) => apiRequest<OffboardingCaseDetails>(`/api/offboarding/${id}/execute-scheduled-actions`, { method: 'POST' }),
  confirmOffboardingItemReturn: (id: string, itemId: string, body: { returnCondition?: string | null; returnLocation?: string | null; notes?: string | null }) => apiRequest<OffboardingCaseDetails>(`/api/offboarding/${id}/items/${itemId}/confirm-return`, { method: 'POST', body: JSON.stringify(body) }),
  completeOffboardingInspection: (id: string, itemId: string, body: { outcome: string; serialNumberMatched: boolean; accessoriesComplete: boolean; dataWiped: boolean; functionalTestPassed: boolean; damageAssessmentNotes?: string | null; notes?: string | null }) => apiRequest<OffboardingCaseDetails>(`/api/offboarding/${id}/items/${itemId}/complete-inspection`, { method: 'POST', body: JSON.stringify(body) }),
  releaseOffboardingLicense: (id: string, itemId: string) => apiRequest<OffboardingCaseDetails>(`/api/offboarding/${id}/items/${itemId}/release-license`, { method: 'POST' }),
  resolveOffboardingItem: (id: string, itemId: string, body: { status: string; notes: string }) => apiRequest<OffboardingCaseDetails>(`/api/offboarding/${id}/items/${itemId}/resolve`, { method: 'POST', body: JSON.stringify(body) }),
  waiveOffboardingItem: (id: string, itemId: string, body: { reason: string }) => apiRequest<OffboardingCaseDetails>(`/api/offboarding/${id}/items/${itemId}/waive`, { method: 'POST', body: JSON.stringify(body) }),
  completeOffboarding: (id: string) => apiRequest<OffboardingCaseDetails>(`/api/offboarding/${id}/complete`, { method: 'POST' }),
  cancelOffboarding: (id: string, body: { reason: string }) => apiRequest<OffboardingCaseDetails>(`/api/offboarding/${id}/cancel`, { method: 'POST', body: JSON.stringify(body) }),
  restoreOffboardingEmployment: (id: string) => apiRequest<OffboardingCaseDetails>(`/api/offboarding/${id}/restore-employment`, { method: 'POST' }),
  downloadOffboardingProtocol: (id: string) => apiBlob(`/api/offboarding/${id}/protocol`),

  publicOffboarding: (token: string) => apiRequest<PublicOffboarding>(`/api/public/offboarding/${token}`),
  submitPublicOffboardingResponse: (token: string, body: { answers: PublicOffboardingAnswer[] }) => apiRequest<PublicOffboarding>(`/api/public/offboarding/${token}/response`, { method: 'POST', body: JSON.stringify(body) }),
  uploadPublicOffboardingEvidence: (token: string, itemId: string, file: File) => {
    const body = new FormData();
    body.set('file', file);
    return apiRequest<unknown>(`/api/public/offboarding/${token}/items/${itemId}/evidence`, { method: 'POST', body });
  },

  assetAuditsPaged: (params: { status?: AssetAuditCampaignStatus | ''; page: number; pageSize: number }) => {
    const query = new URLSearchParams();
    if (params.status) query.set('status', params.status);
    query.set('page', String(params.page));
    query.set('pageSize', String(params.pageSize));
    return apiRequest<Paged<AssetAuditCampaignResponse>>(`/api/asset-audits?${query.toString()}`);
  },
  assetAudit: (id: string) => apiRequest<AssetAuditCampaignDetailsResponse>(`/api/asset-audits/${id}`),
  createAssetAudit: (body: CreateAssetAuditCampaignRequest) => apiRequest<AssetAuditCampaignDetailsResponse>('/api/asset-audits', { method: 'POST', body: JSON.stringify(body) }),
  updateAssetAudit: (id: string, body: UpdateAssetAuditCampaignRequest) => apiRequest<AssetAuditCampaignDetailsResponse>(`/api/asset-audits/${id}`, { method: 'PUT', body: JSON.stringify(body) }),
  previewAssetAudit: (id: string) => apiRequest<AssetAuditCampaignPreviewResponse>(`/api/asset-audits/${id}/preview`, { method: 'POST' }),
  startAssetAudit: (id: string) => apiRequest<AssetAuditCampaignDetailsResponse>(`/api/asset-audits/${id}/start`, { method: 'POST' }),
  remindAssetAuditParticipants: (id: string) => apiRequest<RemindParticipantsResponse>(`/api/asset-audits/${id}/remind`, { method: 'POST' }),
  reopenAssetAuditParticipant: (id: string, participantId: string) => apiRequest<unknown>(`/api/asset-audits/${id}/participants/${participantId}/reopen`, { method: 'POST' }),
  resolveAssetAuditItem: (id: string, itemId: string, body: ResolveAssetAuditItemRequest) => apiRequest<unknown>(`/api/asset-audits/${id}/items/${itemId}/resolve`, { method: 'POST', body: JSON.stringify(body) }),
  completeAssetAudit: (id: string) => apiRequest<unknown>(`/api/asset-audits/${id}/complete`, { method: 'POST' }),
  cancelAssetAudit: (id: string) => apiRequest<unknown>(`/api/asset-audits/${id}/cancel`, { method: 'POST' }),
  downloadAssetAuditCsv: (id: string) => apiBlob(`/api/asset-audits/${id}/export.csv`),
  downloadAssetAuditReport: (id: string) => apiBlob(`/api/asset-audits/${id}/report.pdf`),
  downloadAssetsCsv: (params?: { search?: string; status?: string; location?: string }) => {
    const query = new URLSearchParams();
    if (params?.search) query.set('search', params.search);
    if (params?.status) query.set('status', params.status);
    if (params?.location) query.set('location', params.location);
    const suffix = query.toString() ? `?${query.toString()}` : '';
    return apiBlob(`/api/assets/export.csv${suffix}`);
  },
  downloadAssetsJson: (params?: { search?: string; status?: string; location?: string }) => {
    const query = new URLSearchParams();
    if (params?.search) query.set('search', params.search);
    if (params?.status) query.set('status', params.status);
    if (params?.location) query.set('location', params.location);
    const suffix = query.toString() ? `?${query.toString()}` : '';
    return apiBlob(`/api/assets/export.json${suffix}`);
  },

  publicAssetAudit: (token: string) => apiRequest<PublicAssetAuditResponse>(`/api/public/asset-audits/${token}`),
  submitPublicAssetAuditItemResponse: (token: string, itemId: string, body: SubmitPublicAssetAuditItemRequest) => apiRequest<PublicAssetAuditResponse>(`/api/public/asset-audits/${token}/items/${itemId}`, { method: 'PUT', body: JSON.stringify(body) }),
  submitPublicAssetAudit: (token: string) => apiRequest<PublicAssetAuditResponse>(`/api/public/asset-audits/${token}/submit`, { method: 'POST' }),
  uploadPublicAssetAuditEvidence: (token: string, itemId: string, file: File) => {
    const body = new FormData();
    body.set('file', file);
    return apiRequest<unknown>(`/api/public/asset-audits/${token}/items/${itemId}/evidence`, { method: 'POST', body });
  },

  publicAssignment: (token: string) => apiRequest<PublicAssignment>(`/api/public/assignments/${token}`),
  acceptPublicAssignment: (token: string) => apiRequest<PublicAssignment>(`/api/public/assignments/${token}/accept`, { method: 'POST' }),
  downloadPublicAssignmentProtocol: (token: string) => apiBlob(`/api/public/assignments/${token}/protocol`),
  downloadPublicProcedureDocument: (token: string, procedureId: string, documentId: string) => apiBlob(`/api/public/assignments/${token}/procedures/${procedureId}/documents/${documentId}`),
  publicAssignmentEvidence: (token: string, id: string) => apiBlob(`/api/public/assignments/${token}/evidence/${id}`),
  regenerateAssignmentAcceptanceLink: (id: string) => apiRequest<{ link: string }>(`/api/assignments/${id}/acceptance-link`, { method: 'POST' }),

  myWorkspace: () => apiRequest<MyWorkspace>('/api/my/workspace'),

  activityLog: (params?: { page?: number; pageSize?: number; entityType?: string; entityId?: string; search?: string; dateFrom?: string; dateTo?: string; actor?: string; action?: string }) => {
    const query = new URLSearchParams();
    if (params?.page) query.set('page', String(params.page));
    if (params?.pageSize) query.set('pageSize', String(params.pageSize));
    if (params?.entityType) query.set('entityType', params.entityType);
    if (params?.entityId) query.set('entityId', params.entityId);
    if (params?.search) query.set('search', params.search);
    if (params?.dateFrom) query.set('dateFrom', params.dateFrom);
    if (params?.dateTo) query.set('dateTo', params.dateTo);
    if (params?.actor) query.set('actor', params.actor);
    if (params?.action) query.set('action', params.action);
    const suffix = query.toString() ? `?${query.toString()}` : '';
    return apiRequest<import('../types/domain').PagedActivityLog>(`/api/activity-log${suffix}`);
  }
};
