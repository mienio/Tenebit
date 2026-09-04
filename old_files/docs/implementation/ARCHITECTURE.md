# Architecture Decision Summary

Status: **PROPOSED — awaiting user acceptance**. No production code, schema, dependency, deployment configuration, or migration may change before acceptance.

The implementation extends the current Domain/Application/Infrastructure/Api/React structure. `spec/features.md` defines behavior; current repository conventions define placement and naming. No parallel asset, person, assignment, history, notification, PDF, subscription, or worker system is introduced.

The principal decisions are:

- `Asset.AssignedPersonId` remains the only current-owner field. Assignments, offboarding, audits, and reservations reference it and retain snapshots, but never mirror current ownership.
- Physical return is implemented once through a `PhysicalReturnService` that owns explicit `Asset` transitions, resolves the optional open `AssignmentAsset`, and synchronizes dependent offboarding/reservation records. It also handles directly assigned assets that have no open assignment; no module mutates return state independently.
- `AssignmentAsset` remains an EF-owned child keyed by `(AssignmentId, AssetId)`. It gains `OrganizationId` and return fields, but no surrogate identity or replacement table.
- Existing assignment and procedure hashes remain byte-for-byte version 1. Existing rows are backfilled with `IntegrityVersion = 1`; new assignments created after the evidence migration use version 2. No historical hash is recalculated.
- New public processes use a shared public-token service built on the existing cryptographic primitives. Only hashes are persisted. Legacy GUID assignment and QR URLs remain unchanged.
- `AssetEvidence` is one contextual evidence model. Sanitized full-size bytes and thumbnails stay in PostgreSQL for this MVP. SkiaSharp is the one justified new dependency for Linux-capable decode/re-encode and metadata removal.
- The existing `AlertBackgroundService` remains the single operational timer for alerts, offboarding actions, retention, and digests. It invokes application jobs in bounded batches; `Alerts:Enabled` must not disable employment deactivation or retention.
- Multi-instance background work uses database-backed leases and PostgreSQL `FOR UPDATE SKIP LOCKED` claims. Each claimed offboarding action, retention batch, and alert delivery has a durable state and retry boundary.
- One `AssetAvailabilityGuard` protocol uses stable, sorted PostgreSQL transaction-scoped advisory locks for `(OrganizationId, AssetId)`, tracked Asset row reloads, and an in-transaction availability recheck. Reservation allocation, ordinary assignment/onboarding creation, offboarding start, owner correction, receipt/inspection availability changes, and reservability/status/delete commands all use it. Reservation versions additionally protect stale updates to one reservation.
- Fine-grained permissions extend `RolePermission`, `RolePermissionKeys`, and their defaults. A small evaluator uses the current user's roles and the same override rows; no second permission store is added.
- Paid-module checks use a small feature-entitlement service over `OrganizationSubscription`/`SubscriptionPlan`. It gates creation/start/configuration, not privacy controls or safe completion/export of already persisted records.
- All instants are stored as UTC `DateTimeOffset`. Organization-local employment schedules are resolved once and persisted as UTC with the IANA zone snapshot used for the decision. Digest schedules are recalculated against current organization settings.
- Commands spanning multiple aggregates use an application transaction abstraction implemented by `TenebitDbContext`; email/network activity is never placed inside a database transaction.

Two plan/specification tensions are resolved explicitly:

1. Stage 1 requires reservation cancellation during offboarding, but reservation records do not exist until Stage 4. FND-03 creates the durable action type and scheduler seam only. RES-04 adds the actual pending-rejection/future-cancellation handler and must complete this acceptance criterion before reservations are approved.
2. Section 14 proposes Enterprise-only advanced retention and `LegalHold`, while the repository and PLAN contain only Free/Pro and require legal hold in FND-06. This architecture does not invent an unprovisionable Enterprise billing tier. Basic retention, privacy notice, metadata removal, person export, and organization privacy operations remain Free; advanced per-category retention and legal-hold management are Pro in the current two-plan model. Holds are always enforced after downgrade. A future Enterprise tier can remap the feature key without a schema redesign.

# Existing Mechanisms To Reuse

- `Person`, `PeopleService`, and `IPersonRepository` remain the employee record and organization/email lookup used by `MyWorkspaceService`.
- `Asset`, `AssetCategory`, `Asset.AssignedPersonId`, and existing repositories remain the inventory source of truth.
- `Assignment`, owned `AssignmentAsset`, `AssignmentService`, the public assignment flow, and existing PDF flow remain the issue/return record.
- `License.UnassignSeat` remains the seat-release behavior. Offboarding invokes it; it does not create an offboarding-only seat state.
- `ActivityLog` remains the only user-visible history. Module tables store operational state and immutable snapshots, not duplicate timelines.
- `Result`, `Result<T>`, `PagedResult<T>`, `Error`, and `ResultExtensions` remain the application/HTTP error contract.
- `RolePermission`, `RolePermissionService`, `AccessPolicy`, and `TenebitRoles` remain the authorization foundation.
- `OrganizationSubscription`, `SubscriptionPlan`, and Stripe synchronization remain the plan source of truth.
- `IEmailSender`, `SmtpEmailSender`, `EmailTemplates`, and `IAppLinkBuilder` remain the mail/link infrastructure.
- `AlertBackgroundService` remains the timer. The current 24-hour scan is replaced with a short configurable coordinated cycle because scheduled employment times and digests require minute-scale execution.
- `IPdfProtocolGenerator`/QuestPDF gain offboarding and campaign models; there is no second PDF engine.
- `TenebitEndpoints` remains the minimal-API registration point. New registration methods may split that file internally without changing endpoint conventions.
- React lazy routes, `Layout`, `api/endpoints.ts`, `types/domain.ts`, existing modal/state components, CSS, and the four-language dictionary remain the frontend patterns.
- xUnit, in-memory fakes, and Vitest remain the test frameworks.

# Domain Changes

## Aggregate boundaries

- `Person` owns employment lifecycle invariants only.
- `Asset` owns physical status and current-owner transitions. `AssetCategory` owns default handling/photo/catalog policies.
- `Assignment` owns its `AssignmentAsset` return records and procedure acceptances.
- `OffboardingCase` owns snapshot items and offboarding-specific scheduled actions, while referencing Person, Asset, Assignment, and License by organization-scoped identity. Shared `PublicTokenCandidate` rows represent hash-only dispatch attempts for offboarding cases and audit participants.
- `AssetEvidence` is an independent aggregate because access, locking, retention, and download audit span several parent processes.
- `AssetAuditCampaign` owns participants/items and immutable campaign snapshot facts.
- `EquipmentReservation` owns request items, immutable kit expansion snapshots, and one `ReservationAllocation` child per concrete allocated asset. It never owns physical asset status.
- Alert rules/digest settings are organization aggregates; `SentAlert` remains the durable delivery record for migration compatibility.

## Person and employment

Add `EmploymentStatus`, `EmploymentEndsAt`, `DeactivatedAt`, and `PreferredLanguage`. Domain methods enforce:

- new/current people are `Active` and `IsActive = true`;
- starting offboarding changes `Active -> Offboarding` while retaining `IsActive = true`;
- scheduled deactivation changes `Offboarding -> Inactive`, sets `IsActive = false`, and stamps UTC once;
- employment restore is explicit and does not restore seats, returned assets, or cancelled reservations;
- the legacy update request may still send `IsActive`, but it is translated into a legal lifecycle command rather than setting the boolean independently;
- assignment, onboarding, and reservation entry services require `EmploymentStatus.Active`, not only `IsActive`.

Employee/MyWorkspace/request endpoints reload Person by `(OrganizationId, normalized authenticated email)` on every command and do not rely only on JWT age. Offboarding does not automatically disable `OrganizationUser`: administrative accounts may share an email with a Person. Refresh-token and session revocation, if desired for an employee account, is a separate explicitly selected offboarding action with safeguards that prohibit disabling the last owner or an administrative user by email coincidence.

## Asset, category, and return state

Add `PendingReturn`, reservability fields, catalog fields, return handling/disposition enums, photo policies, checklist template, and `ReturnWorkflowKind` (`Physical`, `Administrative`, `None`). Physical/Vehicle/Key categories may use the physical workflow; Digital/License/Account/Document/Location/Consumable categories default conservatively to Administrative or None and are never blindly changed to `PendingReturn`. Replace guarded uses of the generic `ChangeStatus` path with named domain transitions:

- `MarkPendingReturn` preserves `AssignedPersonId`;
- `ConfirmPhysicalReceipt` clears `AssignedPersonId` and selects `InStock`, `InService`, or `InTransit` from category policy;
- `CompleteInspection` selects `InStock`, `Damaged`, `Retired`, or `Disposed`;
- `ConfirmVendorTransfer` and `ConfirmDisposal` never make inventory available;
- `MarkLost`, `MarkDamaged`, `Retain`, and `WriteOff` require an authorized application command and note where specified;
- only an `InStock` asset with `IsReservable = true` is a catalog candidate.

Final resolution mapping is explicit: received/healthy follows category disposition; received/damaged clears custodian and becomes `Damaged`; missing preserves custodian and becomes `Lost`; retained or written-off clears custodian and becomes `Retired`. `Disposed` is used only after confirmed disposal. An employee's claim never executes these transitions. Waiving an offboarding checklist item neither fabricates receipt nor makes the asset available; a separate authorized asset resolution remains required.

Optional category catalog illustration is an owned sanitized image value on AssetCategory (content type, full/thumbnail bytes, hash, updated metadata) processed by the same sanitizer. It is not forced into `AssetEvidence`, because catalog art has no Asset or process context.

`AssetService.UpdateAsync` may continue editing ordinary statuses, but must reject attempts to bypass assignment, pending-return, receipt, inspection, lost/damaged resolution, retired, or disposed invariants.

## Assignment and owned AssignmentAsset

`AssignmentAsset` keeps `(AssignmentId, AssetId)` as its key and gains `OrganizationId`, `ReturnedAt`, `ReturnLocation`, `ReturnedBy`, `ReturnResolution`, and `ReturnNotes`. `Assignment` gains `PartiallyReturned` and item-level idempotent resolution methods. Aggregate `ReturnedAt` is populated only when the last open item receives a final resolution.

The legacy full-return method delegates to the same item transition for each unresolved child. Repeating the same item result returns the current representation without new state or audit. A conflicting second result is rejected.

`PhysicalReturnService` is the only application entry for item/full/offboarding/reservation returns. It finds at most one unresolved AssignmentAsset for the asset/person. No open row means the offboarding item is the custody-history context; more than one open row is a data conflict that blocks automatic resolution. Every successful return synchronizes the matching OffboardingItem and linked reservation in the same transaction.

`IntegrityVersion` dispatches hash verification. Version 1 is the exact current algorithm and culture/ordering behavior, preserved in a regression fixture. Version 2 hashes the existing v1 business fields plus an ordered sequence of `(EvidenceId, EvidencePhase, Sha256)` for evidence that was locked into the acceptance. Procedure acceptance hashes keep their existing algorithm; they are not silently redefined by assignment v2.

## Offboarding

`OffboardingCase` stores stable snapshots, schedule-zone identity, current token hashes, completion/protocol identity, and derived progress. `ReadyToClose` is recomputed by aggregate methods after item/action changes; it is not a free-form setter. Terminal states are `Completed` and `Cancelled`.

An `OffboardingScheduledAction` child is justified by the real retry/concurrency requirement. It represents one person deactivation, one license release, and later each reservation cancellation/rejection. It stores due UTC, status, attempts, next attempt, bounded error, lease identifier/expiry, and completion time. This avoids duplicating retry fields across case and item while remaining specific to offboarding rather than becoming a generic job framework.

Snapshots retain labels and relevant historical owner/location facts. They reference current records for actions but never rewrite historical labels when a source changes. Physical result remains in `AssignmentAsset`/`Asset`; the offboarding item records its workflow/result reference and must agree with that result.

## Evidence and privacy

`AssetEvidence` contains `OrganizationId`, `AssetId`, optional assignment composite context, optional offboarding/audit item context, phase, sanitized content, thumbnail, actual content type, sizes, SHA-256, caption, upload actor/source/time, lock time, and retention timestamps. Exactly one valid business context is required. Content and thumbnail become nullable so retention can erase bytes while retaining minimal identity/hash/audit facts.

Organization privacy settings are a separate one-to-one organization-owned record rather than expanding `Organization` with many unrelated fields. `PrivacyNoticeVersion` is immutable and language-specific, with text/hash, approval actor/time, effective time, and retirement state. Each public submission references the exact notice version shown. `LegalHold` is organization-scoped, targets an allowed process/evidence context, includes reason/actor/review date/release facts, and blocks every destructive retention/anonymization path.

Offboarding and audit submit actions append an immutable `ProcessSubmission` receipt containing process/participant identity, canonical ordered response hash, channel/actor, language, notice-version ID/hash, submitted time, and separately retained IP fields. Owned immutable `ProcessSubmissionItem` rows preserve the ordered item identity, response, employee comment, and referenced evidence hashes that produced the receipt hash. Reopen creates a later submission/version and new items; it never overwrites a prior receipt. Retention may erase response/comment/IP content while preserving non-personal item identity, erasure marker, and the original hash. IP is never copied into ActivityLog.

## Asset audits

Campaign, participant, and item entities preserve the specification state machines and snapshots. Participant responses are claims. Only a separate authorized resolution command may invoke an `Asset` owner/status transition. Submit locks participant responses; reopen is explicit and audited. Completion freezes report inputs and nonresponse counts.

`OwnershipCorrected` is rejected while the asset has an unresolved AssignmentAsset or active offboarding item unless the operator uses a dedicated reconciliation command that locks and updates every affected aggregate atomically. An audit answer alone never performs that reconciliation.

## Alerts

`SentAlert` remains the mapped entity/table name to avoid replacing persisted delivery history. It evolves into a delivery state machine with recipient, `DeliveryChannel` (`Immediate` or `Digest`), logical key, status (`Pending`, `Sending`, `Sent`, `Failed`, `Suppressed`), attempts, next attempt, lease, bounded error, deterministic message ID, sent time, and digest membership/source references. `DeliveryMode.Both` creates two independent channel records; the digest itself has its own recipient delivery and retry state. `Suppressed` is required so disabled/unconfigured SMTP is not falsely recorded as delivered.

## Reservations

Reservation/item methods enforce legal interval, positive requested quantity, draft/submission/decision/cancellation/checkout transitions, allocation completeness, substitution history, and final completion. Kit submission freezes its category/quantity expansion. `ReservationAllocation` stores one unique concrete Asset per requested unit plus original/replacement history and reason; approval requires exactly the requested number for every item. Pending requests do not block assets. Approved/ready/checked-out allocations do. The parent has a numeric concurrency version returned on stale-sensitive commands and is explicitly bumped for allocation-child changes.

Reservation interval semantics are half-open `[StartAt, EndAt)`, so an asset ending at 10:00 can be reserved again from 10:00. Requester-facing local values are resolved through the organization timezone and persisted as UTC instants.

An ordinary open Assignment/Onboarding is a hard availability block from `IssuedAt` until every item is physically resolved; `DueDate` is only an expected-return date and never creates future availability. `DueDate = null` therefore also blocks all future allocations. A linked reservation's UTC `EndAt` drives its reminders and request history, but approval of a following reservation still requires the asset to be physically returned, inspected when required, and `InStock`; expected return alone is not approvable inventory.

## Service tickets (RMA)

`ServiceTicket` records a repair/vendor engagement for one Asset: vendor, description, estimated/actual cost, currency, SLA due date, open/closed timestamps, status (`Open`, `InProgress`, `WaitingForParts`, `Completed`, `Cancelled`), resolution notes, and an optional link to the `AssetInspection` that triggered it. Opening a ticket moves the Asset to `InService`. `Complete` requires a `ResultStatus` constrained to `InStock`/`Damaged`/`Retired`/`Disposed` (not an arbitrary `AssetStatus` — statuses like `Assigned`/`Reserved`/`InTransit`/`PendingReturn` are owned by other flows and require their own invariants, e.g. `AssignedPersonId`). `Cancel` does not change the Asset status; an operator resolves it manually afterward. `Complete`/`Cancel` are terminal — a closed ticket cannot be reopened or edited.

## Asset export/import

Asset CSV/JSON export (`GET /api/assets/export.csv`, `/export.json`) reuses the same organization-scoped `AssetResponse` projection as the list endpoints, so it never includes `AssetEvidence` (issue/return/audit photos) — evidence is treated as organization-owned sensitive material, not portable inventory data. CSV import (frontend `ImportModal`, entity `assets`) is client-side only: it maps columns to `name`/`assetTag`/`serialNumber`/`category`/`manufacturer`/`model`/`location` and calls the existing `createAsset` endpoint per row; there is no server-side bulk-import endpoint and no photo import path.

# Application Changes

- Add focused services and DTOs under `Offboarding`, `Evidence`, `AssetAudits`, `Alerts`, `Reservations`, and privacy/settings folders, matching current service style.
- Extend repositories with organization-scoped, intent-specific operations. Avoid a generic specification repository.
- Add `OrganizationTimeService` over an `IOrganizationTimeZone` abstraction implemented with BCL `TimeZoneInfo`; do not add NodaTime. It validates IANA identifiers, resolves local schedules, formats organization-local DTO values, and centralizes DST rules. A nonexistent spring-forward employment time is rejected for user correction. An ambiguous fall-back employment time resolves to the later UTC occurrence so employment is not ended earlier than the administrator-selected wall time. For recurring digest/quiet-hour schedules, a nonexistent local time advances to the first valid local minute and a persisted local schedule key prevents a second send during an ambiguous hour. Once an offboarding schedule is started, its resolved UTC instant and zone ID snapshot do not move if organization settings later change.
- Add a small `PermissionEvaluator` (or equivalent methods on `RolePermissionService`) using the current `RolePermission` rows and defaults. Services call it; endpoints do not contain business authorization.
- Add `FeatureEntitlementService` over `ISubscriptionRepository`. It accepts an organization ID for workers and current organization for requests.
- Add `PublicProcessTokenService` using `TokenHasher.NewRawToken`, the unchanged SHA-256 representation, and `CryptographicOperations.FixedTimeEquals`. Do not change existing identity token hashes.
- Add `IImageSanitizer` returning sanitized full bytes, thumbnail, actual media type, dimensions, and hash. Application evidence validation owns size/count/context/permission rules; Infrastructure owns decoding.
- Extend `IUnitOfWork` or add one small `ITransactionRunner` with async transaction execution and requested isolation. In-memory tests execute delegates directly.
- Add an application background coordinator invoked by the existing hosted service. Each job remains independently testable without a timer.
- Extend `IEmailSender.SendAsync` to return an outcome (`Sent` or `Suppressed`) while still throwing on SMTP failure. Existing callers may ignore the returned value; delivery workers must record it.
- Add projection queries for list/dashboard/export screens. New services must not load all people/assets and join them in memory as current small lists sometimes do.
- Add one `AssetAvailabilityGuard` abstraction used by every command that can create/remove an obligation or change physical/catalog availability. It acquires locks in canonical order (Person first when relevant, then sorted Assets), reloads tracked rows, and runs the same blocking-query rules before mutation.
- Extend `AssignmentService` and `OnboardingService`; do not duplicate assignment creation for multipart, starter package, employee package, or reservation checkout. A prepared request/evidence manifest feeds the same internal orchestration.
- Offboarding license actions invoke the same domain operation as `LicenseService`; authorization and audit actor differ, but seat state does not.
- Person privacy export is assembled server-side from organization-scoped repositories. Anonymization checks inactive lifecycle, open processes, current asset ownership, retention eligibility, and legal holds before changing PII.
- FND-06 also owns organization export and deletion-request orchestration required by the specification. Export is server-streamed and owner-only. Deletion validates legal holds, active billing, last-owner/authentication consequences, and retention obligations, records a tombstone/job identity, and never performs an unbounded cascade in an HTTP request.

Token-bearing email needs a special boundary because raw tokens cannot be persisted for retry. Each claimed dispatch creates a fresh raw token only in memory and CAS-inserts a `PublicTokenCandidate` containing organization, process type/ID, hash, expiry, dispatch/lease identity, and state. The previous current token remains valid. Public validation accepts the current hash and any unexpired, non-revoked candidate, so a crash before/after SMTP cannot invalidate an already known link. A retry never promotes or overwrites an ambiguous candidate; it adds a new candidate. Confirmed SMTP success CAS-promotes that attempt's candidate to current and revokes the previous current plus other candidates. Controlled failure revokes only the matching attempt. Expired/abandoned candidates are removed by bounded cleanup. Parallel resend/regenerate operations serialize on process version/lease. `/regenerate-link` deliberately creates and immediately promotes a new candidate while returning the one-time raw URL to the authorized administrator. Raw values and rendered token-bearing bodies are never persisted.

# Infrastructure Changes

- Add EF mappings, repositories, migrations, and dependency registrations in the existing Infrastructure project/schema.
- Implement bounded claims as one atomic `UPDATE ... FROM (SELECT ... FOR UPDATE SKIP LOCKED LIMIT ...) RETURNING` that stamps a unique lease ID/expiry. Success/failure updates use compare-and-swap `WHERE LeaseId = ...`, so an expired worker cannot overwrite a later claimant.
- Implement the shared sorted PostgreSQL transaction advisory lock using a versioned SHA-256-derived canonical 64-bit key for `(OrganizationId, AssetId)`; never use runtime/string `GetHashCode`. The guard also reloads tracked Asset rows and is used by every availability-changing path, not only reservations.
- Use an explicit application-managed parent version for reservation/case/campaign aggregates and bump it when owned children change. This avoids assuming that PostgreSQL `xmin` on a parent changes when only a child row is updated.
- Add SkiaSharp plus its Linux no-dependencies native-assets package to Infrastructure only. Use `SKCodec` to identify and decode JPEG/PNG/WebP, reject other encoded formats, enforce dimension/pixel limits before allocation, draw into a new bitmap, and encode a fresh JPEG/PNG/WebP. Creating a new image and encoder output removes EXIF/GPS and other source metadata. `System.Drawing` is not used.
- Store only sanitized bytes. The endpoint's “original” download means full-resolution sanitized output, never the uploaded byte stream.
- Extend QuestPDF with strongly typed handover/offboarding/audit models. PDF generation receives fully materialized safe models and never queries repositories.
- Keep the `sent_alerts` table and migrate it in place. Implement claim/update repository methods and deterministic RFC message IDs.
- Configure trusted proxy forwarding and use `RemoteIpAddress`; do not trust arbitrary `X-Forwarded-For`. Capture is then applied as Off/truncated/full per organization.
- Update Serilog and `Tenebit.Frontend/nginx.conf` request logging so token path segments are replaced by route templates or logging is disabled for token routes. Public token responses/pages set `Referrer-Policy: no-referrer`, `Cache-Control: no-store`, and `X-Robots-Tag: noindex`; token pages load no third-party resources that could receive a Referer.

# Database Changes

Migrations are additive and unit-scoped. Each migration updates the model snapshot and is reviewed against the immediately previous migration, not only an empty database.

Key migration rules:

- FND-01 adds lifecycle columns, backfills `EmploymentStatus` from `IsActive`, and adds lifecycle/due indexes. Existing active rows become `Active`; inactive rows become `Inactive` with nullable historical deactivation time rather than a fabricated instant.
- FND-02 adds category workflow/policies with conservative defaults, photo policies disabled, `PendingReturn`, and item return columns. Do not infer policy from localized/customer names. Add immutable nullable `SystemKey` for starter categories, backfilled only from the existing deterministic starter identity mapping and then uniquely indexed per organization; customer categories have no system key. Physical system categories may receive specified defaults, while all uncertain rows remain conservative and unavailable until configured.
- Existing returned assignments backfill their owned items as `Returned` at aggregate `ReturnedAt`; open assignments remain unresolved. No missing/damaged meaning is inferred from text.
- `AssignmentAsset.OrganizationId` is added nullable, backfilled by joining assignments, validated, then made required. It uses composite FK `(OrganizationId, AssignmentId)` to Assignment while preserving the current primary key. The same expand/backfill/constrain sequence and composite tenant FK is mandatory for every new parent/child or Person/Asset/Assignment/License relation; `Guid.Empty` defaults are forbidden.
- The evidence migration adds `Assignment.IntegrityVersion` with value 1 for every existing row. The domain constructor for new post-migration assignments explicitly selects version 2. `AcceptanceHash` and `ProcedureAcceptance.ConfirmationHash` are never updated by SQL.
- New parent/child records carry `OrganizationId`. Every supported relation has composite alternate keys/FKs including organization so a child cannot reference another tenant even if application validation fails. Evidence additionally has a database CHECK requiring exactly one valid process context.
- Current token hashes and `PublicTokenCandidate.TokenHash` are globally unique and indexed. Candidate indexes also cover process, state, and expiry for bounded revocation/cleanup. Public lookup is by the high-entropy hash; every returned record supplies its own organization context.
- Offboarding uses a PostgreSQL partial unique index on `(OrganizationId, PersonId)` with the exact persisted string statuses `Draft`, `Active`, `WaitingForReturn`, and `ReadyToClose`. Service validation provides the friendly conflict; the index closes races.
- Evidence content/thumbnail columns are nullable for retention. Index `(OrganizationId, AssetId, EvidencePhase, UploadedAt)` and context indexes support bounded access without loading bytes on lists.
- Legal holds index `(OrganizationId, TargetType, TargetId, ReleasedAt)` and retention candidates index organization/category/due/erased state.
- Existing `sent_alerts` rows backfill as historical `Sent`, attempt 1, with a legacy-recipient/channel marker. Detection checks that marker so migration does not resend every old alert under new recipient keys. The old unique index is replaced only after backfill with a unique `(OrganizationId, LogicalKey, RecipientKey, DeliveryChannel)` key.
- Existing assets backfill `IsReservable = false`. No organization inventory is exposed automatically.
- Reservation allocations have one row per concrete Asset, a positive-quantity/check constraint on requests, unique Asset per reservation, and indexes covering organization, asset, blocking allocation state, and UTC start/end. Shared locks provide cross-row serialization; normal indexes keep overlap rechecks bounded.
- Source deletion uses an explicit policy: true aggregate drafts without history may cascade to their children; Person is anonymized rather than hard-deleted once referenced; Asset/Category/Assignment hard delete is restricted once any assignment, evidence, offboarding, audit, or reservation history exists; historical snapshots and ActivityLog never cascade from current source rows. Operational removal uses deactivate/retire/cancel states.
- Irreversible retention works in bounded batches and sets erasure/anonymization markers in the same transaction as the non-sensitive audit entry.

Migration validation must include representative existing active/inactive people, accepted v1 assignments, returned/open owned items, legacy sent alerts, license seats, and Free/Pro subscriptions.

# Deployment and schema readiness

Schema migration is a deployment prerequisite, not best-effort application startup. Production uses a dedicated migrator identity with DDL rights and a global PostgreSQL migration lock; runtime identity has schema DML only. Local development may migrate synchronously, but always before `app.Run/Start` starts listeners or hosted services. Migration failure is fail-fast.

The runtime refuses business traffic and worker execution when pending/incompatible migrations exist. Readiness checks the expected schema/migration marker, not only `CanConnect`, and returns a generic 503 without `ex.Message`; details remain in correlated server logs. PostgreSQL connections require TLS in production, and default/weak JWT or database secrets fail startup rather than emit a warning.

Large backfills/indexes use preflight duplicate/null checks and an explicit lock budget; operations requiring `CREATE INDEX CONCURRENTLY` run outside the normal migration transaction. Evidence/PII migrations are forward-only in production when a Down operation would destroy retained history.

# API Changes

- Register the specification routes through focused private mapping methods in `TenebitEndpoints`; do not replace minimal APIs with controllers.
- Authenticated routes continue to use `/api` authorization and `ResultExtensions`. Every command carries `CancellationToken`.
- Public offboarding and audit routes use `AllowAnonymous()` and `RequireRateLimiting("public")`; upload routes may use a stricter public-upload limiter in addition to the existing policy if configured within the same ASP.NET rate-limiting mechanism.
- Unknown, expired, revoked, wrong-process, and wrong-tenant tokens all return the same minimal 404-style response. Public DTOs contain no organization/person/asset GUIDs unless an opaque item identifier is necessary for the next operation.
- Public token lookup derives `OrganizationId` from the hash match. The request never supplies an organization ID as authority.
- Multipart endpoints parse `IFormFile` only in Api and pass neutral upload descriptors/streams to Application. Manifests are validated for duplicate/mismatched assets before state mutation.
- Evidence metadata/list endpoints omit content. Full sanitized bytes use explicit download endpoints with permission and ActivityLog checks.
- Reservation conflicts preserve existing `{ message, code }` and add optional `details` containing a typed, safe list of unavailable request item/allocation/category identifiers. `ResultExtensions`, `ErrorResponse`, and frontend `ApiError` treat `details` as additive; existing clients remain compatible.
- List endpoints enforce page/page-size bounds. CSV/PDF endpoints generate server-side and use organization-scoped queries.
- Add a small authenticated effective-capabilities response (permissions plus feature entitlements) rather than putting mutable role overrides into long-lived JWT claims. Frontend hiding is advisory; services still enforce all checks.
- Preserve current JSON assignment/onboarding/full-return/public GUID endpoints. New multipart and item-return routes are additive.

# Frontend Changes

- Add the five specified lazy pages and routes, using current React/CSS/modal/state patterns.
- `Layout` visibility uses effective capabilities and entitlements when loaded; it may fall back to conservative existing role defaults during startup.
- Extend `MyWorkspacePage` rather than creating a second employee portal. Person matching remains organization plus authenticated email.
- Keep API contracts in `types/domain.ts` and calls in `api/endpoints.ts`. Form-data builders remain near the endpoint or a shared evidence component, not duplicated by page.
- Create `AssetEvidenceUploader` and gallery only because assignment, offboarding, and audit reuse them. A progress component is created only when at least two pages need identical behavior.
- Every status/action/error/privacy/accessibility label is added for Polish, English, Spanish, and German while preserving the current dirty localization work.
- Public/token pages expose safe snapshots, masked serials, privacy notice/version, and text progress. They never infer inventory mutation from an employee response.
- New forms provide keyboard operation, error summary focus, labels/description IDs, non-color status text, touch-sized image controls, and equivalent HTML data when PDF accessibility is incomplete.
- Date/time inputs use organization-local values and send explicit local date/time semantics expected by the backend; the browser's own timezone is not treated as the organization timezone.

# Background Processing

`AlertBackgroundService` becomes a thin coordinated loop with a short configurable interval (default five minutes). It always invokes:

1. due offboarding-action claims;
2. bounded retention/anonymization claims;
3. alert event discovery when alerts are enabled;
4. due immediate-delivery claims/retries;
5. due digest generation/delivery.

Each stage catches and records its own failure so one organization/action does not stop others. Claims use the atomic claim/CAS lease protocol, generated lease ID, expiry, and attempt count; an expired lease is recoverable and a stale worker cannot commit. Successful domain mutation, action completion, and ActivityLog entry commit together. Failure writes bounded diagnostic state and next retry without rolling back other already successful actions. The coordinator cannot run until the schema-readiness gate is satisfied.

Employment actions compare `_clock.UtcNow` to the persisted UTC instant. A past instant is immediately due. Reservation action rows are introduced/handled only once RES-04 provides reservation storage.

SMTP delivery is an at-least-once external side effect: persist Pending, claim, send outside a DB transaction, then persist Sent/Failed/Suppressed. A deterministic message ID and lease prevent ordinary duplicates. A process crash after SMTP accepts a message but before the Sent commit is an unavoidable ambiguity without provider idempotency; this limitation is tested/documented and is preferable to marking a failed attempt as sent.

# Authorization

Authorization is enforced in Application services using effective permissions plus record-scope rules:

- `offboarding.view`: owner/admin/hr/asset_operator by default.
- `offboarding.manage`: owner/admin/hr/asset_operator; physical receipt/status mutation additionally requires owner/admin/asset_operator.
- `offboarding.complete`: owner/admin by default; controls waiver and final completion.
- `assetAudits.view`: owner/admin/asset_operator/auditor.
- `assetAudits.manage` and `assetAudits.resolve`: owner/admin/asset_operator; auditor remains read/export only.
- `evidence.view`: roles already allowed to view the owning asset/process, subject to the explicit override.
- `evidence.manage`: owner/admin/asset_operator and the uploader before lock; retention is a system operation, not user delete.
- `alerts.manage`: owner/admin; `alerts.viewHistory`: owner/admin/auditor.
- `reservations.request`: authenticated user linked to an Active Person in the same organization.
- `reservations.approve`: owner/admin/asset_operator, plus manager only for a same-organization direct report when the organization setting enables it.
- `reservations.checkout`: owner/admin/asset_operator/technician.
- `reservations.viewAll`: owner/admin/asset_operator and other explicitly configured roles.

Owner remains an unconditional allow. `RolePermissionService` rejects an override that would deny an owner-only safety capability. For other roles, any allowed current role grants the permission unless future requirements introduce explicit user-level denial; no such user-level model is added now. Related Person, Asset, Team, License, and manager IDs are reloaded with `OrganizationId` before authorization decisions.

Entitlements are separate from authorization. Missing subscription, Free plan, `Cancelled`, or `Expired` means Free. Existing Stripe semantics treat `Active` and `PastDue` with a live Stripe subscription as temporarily entitled Pro; webhook cancellation/expiry removes new-start rights immediately. Free users can still perform ordinary assignments/returns without photos and every mandatory privacy/export/retention/organization-export-or-deletion operation. Pro enables new offboarding, business evidence, campaigns, alert configuration, reservations, and reports. Before FND-08, paid workflow endpoints remain unavailable; foundation code is not publicly exposed. Downgrade prevents new starts/configuration but does not hide records, stop safe physical return, prevent cancellation/completion, or disable retention/legal holds.

# Security

- Raw public tokens exist only in local variables and returned/sent links. They are absent from entities, DTO persistence, ActivityLog details, alert bodies stored in the database, exceptions, and logs.
- Existing `TokenHasher.Hash` representation is unchanged so password/reset/verification/refresh records remain valid. Fixed-time verification is added without reinterpreting those rows.
- Public endpoints resolve the process from a current/candidate hash and verify expiry, revocation, candidate state, process status, and item membership. Hash equality uses `CryptographicOperations.FixedTimeEquals` after lookup.
- Serilog and nginx access logs must redact/omit token segments. Token pages use no-store/no-referrer/noindex headers and no third-party resources. ActivityLog records “link regenerated/sent” without link/token.
- Evidence validation checks declared MIME, magic bytes, codec result, supported encoded format, maximum input/output bytes, dimensions, pixel count, per-phase count, and exact process/asset context. SVG/PDF/executable/polyglot failures do not mutate business state.
- SkiaSharp re-encoding drops EXIF/GPS. Metadata removal is mandatory and cannot be disabled by plan or setting in MVP.
- Public IP capture defaults Off. Truncated/full modes require retention, use only trusted forwarded headers, and never place full IP in normal email/PDF.
- Public response DTOs expose the minimum snapshot and a masked serial suffix. They do not expose costs, keys, internal notes, other participants, or current detailed inventory.
- Employee claims never call lost/damaged/owner transitions. Only authorized resolver commands do.
- Email subjects are generic. License keys and token-bearing URLs never enter digests. Token link dispatches build content in memory.
- Person exports, evidence full downloads, retention/legal-hold changes, waivers, and owner/status corrections are audited.
- Catalog responses use explicit projection DTOs; domain serialization is never returned directly.

# Multi-Tenancy

Every new entity has `OrganizationId`, including owned `AssignmentAsset`. Repository interfaces require organization for authenticated operations. All joins/subqueries include both relevant identity and organization; for example, touched assignment search queries must add organization predicates to the nested People/Assets queries currently filtered only by ID.

Public token lookup is the only intentional initial query without a caller-provided organization: it searches a globally high-entropy indexed hash in the route-specific process table, obtains the record's organization, then performs every related lookup with that value. This is safer than accepting an organization parameter from an anonymous caller.

Workers claim rows containing organization and pass it through every related lookup. Cross-organization IDs are treated as not found. Composite alternate keys/FKs include organization for every supported new relation, and every new unique/index key starts with organization except globally unique token hash indexes.

Tenant-isolation tests create identical-looking records in two organizations and cover reads, commands, nested IDs, exports, downloads, token paths, background claims, manager relationships, reservation allocation, and report generation.

# Transactions

Use one database transaction for each business consistency boundary:

- partial/full/offboarding return: optional AssignmentAsset result, Asset owner/status, inspection record, matching OffboardingItem, evidence metadata/content, ActivityLog, and dependent reservation completion;
- offboarding start: Person lock/recheck, case snapshot, person lifecycle, guarded asset PendingReturn transitions, scheduled actions, reservation actions when available, notification references, and ActivityLog;
- one scheduled digital action: domain mutation, action result, and ActivityLog;
- audit start: campaign freeze, participant/items, safe notification references, and ActivityLog; token hashes are issued only by explicit link generation or claimed dispatch;
- audit resolution: audit item resolution, authorized Asset mutation, and ActivityLog;
- assignment/onboarding/reservation checkout with evidence: assignment, asset ownership, evidence, linking record, ActivityLog, and any durable non-token email delivery metadata;
- reservation approval/substitution: stale-version check, shared availability locks, overlap recheck, every unit allocation, decision state, and ActivityLog;
- offboarding/campaign finalization uses two phases: under parent/child locks, validate closure, persist one immutable canonical final snapshot/hash and a finalization marker that blocks further mutation; then generate PDF from that snapshot and idempotently store bytes/number, terminal state, evidence locks, token revocation, and ActivityLog. A crash retries from the same frozen snapshot rather than regenerating from live rows;
- retention: byte/PII erasure marker plus non-sensitive ActivityLog.

Image decode/re-encode happens before opening the transaction, but no domain state is mutated until all files validate. SMTP and other network sends occur after commit. Failed PDF generation leaves the process frozen/finalizing and retryable; it cannot accept new child mutations. Concurrent completion returns the same frozen snapshot/protocol when another request won.

# Concurrency

- Offboarding open-case races are closed by the partial unique index and translated to 409.
- Background workers claim with `FOR UPDATE SKIP LOCKED`, a lease ID, and expiry. Claim order is deterministic and batches are bounded.
- Reservation and process stale writes use an explicit numeric parent version that is bumped for child mutations.
- All cross-aggregate commands follow one lock order: relevant Person, then process parent, then Assets in sorted ID order, then owned children. The shared availability guard acquires canonical advisory transaction locks, reloads Assets, checks `[start,end)` intervals and all other obligations, and only then mutates. Competing assignment, onboarding, offboarding, audit correction, and reservation paths therefore serialize across instances. A conflict mutates nothing and returns 409.
- Substitution locks old and new assets in the same sorted lock set. Checkout repeats locks and availability checks.
- Offboarding/campaign completion locks the parent, freezes children, and stores only one canonical final snapshot and protocol/report.
- Alert/offboarding logical unique indexes handle duplicate detection races; unique violations are treated as “already created,” not 500.
- Every offboarding/audit child mutation first locks/reloads its case/campaign parent and bumps the parent version. Public submit, admin reopen/resolution, and completion therefore cannot race silently.

# Idempotency

- Repeated item return with the same final result succeeds without a second audit; a different result is a conflict.
- Full return applies only to unresolved items and preserves legacy response behavior.
- Person deactivation, license release, reservation rejection/cancellation, inspection completion, audit submit, offboarding complete/cancel, and reservation cancel/complete are idempotent.
- Each scheduled action has one durable identity and success marker. Successful actions are never retried; failed actions retain bounded retry state.
- `POST /offboarding/{id}/complete` returns the existing completed representation and stored PDF identity/bytes.
- Token regeneration replaces the current hash once; repeated requests deliberately create different tokens and audit only the rotation event.
- Alert detection uses a logical key including organization, type, entity, threshold, due date, recipient, and delivery channel. Insert uniqueness and leases prevent normal duplicate sends; Immediate and Digest remain independent.
- Retention reruns select only content/PII not already erased and keep legal holds authoritative.

# Backwards Compatibility

- `Person.IsActive` remains in schema and DTOs while lifecycle fields are added. Existing clients may keep sending `IsActive`; incompatible combinations are normalized/rejected by service/domain rules.
- Existing assignment JSON endpoints, public GUID links, QR routes, PDFs, and full-return endpoints remain.
- Existing assignment hashes and procedure hashes use the exact current version-1 code path. Persisted fixture tests prove verification before and after migration. Existing rows are not rehashed.
- Existing `ReturnedAt` keeps aggregate-final meaning. Backfilled owned rows use it only where the assignment is already Returned.
- `AssignmentAsset` remains owned and keeps its current primary key/table, avoiding an identity rewrite.
- Existing category/customer data is not name-guessed. Conservative return defaults prevent accidental stock availability; photos remain disabled until configured.
- Existing assets remain non-reservable.
- Existing `sent_alerts` suppress corresponding historical alerts after migration and are not interpreted as failed deliveries.
- Existing Free asset limits, Pro/Stripe activation, cancellation fallback, and pricing behavior remain. Feature keys are additive.
- Existing direct email callers continue compiling when `SendAsync` returns an outcome; ignored outcomes preserve their current flow while alert delivery consumes the result.
- Reservation checkout converts `EndAt` to existing `Assignment.DueDate` using the snapshotted organization zone, while the full UTC `Reservation.EndAt` remains authoritative for reservation alerts and is never reconstructed from DateOnly.
- Current dirty localization/email/result changes are preserved. Coder must inspect the live diff before edits to `AssignmentService`, `AlertCheckService`, `Program`, `ResultExtensions`, `apiClient`, and `translations.ts`.

# ActivityLog

Use the specification event keys. Add entries in the same transaction as state changes for lifecycle changes, item resolutions, policy/permission/retention changes, token rotations, response submit/reopen, allocation/substitution/checkout, campaign/offboarding completion, evidence full download/delete/retention erasure, privacy export, and legal hold operations.

Do not log raw tokens, full IP, license keys, file bytes, erased PII, SMTP credentials, or unbounded exception text. Technical retry attempts do not each create user-visible history; a scheduled action's first/changed failure and eventual success do. Details remain concise and localized presentation is handled by the frontend/error translation layer, not by storing translated secrets.

# Error Handling

- Domain invariants throw `DomainException`; application services map them to existing validation/conflict errors.
- Missing or cross-tenant records return the same NotFound behavior.
- Stale row versions, shared availability-lock findings, and partial unique violations become `Error.Conflict`/409.
- Public invalid/expired/revoked tokens share one generic NotFound response.
- Image validation returns safe validation codes/messages; decoder internals and stack traces are not exposed.
- SMTP outcomes persist Sent, Suppressed, or Failed; bounded error text removes credentials/addresses where unnecessary.
- Unexpected provider exceptions remain handled by the current global 500/correlation response. Infrastructure translates known PostgreSQL constraint names rather than parsing localized messages.
- Background stages isolate exceptions per claimed item and release/expire leases safely.

# Performance

- Add projection repository methods for paged offboarding, campaign, reservation, alert-history, catalog, dashboard, and exception queues.
- Counts and progress are database aggregates. List views never load evidence content or all related tables.
- Generate and persist thumbnails during upload; list APIs return metadata/thumbnail endpoints only.
- Campaign/offboarding snapshot creation uses set-based/batched queries and deduplicates assets by ID before inserts.
- Availability queries use indexed organization/status/asset/interval predicates. Calendar ranges and page sizes are bounded.
- Digest/report data is loaded in a small fixed number of queries per organization/campaign, not per row.
- CSV exports stream or page database results server-side. PDFs use frozen projections and enforce practical campaign/evidence limits.
- Retention and worker claims use bounded batches and due indexes, so a five-minute timer does not scan all binary content or whole tables.

# Testing Strategy

Use existing xUnit and Vitest. Add no E2E framework.

- Domain tests cover every state transition/invariant, lifecycle compatibility, partial/final return, inspection/disposition, snapshot locks, evidence lock, audit submit/reopen, reservation interval/status, and closure gates.
- Service tests use organization-aware fakes and cover every command's tenant isolation, permission, entitlement, idempotency, negative path, ActivityLog, and public minimal DTO.
- Preserve a fixed version-1 assignment/procedure object/hash fixture. Add deterministic version-2 ordering/evidence-hash fixtures.
- Evidence tests use real small JPEG/PNG/WebP fixtures with EXIF/GPS and verify the sanitized output can be decoded and contains no source metadata. Test signature/MIME mismatch, pixel bomb bounds, sixth image, locked delete, and mismatched process.
- Time tests cover valid IANA validation, spring-forward nonexistent local employment time (reject), fall-back ambiguous time (later UTC occurrence), schedule-zone snapshot, due-at equality, and local digest dedup.
- Background tests run two claimers against a real PostgreSQL database and prove one claim/success/audit plus stale-lease CAS rejection. In-memory tests cover orchestration/failure retry.
- Reservation/availability concurrency requires a real PostgreSQL xUnit test using two DbContexts/transactions and the existing provider packages. CI supplies an isolated PostgreSQL service; inability to connect fails this dedicated job rather than skipping it. Test simultaneous assignment versus approval, offboarding versus assignment, two approvals, sorted multi-asset locking, quantity allocations, boundary intervals, stale parent version, substitution history, and rollback.
- Migration tests/verification upgrade a representative copy from the current snapshot and assert v1 hashes, owned-row backfill, legacy alerts, category defaults, non-reservability, and filtered offboarding uniqueness.
- Alert tests cover pending/claim/fail/retry/sent/suppressed, independent Immediate+Digest delivery, recipient/channel dedup, lease expiry/stale CAS, deterministic message ID, due-date key change, and no secrets/tokens in persisted content.
- Token dispatch tests cover SMTP success, controlled failure, crash before/after acceptance, simultaneous current+candidate validation, retry with multiple candidates, success promotion/revocation, parallel resend CAS, expiry/cleanup, nginx/application log redaction, and no-store/no-referrer headers.
- Startup tests prove incompatible/pending schema never starts workers or reports ready and that anonymous readiness never exposes exception text.
- Frontend Vitest targets pure action/status maps, interval validation, filter/query builders, progress summaries, evidence limits, permission/entitlement visibility, and conflict handling.
- CI runs backend tests, the required PostgreSQL job, `npm test`, and `npm run build`. After each unit, run its focused backend/frontend tests and builds. After RES-04, run all of them, representative migration verification, and the manual scenarios in section 12.

# Expected New Files

Create files only when the owning unit starts; this is an expected set, not permission to scaffold unused placeholders.

- Domain: focused enum/entity files under `People`, `Offboarding`, `Evidence`, `Privacy`, `AssetAudits`, `Alerts`, and `Reservations`, including per-unit `ReservationAllocation` and immutable submission/privacy-notice records when their owning unit starts.
- Application: DTO/service files for those modules; `PublicProcessTokenService`, time-zone abstraction/service, permission evaluator, entitlement service, transaction abstraction, image-sanitizer abstraction, and coordinated background job service.
- Infrastructure: one repository per new aggregate, SkiaSharp sanitizer, and only the small PostgreSQL claim/reservation-lock implementation required by the abstractions.
- Api/deploy: only a token-path redaction helper/middleware if it cannot remain in `Program.cs`; endpoint registration stays in the current endpoint area. Existing nginx/deploy configuration is modified only for schema-first startup, readiness, and token-log/header protections.
- Frontend: `OffboardingPage.tsx`, `PublicOffboardingPage.tsx`, `AssetAuditsPage.tsx`, `PublicAssetAuditPage.tsx`, `ReservationsPage.tsx`, and evidence uploader/gallery reused by multiple pages.
- Tests: focused domain/service tests, evidence fixtures, pure frontend utility tests, and a required CI PostgreSQL xUnit fixture using existing packages.
- Data: one migration per approved implementation unit that changes schema, plus its generated designer and the existing model snapshot update.

# Expected Modified Files

- Domain: `Person.cs`, `Asset.cs`, `AssetStatus.cs`, `AssetCategory.cs`, `Assignment.cs`, `AssignmentAsset.cs`, `AssignmentStatus.cs`, `SentAlert.cs`, `SubscriptionPlan.cs`, and possibly `OrganizationSubscription.cs` for entitlement semantics.
- Application: `IRepositories.cs`, `IUnitOfWork.cs`, `IEmailSender.cs`, `IAppLinkBuilder.cs`, `IPdfProtocolGenerator.cs`, `RolePermissions.cs`, `RolePermissionService.cs`, `PeopleService`/DTOs, `AssetService`/category service/DTOs, `AssignmentService`/DTOs, `OnboardingService`/DTOs, `LicenseService`, `MyWorkspaceService`/DTOs, `AlertCheckService`, `SubscriptionService`, dashboard services, and dependency registration.
- Infrastructure: `TenebitDbContext`, current repositories touched by new scoped projections, `AlertBackgroundService`, `SmtpEmailSender`, `AppLinkBuilder`, `PdfProtocolGenerator`, dependency registration, seeding/default policies, migrations, and snapshot.
- Api/deploy: `TenebitEndpoints.cs`, `Program.cs`, readiness/deployment configuration, nginx configuration, and `ResultExtensions.cs` only for additive typed conflict details while preserving current localization.
- Frontend: `App.tsx`, `Layout.tsx`, `MyWorkspacePage.tsx`, `PeoplePage.tsx`, `AssignmentsPage.tsx`, `OnboardingPage.tsx`, `AssetsPage.tsx`, `LicensesPage.tsx`, `DashboardPage.tsx`, `SettingsPage.tsx`, `ReportsPage.tsx`, `api/endpoints.ts`, `types/domain.ts`, `translations.ts`, and existing CSS.
- Tests/fakes: existing repository fakes and service tests affected by interface additions.

# Explicitly Rejected Alternatives

- A second owner field on Assignment, OffboardingItem, AuditItem, or Reservation: rejected because `Asset.AssignedPersonId` is authoritative.
- A second checkout/return model for reservations/offboarding: rejected; both orchestrate the existing Assignment/Asset return path.
- Replacing owned `AssignmentAsset` with a new entity/table or surrogate ID: rejected; adding organization/return fields preserves persisted composite rows.
- Rehashing old assignments or adding evidence to version 1: rejected because it invalidates historical acceptance.
- Migrating legacy assignment/QR GUID links to the new token mechanism in this scope: rejected as a breaking change.
- Global EF tenant query filters: rejected for this implementation because current repositories use explicit organization parameters and introducing filters across identity/public/background paths would be a broad risky refactor. New/touched queries are explicitly scoped and tested.
- A generic repository, mediator, event bus, message broker, external object storage, or new job framework: rejected as unnecessary for the current repository/specification.
- `System.Drawing`: rejected because server-side Linux support is unsuitable. ImageSharp is rejected for this commercial repository unless licensing is separately approved. Magick.NET is rejected as heavier than the required JPEG/PNG/WebP pipeline. SkiaSharp with Linux native assets is the smallest suitable choice.
- Storing raw or reversibly encrypted public tokens for email retry: rejected by the hash-only requirement. Temporarily accepting hash-only dispatch candidates preserves current and ambiguously delivered URLs; only a confirmed successful candidate is promoted.
- Treating `Asset.Status.Reserved` as future availability: rejected; approved reservation intervals are authoritative.
- An ordinary unique index for reservation overlap: rejected because it cannot express range overlap. `btree_gist` exclusion constraints are also deferred because they require duplicated allocation intervals/provider extension privileges; advisory transaction locks plus overlap recheck meet the current PostgreSQL-only requirement with a smaller migration surface.
- Serializable isolation for every reservation request: rejected as unnecessarily broad. Per-asset advisory locks serialize only competing allocations.
- A second hosted service for offboarding/retention: rejected; the existing coordinated timer owns these stages.
- Sending SMTP inside a database transaction or marking Pending as Sent before SMTP: rejected due to long locks and false delivery state.
- Persisting token-bearing email bodies in the delivery table: rejected because it would persist raw public tokens.
- Automatically setting Lost/Damaged from employee responses or setting InStock on person deactivation: rejected by the core business invariants.
- Name-based migration of customer categories: rejected because names are localized/user-controlled. Conservative defaults are safer.
- Introducing a third Enterprise billing SKU without provisioning/Stripe behavior: rejected. Current Free/Pro feature keys implement the behavior; future packaging can remap them.
- Building a new frontend UI library, global state framework, or E2E framework: rejected; current React patterns and Vitest are sufficient.

# Implementation Order

The PLAN order is retained. Each unit is coded, tested, independently reviewed, corrected, and approved before the next starts.

1. **FND-01** — Person lifecycle, compatible DTO/migration, and all existing assignment/onboarding obligation guards.
2. **FND-02** — Category workflow/return policies, `PendingReturn`, owned AssignmentAsset backfill, shared Asset availability/physical-return guard, partial/full return, and inspection transitions.
3. **FND-03** — Minimal offboarding aggregate plus durable scheduled actions, local-time resolver, multi-instance claims, person deactivation, and license release. Reservation handlers remain deliberately unimplemented until RES-04.
4. **FND-04** — Shared hash-only current/candidate dispatch semantics, application/nginx redacted logging and no-referrer/no-store headers, safe link building, expiry/revoke/regenerate, and offboarding token persistence.
5. **FND-05** — Evidence/privacy settings, SkiaSharp sanitation, contextual access, thumbnails, locking, and downloads.
6. **FND-06** — Basic/advanced retention, legal holds, person/organization export, controlled anonymization, organization deletion-request orchestration, and bounded retention worker.
7. **FND-07** — In-place `SentAlert` delivery state migration, independent Immediate/Digest channels, SMTP outcomes, CAS leases, retry, quiet-time primitives, and legacy suppression.
8. **FND-08** — Central feature entitlements and effective capabilities; Free privacy exceptions and safe downgrade behavior.
9. **OFB-01** — Full administrative offboarding lifecycle, stable snapshots, partial unique open-case constraint, start/cancel/restore, and UI.
10. **OFB-02** — Token/MyWorkspace employee response, candidate-based link dispatch, evidence context, safe DTO, and privacy notice.
11. **OFB-03** — Physical receipt through the shared return engine, inspections/exceptions/waivers, action-now, and closure eligibility.
12. **EVD-02** — Assignment/onboarding/return multipart variants, atomic evidence integration, v1 fixture/backfill and v2 hashing, public evidence, and PDF thumbnails.
13. **ALR-02** — Configurable rules/digests/history/test send for sources that exist at this point.
14. **OFB-04** — Frozen final protocol, idempotent completion/token revocation, dashboard/list polish, and first-package cumulative regression.
15. **AUD-01** — Campaign draft/preview/start and immutable organization-scoped participant/item snapshots.
16. **AUD-02** — Public/MyWorkspace drafts/submission/reopen, contextual evidence, reminders, and no-asset-mutation regression.
17. **AUD-03** — Authorized exception resolution, snapshot CSV/PDF, completion with nonresponses, and digest integration.
18. **RES-01** — Non-reservable migration defaults, catalog/category/kit configuration and snapshots, safe aggregated availability projections.
19. **RES-02** — Authenticated Active-person draft/submit/edit/cancel request lifecycle, positive quantities, and frozen kit expansion in MyWorkspace.
20. **RES-03** — Per-unit `ReservationAllocation`, parent versions, shared availability locks/overlap recheck, substitution history, 409 details, calendar, and required PostgreSQL concurrency verification.
21. **RES-04** — Atomic checkout through Assignment/evidence, final return completion hook, offboarding pending/future reservation actions, catalog reintegration, alerts, and full-cycle regression.

After unit 21, perform migration upgrade verification with representative old data, full backend/frontend test/build execution, all section 12 manual scenarios, and the required cumulative independent final review.
