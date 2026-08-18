# Current State

## Repository and architecture

- The requested specification is at `spec/features.md`; no root `features.md` exists. The plan treats `spec/features.md` as the behavioral source of truth.
- The backend is a .NET 10 solution with the existing Clean Architecture projects `Tenebit.Domain`, `Tenebit.Application`, `Tenebit.Infrastructure`, `Tenebit.Api`, and `Tenebit.Tests` (`Tenebit.Backend/Tenebit.sln`). Dependencies already point Domain <- Application <- Infrastructure/Api.
- Persistence is EF Core 10 with Npgsql/PostgreSQL. `TenebitDbContext` maps the `tenebit` schema; repositories in `Tenebit.Infrastructure/Repositories` explicitly accept `OrganizationId`. There are no global tenant query filters, so every new repository query and nested subquery must scope organization ownership explicitly.
- Four migrations currently exist under `Tenebit.Infrastructure/Data/Migrations`: initial schema, merge synchronization, assignment integrity fields, and Stripe subscription fields. Schema changes must extend this chain and preserve all existing rows.
- API registration is centralized in `Tenebit.Api/Endpoints/TenebitEndpoints.cs` as minimal APIs. The authenticated `/api` group uses JWT authorization. Anonymous routes opt out with `AllowAnonymous()` and existing public endpoints use `RequireRateLimiting("public")`.
- Application errors use `Result`, `Result<T>`, `Error`, and `PagedResult<T>` in `Tenebit.Application/Common/Result.cs`; `ResultExtensions` maps them to the current JSON `{ message, code }` format.
- Authorization is currently coarse role checking via `AccessPolicy`/`TenebitRoles`. `RolePermissionService` and `RolePermission` provide one organization-specific override (`licenses.viewKey`). The new feature permissions should extend this mechanism rather than add a second authorization store.

## Existing business mechanisms to reuse

- `Person` has organization identity, manager/team/location fields and `IsActive`, but no employment lifecycle, end date, deactivation timestamp, or preferred language. `PeopleService.UpdateAsync` directly toggles `IsActive`.
- `Asset.AssignedPersonId` is the existing owner source of truth. `Asset` has physical status and transition methods; `AssetStatus` lacks `PendingReturn`. `AssetCategory` has metadata/custom fields but no return, inspection, photo, or catalog policy.
- `Assignment` owns `AssignmentAsset` and `ProcedureAcceptance`. `Assignment.Return(...)` closes the whole assignment and `AssignmentService.ReturnAsync` returns every asset directly to `InStock`. `AssignmentAsset` currently contains issue/return condition only. There is no partial return or inspection workflow.
- Assignment acceptance stores `AcceptedAt`, `AcceptedIp`, and `AcceptanceHash`. `Assignment.VerifyIntegrity()` uses the current unversioned algorithm. Existing hashes must remain version 1 and must never be recalculated with evidence fields.
- Existing public assignment and QR URLs expose organization/record GUIDs. The new ExitProof and asset-audit flows must use hashed expiring tokens; changing the old assignment URLs is not required and would break existing links.
- `TokenHasher` already generates 32-byte Base64URL tokens and SHA-256 hashes. It is suitable as the basis for a shared public-process token component, but verification semantics, constant-time comparison, expiry/revocation/regeneration, and log redaction still need to be added.
- `License.UnassignSeat` is the existing release behavior. Scheduled offboarding must orchestrate this behavior and preserve its audit semantics; it must not duplicate license-seat state.
- `ActivityLog`/`ActivityLogService` is the only user-visible audit history and is already organization scoped and paginated.
- `AlertBackgroundService` is the existing periodic worker. `AlertCheckService` handles warranty, overdue assignment, and onboarding reminders through SMTP. `SentAlert` only records `(OrganizationId, AlertKey, EntityId, SentAt)`. Send exceptions are swallowed and a `SentAlert` is still written, so delivery status/retry must be corrected before configurable alerts.
- `SmtpEmailSender`, `IEmailSender`, `EmailTemplates`, and `IAppLinkBuilder` are the current notification/link mechanisms.
- `PdfProtocolGenerator`/`IPdfProtocolGenerator` use QuestPDF for assignment protocols and should be extended for offboarding and audit reports.
- `MyWorkspaceService` identifies the signed-in person's record by organization and email, then returns assigned assets and assignments. It is the correct authenticated employee surface for offboarding, audits, and reservations.
- `SubscriptionService`, `OrganizationSubscription`, and `SubscriptionPlan` enforce the existing Free asset limit and Stripe-backed Pro plan. Paid-module gates belong here (or in one small application entitlement method used by it), while privacy export/basic retention/metadata removal must remain available on Free.

## Frontend and tests

- The frontend is React 18/TypeScript/Vite. Routes are lazy-loaded in `src/App.tsx`; role-visible navigation is in `src/components/Layout.tsx`; contracts and calls are centralized in `src/types/domain.ts` and `src/api/endpoints.ts`.
- Existing pages to extend are `PeoplePage`, `AssignmentsPage`, `OnboardingPage`, `MyWorkspacePage`, `AssetsPage`, `LicensesPage`, `DashboardPage`, and `SettingsPage`. New pages should follow existing page, modal, async-state, and CSS patterns without adding a UI framework.
- `translations.ts` has Polish, English, Spanish, and German dictionaries. All new labels, states, errors, and accessibility text require all four languages.
- Backend tests use xUnit and in-memory repositories in `Tenebit.Tests/Fakes`. Frontend unit tests use Vitest. There is no E2E framework and no existing database integration-test harness; do not add a new test framework solely for this scope.

## Dirty worktree constraint

- Discovery found pre-existing modified files including `Program.cs`, `ResultExtensions.cs`, `AlertCheckService.cs`, `AssetCategoryService.cs`, `AssignmentService.cs`, identity services, frontend API/i18n files, `ImportModal`, and `LandingPage`, plus untracked localization helpers/tests and `spec/`. These changes are user-owned and unrelated to this plan.
- Several required units will necessarily overlap `AssignmentService.cs`, `AlertCheckService.cs`, `Program.cs`, `apiClient.ts`, and especially `translations.ts`. The Coder must inspect the live diff immediately before every edit, preserve current localization/email changes, and keep each unit's diff surgical. No unit may reset, replace, or bulk-regenerate those files.

# Requirement Mapping

| Requirement | Existing implementation | Required change | Expected affected area |
|---|---|---|---|
| Organization isolation for every business record/operation | Entities and repository methods generally carry/filter `OrganizationId`; no global query filters | Add `OrganizationId` to every new entity and include it in every read/write/delete, token lookup, export, background query, nested lookup, and unique/index key; add cross-tenant tests | Domain entities, `IRepositories.cs`, repositories, `TenebitDbContext`, services, tests |
| UTC persistence and organization-local scheduling | `IClock.UtcNow` and `Organization.TimeZone` exist; most date calculations use raw UTC date and no conversion helper exists | Validate IANA timezone, convert employment/digest local schedules to UTC deterministically (including DST), persist instants as `DateTimeOffset`, display in organization timezone | Organizations, offboarding scheduler, alerts/digest, DTO/frontend formatting |
| Existing Result/HTTP format, ActivityLog, paging, translations | Existing application/HTTP/audit/i18n mechanisms are reusable | Use them for every state change; page admin lists/history and generate exports server-side; add four-language keys | All application/API/frontend units |
| Person lifecycle (`Active`, `Offboarding`, `Inactive`) | `Person.IsActive` only; direct activate/deactivate in `PeopleService` | Add employment status/end/deactivated/preferred-language fields and invariant-preserving methods; keep `IsActive=true` for Active/Offboarding and false for Inactive; block new assignments/reservations for non-Active people | `Domain/People`, People DTO/service, EF mapping/migration, assignment/onboarding guards, frontend |
| `PendingReturn`, category return policies, inspection and dispositions | Asset statuses exist without `PendingReturn`; categories have no policy; returns always call `ReturnToStock` | Add policy enums/fields/defaults and explicit transitions preserving `AssignedPersonId` until physical receipt; support DirectToStock, inspection, vendor return, disposal, missing/damaged/retained operator resolution | Assets/categories, assignments, settings/assets UI, migrations |
| Partial return and backwards-compatible full return | `Assignment.Return` returns all `AssignmentAsset` rows; no item timestamps/resolution | Extend `AssignmentAsset`, add `PartiallyReturned`, implement idempotent per-asset return endpoint; keep current full-return endpoint delegating to item returns | Assignment domain/service/repository/DTO/API/UI/PDF/tests |
| Scheduled deactivation/license release/reservation cancellation | Only `AlertBackgroundService`/dashboard timer exist; no offboarding or reservation records | Add idempotent scheduled offboarding actions to the existing periodic execution mechanism, with separate action outcomes/retries; deactivate on local employment end even without employee response; reservation cancellation integration is completed when reservation records exist | Offboarding foundation, AlertBackgroundService or its existing cycle, licenses, later reservations |
| Secure public tokens | Identity `TokenHasher` exists; assignment public links use GUIDs | Add shared generation/hash/constant-time verification semantics and process fields (`ExpiresAt`, `RevokedAt`); regenerate revokes previous token; never log raw token; apply to offboarding/audit participants only | Application common/identity utility, offboarding/audits, link builder, API/tests |
| `AssetEvidence`, safe image handling, locking and privacy | Procedure documents store bytes in DB; no image evidence model/processor | Store contextual evidence in DB, validate actual JPEG/PNG/WebP signature and decoded image, re-encode without EXIF/GPS, enforce 5 MB/5-per-phase, lock after finalization, restrict downloads/deletes, audit downloads and retention erasure | Evidence domain/application/infrastructure/API, multipart endpoints, shared React uploader/gallery |
| Evidence privacy settings, retention, `LegalHold` | Organization has only profile fields; no retention or privacy model/job | Add organization-scoped settings and legal holds, default IP capture Off, configurable retention categories, periodic content erasure/anonymization, privacy notices by language, retention audit entries without deleted content | Settings/privacy domain/services, EF, worker, SettingsPage, public pages |
| Person privacy export/anonymization | No export/anonymize endpoint | Server-side organization-scoped person export covering existing/new records; controlled expired-data anonymization that respects active cases/legal holds and preserves inventory referential integrity | People/privacy service, repositories, API, ActivityLog/tests |
| Assignment/onboarding evidence and integrity compatibility | JSON assignment creation, onboarding delegates to assignment service except starter package; integrity hash has no version | Add multipart variants sharing the existing assignment workflow, atomic evidence+assignment persistence before email, evidence-aware integrity v2, preserve/version all existing records as v1, expose issue photos on public acceptance | Assignments/onboarding/evidence, link/PDF/API/frontend/migration |
| ExitProof case/item snapshots and state machine | No offboarding model/page/API | Add one open case per org/person, snapshots of assets/open assignments/license seats, required/manual items, computed ReadyToClose, explicit cancel/restore/waive rules, idempotent complete, role/permission enforcement and ActivityLog | New offboarding domain/application/repositories/API/pages plus existing People/MyWorkspace/Licenses/Dashboard |
| ExitProof public employee flow | No equivalent; current public assignment flow is GUID based | Token-only `/exit/{token}`, minimal/safe fields, masked serial, optional per-item response/evidence, privacy notice/language fallback, no direct asset state mutation; same case in MyWorkspace | Offboarding/evidence/link builder/API/App/MyWorkspace/translations |
| Offboarding physical resolution and protocol | Full assignment return and handover PDF only | Confirm individual receipt, inspection, missing/damaged/retained/waived decisions by authorized actors, enforce closure gates, store/reuse one final protocol and invalidate token | Offboarding + partial-return engine + QuestPDF + ActivityLog |
| Asset audit campaign snapshots/public responses | No campaign model/API/UI | Add campaign/participant/item aggregates, scoped preview/start snapshot, unique participant tokens, draft responses then submit lock/reopen, MyWorkspace surface; employee responses never mutate Asset | New asset-audit layers, public/API/frontend/evidence |
| Asset audit resolution/export/report | Reports page exists but no audit export | Authorized, noted exception resolution using existing Asset owner/status; paginated dashboard; server CSV and QuestPDF report preserving snapshot; explicit complete with nonresponses | Asset audits, assets, reports/PDF, ActivityLog |
| Configurable alert rules/digest | Hard-coded warranty/assignment/onboarding checks; `SentAlert` incorrectly represents failed sends as sent | Migrate delivery records with recipient/status/attempt/retry/digest link and logical unique key; add rule/digest settings, quiet hours/business days/timezone, SMTP retry, safe subjects/content, paginated history/test send; reuse one worker | Alerts domain/application/repo/background/API/SettingsPage |
| Reservation catalog and asset/category configuration | Asset has `Reserved` status but no reservations/reservability; no kit/catalog settings | Add reservable asset fields, category catalog metadata and simple category-quantity kits; existing assets default not reservable; aggregated catalog hides internal owner/serial/cost | Assets/categories plus reservations domain/repositories/catalog API/frontend |
| Employee reservation requests | MyWorkspace maps signed-in user to Person by email | Add draft/submit/cancel own requests only for `EmploymentStatus.Active`; no public token; category/kit requests with date validation and approximate counts | Reservation application/API/MyWorkspace/types/translations |
| Approval/allocation/conflict prevention | No reservation rows or concurrency model | Add approval/rejection/substitution/allocation with `RowVersion`, atomic overlap recheck, HTTP 409 without partial approval, manager direct-report policy and operator permissions, paginated queue/calendar | Reservation domain/repository/service/API/admin page/migration/tests |
| Reservation checkout/return/offboarding integration | AssignmentService creates assignments; full returns do not inform other processes | Checkout through existing assignment creation/evidence, preserve EndAt, revalidate availability, complete reservation after all assignment items resolve; offboarding rejects pending/cancels future reservations; PendingReturn never counts as available | Reservations, assignments, offboarding, alerts, MyWorkspace |
| Feature packaging | Free/Pro and Stripe exist; only asset count is enforced server-side | Add server-side entitlement checks for paid modules, not UI-only hiding; keep privacy notice, metadata removal, basic retention/export and organization deletion available on Free | SubscriptionService/plan model, module entry services/API/frontend |
| Accessibility/mobile/nonfunctional behavior | Existing responsive React layout and basic modal focus utilities; no feature-specific screens | WCAG-oriented labels, keyboard handling, error focus/summary, text progress, touch-sized evidence controls; list aggregation in DB, thumbnails on lists, no N+1/full-table exports | All new frontend/API list/report units |

# Implementation Units

## FND-01 — Employment lifecycle and new-obligation guards

- **Goal:** Introduce the compatible `Person` lifecycle and make every existing assignment/onboarding entry point reject people who are not `Active`.
- **Dependencies:** None.
- **Scope:** Domain invariants; additive nullable/defaulted columns; People API/DTO/UI; assignment and both onboarding package guards; preferred-language fallback field. Do not introduce offboarding cases yet.
- **Expected files/modules:** `Domain/People/Person.cs` plus lifecycle enum; `PeopleService`/DTOs; `AssignmentService`; `OnboardingService`; `TenebitDbContext`; migration/snapshot; `PeoplePage`; `types/domain.ts`; translations; backend tests.
- **Acceptance criteria:** Existing people migrate as Active with `IsActive=true`; Offboarding remains active for login/display but cannot receive a new assignment/onboarding package; Inactive has `IsActive=false`; activate/deactivate APIs cannot create contradictory field combinations; all reads/writes are tenant scoped.
- **Required automated tests:** lifecycle transitions and invariants; existing-person compatibility; guards for direct assignment, starter package, employee package; cross-organization person ID rejection; frontend status/action mapping.
- **Major risks:** breaking existing `UpdatePersonRequest.IsActive`; nullable/backfill semantics; dirty `AssignmentService`/translations overlap.

## FND-02 — Return policy, `PendingReturn`, partial returns, and inspection

- **Goal:** Establish one physical-return engine shared by ordinary assignments, offboarding, and later reservations.
- **Dependencies:** FND-01.
- **Scope:** Add category return/photo policies and checklist template; `PendingReturn`; item return metadata/resolution; `PartiallyReturned`; per-item return and inspection operations; keep full-return API compatible by invoking the same item logic. Do not implement offboarding screens or evidence bytes.
- **Expected files/modules:** `Domain/Assets/Asset.cs`, `AssetStatus.cs`, `AssetCategory.cs` and policy enums; `Domain/Assignments/Assignment.cs`, `AssignmentAsset.cs`, `AssignmentStatus.cs`; asset/category and assignment DTO/services; repositories; `TenebitDbContext`; migration; `TenebitEndpoints`; `AssignmentsPage`, `AssetsPage`/`SettingsPage`, domain types/translations; tests.
- **Acceptance criteria:** Item returns are idempotent; partial/complete statuses are computed; physical receipt clears `AssignedPersonId` only at receipt; DirectToStock+Reuse goes to InStock; InspectionRequired+Reuse goes to InService until technician completion; ReturnToVendor/Dispose never enter stock; employee-originated missing/damaged claims cannot invoke operator resolution; legacy full return still returns every open item.
- **Required automated tests:** partial and last return; repeat return; cancelled/closed mutation rejection; direct-stock/inspection/vendor/dispose transitions; lost/damaged authorization path; full-return regression; other-organization assignment/asset IDs; frontend action-state mapping.
- **Major risks:** old owned `assignment_assets` rows need safe nullable fields/default resolution; existing `ReturnedAt` meaning; illegal manual `AssetService.UpdateAsync` status changes bypassing physical rules; protocol compatibility.

## FND-03 — Offboarding scheduling kernel and idempotent digital actions

- **Goal:** Provide the minimal persisted offboarding case/item foundation required to schedule deactivation and license release independently of employee response or physical return.
- **Dependencies:** FND-01, FND-02.
- **Scope:** Core `OffboardingCase`/`OffboardingItem` identity/status/scheduled-action fields; creation/start snapshot needed to schedule; organization-local due calculation; existing worker extension; separate idempotent person and per-license action attempts; overdue immediate execution; failure visibility. Only the backend foundation needed for scheduling is in this unit.
- **Expected files/modules:** new `Domain/Offboarding`; application offboarding scheduler/service DTO subset; `IRepositories.cs`; offboarding repository; `TenebitDbContext`; DI; migration; `AlertBackgroundService` (reuse its periodic cycle or a single coordinated application cycle, not a duplicate timer); `License`/repository use; tests.
- **Acceptance criteria:** At employment end in organization timezone, person becomes Inactive despite no response/open equipment; assigned equipment remains owner-linked and PendingReturn; selected license seats release independently and idempotently; one failure does not roll back successful actions and is retried; a past end date executes immediately; repeated worker runs do not duplicate actions/logs.
- **Required automated tests:** local-time/DST boundaries; no-response deactivation; past date; repeat execution; one-license failure and retry; tenant-isolated worker batch; physical asset non-release regression.
- **Major risks:** multiple application instances racing; action-level persistence/transactions; no reservation model exists yet. Do not invent reservation storage now—complete pending-request rejection/future-reservation cancellation in RES-04 and add that integration before reservations are considered complete.

## FND-04 — Shared public-process tokens

- **Goal:** Implement one secure token mechanism for offboarding cases and audit participants.
- **Dependencies:** FND-03.
- **Scope:** Reuse/extend `TokenHasher`; generation, hash storage, fixed-time validation, expiration, revocation, regeneration; safe logging; link-builder methods and generic public error behavior. Do not replace legacy assignment/QR URLs.
- **Expected files/modules:** `Application/Identity/TokenHasher.cs` or a narrowly named common token service; offboarding token fields/service/repository; `IAppLinkBuilder`/`AppLinkBuilder`; API public route conventions; tests.
- **Acceptance criteria:** At least 32 random bytes/Base64URL; database stores hash only; old token invalid immediately after regeneration; expired/revoked/unknown produce the same minimal response; raw tokens never enter ActivityLog/server logs; participant/case access cannot cross process or tenant.
- **Required automated tests:** uniqueness/hash/no raw persistence; valid/expired/revoked/regenerated paths; constant-time comparison helper; token isolation across tenants/cases; log-detail redaction.
- **Major risks:** token lookup timing/enumeration; accidental raw token serialization; breaking existing public assignment links.

## FND-05 — Evidence core, safe image processing, privacy settings, and locking

- **Goal:** Store contextual, sanitized evidence once for assignment, return, offboarding, and audit consumers.
- **Dependencies:** FND-02, FND-04.
- **Scope:** `AssetEvidence`, phase/upload-source/lock/legal-hold metadata; organization privacy settings; actual-format validation; decode/re-encode JPEG/PNG/WebP without EXIF/GPS; limits; authorized list/download/delete; public-token upload authorization; thumbnails/list metadata; audit downloads/policy changes. Integration into each business form comes in its owning unit.
- **Expected files/modules:** new Domain/Application evidence files; organization/privacy settings model; repositories/DbContext/migration/DI; multipart API endpoints; one supported image-decoding implementation selected by Architect; shared `AssetEvidenceUploader`/`Gallery`; SettingsPage privacy tab; translations/tests.
- **Acceptance criteria:** Invalid, spoofed, executable/SVG/PDF, >5 MB, and sixth phase image fail before business state change; stored output contains no EXIF/GPS; evidence always belongs to matching organization/asset/process; locked evidence cannot be ordinary-deleted/replaced; public upload requires a valid process token; original download requires `evidence.view` and is audited.
- **Required automated tests:** signatures/MIME/size/count; metadata stripping using fixture images; lock/delete; authenticated/public permissions; cross-tenant and mismatched-process access; frontend size/count validation.
- **Major risks:** no current image decoder is installed and `System.Drawing` is unsuitable for Linux containers; binary DB growth; decompression bombs; transaction placement between sanitization and entity mutation.

## FND-06 — Retention, legal hold, privacy export, and controlled anonymization

- **Goal:** Make evidence/response retention and person privacy operations operational before product modules accumulate data.
- **Dependencies:** FND-03, FND-05.
- **Scope:** Retention settings by data category, legal-hold reason/actor/review date, periodic retention processing in the existing coordinated background cycle, person export endpoint, expired-data anonymization and audit. Basic functionality remains Free.
- **Expected files/modules:** privacy/retention domain types and services; evidence/offboarding repositories; organization settings; DbContext/migration; background cycle; People/privacy API; SettingsPage; tests.
- **Acceptance criteria:** Job erases eligible content/anonymizes only the target organization; legal hold blocks erasure; deactivation calculates retention but does not immediately erase history; export is tenant scoped and audited; anonymization preserves inventory/history referential integrity; audit message never retains erased content; UI shows purpose/next deletion/indefinite warning.
- **Required automated tests:** expiry boundaries; legal hold and review; tenant isolation; idempotent rerun; person export containment; active-case protection; Free entitlement access.
- **Major risks:** irreversible data removal, FK integrity, incomplete person-data inventory, backup behavior is operational/deployment policy and cannot be solved solely in application code.

## FND-07 — Reliable alert deliveries and scheduling primitives

- **Goal:** Stop failed SMTP attempts from being treated as sent and provide deduplicated retry/quiet-hour/digest-ready deliveries.
- **Dependencies:** FND-01.
- **Scope:** Evolve `SentAlert` in place (or migration-compatible successor) with recipient/status/attempt/next-at/error/sent-at/digest ID and logical key; pending-before-send workflow; bounded retry; organization timezone/quiet-hour/business-day primitives; preserve current warranty/assignment/onboarding alerts.
- **Expected files/modules:** `Domain/Alerts/SentAlert.cs`; `AlertCheckService`; `ISentAlertRepository`/repository; DbContext/migration; `AlertBackgroundService`; tests.
- **Acceptance criteria:** SMTP failure persists Failed and retries; success persists Sent; unique logical key includes organization/type/entity/threshold/due date/recipient; restart/repeated detection does not duplicate; disabled email behavior is defined and not falsely reported as externally delivered; no license keys/public tokens in content.
- **Required automated tests:** fail/retry/success; dedup per recipient; due-date change key; tenant isolation; quiet-hour and timezone boundaries; regression tests for existing alert types.
- **Major risks:** SMTP is not transactional; uniqueness races across instances; existing `SentAlert` rows lack recipient/status and require conservative migration semantics.

## FND-08 — Paid feature entitlements with privacy-safe exceptions

- **Goal:** Centralize server-side availability checks for the Pro modules without gating mandatory privacy controls.
- **Dependencies:** FND-06, FND-07.
- **Scope:** Extend the existing subscription/plan mechanism with simple feature keys/checks used by offboarding, evidence business features, audits, alert configuration, and reservations; frontend visibility mirrors, but never replaces, backend enforcement.
- **Expected files/modules:** `SubscriptionPlan`, `SubscriptionService` or one small application entitlement service, module entry services/API, frontend nav/page gating, tests.
- **Acceptance criteria:** Free cannot start paid product workflows; Pro can; existing Free assignment/return remains usable without photos; privacy notice, metadata stripping, basic retention, person export, and organization deletion/privacy operations remain available without Pro.
- **Required automated tests:** Free/Pro per feature; cancelled Stripe subscription fallback; privacy exceptions; no UI-only enforcement.
- **Major risks:** changing current Stripe plan behavior; enforcing too early on existing basic assignment flows.

## OFB-01 — ExitProof administrative case lifecycle

- **Goal:** Deliver organization-scoped draft/list/detail/edit/start/cancel/restore case management and snapshot visibility.
- **Dependencies:** FND-03, FND-04, FND-08.
- **Scope:** Complete Offboarding aggregate fields/statuses; one open case constraint; snapshot direct-owned assets, open assignments, seats, and manual tasks; block new obligations at start; set assets PendingReturn; computed progress/ReadyToClose; cancellation before deactivation and explicit restore after; permissions/activity events; paged UI and People action.
- **Expected files/modules:** Offboarding domain/application/repository/API; DbContext migration/index; `TenebitEndpoints`; `OffboardingPage`; `PeoplePage`; `LicensesPage`; `App.tsx`, `Layout.tsx`, API/types/translations/tests.
- **Acceptance criteria:** Second open case is rejected by service and PostgreSQL constraint; person without email can start; snapshot is stable after later source changes and flags conflicts; start immediately marks lifecycle/asset states without clearing owners; cancel rules restore only unreceived states and never resurrect cancelled reservations; restore employment lists manual recovery work; every state change is in ActivityLog.
- **Required automated tests:** duplicate/open constraint; snapshot completeness; no-email start; tenant isolation; computed status; cancel before/after deactivation; restore behavior; authorization for hr/operator/admin roles.
- **Major risks:** PostgreSQL partial unique index expression with string enum statuses; snapshot duplication between direct ownership and open assignments; start transaction atomicity.

## OFB-02 — Employee response, link management, and MyWorkspace

- **Goal:** Add optional public and authenticated employee response channels without coupling them to deactivation or physical receipt.
- **Dependencies:** OFB-01, FND-05.
- **Scope:** `/exit/{token}`, minimal DTO, preferred-language/privacy notice, per-item responses/comments/damage evidence, submit; resend/regenerate/revoke; active case card in MyWorkspace; mobile-accessible public page.
- **Expected files/modules:** Offboarding service/DTO/API; evidence integration; link builder/email templates; `PublicOffboardingPage`, `MyWorkspacePage`, App/api/types/translations/tests.
- **Acceptance criteria:** Valid token exposes only its case and safe serial subset; no cost/license key/internal comments/other people; response cannot change Asset owner/status or overwrite a physically resolved item; no response/expired token never blocks scheduler/admin; button/privacy language matches spec; resend does not log token.
- **Required automated tests:** public token isolation; minimal DTO; all response values; resolved-item lock; no asset mutation; preferred-language fallback; MyWorkspace email+org mapping; frontend response/action mapping.
- **Major risks:** privacy notice versioning per language; public evidence context binding; email subject data leakage.

## OFB-03 — Physical resolution, inspections, exceptions, and completion gate

- **Goal:** Complete the administrator/operator workflow from individual receipt through inspection or explicit exception.
- **Dependencies:** OFB-01, OFB-02, FND-02.
- **Scope:** confirm return, complete inspection, manual license release, missing/damaged/retained/waived decisions, scheduled-actions-now, item editing/manual tasks, operator queue, computed closure eligibility and permissions.
- **Expected files/modules:** Offboarding and Assignment services/domain; Asset transitions; LicenseService reuse; API; Offboarding/Assignments pages; ActivityLog timeline; translations/tests.
- **Acceptance criteria:** Employee claims remain informational; operator confirmation is required for Lost/Damaged; waiver requires `offboarding.complete`, reason, actor; received category policy drives stock/inspection/vendor/dispose outcome; required open item blocks close; early physical completion remains ReadyToClose until person deactivation/scheduled actions finish; all commands idempotent.
- **Required automated tests:** each resolution/role; employee-claim separation; inspection-only stock release; required-item gate; early completion; repeated command; cross-tenant IDs; transaction rollback on invalid mixed state.
- **Major risks:** coordinating AssignmentAsset and OffboardingItem without dual physical-return truth; role override behavior; partial state if multiple aggregates are saved separately.

## EVD-02 — Photos in assignment/onboarding/return and integrity version 2

- **Goal:** Integrate the evidence foundation into existing issue/accept/return flows without breaking legacy hashes or JSON clients.
- **Dependencies:** FND-05, OFB-03.
- **Scope:** multipart assignment and per-item return endpoints, onboarding evidence variant, public issue-photo display, atomic create/store-before-email and return/store transactions, evidence lock on acceptance/return, integrity versioning and PDF thumbnails/hashes. Preserve existing JSON endpoints.
- **Expected files/modules:** Assignment domain/service/DTO/API; OnboardingService/page; evidence service; `IPdfProtocolGenerator`/QuestPDF; App link/public assignment page; DbContext migration; Assignments/Onboarding pages; shared evidence components; tests.
- **Acceptance criteria:** No-photo callers behave unchanged; any image failure leaves no assignment/return state; acceptance sees issue photos; finalization locks photos; v1 hashes verify exactly as before; v2 includes ordered evidence IDs/phase/hash; old records are not rehashed; protocol contains thumbnails/hashes.
- **Required automated tests:** JSON regression; multipart atomic failure/success; onboarding route; lock timing; deterministic v2 hash; persisted v1 fixture compatibility; public evidence process isolation; frontend manifest/filter helpers.
- **Major risks:** dirty AssignmentService/email changes; multipart model binding; large PDF/binary payloads; preserving byte-stable business protocol on repeated download.

## ALR-02 — Configurable alert rules, digest, history, and current product sources

- **Goal:** Add administrator-managed rules/digest over the reliable delivery foundation.
- **Dependencies:** FND-07, FND-08, OFB-03.
- **Scope:** `AlertRule`, `AlertDigestSettings`, digest records; validation; immediate/digest/both; recipients and minimal permissions; current warranty, license, procedure, assignment/onboarding, and offboarding sources; test email; paginated history; Settings tab; generic subjects and safe links.
- **Expected files/modules:** Alerts domain/application/repositories/DbContext/migration; background check; email/link builder; settings API; `SettingsPage`; types/translations/tests.
- **Acceptance criteria:** Disabled rule creates no delivery; thresholds max 5 and 0..365; weekly requires day; local schedule/quiet hours/business days respected; digest groups due items and contains no keys/tokens; rule changes are audited; history is paged and errors length-limited; test send reports actual result.
- **Required automated tests:** validations; recipients/permissions; immediate/digest/both; timezone/quiet hours; empty digest; safe-content checks; query-count/aggregation-oriented repository tests where practical; frontend form validation.
- **Major risks:** recipient authorization calculation; query volume across organizations; holiday country semantics; campaigns/reservations sources do not exist yet and are added in their module units.

## OFB-04 — Final protocol, completion, dashboards, and sales-package polish

- **Goal:** Close ExitProof idempotently with one reusable final protocol and complete the required operator visibility.
- **Dependencies:** EVD-02, ALR-02.
- **Scope:** idempotent complete; stored/frozen protocol identity/content inputs; token revocation; offboarding QuestPDF with evidence appendix; list counters/details/timeline; Dashboard attention widget; plan enforcement and translations/accessibility pass.
- **Expected files/modules:** Offboarding service/domain; PDF abstraction/generator; repository/migration if protocol bytes/snapshot are persisted; dashboard service/page; Offboarding page; API/tests.
- **Acceptance criteria:** Complete fails until required items, deactivation, and scheduled actions resolve; repeated complete returns same case/protocol and never generates a second protocol number; protocol omits license keys/full IP, uses electronic-confirmation terminology, contains actors/dates/hashes/exceptions; download is tenant/permission scoped; final link is revoked.
- **Required automated tests:** closure gates; repeated complete/PDF business identity; token revocation; PDF model excludes secrets; dashboard organization isolation; authorization.
- **Major risks:** deterministic PDF metadata versus identical business content; PDF accessibility limitations require equivalent HTML detail; evidence size.

## AUD-01 — Asset-audit campaign creation and immutable snapshot

- **Goal:** Provide paged draft campaign management, scoped preview, start, participant/item snapshots, and completion/cancel state rules.
- **Dependencies:** FND-04, FND-05, FND-08, OFB-04.
- **Scope:** campaign/participant/item aggregates; scope filters by organization/team/location/category/person; preview; start snapshot and unique per-participant token; due-date extension/manual participant; permissions/activity; admin campaign list/detail creator.
- **Expected files/modules:** new AssetAudits domain/application/repos/API; DbContext/migration/indexes; App/Layout/page/api/types/translations/tests.
- **Acceptance criteria:** Preview/start reflect current `Asset.AssignedPersonId`; actual items freeze historical owner/location; participant tokens differ; no cross-tenant selection; started scope cannot be rewritten; missing email is warned and does not leak another address; lists are paged/aggregated server-side.
- **Required automated tests:** all scope modes and combined filters; snapshot immutability; token uniqueness; no-email; status transitions; tenant isolation; permissions; frontend scope/filter builders.
- **Major risks:** `ScopeJson` validation/history only; duplicate assets across filters; large campaign start transaction.

## AUD-02 — Public/MyWorkspace audit responses, reminders, and evidence

- **Goal:** Let each participant safely draft and submit their own answers through token or MyWorkspace.
- **Dependencies:** AUD-01, FND-05, ALR-02.
- **Scope:** public token DTO/item update/submit, MyWorkspace card, damaged-evidence requirement, submit lock/admin reopen, reminder delivery and campaign alert source, privacy notice/mobile form.
- **Expected files/modules:** AssetAudit application/API; evidence; alerts; `PublicAssetAuditPage`, `MyWorkspacePage`, App/api/types/translations/tests.
- **Acceptance criteria:** Token shows only participant items; drafts editable until submit; submit locks; only admin reopen re-enables and audits; public response never changes Asset owner/status; configured damaged photo required; reminder respects delivery rules; privacy text contains no consent claim/GPS collection.
- **Required automated tests:** participant isolation; draft/submit/reopen; completed/cancelled lock; no asset mutation; evidence requirement/context; reminders/dedup; MyWorkspace org/email isolation; frontend progress/action mapping.
- **Major risks:** race between submit and campaign complete; public upload rate limiting; reopened snapshot integrity.

## AUD-03 — Exception resolution, campaign report/export, and digest integration

- **Goal:** Resolve audit exceptions deliberately and produce historical CSV/PDF results.
- **Dependencies:** AUD-02.
- **Scope:** exception queue/filtering; accepted/lost/damaged/ownership-corrected/dismissed outcomes; note requirement; existing Asset mutation and ActivityLog; explicit completion with nonresponses; server CSV/QuestPDF; digest campaign section.
- **Expected files/modules:** AssetAudit service/domain/repository/API/page; Asset service/domain reuse; PDF abstraction/generator; alerts/digest; tests.
- **Acceptance criteria:** Employee answer alone never mutates Asset; authorized resolution does, with note for owner/status change; WrongOwner uses `Asset.AssignedPersonId`; auditor can view/export but not mutate; complete report preserves snapshot/nonresponse count after later asset changes; export is server-side and tenant scoped.
- **Required automated tests:** every resolution and authorization; note rules; cross-tenant target-owner rejection; nonresponse completion; snapshot report regression; CSV escaping/secret omission; digest section.
- **Major risks:** correcting ownership while an open assignment/offboarding conflicts; report N+1; status decisions requiring operator confirmation.

## RES-01 — Reservable asset/category/kit catalog

- **Goal:** Configure reservability and expose a privacy-safe, aggregated availability catalog.
- **Dependencies:** FND-02, FND-08, AUD-03.
- **Scope:** asset reservability/instructions/max days; category catalog visibility/name/description/image/selection mode; simple kit definition as category+quantity rows; existing assets default false; operator availability query considering present physical state and existing assignment/offboarding facts.
- **Expected files/modules:** Asset/AssetCategory domain/service/DTO/UI; new reservation/kit domain/repositories; DbContext/migration; catalog API; Assets/Settings/MyWorkspace UI; types/translations/tests.
- **Acceptance criteria:** Catalog shows categories/kits/counts, not owners/cost/full serial/list of inventory; PendingReturn/InService/Damaged/Lost/Retired/Disposed and assigned/open-assignment assets do not count; DirectToStock/inspection completion makes eligible InStock assets appear automatically; exact asset selection only for configured shared categories.
- **Required automated tests:** migration default false; each exclusion; counts/quantities/kits; automatic availability after return/inspection; privacy DTO field whitelist; tenant isolation; frontend date/filter helpers.
- **Major risks:** availability source before reservation rows exist is provisional; category images should reuse evidence/file safety rather than create arbitrary paths; expensive count queries.

## RES-02 — Employee reservation request lifecycle

- **Goal:** Add authenticated own draft/submit/edit/cancel requests in MyWorkspace.
- **Dependencies:** RES-01, FND-01.
- **Scope:** reservation/item aggregate base; date/purpose/location/kit expansion; signed-in Person-by-email lookup; Active-only request; approximate availability; own-record API and MyWorkspace tab; activity and entitlements.
- **Expected files/modules:** Reservations domain/application/repositories/API; DbContext/migration; MyWorkspace service/page; App API/types/translations/tests.
- **Acceptance criteria:** Unlinked account cannot create; Offboarding/Inactive cannot create; requests default to category/kit rather than asset IDs; invalid/max-duration interval rejected; submitted request is visible only to requester and authorized staff; cancellation before checkout is idempotent; PendingApproval does not block stock.
- **Required automated tests:** identity linkage and tenant isolation; employment guards; interval/max days; kit expansion; own-access authorization; submit/edit/cancel transitions; frontend validation/action mapping.
- **Major risks:** users' globally unique email versus organization Person mapping; timezone semantics for pickup intervals; category availability can change before approval.

## RES-03 — Approval, allocation, substitution, concurrency, and calendar

- **Goal:** Prevent double-approved asset intervals while supporting operator allocation and management views.
- **Dependencies:** RES-02.
- **Scope:** full reservation/item statuses; exact allocation/substitution history; approver rules including optional direct-manager policy; transaction and `RowVersion`; database overlap recheck; 409 result; paged queue/calendar/conflicts/today/overdue UI; alert sources for approval/pickup/overdue.
- **Expected files/modules:** Reservation domain/service/repository; authorization settings; DbContext/migration/indexes/concurrency; API/Result mapping; ReservationsPage; alerts/digest; tests.
- **Acceptance criteria:** Pending requests do not block; approval atomically allocates all items or none; overlap returns Conflict/409 with unavailable items; two stale/concurrent approvals cannot both succeed; substitution retains original asset/reason; managers only approve allowed direct reports; list/calendar tenant scoped and bounded.
- **Required automated tests:** overlap boundary rules; all-or-nothing multi-item approval; simulated stale row version; PostgreSQL-backed unique/concurrency verification where environment permits using existing xUnit; manager/role authorization; substitution; alert delivery; frontend conflict handling.
- **Major risks:** PostgreSQL has no simple cross-row exclusion via ordinary unique index; service transaction/locking strategy must be designed and tested; multi-instance races; calendar query performance.

## RES-04 — Checkout, assignment returns, offboarding cancellation, and full-cycle integration

- **Goal:** Connect approved reservations to the existing assignment/evidence/return system and close the remaining cross-module rules.
- **Dependencies:** RES-03, EVD-02, OFB-04.
- **Scope:** final availability recheck; checkout through `AssignmentService` with optional issue evidence; copy EndAt/due date; reservation assignment link/status; completion when all AssignmentAssets are finally resolved; offboarding start rejects PendingApproval and cancels configured future Approved reservations; reservation catalog respects active offboarding; final integrated dashboard/alerts.
- **Expected files/modules:** Reservation/Assignment/Offboarding services and repositories; API; Assignments/MyWorkspace/Reservations/Offboarding pages; alerts/digest; ActivityLog; tests.
- **Acceptance criteria:** Approval alone creates no assignment; checkout requires every concrete asset and revalidates InStock/IsReservable/no overlap; assignment is the existing assignment, not a parallel checkout record; partial return does not complete reservation, final item does; offboarding immediately blocks new requests, rejects pending and cancels future approved as configured; person deactivation never makes PendingReturn stock; inspected return automatically restores catalog eligibility.
- **Required automated tests:** checkout atomicity and field copying; issue evidence path; partial/final return integration; offboarding pending/approved cancellation and idempotency; no automatic inventory release; full-cycle tenant isolation; cumulative regression across all modules.
- **Major risks:** circular orchestration between services; avoid duplicating return/owner state; historical cancelled reservations are not restored on offboarding cancellation; cumulative transaction boundaries.

# Compatibility Risks

- Existing `Person.IsActive` clients and rows must continue to work. The migration must backfill lifecycle from current `IsActive`; API changes should be additive or keep accepting the current request shape until the frontend and external clients migrate.
- Existing assignment JSON endpoints, public GUID acceptance links, QR links, protocol downloads, and assignment hashes must remain valid. New token rules apply only to new offboarding/audit links unless a separately versioned migration for assignment links is approved.
- Existing `Assignment.AcceptanceHash` and `ProcedureAcceptance.ConfirmationHash` were created without evidence. Add `IntegrityVersion=1` for existing rows and dispatch verification by version; never rewrite historical hashes.
- `Assignment.ReturnedAt` currently means the whole assignment returned. Partial returns need item timestamps while setting the aggregate timestamp only at final resolution, without changing old Returned rows.
- Existing `assignment_assets` rows have a return condition but no resolution. Migration/backfill must infer only safe facts: rows under aggregate `Returned` may be marked Returned at aggregate `ReturnedAt`; open rows remain unresolved. Do not infer Lost/Damaged from free text.
- Existing category rows need safe defaults. System laptop/phone/vehicle categories require InspectionRequired+Reuse; simple accessories may default DirectToStock+Reuse only through explicit, deterministic migration/seed logic. Customer-created categories should receive conservative defaults rather than name guessing.
- Existing assets must default `IsReservable=false`; no migration may expose inventory to employees automatically.
- Existing `SentAlert` rows lack recipients/status. Treat them conservatively as historical sent/dedup records and do not re-send old alerts during migration; new recipient-specific delivery keys begin from the migration boundary.
- Current dirty localization/email work overlaps required hotspots. Every implementation unit must merge with, not replace, those changes.
- No implementation unit may rename routes/classes solely to match `features.md`; established minimal API, repository, DTO, service, React page, and CSS conventions take precedence.

# Security Risks

- Cross-tenant object access is the highest risk because repositories rely on explicit filters. Every service must pass current `OrganizationId`, verify related IDs belong to it, and public token resolution must derive the organization from the matched hashed record rather than trust an organization ID from the request.
- Raw public tokens must exist only at generation/link-send time. Do not place them in database fields, entity `ToString`, exceptions, ActivityLog details, structured logs, digest bodies, or analytics.
- Public invalid/expired/revoked token responses must be indistinguishable and reveal no internal GUIDs or prior organization membership.
- Employee/public audit and offboarding responses are claims, not authorization to mutate owner/status. Only authorized administrative resolution paths may set Lost, Damaged, Retained, or ownership changes.
- Evidence uploads are hostile input. Validate decoded content, dimensions/resource use, signatures and MIME; re-encode; discard metadata; never use filename as path; bind evidence to token process/item; enforce rate limits before expensive work where possible.
- Full evidence downloads, person export, legal-hold/policy changes, waivers, and status/owner corrections require explicit permissions and ActivityLog entries.
- Current `CurrentUser.IpAddress` trusts `X-Forwarded-For`; evidence IP capture must honor organization default Off and deployment proxy trust configuration. Full IP must never enter normal PDF/email and needs separate retention/access.
- Reservation catalog/API must use response whitelists to avoid serial, price, current owner, sensitive custom fields, or detailed inventory disclosure.
- Email subjects must remain generic; license keys and raw public tokens must never be placed in digest/content beyond the one intended public link sent to its participant.
- Paid-plan checks must be backend enforced, but security/privacy controls cannot be disabled when an organization downgrades.

# Database/Migration Risks

- PostgreSQL/Npgsql supports partial indexes, so one-open-offboarding-per-organization/person can use a partial unique index after confirming exact string-enum column values. Service validation is still required for useful errors.
- All new unique keys/indexes must start with `OrganizationId` unless the value is intentionally globally random (token hashes can be globally unique but still need organization/process checks). Index status/due dates, token hashes, evidence process keys, alert next-at/status, and reservation interval/allocation lookups.
- Additive migrations should use defaults/nullability that keep current rows readable, then backfill deliberately. Avoid destructive renames or table replacement for `SentAlert`, assignments, people, assets, categories, licenses, and subscriptions.
- Owned `AssignmentAsset` rows use `(AssignmentId, AssetId)` and do not have their own surrogate ID. Evidence and offboarding references must use the established composite/process context safely or introduce an ID only with an explicit compatibility migration.
- Binary evidence in PostgreSQL can increase database and backup size. Lists must return metadata/thumbnails only and retention must clear content without deleting required audit identity.
- Legal hold and retention deletion are irreversible and need idempotent predicates, bounded batches, tenant filters, and transactional audit markers.
- SMTP delivery cannot share a database transaction with the network send. Persist Pending first, send, then persist Sent/Failed; unique logical keys prevent duplicate detection, while retry handles crashes.
- Reservation overlap is a cross-row concurrency problem. `RowVersion` alone protects one request but not two different reservations; Architect must choose a PostgreSQL-safe transaction/locking or constraint strategy and verify it under concurrent attempts.
- Background action concurrency requires claim/version semantics so two app instances do not release the same seat, generate two protocols, or duplicate ActivityLog events.
- Generated migrations and the model snapshot must be reviewed together; validate upgrade from the current snapshot with existing representative rows, not only creation of an empty database.

# Recommended Implementation Order

Follow `features.md` section 11 and approve each unit independently before the next begins:

1. FND-01 — Person lifecycle and obligation guards.
2. FND-02 — PendingReturn, category policies, partial returns, inspection.
3. FND-03 — Scheduled deactivation/license actions. Reservation cancellation is explicitly completed in RES-04 because reservation records do not yet exist.
4. FND-04 — Shared public tokens.
5. FND-05 — Evidence/privacy-safe upload foundation.
6. FND-06 — Retention, legal hold, privacy export/anonymization.
7. FND-07 — Reliable alert deliveries/retry/quiet-time primitives.
8. FND-08 — Module entitlement gates with privacy exceptions.
9. OFB-01 — Administrative ExitProof lifecycle and snapshots.
10. OFB-02 — Employee token/MyWorkspace response.
11. OFB-03 — Physical returns, inspections, and exception resolution.
12. EVD-02 — Assignment/onboarding/return photos and integrity v2.
13. ALR-02 — Configurable rules/digest for existing and offboarding sources.
14. OFB-04 — Final protocol/completion/dashboard; this completes the first saleable package.
15. AUD-01 — Campaign creation/snapshot.
16. AUD-02 — Public/MyWorkspace responses/reminders/evidence.
17. AUD-03 — Exception resolution/report/export/digest.
18. RES-01 — Reservable configuration/catalog/kits.
19. RES-02 — Employee requests.
20. RES-03 — Approval/allocation/concurrency/calendar.
21. RES-04 — Checkout/return/offboarding/alerts and cumulative full-cycle integration.

For every unit: Coder runs the smallest relevant backend/frontend builds and tests plus new tests; Reviewer checks the actual diff, tenant filters, authorization, ActivityLog, migration safety, UTC/timezone behavior, and compatibility. After RES-04, run the full backend test suite, frontend Vitest suite/build, migration validation against representative existing data, the section 12 manual scenarios, and a cumulative independent final review.
