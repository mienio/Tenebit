import { apiBaseUrl } from '../api/apiClient';

// Deliberately isolated from ../api/apiClient.ts: the platform-admin token must never share a token
// provider, storage key, or refresh flow with the tenant session. sessionStorage (not localStorage)
// so the session does not outlive the browser tab.
const STORAGE_KEY = 'tenebit_admin_token';

export function getAdminToken(): string | null {
  try {
    return sessionStorage.getItem(STORAGE_KEY);
  } catch {
    return null;
  }
}

function setAdminToken(token: string | null) {
  try {
    if (token) sessionStorage.setItem(STORAGE_KEY, token);
    else sessionStorage.removeItem(STORAGE_KEY);
  } catch {
    // Storage unavailable (private mode) - session just won't survive a reload.
  }
}

export function adminLogout() {
  setAdminToken(null);
}

export class AdminApiError extends Error {
  status: number;
  code: string;
  constructor(message: string, status: number, code = '') {
    super(message);
    this.status = status;
    this.code = code;
  }
}

async function adminFetch<T>(path: string, init: RequestInit = {}): Promise<T> {
  const token = getAdminToken();
  const headers = new Headers(init.headers);
  headers.set('Content-Type', 'application/json');
  if (token) headers.set('Authorization', `Bearer ${token}`);

  const response = await fetch(`${apiBaseUrl}${path}`, { ...init, headers });
  if (!response.ok) {
    const body = await response.json().catch(() => null);
    // 403 with STEP_UP_REQUIRED means the token is fine but this action needs a fresh 2FA code,
    // so it must not clear the session the way a genuine auth failure does.
    const code = body?.code ?? '';
    if (response.status === 401 || (response.status === 403 && code !== 'STEP_UP_REQUIRED')) {
      setAdminToken(null);
    }
    throw new AdminApiError(body?.message ?? `Błąd (${response.status})`, response.status, code);
  }
  if (response.status === 204) return undefined as T;
  return response.json() as Promise<T>;
}

export async function adminLogin(email: string, password: string, totpCode: string): Promise<void> {
  const data = await adminFetch<{ token: string }>('/api/admin/login', {
    method: 'POST',
    body: JSON.stringify({ email, password, totpCode }),
  });
  setAdminToken(data.token);
}

export interface AdminOrganizationSummary {
  id: string;
  name: string;
  country: string;
  createdAt: string;
  planName: string;
  subscriptionStatus: string;
  currentPeriodEnd: string | null;
  assetCount: number;
  peopleCount: number;
  locationCount: number;
  userCount: number;
  isSuspended: boolean;
  suspendedAt: string | null;
  suspendedReason: string | null;
  /** null = nazwa nie została jeszcze sprawdzona pod kątem regulaminu */
  reviewedAt: string | null;
}

export interface AdminUserSummary {
  id: string;
  /** Server-masked, e.g. "an•••@fi•••.com". The full address never leaves the API. */
  maskedEmail: string;
  /** Initials only, e.g. "A. K." */
  initials: string;
  isActive: boolean;
  isEmailVerified: boolean;
  isTwoFactorEnabled: boolean;
  createdAt: string;
  lastLoginAt: string | null;
  roles: string[];
}

export interface AdminCountSlice {
  label: string;
  count: number;
}

export interface AdminOrganizationDetail {
  summary: AdminOrganizationSummary;
  users: AdminUserSummary[];
  assetsByStatus: AdminCountSlice[];
  assetsByCategory: AdminCountSlice[];
  peopleByStatus: AdminCountSlice[];
  locationCount: number;
  assetsCreated: AdminSeries;
}

export interface AdminSeriesPoint {
  day: string;
  count: number;
}

export interface AdminSeries {
  label: string;
  points: AdminSeriesPoint[];
}

export interface AdminPlanSlice {
  plan: string;
  count: number;
}

export interface AdminDashboard {
  organizations: number;
  suspendedOrganizations: number;
  users: number;
  activeUsers: number;
  assets: number;
  people: number;
  locations: number;
  licenses: number;
  loginsInRange: number;
  failedLoginsInRange: number;
  pendingReview: number;
  rangeFrom: string;
  rangeTo: string;
  assetsCreated: AdminSeries;
  organizationsCreated: AdminSeries;
  logins: AdminSeries;
  failedLogins: AdminSeries;
  plans: AdminPlanSlice[];
  newestOrganizations: AdminOrganizationSummary[];
}

export interface AdminUserListItem {
  id: string;
  organizationId: string;
  organizationName: string;
  organizationSuspended: boolean;
  maskedEmail: string;
  initials: string;
  isActive: boolean;
  isEmailVerified: boolean;
  isTwoFactorEnabled: boolean;
  createdAt: string;
  lastLoginAt: string | null;
  roles: string[];
}

export interface AdminLoginEntry {
  id: string;
  organizationId: string | null;
  organizationName: string | null;
  userId: string | null;
  maskedEmail: string;
  succeeded: boolean;
  failureReason: string | null;
  ipAddress: string | null;
  userAgent: string | null;
  createdAt: string;
}

export interface AdminAuditEntry {
  id: string;
  action: string;
  targetType: string | null;
  targetId: string | null;
  targetLabel: string | null;
  details: string | null;
  ipAddress: string | null;
  createdAt: string;
}

export interface AdminPromoCode {
  id: string;
  code: string;
  planKey: string;
  discountType: 'Percentage' | 'FixedAmount';
  discountValue: number;
  maxRedemptions: number | null;
  timesRedeemed: number;
  expiresAt: string | null;
  isActive: boolean;
  createdAt: string;
}

export interface AdminPage<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
}

export function getAdminDashboard(from: string, to: string): Promise<AdminDashboard> {
  return adminFetch(`/api/admin/dashboard?from=${from}&to=${to}`);
}

export function listAdminOrganizations(): Promise<AdminOrganizationSummary[]> {
  return adminFetch('/api/admin/organizations');
}

export function getAdminOrganization(id: string, from: string, to: string): Promise<AdminOrganizationDetail> {
  return adminFetch(`/api/admin/organizations/${id}?from=${from}&to=${to}`);
}

export function listAdminUsers(search: string, page = 1, pageSize = 50): Promise<AdminPage<AdminUserListItem>> {
  const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
  if (search) params.set('search', search);
  return adminFetch(`/api/admin/users?${params}`);
}

export function listAdminLogins(
  search: string,
  succeeded: boolean | null,
  page = 1,
  pageSize = 50
): Promise<AdminPage<AdminLoginEntry>> {
  const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
  if (search) params.set('search', search);
  if (succeeded !== null) params.set('succeeded', String(succeeded));
  return adminFetch(`/api/admin/logins?${params}`);
}

export function listAdminAudit(limit = 200): Promise<AdminAuditEntry[]> {
  return adminFetch(`/api/admin/audit?limit=${limit}`);
}

// Moderation. Each call carries a fresh TOTP code (step-up auth) - the server rejects it otherwise,
// so a stolen session token alone cannot change anyone's access.
export function suspendOrganization(id: string, reason: string, totpCode: string): Promise<void> {
  return adminFetch(`/api/admin/organizations/${id}/suspend`, {
    method: 'POST',
    body: JSON.stringify({ reason, totpCode }),
  });
}

/** Marks an organization's name as checked. No 2FA step-up: it grants nobody anything. */
export function markOrganizationReviewed(id: string): Promise<void> {
  return adminFetch(`/api/admin/organizations/${id}/review`, { method: 'POST' });
}

export function restoreOrganization(id: string, totpCode: string): Promise<void> {
  return adminFetch(`/api/admin/organizations/${id}/restore`, {
    method: 'POST',
    body: JSON.stringify({ totpCode }),
  });
}

export function blockUser(id: string, reason: string, totpCode: string): Promise<void> {
  return adminFetch(`/api/admin/users/${id}/block`, {
    method: 'POST',
    body: JSON.stringify({ reason, totpCode }),
  });
}

export function unblockUser(id: string, totpCode: string): Promise<void> {
  return adminFetch(`/api/admin/users/${id}/unblock`, {
    method: 'POST',
    body: JSON.stringify({ totpCode }),
  });
}

export function forceSignOut(id: string, totpCode: string): Promise<void> {
  return adminFetch(`/api/admin/users/${id}/force-signout`, {
    method: 'POST',
    body: JSON.stringify({ totpCode }),
  });
}

export function listPromoCodes(): Promise<AdminPromoCode[]> {
  return adminFetch('/api/admin/promo-codes');
}

export function createPromoCodes(body: {
  planKey: string;
  discountType: 'Percentage' | 'FixedAmount';
  discountValue: number;
  quantity: number;
  code?: string;
  maxRedemptions?: number | null;
  expiresAt?: string | null;
}): Promise<AdminPromoCode[]> {
  return adminFetch('/api/admin/promo-codes', { method: 'POST', body: JSON.stringify(body) });
}

export function setPromoCodeActive(id: string, active: boolean): Promise<void> {
  return adminFetch(`/api/admin/promo-codes/${id}/active`, { method: 'POST', body: JSON.stringify({ active }) });
}

export function deletePromoCode(id: string): Promise<void> {
  return adminFetch(`/api/admin/promo-codes/${id}`, { method: 'DELETE' });
}
