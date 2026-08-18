#!/usr/bin/env bash
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
out="${1:-$root/artifacts/tenebit-source.zip}"
tmp="$(mktemp -d)"; trap 'rm -rf "$tmp"' EXIT
mkdir -p "$tmp/source" "$(dirname "$out")"
for p in Tenebit.Backend Tenebit.Frontend .github scripts .gitignore .dockerignore docker-compose.production.yml; do
  [[ -e "$root/$p" ]] && cp -a "$root/$p" "$tmp/source/"
done
find "$tmp/source" -type d \( -name node_modules -o -name dist -o -name bin -o -name obj -o -name logs -o -name test-results -o -name playwright-report -o -name coverage \) -prune -exec rm -rf {} +
find "$tmp/source" -type f \( -name '*.log' -o -name '*.tsbuildinfo' -o -name '.env' -o -name '.env.*' -o -name 'appsettings.Production.json' \) -delete
"$root/scripts/verify-release-artifact.sh" "$tmp/source"
(cd "$tmp/source" && zip -qr "$out" .)
echo "$out"
