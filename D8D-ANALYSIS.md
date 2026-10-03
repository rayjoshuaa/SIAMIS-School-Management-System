# D8D — Leave evidence and sandwich rule analysis

Date: 2026-10-03. **Analysis only; implementation is stopped pending the decisions in section 21.**

Repository root: `C:/Users/USER/Documents/ChatGPT/SIAMIS School Management System`.

The working tree was clean at inspection. This report is the only new file. No application files, database data, migrations, Attendance, Payroll or Git state were changed. No build, tests, API mutations or database commands were run. Existing verification results in D8C-REPORT.md are historical evidence, not newly executed tests.

## 1. Repository evidence

| Inspected implementation | Finding |
|---|---|
| EmployeeDocument; EmployeeContractDocumentContracts | FileName and StorageKey; verification status, employee verifier ID and caller-provided verification timestamp; no leave relationship, MIME type or file size. |
| EmployeeContractDocumentService, lines 76–140, 156–185 | Metadata reads expose StorageKey; create/update accept it; document update replaces verification and storage metadata; deletion is blocked for contract links only. |
| EmployeeConfigurations, lines 117–162 | Contract ownership is enforced through `(EmployeeId, DocumentId)` referencing `(EmployeeId, EmployeeDocumentId)`; document employee/type/verifier FKs use NoAction. Verifier references Employees. |
| EmployeeDocumentsController | Explicitly documents metadata-only CRUD; no bytes are uploaded, downloaded or written. |
| Program.cs | Controllers, SQL Server, Swagger and static files; no authentication registration, authentication middleware or authorization policies. |
| Search of src excluding migration copies | No IFormFile, PhysicalFile, FileStream, ClaimsPrincipal or Authorize match; no implemented secure binary delivery identified. StorageKey occurs in the document model, contracts, query/service and EF mapping. |
| MasterDataSeeds and InitialHrDatabase seed excerpt | DOC-011 is Medical Certificate, ID `b0000000-0000-0000-0000-000000000011`. All three document flags initially false. These are repository seed defaults, not a fresh query of mutable Development master data. |
| LeaveRequestCalculator, lines 73–98 | Frozen certificate reasons and policy evidence; SandwichParticipation is metadata only. |
| EmployeeLeaveService | Serializable creation locks employee, sorted calendars, LeaveType; transitions lock employee then request. Approval validates frozen calculation and reservations, without checking attached evidence. |
| LeaveSnapshotIntegrity | Validates snapshot against header, schedules, intersections, allocations and certificate facts using frozen data; does not consult current configuration. |
| EmployeeLeaveReadService; LeavePolicyEntitlementService | Pending/Approved allocation accounting; append-only adjustments cannot reduce entitlement below committed usage/reservations. |
| D8C migration and EF configurations | Request lifecycle checks and unique request/year allocations; no evidence or sandwich tables. |

The source and D8C report support the approved D8B/D8C contracts. This review does not independently assert the current SQL database contents.

## 2. Current EmployeeDocuments security findings

The existing API is a 201-file metadata workflow. It does not establish that a file exists, is safe, is privately stored, or was reviewed by an authorized person.

- StorageKey is supplied by callers, trimmed and length checked, and returned in ordinary DTOs. There is no storage-root validation, opaque server allocation or binary validation in this workflow. Because it does not access files, this is not evidence of an existing exploitable file-reading route; it is an unsafe contract to reuse for future delivery.
- FileName has only required/length checks. There is no separate validated content type, size, hash, scan result or binary storage service.
- VerificationStatus permits arbitrary non-Pending text within its length limit. VerifiedBy and VerifiedAt are caller supplied. VerifiedBy must identify an existing Employee, which is not an authenticated SIAMIS user.
- Non-Pending verification is rejected when DocumentType.RequiresVerification is false. Therefore the seeded DOC-011 contract cannot simply be used as accepted medical evidence. D8D must not silently change that master flag or reinterpret Pending as accepted.
- PUT can change storage identity, type and verification metadata. DELETE checks contract ownership but knows nothing about historical leave evidence.
- Employee/child ownership checks prevent attaching another employee's row through existing routes. They do not authorize the caller to read or administer that employee's medical information.
- Program.cs serves static files. Medical content must never be placed in that public tree.

**Recommendation:** a dedicated leave-evidence boundary; do not treat a mutable EmployeeDocument row or its claimed verification fields as sufficient proof.

## 3. Proposed evidence model

Proposed names and fields below are design recommendations, not implemented contracts.

Use a small `EmployeeLeaveEvidence` receipt/version model with GUID EvidenceId, EmployeeId, LeaveId, required DocumentTypeId, optional SupersedesEvidenceId, server RecordedAt, and immutable evidence description/reference. Each replacement creates another version. In a metadata-only phase, an external reference must be opaque and must not pretend to prove stored binary delivery.

For future binary receipts add a server-generated internal storage identifier, sanitized original display filename, validated content type, actual byte size, server UploadedAt and content digest. Keep the storage identifier out of normal DTOs and caller contracts. Metadata-only records must distinguish absence of a binary receipt from a completed upload; no invented size or upload timestamp.

Use a small append-only evidence event history for Accepted/Rejected/Superseded actions: event ID, evidence ID, state/action, required review explanation where appropriate, server UTC time and future nullable authenticated actor. Ownership should be enforced relationally through `(EmployeeId, LeaveId)` to an alternate key on EmployeeLeave, plus same-owner validation for supersession. FKs use NoAction; a successor cannot reference itself or form a cycle. The service enforces acyclic append-only supersession under locks.

Record the exact accepted EvidenceIds used by each approval in an immutable approval-evidence association. This prevents a later replacement from changing which version justified an historical approval. Avoid a second general-purpose document-management engine. A future private blob service could be shared with a hardened 201 workflow, but this analysis does not require redesigning that workflow.

## 4. Upload/download security boundary — STOP

**A. Foundation possible now:** schema/contracts for ownership, evidence receipts, append-only review history and frozen approval references; synthetic metadata tests in restricted local Development. Metadata itself is sensitive. This is not safe public medical handling and must not receive real medical information through unauthenticated routes. Approval/evidence commands without authentication cannot establish that an HR officer performed the action.

**B. Defer actual binary upload/download:** the repository lacks caller authentication and resource/role authorization. Opaque IDs and parent ownership do not solve this. No production secure-delivery claim is possible now. Authentication/RBAC remains Production Hardening; no fabricated user identity is proposed.

Before binary release require authenticated upload/download/review authorization, employee/leave ownership checks on every operation, private storage outside webroot, server-only storage allocation and controlled downloads. A storage adapter must constrain resolved paths to its private root, reject traversal and never combine client filenames into storage paths. Sanitize display names and Content-Disposition headers; use attachment delivery, safe content types, no MIME sniffing and private/no-store caching.

Use a narrow approved extension/content-signature allowlist and enforced upload/stream size limit; do not trust client MIME types. Proposed starting allowlist is PDF/JPEG/PNG, subject to approval of formats and size. Include quarantine/content scanning before acceptance, storage ACLs, protected transport/backups, access audit and retention/access-revocation policy. Do not log content, paths or medical details. No filesystem/cloud provider is selected by existing code.

Do not mark a metadata receipt accepted merely to bypass deferred binary verification. Decide explicitly whether the first foundation permits independently reviewed external evidence or keeps required-evidence approvals blocked until secure delivery exists.

## 5. Verification/acceptance recommendation

Require an explicit **Accepted** review state. Uploaded/Recorded only means receipt; it does not prove relevance, authenticity or that a reviewer checked the required evidence.

Acceptance must bind to an immutable version, employee, request and required document type. The reviewer confirms relevance to the request period; do not invent automatic expiry, medical diagnosis or document-number requirements. Caller-selected VerifiedBy/VerifiedAt from the existing document API cannot provide this authority. Acceptance time is server UTC; real reviewer identity is deferred until authentication exists. An external-review foundation, if approved, must explicitly describe its limited trust boundary.

## 6. Evidence history

Append new receipts and review/supersession events. Freeze receipt contents on submission; Accepted versions cannot be overwritten. A corrected document creates a successor and requires its own acceptance.

Before approval, a superseded version cannot satisfy the current requirement. After approval, preserve the approval-to-version association even if a successor later exists. Do not hard-delete Approved evidence or unlink its approval association. Rejection/cancellation retains evidence/history. Any future exceptional revocation must be a dedicated audited correction, never a silent erasure of why approval occurred. Binary retention/deletion rules remain a separate security decision; audit metadata must not falsely imply bytes still exist after an authorized retention action.

## 7. Exact certificate approval rule

Keep Pending creation without evidence. Proposed approval flow, inside the lifecycle transaction:

1. Lock/reload the employee and Pending request; run existing LeaveSnapshotIntegrity.
2. Read frozen SupportingDocumentRequired. If false, impose no additional mandatory evidence.
3. If true, identify required DocumentTypeIds from the frozen policies that actually triggered requirements. Require current Accepted, non-superseded evidence of each required type, belonging to this employee and this request. Missing/Recorded/Rejected evidence blocks approval with a clear 409 prerequisite conflict.
4. Freeze the exact accepted versions used; perform existing balance checks and transition atomically. An evidence failure leaves status, timestamps, allocations and balances unchanged.

Step 3's **all distinct required types** is a recommendation requiring approval. D8C stores an aggregate reason list rather than a reason-to-policy/type association; it permits multiple revisions/document types. The type set can be derived from frozen per-date policy evidence and the frozen longest sequence using exactly the existing triggers, without live configuration or changing certificate semantics. Prefer persisting this mapping for new D8D snapshots; older valid D8C snapshots can derive it deterministically from their own frozen facts. Missing/inconsistent facts require review, never today's DOC-011 fallback.

Established triggers remain: AlwaysRequired on a positively charged working date; Conditional consecutive sequence strictly greater than N; Conditional Monday/Friday only on positively charged scheduled working dates. Any positive partial charge counts as one qualifying certificate date. Nonworking dates neither count nor break; scheduled working dates with zero charge break. Current request only. Sandwich debits do not become certificate qualifying working dates. A sandwich exception does not waive medical evidence automatically.

## 8. Sandwich detection contract

Find a maximal continuous nonworking span with zero scheduled intervals for this employee on every intervening date. Its boundaries are the immediately adjacent scheduled working dates, not fixed Friday/Monday labels. Include actual holidays, weekly rest, calendar switches and replacement exceptional-working intervals.

Qualify a boundary only when its frozen charged intervals cover **all** its frozen scheduled intervals, with a positive schedule. Compare interval coverage, not FullDay mode, Days, or an assumed daily duration. A Timed request covering every interval may qualify; a one-hour partial request does not. Lunch gaps are excluded. An ExceptionalWorkingDay interrupts a nonworking span.

Support one request spanning both boundaries and two separate requests. Proposed V1 requires a single request to cover each boundary completely; do not union multiple partial requests on a boundary without approval of that additional aggregation rule. Rejected/Cancelled requests never qualify.

Do not search an arbitrary short window and silently miss long school holidays. Resolve until the next working date with bounded implementation safeguards; unresolvable employment/calendar coverage returns an explicit configuration conflict.

## 9. Policy participation boundary

Proposed smallest V1: both boundary-date frozen policies must participate, and every intervening date must have a participating Published policy for the same LeaveType. Require consistent BalanceTracked classification across the case. A false participating flag means no sandwich under this proposed contract; missing/ambiguous required coverage means conflict, not implied exemption.

This stricter gap-policy participation rule is a proposed decision, not something implemented by SandwichParticipation today. Policies can have different revision IDs across dates; preserve each revision and its charge configuration. Do not mutate Published policies or introduce calendar defaults.

## 10. Same/different LeaveType

Recommend **same LeaveType only** for V1. The independent case owns that type's debit; neither boundary request's immutable allocation is enlarged. Mixed participating types require an explicit ownership/priority/split rule and are deferred. Expose a diagnostic explanation when a pair is ineligible under this contract rather than picking an arbitrary owner.

## 11. Paid/Unpaid

Recommend matching non-null frozen IsPaid on both boundaries and freezing that classification on the case. Different types and mixed Paid/Unpaid pairs do not charge under proposed V1. Even the same type can have different historical classification after a master update; do not resolve this with its current flag.

The case's classification is HR history only. No baht amount or salary deduction follows. Unknown historical IsPaid requires integrity review. Intervening policies do not contain their own IsPaid field; adopting matching boundary classification for the case requires approval.

## 12. Charge unit — STOP / approval required

| Option | Assessment |
|---|---|
| Policy-defined equivalent leave-day minutes | Recommended: integrates with minute entitlement while explicitly representing a balance debit, not work performed/scheduled. |
| Derived standard day | No approved derivation exists; varying schedules and calendar changes make this ambiguous. |
| Separate days/quantity only | Honest quantity but cannot debit existing minute budgets without another conversion contract. |

Propose positive integer `SandwichEquivalentDayMinutes` on future policy versions, required when participating. Freeze the applicable per-gap-date value and sum **SandwichDebitMinutes** separately from normal scheduled ChargeableMinutes. Preserve each date's ScheduledMinutes=0 and empty working/charged intervals. Do not add fictional Attendance intervals or increase EmployeeLeave.Days.

No default 480, no assumed eight hours, no seeded school value and no monetary conversion. Existing Published participating policies have no charge configuration: do not modify them or guess a value. They require explicit new approved policy coverage or a stop on incompatible coverage. No automatic shortening of Published predecessors.

For nontracked policies, preserve the case/date equivalent debit facts but require no entitlement and expose no fabricated unlimited balance. For tracked cases require every affected year's entitlement; missing versus configured zero remains distinct.

## 13. Cross-request behavior

Recommend a separate versioned `EmployeeLeaveSandwichCase` with employee, owner LeaveType, boundary dates/LeaveIds, frozen IsPaid/BalanceTracked, detection timestamp, versioned calculation JSON and date/year debits. One request may occupy both boundary links. No rewriting either request's D8C snapshot.

At creation of the request completing the pair, detect and reserve normal minutes plus any new case debit atomically. Pending/Pending or Pending/Approved cases remain Reserved. Both Approved changes Reserved to Charged in the final boundary approval transaction. A same-request case becomes Charged on that request's approval. Approval is blocked on evidence prerequisites independently.

Insufficient entitlement rolls back the new request and case together; it does not withdraw the older Approved request. Include case reservations in all availability reads/checks and the entitlement-adjustment guard. Do not wait until both approvals to reserve and allow the second request to overspend.

Use an employee/nonworking-span identity and uniqueness protection so identical spans cannot be charged twice. Preserve released cases and events; a replacement boundary creates a new revision with one active revision per span. Re-detection is idempotent and cannot duplicate charges. No retroactive batch charging of pre-D8D Approved pairs without a separately approved historical process.

**Missing frozen facts:** separate requests generally do not snapshot gap dates; neither stores charge equivalents, case ownership, counterpart identity or exception history. Freeze previously uncaptured gap employment/calendar/assignment/policy facts when the case is first detected, clearly recording that observation time. Use existing frozen gap facts when available; if two source snapshots disagree, require review rather than selecting the newest. Do not treat current gap configuration as proof of what an old Approved request knew. After detection, approval uses the frozen case, even if live schedules change.

## 14. Cancellation/rejection

Under the same employee lock, rejecting or cancelling either boundary releases the Reserved case or reverses the Charged case's balance effect exactly once. Retain original calculation, approval association, allocations and a release event with cause/request/server time. Preserve the unaffected boundary's status and original minutes.

Retain D8C ExpectedStatus and Approved cancellation reason rules. Repeated/stale commands cannot release twice. A later replacement request may form a new case revision; keep the old case and release history. This derived reversal is explicit sandwich lifecycle history, not mutation of the original Approved leave snapshot.

## 15. Exception/override model

Use a dedicated append-only case exception event: GUID ID, case/employee and affected request identities, mandatory reason, UTC server timestamp, nullable future actor and expected case state. Normal request JSON must not accept a bypass boolean or client audit fields.

Recommend deliberate HR review **after detection and before final charge**, through a dedicated command. Reserved -> Exempted releases only sandwich reservation; normal working leave remains. The exemption is scoped to that case revision and does not automatically transfer to future replacement requests. Preserve evidence references supporting the reason if needed, without automatically reading medical files.

For smallest V1 disallow post-charge exception mutation; a future dedicated audited correction can support it if the school requires that now. “Valid reason” is an HR decision requiring explanation, not inferred from leave Reason, SuddenIllness, uploaded bytes or arbitrary nonempty text. Real HR authorization remains blocked on authentication/RBAC.

## 16. Cross-year

Allocate each equivalent debit by the intervening date's calendar year, not either request's start year. One span can debit two annual budgets; reserve all affected years atomically or fail entirely. Retain per-date units, policy revision and summed year allocations. Apply the same rule for Used/release and preserve all allocations historically.

This reuses D8B's calendar-year convention. Equivalent debit ownership/conversion still needs approval; no carryover or annual grant is invented.

## 17. Concurrency and locking

Keep Serializable and the shared employee UPDLOCK as the first mutation lock. For new gap resolution follow D8C's employee -> sorted calendar GUIDs -> sorted LeaveType GUIDs order. Then lock relevant request GUIDs, case/evidence GUIDs deterministically and read affected year budgets in ascending year order. Frozen-only transitions need no new calendar/type locks. All new writers must use the employee lock, including evidence decisions, exceptions and case release. Existing calendar/policy writers remain compatible with their parent locks; do not acquire an employee lock after taking those configuration locks.

| Race | Required outcome |
|---|---|
| Upload/attachment vs approval | Publish the completed receipt/link under employee/request locks; approval sees either a complete eligible version or fails prerequisites. Never hold SQL locks while streaming bytes. Private staging and cleanup must handle interrupted uploads. |
| Acceptance vs approval | Locked re-read of evidence state; success references that exact immutable accepted version. |
| Second request vs approval | Same employee serialization; exactly one case reservation, correct boundary statuses. No Approved snapshot rewrite. |
| Exception vs approval | Expected case state checked under locks; exemption/charge has one ordered winner, with a stale command conflict. |
| Boundary cancellation/rejection vs charge | Atomically transition request and release case; no residual/double Used debit. |
| Concurrent forming requests | Existing charged-interval overlap plus case uniqueness and employee serialization prevent duplicate spans/reservations. |
| Entitlement adjustment vs case reservation | Extend the existing guard to include case Pending/Used amounts under the same lock. |
| Supersession vs approval | Freeze versions under locks; successor cannot replace historical approval evidence. |

Use coherent transactional case/balance reads. Preserve sanitized 409 concurrency responses, explicit expected-state checks and database uniqueness; do not mask stale commands with blind retries. Storage and SQL are separate durability boundaries, so future binary delivery needs private staging/finalization and orphan cleanup, not claims of a single filesystem/SQL transaction.

## 18. Schema changes likely required

Focused future migration(s), after decisions: evidence receipts/events and approval references; EmployeeLeave alternate ownership key; sandwich cases/events plus per-date/year debit rows; policy equivalent-minute configuration and new snapshot format where needed. Add NoAction FKs, ownership checks, unique active span/revision protection, positive debit/year constraints and typed state validation.

No statutory entitlement seed, actor FK to Employees, rewrite of historical request snapshots or Payroll/Attendance schema change. Future actor type/FK must follow the eventual authenticated-user architecture. Exact DDL/table count is intentionally deferred until the approved minimal contracts are settled.

## 19. API changes likely required

Proposed routes beneath `/api/employees/{employeeId}/leave/{leaveId}`: GET/POST evidence metadata, dedicated evidence acceptance/rejection/supersession commands, and read-only evidence history. Approval adds evidence/case prerequisite checks and returns clear 409 conflicts. Evidence DTOs omit storage identity; reject unknown client audit/provenance fields.

Employee-scoped sandwich list/detail/history and a dedicated case `/exception` command; no generic status/bypass PUT. Include separately named scheduled leave and sandwich debit totals in balance/detail outputs. Keep normal request calculated fields server-owned. Existing approve/reject/cancel commands reconcile affected cases atomically.

Multipart upload and controlled download routes are **deferred**, not offered as unauthenticated secure APIs. Any metadata acceptance endpoint before hardening must be restricted synthetic Development functionality or an explicitly approved external-review foundation.

## 20. Security/privacy risks

Unauthenticated employee document and leave routes expose metadata and permit workflow actions to anyone who can reach them. GUID knowledge is not authorization. Medical filenames/reasons, evidence existence, history and review outcomes are themselves sensitive. Existing verifier Employee IDs are not user identities and client timestamps are not an audit authority.

Future access must cover requests, evidence, exceptions, queues, downloads and backups; protecting only downloads is insufficient. Avoid publicly served paths, response/log/storage-key leaks, filename/header attacks, mismatched MIME/signatures, oversized streams, malicious PDFs/images and unauthorized supersession. Restrict metadata exposure by role/resource, define retention and access auditing, and never infer statutory medical validity from upload success.

D8C freeze is an application/API guarantee checked against stored facts, not a cryptographic signature or protection against a privileged direct SQL rewrite. History queue parsing is not equivalent to LeaveSnapshotIntegrity; enforcement must call the integrity validator rather than trust list DTOs.

## 21. Explicit decisions requiring approval

All items below are **proposed, not approved by this analysis**. Previously approved D8B/D8C semantics are not reopened.

| ID | Decision | Recommended smallest contract |
|---|---|---|
| E1 | Security phase | Metadata/history foundation with synthetic local data now; real binary delivery and authorized review wait for authentication/RBAC. |
| E2 | Approval evidence trust | Explicit Accepted version; decide whether independent external verification is allowed before secure delivery, otherwise required-evidence approvals remain blocked. |
| E3 | Multiple document types | All distinct types triggered by frozen policies must be satisfied; store/derive frozen requirement mapping with unchanged D8C triggers. |
| E4 | Evidence versions | Dedicated immutable receipts, append-only review/supersession, exact approval-version associations; no reuse of mutable 201 verification as proof. |
| S1 | Participation/owner | Both boundary policies and every gap-date policy participate; same LeaveType and consistent BalanceTracked; separate case owns debit. |
| S2 | Paid/Unpaid | Equal known frozen classification on boundaries; preserve it on case; exclude mixed pairs in V1. No monetary consequences. |
| S3 | Debit unit | Positive policy-defined equivalent minutes per gap date, explicitly separate from zero scheduled minutes; values supplied by school, no default. |
| S4 | Boundary aggregation | One request covers complete schedule per boundary; support same or separate boundary requests, defer union of partial requests. |
| S5 | Timing/budget | Reserve at pair formation; charge when all boundaries Approved; release on rejection/cancellation. Insufficient case budget rolls back forming request. |
| S6 | Uncaptured gap facts | Freeze at first detection; reuse existing frozen evidence where present; conflicting histories require review. No historical Approved-pair backfill. |
| S7 | Exception lifecycle | Dedicated reasoned HR command on Reserved case before final charge; scoped revision; post-charge correction deferred. |

Before later binary implementation additionally approve formats/maximum size, storage deployment, scanning, authorized roles and retention. These settings do not justify enabling unauthenticated medical delivery now.

**Stop conclusion:** security blocks actual binary delivery; charge unit, participation/ownership, mixed classification, reservation and exception choices block sandwich implementation. This report completes the requested review; no implementation should begin by guessing those choices.

## 22. Smallest recommended V1 implementation

After approval, implement two focused parts: leave-evidence metadata/history and approval prerequisites under the approved trust boundary; and an independent minute-debit sandwich case with frozen inputs, case allocations, pre-charge exceptions and atomic balance/lifecycle reconciliation.

Keep strict DTOs and the existing controller -> Application contracts -> Infrastructure/EF layering. Reuse employee locks, Published policy rules, explicit assignments, replacement override intervals, calendar-year budgets and UTC timestamps. Avoid a generic workflow engine, document platform, Attendance integration or Payroll calculations. Deliver binary protection in the separate security hardening phase.

## 23. Verification plan — future implementation only

- Evidence: wrong employee/request/type; Recorded cannot satisfy Accepted; multi-type requirements; missing/inactive-current master must not rewrite frozen requirement meaning; Unknown JSON/audit fields rejected; server timestamps; immutable accepted receipts; supersession and historical approval references; rejected/cancelled history retained. Test exact D8C threshold, partial-date, nonworking and Monday/Friday behavior unchanged.
- Future binary security: unauthenticated/unauthorized denial, cross-owner access, no StorageKey DTO leak, no webroot delivery, traversal/absolute names/header injection, spoofed MIME/signatures, allowlist, maximum/stream limit, quarantine/scan rejection, interrupted upload cleanup and protected downloads/logs. These tests cannot be claimed passed before secure delivery exists.
- Sandwich: full interval coverage versus partial hours; variable-length/split schedules; fully covering Timed; holiday spans; working Saturday/ExceptionalWorkingDay interrupts; explicit calendar switches/missing assignments/employment gaps; same request/two requests; approved participation/mixed-type/paid/tracking boundaries; no automatic default or assumed day.
- Accounting: case debit separate from scheduled minutes; no double charge; Pending/Approved permutations; insufficient/missing/zero year budgets; cross-year per-date allocation; adjustment lower-bound guard; cancellation/rejection releases once; new boundary after release creates auditable new revision; exception requires reason/state and preserves historical case.
- History: change live calendar, policy/type classifications or document metadata; old requests/cases/approval evidence remain reproducible. Conflicting or missing legacy facts require explicit review. No backfill from current values.
- Concurrency: upload-finalization/approval, acceptance/approval, second-request/approval, exception/final charge, cancellation/final charge, competing forming requests and adjustment/reservation; verify one ordered outcome, no overspend or duplicate active case and transactional rollback on failure.
- Regression: D8B calendar/policy/entitlement, D8C snapshot/notice/overlap/balance/lifecycle/ExpectedStatus, D1 employment and established Payroll regressions. Verify no Attendance/Payroll rows or monetary effects. Cleanup all fixtures and compare exact Development baseline.
- Implementation acceptance later: restore, Release build, focused tests, inspected/approved migration and local schema checks, EF pending-model-changes check and git diff --check. No commit/push without authorization.

## 24. Complete files inspected

Paths below are relative to the repository root stated above. Selected portions are identified; the global source search also covered authentication/storage API symbols. Unmatched search names were not treated as inspected files.

1. `src/SIAMIS.Api/Program.cs`
2. `src/SIAMIS.Api/Controllers/EmployeeDocumentsController.cs`
3. `src/SIAMIS.Api/Controllers/EmployeeLeaveController.cs`
4. `src/SIAMIS.Api/Controllers/LeaveOperationsController.cs`
5. `src/SIAMIS.Application/Employees/EmployeeContractDocumentContracts.cs`
6. `src/SIAMIS.Application/Employees/EmployeeLeaveContracts.cs`
7. `src/SIAMIS.Domain/Entities/Employees/EmployeeDocument.cs`
8. `src/SIAMIS.Domain/Entities/Employees/EmployeeLeave.cs`
9. `src/SIAMIS.Domain/Entities/Leave/LeaveFoundation.cs`
10. `src/SIAMIS.Domain/Entities/Leave/EmployeeLeaveAllocation.cs`
11. `src/SIAMIS.Domain/Entities/MasterData/MasterDataEntity.cs`
12. `src/SIAMIS.Infrastructure/Services/EmployeeContractDocumentService.cs`
13. `src/SIAMIS.Infrastructure/Services/EmployeeLeaveService.cs`
14. `src/SIAMIS.Infrastructure/Services/EmployeeLeaveReadService.cs`
15. `src/SIAMIS.Infrastructure/Services/LeaveRequestCalculator.cs`
16. `src/SIAMIS.Infrastructure/Services/LeaveSnapshotIntegrity.cs`
17. `src/SIAMIS.Infrastructure/Services/LeaveFoundationService.cs`
18. `src/SIAMIS.Infrastructure/Services/LeavePolicyEntitlementService.cs`
19. `src/SIAMIS.Infrastructure/Services/EmploymentIntegrity.cs` — first 65 lines (complete file).
20. `src/SIAMIS.Infrastructure/Configurations/EmployeeConfigurations.cs` — contract/document mappings, lines 117–163 and StorageKey search.
21. `src/SIAMIS.Infrastructure/Configurations/LeaveRequestConfiguration.cs`
22. `src/SIAMIS.Infrastructure/Configurations/LeaveFoundationConfiguration.cs`
23. `src/SIAMIS.Infrastructure/Configurations/MasterDataConfiguration.cs`
24. `src/SIAMIS.Infrastructure/Data/SIAMISDbContext.cs` — first 110 lines, registration/mapping.
25. `src/SIAMIS.Infrastructure/Data/MasterDataSeeds.cs`
26. `src/SIAMIS.Infrastructure/Migrations/20260928183455_InitialHrDatabase.cs` — document column/Medical Certificate seed search excerpts.
27. `src/SIAMIS.Infrastructure/Migrations/20261003100244_AddLeaveRequestCalculationLifecycle.cs`
28. `tests/SIAMIS.Payroll.RegressionTests/D8CLeaveCalculationTests.cs` — certificate, Paid/Unpaid and sandwich search excerpts.
29. `D8C-REPORT.md` — first 95 lines, completed schema/calculation/lifecycle report.

Also read the complete active user request at `C:/Users/USER/.codex/attachments/acb83f00-871e-4a99-be06-c3c99d0254b2/Pasted text.txt`. Migration designer/snapshot search matches were used only to corroborate seed repetition, not reviewed as separate full files. No AGENTS.md was found by the repository file listing.
