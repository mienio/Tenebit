#!/usr/bin/env bash
set -euo pipefail

# Detect historical Tenebit bearer credentials without ever printing the matching line or secret.
# Usage: scripts/scan-sensitive-runtime-artifacts.sh [root ...]
# If no root is supplied the current directory is scanned.
roots=("${@:-.}")
pattern='(/api/public/(assignments|offboarding|asset-audits)/[A-Za-z0-9_-]{40,})|(/(reset-password|verify-email)\?token=[A-Za-z0-9._~%+-]{20,})'
found=0
files_scanned=0

scan_stream() {
  local display="$1"
  shift
  files_scanned=$((files_scanned + 1))
  if "$@" | grep -aEq -- "$pattern"; then
    printf 'SENSITIVE_CREDENTIAL_PATTERN %s\n' "$display" >&2
    found=$((found + 1))
  fi
}

for root in "${roots[@]}"; do
  [[ -e "$root" ]] || { printf 'Incident scan root does not exist: %s\n' "$root" >&2; exit 3; }

  while IFS= read -r -d '' file; do
    case "$file" in
      *.gz)
        if command -v gzip >/dev/null 2>&1; then
          scan_stream "$file" gzip -cd -- "$file"
        fi
        ;;
      *.zip)
        if command -v unzip >/dev/null 2>&1; then
          scan_stream "$file" unzip -p "$file"
        fi
        ;;
      *)
        files_scanned=$((files_scanned + 1))
        if grep -aEq -- "$pattern" "$file"; then
          printf 'SENSITIVE_CREDENTIAL_PATTERN %s\n' "$file" >&2
          found=$((found + 1))
        fi
        ;;
    esac
  done < <(find "$root" -type f \
    \( -name '*.log' -o -name '*.log.*' -o -name '*.txt' -o -name '*.out' -o -name '*.err' \
       -o -name '*.jsonl' -o -name '*.support' -o -name '*.gz' -o -name '*.zip' \) \
    -not -path '*/node_modules/*' -not -path '*/.git/*' -print0)
done

if (( found > 0 )); then
  printf 'AUD9-001 runtime/support artifact scan: FAIL (%d files contain a credential-shaped legacy request target; secrets were not printed).\n' "$found" >&2
  exit 2
fi

printf 'AUD9-001 runtime/support artifact scan: PASS (%d files inspected).\n' "$files_scanned"
