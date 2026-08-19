import { useEffect, useState } from 'react';
import { apiRequest } from '../api/apiClient';

export type PublicCapabilityPurpose = 'assignment' | 'offboarding' | 'asset-audit';

function readRawFragment(): string | null {
  const raw = window.location.hash.startsWith('#') ? window.location.hash.slice(1) : '';
  return raw || null;
}

export function clearUrlFragment() {
  if (!window.location.hash) return;
  history.replaceState(history.state, '', window.location.pathname + window.location.search);
}

function readAndClearRawFragment(): string | null {
  const raw = readRawFragment();
  if (raw) clearUrlFragment();
  return raw;
}

function decodeFragment(value: string) {
  try { return decodeURIComponent(value); } catch { return value; }
}

export function consumeFragmentToken(): string | null {
  const raw = readAndClearRawFragment();
  return raw ? decodeFragment(raw) : null;
}

export function readRecoveryCodeFragment(): { email: string; code: string } {
  const raw = readRawFragment();
  if (!raw) return { email: '', code: '' };

  const parameters = new URLSearchParams(raw);
  return {
    email: parameters.get('email')?.trim() ?? '',
    code: (parameters.get('code') ?? '').replace(/\D/g, '').slice(0, 6)
  };
}

export function usePublicCapabilitySession(purpose: PublicCapabilityPurpose) {
  const [state, setState] = useState<'loading' | 'ready' | 'error'>('loading');

  useEffect(() => {
    let cancelled = false;
    const rawToken = readAndClearRawFragment();
    if (!rawToken) {
      // A refresh may legitimately have no fragment because the HttpOnly session cookie already exists.
      setState('ready');
      return () => { cancelled = true; };
    }

    apiRequest<void>('/api/public/capability-session', {
      method: 'POST',
      body: JSON.stringify({ purpose, token: decodeFragment(rawToken) })
    }).then(() => {
      if (!cancelled) setState('ready');
    }).catch(() => {
      if (!cancelled) setState('error');
    });
    return () => { cancelled = true; };
  }, [purpose]);

  return state;
}
