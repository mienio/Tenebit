#!/usr/bin/env bash
set -euo pipefail
root="${1:-.}"
forbidden='(^|/)(node_modules|dist|bin|obj|logs|test-results|playwright-report|coverage|\.vs)(/|$)|\.log$|\.tsbuildinfo$|(^|/)\.env($|\.)'
found="$(find "$root" -path '*/.git' -prune -o -type f -printf '%P\n' | grep -E "$forbidden" || true)"
[[ -z "$found" ]] || { printf 'Forbidden release paths:\n%s\n' "$found" >&2; exit 1; }
pem='^[[:space:]]*-----BEGIN (RSA |EC |OPENSSH )?PRIVATE KEY-----[[:space:]]*$'
live='(sk_live_[A-Za-z0-9]{16,}|rk_live_[A-Za-z0-9]{16,}|whsec_[A-Za-z0-9]{16,}|AIza[0-9A-Za-z_-]{30,}|AKIA[0-9A-Z]{16})'
! grep -RIE --exclude-dir=.git --exclude='*.md' --exclude='*.example' "$pem|$live" "$root" || { echo 'Potential production secret detected.' >&2; exit 1; }
