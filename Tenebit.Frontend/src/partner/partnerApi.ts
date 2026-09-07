// Own token/session handling, deliberately isolated from ../api/apiClient.ts (tenant) and
// ../admin/adminApi.ts (platform admin) - spec §4.1/§10: a partner session must never be confused
// with either of the other two identity kinds, even in the same browser tab.
const apiBaseUrl = (import.meta.env.VITE_API_BASE_URL ?? '').replace(/\/$/, '');

let accessToken: string | null = null;
export function getPartnerToken(): string | null {
  return accessToken;
}
function setPartnerToken(token: string | null) {
  accessToken = token;
}

export class PartnerApiError extends Error {
  status: number;
  code: string;
  constructor(message: string, status: number, code = '') {
    super(message);
    this.status = status;
    this.code = code;
  }
}

let refreshPromise: Promise<string | null> | null = null;

function refreshPartnerToken(): Promise<string | null> {
  if (!refreshPromise) {
    refreshPromise = fetch(`${apiBaseUrl}/api/partner/refresh`, { method: 'POST', credentials: 'include' })
      .then(async res => {
        if (!res.ok) return null;
        const data = await res.json().catch(() => null);
        if (typeof data?.token !== 'string') return null;
        setPartnerToken(data.token);
        return data.token as string;
      })
      .catch(() => null)
      .finally(() => { refreshPromise = null; });
  }
  return refreshPromise;
}

async function performFetch(path: string, init: RequestInit, token: string | null): Promise<Response> {
  const headers = new Headers(init.headers);
  if (init.body !== undefined && !headers.has('Content-Type')) headers.set('Content-Type', 'application/json');
  if (token) headers.set('Authorization', `Bearer ${token}`);
  return fetch(`${apiBaseUrl}${path}`, { ...init, headers, credentials: 'include' });
}

async function partnerFetch<T>(path: string, init: RequestInit = {}, isRetry = false): Promise<T> {
  const response = await performFetch(path, init, accessToken);

  const isAuthRoute = path.startsWith('/api/partner/login') || path.startsWith('/api/partner/register') || path === '/api/partner/refresh';
  if (response.status === 401 && !isAuthRoute && !isRetry) {
    const newToken = await refreshPartnerToken();
    if (newToken) return partnerFetch<T>(path, init, true);
  }

  if (!response.ok) {
    const body = await response.json().catch(() => null);
    if (response.status === 401) setPartnerToken(null);
    throw new PartnerApiError(body?.message ?? `Błąd (${response.status})`, response.status, body?.code ?? '');
  }
  if (response.status === 204 || response.status === 202) return (await response.json().catch(() => undefined)) as T;
  return response.json() as Promise<T>;
}

export interface AffiliateProfile {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  status: 'PendingApproval' | 'Active' | 'Blocked';
  countryCode: string | null;
  phoneNumber: string | null;
  companyName: string | null;
  taxId: string | null;
  revolutTag: string | null;
  isEmailVerified: boolean;
  acceptedTermsAt: string | null;
  createdAt: string;
}

export async function partnerLogin(email: string, password: string): Promise<AffiliateProfile> {
  const data = await partnerFetch<{ token: string; affiliate: AffiliateProfile }>('/api/partner/login', {
    method: 'POST', body: JSON.stringify({ email, password }),
  });
  setPartnerToken(data.token);
  return data.affiliate;
}

export async function partnerTryRestoreSession(): Promise<AffiliateProfile | null> {
  const token = await refreshPartnerToken();
  if (!token) return null;
  return getMyProfile();
}

export async function partnerLogout(): Promise<void> {
  await partnerFetch('/api/partner/logout', { method: 'POST' }).catch(() => undefined);
  setPartnerToken(null);
}

export function registerAffiliate(body: {
  email: string; password: string; firstName: string; lastName: string;
  countryCode?: string | null; revolutTag?: string | null; acceptTerms: boolean;
}): Promise<{ requiresEmailVerification: boolean }> {
  return partnerFetch('/api/partner/register', { method: 'POST', body: JSON.stringify(body) });
}

export function verifyAffiliateEmail(email: string, code: string): Promise<void> {
  return partnerFetch('/api/partner/verify-email', { method: 'POST', body: JSON.stringify({ email, code }) });
}

export function requestAffiliatePasswordReset(email: string): Promise<{ message: string }> {
  return partnerFetch('/api/partner/password-reset/request', { method: 'POST', body: JSON.stringify({ email }) });
}

export function confirmAffiliatePasswordReset(email: string, code: string, newPassword: string): Promise<void> {
  return partnerFetch('/api/partner/password-reset/confirm', { method: 'POST', body: JSON.stringify({ email, code, newPassword }) });
}

export function getMyProfile(): Promise<AffiliateProfile> {
  return partnerFetch('/api/partner/me');
}

export function updateMyProfile(body: {
  firstName: string; lastName: string; phoneNumber?: string | null; countryCode?: string | null;
  companyName?: string | null; taxId?: string | null; revolutTag?: string | null;
}): Promise<AffiliateProfile> {
  return partnerFetch('/api/partner/me', { method: 'PATCH', body: JSON.stringify(body) });
}

export interface AffiliateCode {
  id: string;
  code: string;
  countryCode: string | null;
  isActive: boolean;
  clickCount: number;
  createdAt: string;
  trackingUrl: string;
}

export function listMyCodes(): Promise<AffiliateCode[]> {
  return partnerFetch('/api/partner/codes');
}

export function checkCodeAvailability(code: string): Promise<{ available: boolean }> {
  return partnerFetch(`/api/partner/codes/availability?code=${encodeURIComponent(code)}`);
}

export function createMyCode(code: string | null, countryCode: string | null): Promise<AffiliateCode> {
  return partnerFetch('/api/partner/codes', { method: 'POST', body: JSON.stringify({ code, countryCode }) });
}

export function setMyCodeActive(id: string, active: boolean): Promise<void> {
  return partnerFetch(`/api/partner/codes/${id}`, { method: 'PATCH', body: JSON.stringify({ active }) });
}

export interface AffiliateDashboard {
  activeCodeCount: number;
  maxActiveCodeCount: number;
  commissionThisOpenPeriod: number;
  totalAwaitingPayout: number;
  totalPaidLifetime: number;
  nextPayoutTargetDate: string | null;
  payoutDayOfMonth: number;
  payoutGraceDays: number;
}

export function getMyDashboard(): Promise<AffiliateDashboard> {
  return partnerFetch('/api/partner/dashboard');
}

export interface AffiliateConversionSummary {
  occurredAt: string;
  code: string;
  eventType: 'InitialSale' | 'Renewal';
  commissionAmount: number;
  currency: string;
  isWithinCommissionWindow: boolean;
  requiresReview: boolean;
}

export interface PagedResult<T> { items: T[]; total: number; page: number; pageSize: number }

export function getMyConversions(page = 1, pageSize = 50): Promise<PagedResult<AffiliateConversionSummary>> {
  return partnerFetch(`/api/partner/conversions?page=${page}&pageSize=${pageSize}`);
}

export interface AffiliatePayoutSummary { markedPaidAt: string; amount: number; currency: string; paymentReference: string | null }

export function getMyPayouts(): Promise<AffiliatePayoutSummary[]> {
  return partnerFetch('/api/partner/payouts');
}

export interface AffiliateMessageThreadSummary {
  id: string; affiliateId: string; subject: string; category: 'General' | 'Complaint';
  status: 'Open' | 'Closed'; lastMessageAt: string; unreadByAdmin: boolean;
}

export interface AffiliateMessage { id: string; senderType: 'Affiliate' | 'Admin'; body: string; sentAt: string }

export interface AffiliateMessageThread {
  id: string; subject: string; category: 'General' | 'Complaint'; status: 'Open' | 'Closed';
  lastMessageAt: string; unreadByAdmin: boolean; unreadByAffiliate: boolean; messages: AffiliateMessage[];
}

export function listMyMessageThreads(): Promise<AffiliateMessageThreadSummary[]> {
  return partnerFetch('/api/partner/messages');
}

export function getMyMessageThread(id: string): Promise<AffiliateMessageThread> {
  return partnerFetch(`/api/partner/messages/${id}`);
}

export function createMyMessageThread(subject: string, isComplaint: boolean, body: string): Promise<AffiliateMessageThread> {
  return partnerFetch('/api/partner/messages', { method: 'POST', body: JSON.stringify({ subject, isComplaint, body }) });
}

export function replyToMyMessageThread(threadId: string, body: string): Promise<void> {
  return partnerFetch(`/api/partner/messages/${threadId}/reply`, { method: 'POST', body: JSON.stringify({ body }) });
}
