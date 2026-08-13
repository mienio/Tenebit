import { apiBlob, apiRequest } from './apiClient';
import type {
  Asset,
  AssetCategory,
  AssetCategoryType,
  AssetFieldDefinition,
  AssetStatus,
  AssetStatusSetting,
  Assignment,
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
  RoleInfo,
  RolePermission,
  SaveAssetFieldDefinitionRequest,
  Team
} from '../types/domain';

export const api = {
  dashboard: () => apiRequest<DashboardSummary>('/api/dashboard'),
  dashboardComparison: (daysAgo: number) => apiRequest<DashboardComparison>(`/api/dashboard/comparison?daysAgo=${daysAgo}`),
  dashboardLayout: () => apiRequest<{ layoutJson: string | null }>('/api/dashboard/layout'),
  saveDashboardLayout: (layoutJson: string) => apiRequest<{ layoutJson: string | null }>('/api/dashboard/layout', { method: 'PUT', body: JSON.stringify({ layoutJson }) }),
  organization: () => apiRequest<Organization>('/api/organization'),
  updateOrganization: (body: Omit<Organization, 'id'>) => apiRequest<Organization>('/api/organization', { method: 'PUT', body: JSON.stringify(body) }),

  onboardingStatus: () => apiRequest<OnboardingStatus>('/api/onboarding/status'),
  createEmployeePackage: (body: CreateEmployeePackageRequest) => apiRequest<EmployeePackageResponse>('/api/onboarding/employee-package', { method: 'POST', body: JSON.stringify(body) }),
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

  licenses: () => apiRequest<License[]>('/api/licenses'),
  createLicense: (body: { name: string; vendor?: string | null; licenseKey?: string | null; seatsTotal: number; expiresAt?: string | null; notes?: string | null }) => apiRequest<License>('/api/licenses', { method: 'POST', body: JSON.stringify(body) }),
  updateLicense: (id: string, body: { name: string; vendor?: string | null; licenseKey?: string | null; seatsTotal: number; expiresAt?: string | null; notes?: string | null }) => apiRequest<License>(`/api/licenses/${id}`, { method: 'PUT', body: JSON.stringify(body) }),
  deleteLicense: (id: string) => apiRequest<void>(`/api/licenses/${id}`, { method: 'DELETE' }),
  assignLicenseSeat: (id: string, personId: string) => apiRequest<License>(`/api/licenses/${id}/seats`, { method: 'POST', body: JSON.stringify({ personId }) }),
  unassignLicenseSeat: (id: string, personId: string) => apiRequest<License>(`/api/licenses/${id}/seats/${personId}`, { method: 'DELETE' }),

  subscription: () => apiRequest<import('../types/domain').Subscription>('/api/subscription'),
  upgradeSubscription: (planKey: string) => apiRequest<import('../types/domain').Subscription>('/api/subscription/upgrade', { method: 'POST', body: JSON.stringify({ planKey }) }),
  createCheckoutSession: (successUrl: string, cancelUrl: string) => apiRequest<string>('/api/subscription/checkout', { method: 'POST', body: JSON.stringify({ successUrl, cancelUrl }) }),
  createBillingPortalSession: (returnUrl: string) => apiRequest<string>('/api/subscription/billing-portal', { method: 'POST', body: JSON.stringify({ returnUrl }) }),

  assets: (params?: { search?: string; status?: AssetStatus | ''; location?: string | '' }) => {
    const query = new URLSearchParams();
    if (params?.search) query.set('search', params.search);
    if (params?.status) query.set('status', params.status);
    if (params?.location) query.set('location', params.location);
    const suffix = query.toString() ? `?${query.toString()}` : '';
    return apiRequest<Asset[]>(`/api/assets${suffix}`);
  },
  assetsPaged: (params: { search?: string; status?: AssetStatus | ''; location?: string; teamId?: string; owner?: string; warranty?: string; sort?: string; desc?: boolean; page: number; pageSize: number }) => {
    const query = new URLSearchParams();
    if (params.search) query.set('search', params.search);
    if (params.status) query.set('status', params.status);
    if (params.location) query.set('location', params.location);
    if (params.teamId) query.set('teamId', params.teamId);
    if (params.owner) query.set('owner', params.owner);
    if (params.warranty) query.set('warranty', params.warranty);
    if (params.sort) query.set('sort', params.sort);
    if (params.desc) query.set('desc', 'true');
    query.set('page', String(params.page));
    query.set('pageSize', String(params.pageSize));
    return apiRequest<Paged<Asset>>(`/api/assets?${query.toString()}`);
  },
  createAsset: (body: CreateAssetRequest) => apiRequest<Asset>('/api/assets', { method: 'POST', body: JSON.stringify(body) }),
  updateAsset: (id: string, body: CreateAssetRequest & { status: AssetStatus }) => apiRequest<Asset>(`/api/assets/${id}`, { method: 'PUT', body: JSON.stringify(body) }),
  deleteAsset: (id: string) => apiRequest<void>(`/api/assets/${id}`, { method: 'DELETE' }),
  getAsset: (id: string) => apiRequest<Asset>(`/api/assets/${id}`),
  assetQr: (id: string) => apiRequest<string>(`/api/assets/${id}/qr`),
  revealAssetField: (id: string, fieldKey: string) => apiRequest<string>(`/api/assets/${id}/fields/${encodeURIComponent(fieldKey)}/reveal`, { method: 'POST' }),
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
  acceptAssignment: (id: string) => apiRequest<Assignment>(`/api/assignments/${id}/accept`, { method: 'POST' }),
  returnAssignment: (id: string, body: { returnCondition?: string | null; destinationLocation?: string | null; assets?: { assetId: string; returnCondition?: string | null }[] }) => apiRequest<Assignment>(`/api/assignments/${id}/return`, { method: 'POST', body: JSON.stringify(body) }),
  downloadAssignmentProtocol: (id: string) => apiBlob(`/api/assignments/${id}/protocol`),

  publicAssignment: (organizationId: string, assignmentId: string) => apiRequest<PublicAssignment>(`/api/public/assignments/${organizationId}/${assignmentId}`),
  acceptPublicAssignment: (organizationId: string, assignmentId: string) => apiRequest<PublicAssignment>(`/api/public/assignments/${organizationId}/${assignmentId}/accept`, { method: 'POST' }),
  downloadPublicAssignmentProtocol: (organizationId: string, assignmentId: string) => apiBlob(`/api/public/assignments/${organizationId}/${assignmentId}/protocol`),
  downloadPublicProcedureDocument: (organizationId: string, assignmentId: string, procedureId: string, documentId: string) => apiBlob(`/api/public/assignments/${organizationId}/${assignmentId}/procedures/${procedureId}/documents/${documentId}`),

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
