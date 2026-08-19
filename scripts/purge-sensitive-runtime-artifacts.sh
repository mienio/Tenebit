#!/usr/bin/env bash
set -euo pipefail
root="${1:-.}"

# Runtime logs may contain credentials from pre-PATCH08 capability URLs. Remove them rather than
# attempting to redact a credential after it has already been copied into archives/support bundles.
find "$root" -type f \
  \( -name '*.log' -o -name '*.log.*' -o -name '*.tsbuildinfo' -o -name '*.log.gz' \) \
  -print -delete

# Build/runtime dependencies are reproducible from lockfiles and must never travel with source artifacts.
# -prune removes the highest matching directory instead of noisily walking every nested dist/bin folder.
find "$root" -type d \
  \( -name logs -o -name node_modules -o -name dist -o -name test-results -o -name playwright-report \
     -o -name coverage -o -name bin -o -name obj -o -name TestResults \) \
  -prune -print -exec rm -rf {} + 2>/dev/null || true

printf 'Sensitive runtime/build artifacts removed under %s.\n' "$root"
"$(cd "$(dirname "$0")" && pwd)/scan-sensitive-runtime-artifacts.sh" "$root"
printf 'Final AUD9-001 incident migration revokes all pre-deployment public/reset/verification credentials; regenerate required links after deployment.\n'
