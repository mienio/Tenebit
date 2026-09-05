# Security Hardening — Manual Infrastructure Actions (teneb.it)

These two items from the security hardening pass **cannot be completed from this
repository or this VPS** — they require access to accounts (OVH customer panel,
a WAF vendor) that this session does not have. Both are documented here with
concrete, verifiable steps. Neither should be marked `FIXED` until someone with
that access completes the steps and the verification command shows the expected
result.

Current facts (verified from this session, 2026-09-05):

- DNS for `teneb.it` is hosted at OVH: `ns111.ovh.net` / `dns111.ovh.net`.
- The origin (`57.128.197.215`) is reached directly — there is no CDN/WAF proxy
  in front of nginx today (`curl -I` shows `server: nginx` with no `cf-ray` or
  similar proxy header).
- `dig +dnssec teneb.it DNSKEY` returns no records — the zone is not signed.

---

## 1. WAF (managed rules + bot protection)

### What already exists (do not remove)

- App-level rate limiting per policy (login, register, refresh, recovery,
  OAuth, admin-login) in `Tenebit.Backend/Tenebit.Api/Program.cs`, shared
  across replicas via Postgres for credential-specific brute force.
- nginx-level rate limiting on `/api/*`: `limit_req_zone ... rate=100r/s`
  with `burst=200 nodelay` (`Tenebit.Frontend/nginx.conf`).

This covers **rate limiting**, but not **managed WAF rules** (signature-based
blocking of SQLi/XSS/RCE payloads) or **bot detection** — those require a
component in front of nginx that this session cannot deploy unilaterally,
because it means changing DNS delegation and origin trust for a
production domain with live users, OAuth callbacks, and a Stripe webhook.

### Recommended approach: Cloudflare in front of the existing origin

OVH stays the registrar; only the DNS zone moves to Cloudflare's proxy. No
application code changes.

1. **Create a Cloudflare account** and add `teneb.it` as a site (free or pro
   plan; pro adds more managed-rule coverage).
2. **Re-create every existing DNS record** in the Cloudflare zone before
   cutting over — export the current OVH zone first so nothing is missed
   (A record for `teneb.it`/`www`, the mail server's MX/SPF/DKIM/DMARC TXT
   records, and anything else under `dig teneb.it ANY` / the OVH DNS zone
   editor). Get this exact list from whoever administers the OVH account.
3. **Proxy (orange cloud) only the web records** (`teneb.it`, `www`). Leave
   mail-related records (MX, and any `mail.` A/CNAME) **unproxied (grey
   cloud)** — Cloudflare's proxy does not forward SMTP/IMAP, and this stack
   runs its own `docker-mailserver` container.
4. **Change nameservers at OVH** from `ns111.ovh.net`/`dns111.ovh.net` to the
   two nameservers Cloudflare assigns. Propagation can take up to 24-48h;
   don't decommission the OVH DNS records until the cutover is confirmed.
5. **SSL/TLS mode: "Full (strict)"** — the origin already serves a valid
   Let's Encrypt certificate, so Cloudflare can validate it end-to-end
   instead of falling back to a flexible/self-signed trust model.
6. **Security → WAF → Managed rules**: enable the OWASP Core Ruleset (or
   Cloudflare's Free/Managed ruleset). Start in **Log** mode, watch for false
   positives for a few days against real traffic (especially
   `/api/auth/*`, `/api/oauth/*/callback`, `/api/stripe/webhook`, and any
   `/api/public/*` capability-token endpoints), then switch to **Block**.
7. **Bot Fight Mode / Super Bot Fight Mode**: enable it, but exempt the
   Stripe webhook path and OAuth callback paths from any JS-challenge or
   interactive-challenge action — those are server-to-server or
   redirect-driven requests that cannot solve a browser challenge.
8. **Rate limiting rules** (Cloudflare-side, additive to the existing
   app/nginx limits): a tighter rule on `/api/auth/login` and
   `/api/admin/login` is reasonable since Cloudflare sees traffic before
   it reaches the app-level limiter.
9. **Restrict the origin to Cloudflare's IP ranges** once the above is
   verified stable: either an OVH firewall rule or an nginx
   `allow`/`deny` list using Cloudflare's published IP ranges
   (`https://www.cloudflare.com/ips/`), so attackers can't bypass the WAF
   by hitting `57.128.197.215` directly. Do this last, after confirming
   real traffic flows correctly through the proxy — locking this in too
   early can take the site down.
10. **Backend trusted-proxy config**: once Cloudflare is in front of nginx,
    nginx receives Cloudflare's edge IP, not the visitor's. nginx must map
    `CF-Connecting-IP` (or the `X-Forwarded-For` chain Cloudflare sets) to
    `X-Real-IP` before proxying to the backend, or the app's per-IP
    rate limiting and audit logs will all attribute traffic to Cloudflare's
    edge IPs instead of real clients. This needs a small, deliberate nginx
    change at cutover time — not before, since it would break IP attribution
    while still receiving direct traffic.

### Verification once done

```
curl -I https://teneb.it/          # expect a Cloudflare header (cf-ray, server: cloudflare)
curl -I https://teneb.it/?<sqli-test-payload>   # expect a WAF challenge/block page, not the app
```

### Status: `MANUAL INFRASTRUCTURE ACTION REQUIRED`

---

## 2. DNSSEC for teneb.it

DNS is hosted on OVH's own nameservers (`ns111.ovh.net` / `dns111.ovh.net`),
which is the simplest possible case for DNSSEC — OVH generates and manages
the signing keys and publishes the DS record automatically once enabled. No
manual key generation is needed and none should be invented.

### Steps (OVH Manager)

1. Log into the OVH Control Panel with the account that manages `teneb.it`
   (**Web Cloud → Domain names → teneb.it**).
2. Open the **DNS Zone** tab, then the **DNSSEC** section.
3. Click **Enable DNSSEC**. Because the zone is hosted on OVH's own
   nameservers, OVH generates the KSK/ZSK key pair and submits the DS
   record to the `.it` registry on your behalf — no manual DS value needs
   to be entered anywhere.
4. Wait for OVH to report the DS record as active (usually within a few
   hours; can take up to 24h for full registry propagation).

If `teneb.it` is instead delegated to a different DNS provider than OVH
(unlikely given the NS records observed, but confirm with whoever owns the
OVH account), the equivalent steps are: generate DS record data at that DNS
host, then add the DS record at the `.it` registry via whichever registrar
holds the domain.

### Verification

```
# 1. DS record exists at the registry
dig teneb.it DS +short

# 2. The zone actually validates end-to-end (look for the "ad" flag)
dig @1.1.1.1 +dnssec teneb.it A
# -> flags should include "ad" (Authenticated Data) once trusted

# 3. Full chain-of-trust check
delv teneb.it            # (bind9-dnsutils) - should print "fully validated"
# or the web tool: https://dnssec-analyzer.verisignlabs.com/teneb.it
```

Do not mark this `FIXED` until step 1 returns a DS record **and** step 2/3
show a validated chain — enabling the toggle alone is not sufficient
evidence.

### Status: `MANUAL INFRASTRUCTURE ACTION REQUIRED`
