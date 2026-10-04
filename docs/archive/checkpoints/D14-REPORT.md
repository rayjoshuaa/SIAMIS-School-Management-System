# D14 â€” Secure HR Documents & Evidence

Completed against local Development `localhost/SIAMIS`. No commit or push.

## 1. Executive summary

EmployeeDocuments now supports private, integrity-verified binary content with authenticated upload/download, immutable superseding versions and archival. Confidential access uses the approved HRDocuments capabilities. Existing Employee capabilities and SystemAdmin alone cannot access documents. D8 ExternalReceipt behavior and all Payroll/Leave/Attendance calculations remain unchanged.

Focused migration applied: `20261004115720_AddSecureHrDocumentFoundation`. Release: **0 warnings, 0 errors**. EF: **no pending model changes**. Exact original Development rows/timestamps and private-storage baseline restored.

## 2. Repository/database/storage state inspected

Starting Git HEAD: `0a78bfd` (D13). Inspected actual entities/configuration/controllers/services, D8 immutable receipts/events/frozen approval references, D10 role policies and authoritative principal refresh, D12 Employee-first locking, D13 credentials, audit attribution, static-file configuration, SQL constraints, migration history and regression harnesses.

Before D14: 85 application tables, 41 applied migrations, 172 master rows, five permanent roles, one inactive TEST-EMP-001, one current EmploymentRecord. All operational and Identity/audit tables were empty. No existing secure binary provider/private files existed. Full rows were captured before migration, not just counts.

## 3. Existing D8 Leave evidence relationship

D8 EmployeeLeaveEvidence records an immutable `ExternalReceipt` and opaque external reference. Its lifecycle is represented by append-only evidence events; approval freezes receipt IDs. It did not store binary content. D14 optionally attaches an EmployeeDocument to the existing receipt through the same-Employee/Leave/evidence composite FK. No second LeaveAttachment table or receipt API was introduced.

## 4. Document domain model

Reuse EmployeeDocuments; no new document table or generic document engine. Every successful stored version has a server-generated EmployeeDocumentId and independent opaque storage key. Existing creation/content metadata is retained; lifecycle status, version and lifecycle actor/time change when archived/superseded. A replacement creates a separate row with SupersedesDocumentId.

Existing metadata-only records receive `MetadataOnly`, default category and a random concurrency version. Migration does not fabricate hashes/content or reinterpret legacy storage keys. Legacy content remains unavailable until explicitly replaced through secure upload.

## 5. Employee/Employment associations

Employee ownership is mandatory and checked server-side, including wrong-parent 404 behavior. Optional EmploymentRecordId uses `(EmployeeId, EmploymentRecordId)` against a new alternate key on EmploymentRecords. Historical employment linkage is permitted; current employment is not required for historical documents. Offboarding/rehire retains the same document IDs and bytes. Existing contract/document composite ownership and one-to-one link remain intact; replacement does not silently retarget contracts to a different document version.

## 6. Document categories

Controlled values: EmploymentContract, Identification, WorkAuthorization, QualificationOrCertificate, GeneralHRDocument. Default: GeneralHRDocument. Application and SQL validate this bounded set; no speculative category master table. DocumentTypeId remains optional under the existing nullable data model. When supplied, it must be active and its required document-number/expiry rules are enforced. Leave-linked content must match the existing receipt's DocumentTypeId.

## 7. Storage abstraction

Application exposes IPrivateDocumentStorage for store, integrity-verified read and compensating removal of failed writes. Infrastructure implements the private filesystem adapter. Domain models contain no provider SDK/path logic. Storage keys are internal adapter/database data, never API input/output. Replacing the adapter later requires preserving validation, immutable writes and integrity semantics; no vendor selected.

## 8. Development storage

Development-only default: `<API content root>/App_Data/hr-documents`. An explicit absolute `PrivateDocuments:Root` may override it. The root must be isolated from wwwroot; absolute paths, reparse-point checks and generated keys prevent traversal. `.gitignore` excludes the private root. No static-file provider maps this directory. Initialization is lazy; verification removed the empty test root as well as every binary/stage.

## 9. Production storage boundary

No Development fallback outside Development. Without an explicit private root/provider, uploads/replacements return safe 503 before multipart model binding; metadata reads remain authorized and content reads fail safely. Public-root/ancestor and relative-root configurations are rejected at startup. Actual Production/VPS storage was not configured or contacted; Production-mode tests used only loopback and local SIAMIS.

## 10. Upload contract

Authenticated multipart with exactly one `file`. Descriptive fields: category, optional DocumentTypeId, EmploymentRecordId, LeaveId/LeaveEvidenceId pair, Remarks, DocumentNumber, IssueDate, ExpiryDate. Unknown/duplicate fields and forged ID/key/hash/actor/lifecycle/version inputs are rejected. Replacement accepts only file and the current opaque version. Server generates identity, key, SHA-256, size, MIME, actor and UTC creation time. New content starts Pending verification; this checkpoint does not add a verification workflow.

## 11. File-size/type/signature validation

Limit: **20 MiB = 20,971,520 bytes**. Validate reported file length and actual streamed length. Request/form limit adds only 64 KiB for multipart metadata. File buffering is explicitly bounded in memory, configured before CSRF/form parsing; accepted uploads do not use an OS temporary-file fallback. Existing CSRF rules remain mandatory.

Allow PDF/application/pdf (`%PDF-`), JPEG/image/jpeg (FFD8FF), PNG/image/png (PNG signature), with matching extension and declared MIME. Executables/scripts/SVG/archives and mismatches are rejected. Basic signatures are not full PDF/image decoding or malware scanning. Exact 20 MiB accepted; +1 byte returns 413; malformed form input returns 400.

## 12. Filename/storage-key security

Filename is normalized display metadata only, maximum existing 260-character limit. Reject directories, drive/stream syntax, traversal separators, control characters, excessive length, unsafe punctuation and Windows reserved device names. Actual filename: independent random 32-hex key plus `.blob`; original filenames and Employee identifiers are not used as paths. Strict key grammar, root checks and create-new/atomic move prevent overwriting established objects.

## 13. SHA-256/integrity behavior

Server calculates incremental SHA-256 during storage and persists canonical MIME/actual byte size. Downloads verify size and SHA-256 before returning a bounded read-only memory snapshot, preventing content changing after verification. Missing/corrupt objects return safe 503; historical hash is never rewritten. Identical content is not automatically deduplicated.

## 14. Metadata model

Safe DTO includes EmployeeDocumentId, Employee/context IDs, category, filename, MIME, size, SHA-256, authenticated uploader ID, UploadedAt, lifecycle/version/predecessor/lifecycle attribution, Remarks and existing document-number/dates/verification metadata. No StorageKey, filesystem path, credentials, navigation properties or EF entities. Actor IDs are immutable historical GUID snapshots rather than User FKs, consistent with existing audit conventions. UTC application timestamps use SQL datetime2(7); document creation/lifecycle DTO mapping explicitly preserves the UTC marker after SQL reads without changing stored ticks.

## 15. Authorization/capabilities

| Role | HRDocuments.Read | HRDocuments.Manage |
|---|---|---|
| HRAdmin | Yes | Yes |
| SystemAdmin only | No | No |
| SystemAdmin + HRAdmin | Via explicit HRAdmin | Via explicit HRAdmin |
| PayrollAdmin | No | No |
| Management | No | No |
| Employee | No | No |

All EmployeeDocuments and HrDocuments actions use these policies; the Infrastructure service also checks capabilities. Linked Leave content additionally requires Leave.Evidence. Existing SystemAdmin Employee/Leave/Attendance/Payroll/Security grants remain unchanged. Roles are assigned through the existing audited role architecture; no per-user overrides or hidden SystemAdmin bypass.

## 16. Employee self-service boundary

Employees cannot browse/download even their own HR documents. Authenticated User -> Employee ownership does not automatically grant document access. Other-Employee enumeration and direct document-ID access are denied. Future self-service visibility requires a separate explicit policy.

## 17. Download behavior

Authorized owned or direct-ID route resolves metadata/context, verifies private bytes, audits access and returns an attachment with canonical Content-Type, safe Content-Disposition, `Cache-Control: no-store` and `X-Content-Type-Options: nosniff`. Authorized historical downloads remain available after archival/supersession. No direct public/storage URL.

## 18. Replacement/versioning

Employee-first Serializable transaction plus expected opaque version. Old row becomes Superseded with lifecycle actor/time/version; original filename/hash/size/creation fields/bytes remain. New row becomes Active with independent identity/bytes and predecessor FK. Unique filtered successor index prevents branching. Stale or non-current replacement returns 409. A replacement cannot change ownership/context/category through form input and does not inherit a prior binary's verified status.

## 19. Archive/retention

Explicit archive and legacy DELETE both archive current content with expected version; no ordinary hard delete. Active listing excludes archived/superseded rows; includeHistory includes retained metadata. Historical bytes, hashes, contract links and evidence links remain. No retention durations, purge job, offboarding/account-disable deletion or automatic rehire duplication introduced.

## 20. Atomicity/failure handling

Stage write in the private root, flush/close, atomic move to independent final key, then commit metadata/audit. Storage failure commits no metadata. Save/commit failures roll back lifecycle changes and remove new bytes only after a fresh connection confirms no committed document. Failed replacement preserves predecessor metadata/bytes. A simulated failure after successful commit retained the valid committed object.

An uncertain commit/database outage or OS refusal to delete requires conservative retention/reconciliation; deleting possibly committed bytes would be unsafe. Logs contain only safe operation/type/document ID. Crash/reconciliation tooling and coordinated storage/database backup are deployment boundaries. This is compensating consistency, not a distributed filesystem/SQL transaction. A response failure after commit must not invalidate the document.

## 21. Concurrency behavior

Established Employee-first UPDLOCK order inside Serializable; optimistic version protects stale lifecycle commands. Tested simultaneous uploads, competing replacements, archive-versus-replace, download-versus-archive, offboarding-versus-upload and rehire retention. One winning lifecycle operation; losers return conflict; independent uploads retain independent identities. No automatic source callbacks or unrelated domain locking changes.

## 22. Audit behavior

Append-only existing SecurityAuditEvents capture upload, replacement/predecessor supersession, archive, successful download and missing/tampered-content availability failures. Include server actor, document ID, Employee ID and UTC time. Generic D10 mutation audit remains too. No bytes, filenames, internal keys, paths, credentials or tokens in document operation text. Existing authentication/authorization/validation rejection conventions remain; this checkpoint does not invent anonymous actor attribution or a new audit platform.

## 23. Leave evidence compatibility

Upload/link does not record acceptance, satisfy evidence prerequisites, change predecessor receipt history, freeze new approval facts or modify entitlement/sandwich/paid-unpaid calculation. Live verification compared exact receipt/event/Leave/allocation rows before/after linkage; unaccepted evidence still blocked approval. After explicit D8 acceptance/approval, replacement/archive left frozen approval receipt IDs and all Leave rows unchanged. These binaries are associated supporting copies; they do not retroactively become D8's frozen binary approval payload.

## 24. Privacy/logging

No request-body/file-body logging enabled. Filename/hash/key never enter general operation text. Document exception filter returns safe operational responses rather than Development stack/path disclosure. SQL command-count logging for regressions did not enable sensitive parameter values. Metadata is confidential under the same read gate as content. Deployment must also enforce OS/storage permissions; application SystemAdmin separation does not replace host-level controls.

## 25. API/DTO changes

| Method | Route | Purpose |
|---|---|---|
| GET | /api/employees/{employeeId}/documents | Current list; includeHistory/category filters |
| POST | /api/employees/{employeeId}/documents | Secure multipart upload |
| GET | /api/employees/{employeeId}/documents/{documentId} | Owned metadata |
| PUT | /api/employees/{employeeId}/documents/{documentId} | Retired metadata overwrite; 409 after owned lookup |
| DELETE | /api/employees/{employeeId}/documents/{documentId} | Version-protected archive; 204 |
| GET | /api/employees/{employeeId}/documents/{documentId}/content | Owned verified download |
| POST | /api/employees/{employeeId}/documents/{documentId}/replace | Owned immutable replacement |
| POST | /api/employees/{employeeId}/documents/{documentId}/archive | Owned archive |
| GET | /api/hr-documents/{documentId} | Direct-ID metadata |
| GET | /api/hr-documents/{documentId}/content | Direct-ID verified download |
| POST | /api/hr-documents/{documentId}/replace | Direct-ID replacement |
| POST | /api/hr-documents/{documentId}/archive | Direct-ID archive |

Intentional compatibility changes: JSON metadata-only POST is replaced by multipart; arbitrary metadata PUT is retired; DELETE archives and requires version; StorageKey is removed from public contracts. Obsolete unsafe document methods/DTOs were removed from the old mixed service; contract methods/rules remain unchanged. Swagger documents capabilities, multipart fields, response codes and binary MIME/schema.

## 26. Error contract

400 invalid metadata/form/filename; 401 unauthenticated; 403 missing document/context capability; 404 unknown/wrong-parent resource; 409 stale/non-current lifecycle; 413 oversized file/request; 415 unsupported type/media; safe 503 unconfigured/missing/corrupt storage or document operational failure. Framework form/request parser validation may return its existing 400. No raw storage/database exception internals returned by document actions.

## 27. Migration/schema changes

`20261004115720_AddSecureHrDocumentFoundation` applied only to local SIAMIS. No earlier migration rewritten. No new tables; still 85 application tables, now 42 migration-history rows.

13 additive EmployeeDocuments columns: Category, ContentSha256, ContentType, CreatedByUserId, EmploymentRecordId, LeaveEvidenceId, LeaveId, LifecycleChangedAtUtc, LifecycleChangedByUserId, LifecycleStatus, SizeBytes, SupersedesDocumentId, Version. UploadedAt explicitly uses datetime2(7), equivalent to its previous default datetime2 precision.

Added EmploymentRecord same-owner alternate key, three same-owner composite FKs (employment/evidence/predecessor), five indexes including filtered unique successor and Employee/lifecycle listing, and five trusted checks (category/content tuple/Leave association/lifecycle/non-self successor). All document FKs NoAction. Actual SQL catalog and rejection tests verified constraints; no actor-deletion FK. Master/Employee rows unchanged; no D14 seed rows.

## 28. Production hardening boundaries

Configure an explicit absolute private persistent `PrivateDocuments:Root` outside all publicly served/deployment directories, restricted read/write service-account permissions and no execute permission; filesystem reparse points are rejected. Provide capacity monitoring, secure backups/restores coordinated with SQL, at-rest protection, retention/purge governance and crash/uncertain-commit reconciliation. Keep credentials/paths in deployment configuration. Add malware scanning/content inspection before real Production document ingestion; basic magic validation is not scanning. Benchmark bounded buffering under expected concurrency. These deployment measures follow [ASP.NET Core upload guidance](https://learn.microsoft.com/en-us/aspnet/core/mvc/models/file-uploads?view=aspnetcore-10.0).

No vendor, cloud SDK, legal retention period or real VPS setting chosen. No frontend/self-service sharing policy added.

## 29. Pure/live/security test results

| Suite | Assertions/checks | Result |
|---|---:|---|
| Full pure suite (includes 43 D14) | 1,024 | Pass |
| Focused final D14 API/SQL/Swagger/races/cleanup | 93 | Pass |
| D14 real SQL/filesystem fault harness | 19 | Pass |
| Public/relative Production root rejection | 2 | Pass |
| Final SQL/schema/storage baseline | 17 | Pass |

D14-specific focused total: 157 (43 pure + 93 live + 19 fault + 2 configuration). Synthetic signature-valid PDF/JPEG/PNG fixtures verify the deliberately basic validator; no claim of complete format parsing. Authenticated tests use real cookies/CSRF/role assignments, no production bypass. Fault interceptors exist only in the explicitly opted-in test executable.

Initial test-client Production cookie handling and a regression SQL-log filename/configuration mismatch were corrected in verification setup; final reruns passed. No financial/domain expectation was weakened to pass.

## 30. Regression results

Domain scenario totals: D9D 174; D9B 121; D9C 116; D9E 201; D8B/D1 117; D8C 246; ExpectedStatus concurrency 230; D8D evidence/sandwich 527; capped sandwich 392. Payroll: D5A 70, D5C 306, D6B 76, D6C 48, D6D 38, D6E 88, D6E boundaries 128, D7 operations 84. **2,962 scenario assertions + 14 wrapper/audit/baseline checks = 2,976**, all passed.

| Security/HR regression | Checks | Result |
|---|---:|---|
| verify_d13_live.py | 78 | Pass |
| verify_d13_production.py | 7 | Pass |
| verify_d12_live.py | 52 | Pass |
| verify_d11_live.py | 73 | Pass |
| verify_d10_security.py | 45 | Pass |
| verify_d10_production.py | 26 | Pass |
| verify_d10_advanced.py | 28 | Pass |
| verify_d10_route_security.py | 237 | Pass |
| verify_d13_rate_limit.py | 2 | Pass |

Security/HR regression total: **548**. No PayrollCalculationService/Generation/Preview/formulas/lifecycle, Leave financial rules or Attendance calculations were changed. Full regressions included failed-generation rollback, failed-regeneration preservation, provenance, manual reconciliation and lifecycle/period guards through existing suites. SQL performance count verification passed with the required existing log configuration.

## 31. Exact database/storage cleanup

Exact comparison across all 85 application tables passed against the pre-migration full-row baseline, including timestamps. Only the approved focused migration/schema remains; capability grants are code configuration. Employees=1, EmploymentRecords=1, PayrollComponents=17, Roles=5; all operational/Identity/audit tables=0. TEST-EMP-001 remains inactive with original core data/history intact. All temporary users/tokens/audits/employees/calendar/policy/payroll/Leave/document fixtures removed. All private binaries/stages and fault-test directories removed; Development private root restored to absent/empty initialization state. Owned Development/Production-mode API processes stopped.

Complete final application table counts (migration history separately contains 42 entries):

| Table | Rows |
|---|---:|
| AddressTypes | 3 |
| Attendance | 0 |
| AttendanceEvents | 0 |
| AttendanceReviewActions | 0 |
| AttendanceReviewCases | 0 |
| AttendanceStatuses | 11 |
| ContractTypes | 7 |
| Countries | 13 |
| Departments | 12 |
| Designations | 20 |
| DocumentTypes | 15 |
| EmergencyContacts | 0 |
| EmployeeAddresses | 0 |
| EmployeeCompensations | 0 |
| EmployeeContacts | 0 |
| EmployeeContracts | 0 |
| EmployeeDocuments | 0 |
| EmployeeHistory | 0 |
| EmployeeLeave | 0 |
| EmployeeLeaveAllocations | 0 |
| EmployeeLeaveApprovalEvidence | 0 |
| EmployeeLeaveEntitlementAdjustments | 0 |
| EmployeeLeaveEntitlements | 0 |
| EmployeeLeaveEvidence | 0 |
| EmployeeLeaveEvidenceEvents | 0 |
| EmployeeLeaveSandwichAllocations | 0 |
| EmployeeLeaveSandwichCases | 0 |
| EmployeeLeaveSandwichDates | 0 |
| EmployeeLeaveSandwichEvents | 0 |
| EmployeePayrollComponentAssignments | 0 |
| EmployeePayrollLines | 0 |
| EmployeePayrollPitResults | 0 |
| EmployeePayrollSocialSecurityResults | 0 |
| EmployeePayrollStatutoryResults | 0 |
| EmployeePayrolls | 0 |
| EmployeePayslips | 0 |
| EmployeePerformance | 0 |
| EmployeePitPaymentScheduleEntries | 0 |
| EmployeePitPaymentScheduleSelections | 0 |
| EmployeePitPaymentSchedules | 0 |
| EmployeeStatutoryEnrollments | 0 |
| EmployeeTaxClaims | 0 |
| EmployeeTaxDeclarationSelections | 0 |
| EmployeeTaxDeclarations | 0 |
| EmployeeTaxOpeningBalances | 0 |
| EmployeeTaxProfiles | 0 |
| EmployeeWorkCalendarAssignments | 0 |
| Employees | 1 |
| EmploymentRecords | 1 |
| EmploymentStatuses | 9 |
| EmploymentTypes | 6 |
| FinalizedAttendanceRevisions | 0 |
| Genders | 4 |
| HiringSources | 10 |
| LeavePolicies | 0 |
| LeaveTypes | 10 |
| Locations | 5 |
| MaritalStatuses | 6 |
| Nationalities | 13 |
| OrganizationProfiles | 0 |
| PayTypes | 6 |
| PayrollComponents | 17 |
| PayrollPeriods | 0 |
| PayrollRuleTargets | 0 |
| PayrollRules | 0 |
| PayrollSettings | 0 |
| PerformanceRatings | 5 |
| PitPolicyConfigurations | 0 |
| PitTaxBrackets | 0 |
| RoleClaims | 0 |
| Roles | 5 |
| SecurityAuditEvents | 0 |
| SocialSecurityPolicyConfigurations | 0 |
| StatutoryPolicyVersions | 0 |
| StatutorySchemes | 0 |
| TeacherProfiles | 0 |
| UserClaims | 0 |
| UserLogins | 0 |
| UserRoles | 0 |
| UserTokens | 0 |
| Users | 0 |
| WorkCalendarDateOverrides | 0 |
| WorkCalendarOverrideIntervals | 0 |
| WorkCalendarWeeklyIntervals | 0 |
| WorkCalendars | 0 |

## 32. Complete changed-file list

- `.gitignore`
- `D14-REPORT.md`
- `src/SIAMIS.Api/Controllers/EmployeeDocumentsController.cs`
- `src/SIAMIS.Api/Controllers/HrDocumentHttp.cs`
- `src/SIAMIS.Api/Controllers/HrDocumentsController.cs`
- `src/SIAMIS.Api/HrDocumentRegistration.cs`
- `src/SIAMIS.Api/Program.cs`
- `src/SIAMIS.Api/Security/HrAuthorizationFilter.cs`
- `src/SIAMIS.Api/Security/SecurityDocumentationFilter.cs`
- `src/SIAMIS.Application/Employees/EmployeeContractDocumentContracts.cs`
- `src/SIAMIS.Application/Employees/HrDocumentContracts.cs`
- `src/SIAMIS.Application/Security/SecurityContracts.cs`
- `src/SIAMIS.Domain/Entities/Employees/EmployeeDocument.cs`
- `src/SIAMIS.Infrastructure/Configurations/HrDocumentConfiguration.cs`
- `src/SIAMIS.Infrastructure/Documents/HrDocumentService.cs`
- `src/SIAMIS.Infrastructure/Documents/PrivateDocumentStorage.cs`
- `src/SIAMIS.Infrastructure/Migrations/20261004115720_AddSecureHrDocumentFoundation.Designer.cs`
- `src/SIAMIS.Infrastructure/Migrations/20261004115720_AddSecureHrDocumentFoundation.cs`
- `src/SIAMIS.Infrastructure/Migrations/SIAMISDbContextModelSnapshot.cs`
- `src/SIAMIS.Infrastructure/Services/EmployeeContractDocumentService.cs`
- `tests/SIAMIS.Payroll.RegressionTests/D14DatabaseTests.cs`
- `tests/SIAMIS.Payroll.RegressionTests/D14DocumentTests.cs`
- `tests/SIAMIS.Payroll.RegressionTests/Program.cs`
- `tests/verify_d14_baseline.py`
- `tests/verify_d14_configuration.py`
- `tests/verify_d14_live.py`
- `tests/verify_d14_regressions.py`
- `tests/verify_d14_security_regressions.py`

Verification artifacts/logs remain under ignored `tests/SIAMIS.Payroll.RegressionTests/bin/`; no private binaries or credentials are committed. No csproj/dependency or existing business-calculation changes.

## 33. Remaining HR gaps

Production persistence/ACL/scanning/backup/reconciliation/retention configuration; separately approved Employee self-service document visibility; a future explicit document-verification/descriptive-correction workflow where needed; UI. D14 does not implement signing/OCR/sharing, generalized drive features, legal retention durations, statutory/payroll/Attendance/Leave changes or new authentication policy.

## 34. Verification/status table

| Check | Final result |
|---|---|
| Focused local migration | Applied successfully |
| Release restore/build | Success; 0 warnings / 0 errors |
| Pure/focused/live/configuration/fault verification | Pass |
| Established domain/security regressions | Pass |
| EF pending-model changes | None |
| git diff --check | Clean |
| Targeted source secret/path scan | Completed; no embedded real credentials/private-path disclosure |
| Exact Development database baseline | Restored; original 85-table rows/timestamps preserved |
| Exact private-storage cleanup | No test binaries/stages/orphans |
| Owned API processes | Stopped |
| Commit/push | Neither performed |

Total counted verification assertions/checks: **4,679**, including wrapper/baseline assertions. No unresolved D14 stop condition remains. Future Production deployment boundaries above remain deliberate.
