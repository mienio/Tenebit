if (typeof (globalThis as { process?: unknown }).process === 'undefined') {
  (globalThis as unknown as { process: { env: Record<string, string> } }).process = { env: { NODE_ENV: 'production' } };
}
