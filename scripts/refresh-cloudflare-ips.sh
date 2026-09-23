#!/usr/bin/env bash
# Regenerates Tenebit.Frontend/cloudflare-real-ip.conf from Cloudflare's published ranges.
#
# nginx cannot fetch these at runtime the way the backend fetches Paddle's webhook IPs, so the list has to
# be committed. Run this when Cloudflare announces a change (rare), then redeploy. Failing loudly beats
# writing a truncated file: a short list would make nginx distrust real Cloudflare edges and hand the whole
# application Cloudflare addresses again instead of real client IPs.
set -euo pipefail

target="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)/Tenebit.Frontend/cloudflare-real-ip.conf"
tmp="$(mktemp)"
trap 'rm -f "$tmp"' EXIT

{ curl -fsS https://www.cloudflare.com/ips-v4; echo; curl -fsS https://www.cloudflare.com/ips-v6; echo; } \
  | grep -E '^[0-9a-fA-F:.]+/[0-9]+$' > "$tmp"

count=$(wc -l < "$tmp")
# Cloudflare has published roughly 22 ranges for years. Anything far below that means a truncated response
# or a changed format, not a genuinely shorter list.
if [ "$count" -lt 15 ]; then
  echo "Refusing to write: only $count ranges came back from Cloudflare." >&2
  exit 1
fi

head -n "$(grep -n '^set_real_ip_from' "$target" | head -1 | cut -d: -f1 | awk '{print $1-1}')" "$target" > "$tmp.out"
sed -i "s|fetched from https://www.cloudflare.com/ips-v4 and /ips-v6 on [0-9-]*|fetched from https://www.cloudflare.com/ips-v4 and /ips-v6 on $(date -u +%Y-%m-%d)|" "$tmp.out"
sed 's/^/set_real_ip_from /; s/$/;/' "$tmp" >> "$tmp.out"
printf '\nreal_ip_header CF-Connecting-IP;\nreal_ip_recursive off;\n' >> "$tmp.out"

mv "$tmp.out" "$target"
echo "Wrote $count Cloudflare ranges to $target"
