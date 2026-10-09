# D9D â€” Attendance review, corrections and finalization

## 1. Executive summary

D9D implements immutable attendance revisions, reasoned correction/adjudication history, explicit absence confirmation, and explicit reopening. Current review reads distinguish a historical finalized revision from its current validity. Approved Leave cancellation remains independent and produces structured read-time staleness without rewriting attendance. No frontend or downstream monetary/HR consequences were added.

Verification status is recorded in sections 24â€“28. No commit or push was performed.

## 2. Repository/database state inspected

- Starting commit: `7ca5c29 feat(hr): add D9C daily attendance calculation`; initial tree clean before the inspection report.
- Inspected actual D9B evidence/configuration/resolver, D9C queries/calculator, employment and calendar contracts, D8 frozen Leave/integrity/lifecycle/evidence/sandwich code, audit and immutable snapshot conventions, locking, legacy Attendance, Program.cs and SQL schema/counts.
- Local Development: `localhost/SIAMIS`, Windows integrated authentication. Original 74 application tables and 38 applied migrations; latest original migration `20261003175821_AddAttendanceEvidenceFoundation`.
- One inactive TEST-EMP-001 (`433f2c1a-6222-494f-a64f-cd0c31126dc4`), one current/open EmploymentRecord, 172 master rows (17 PayrollComponents); remaining transactional/configuration tables empty.
- No AGENTS.md found. The initial cross-domain stop was resolved by the user's approved stale/reopen contract.

## 3. Review model

`AttendanceReviewCase`: one employee/date case, unique in SQL, with immutable original calculation/findings, source fingerprint, UTC opening time and nullable actor. State is Open/Resolved. Correction, adjudication, absence confirmation or reopening creates a case if needed. Clean finalization creates no artificial review case. The same case survives successive reopen cycles; append-only history records each action.

A previously Resolved case can have stale finalized facts: workflow state is separate from current validity, which is explicitly returned by review reads.

## 4. Correction evidence model

Corrections insert new `ManualAuthorized` AttendanceEvents and a linked `CorrectionAdded` action atomically. Required reason, explicit-offset same-business-date timestamp, In/Out direction, globally unique nonempty ManualRequestKey. Original Device/Imported/Manual evidence is never changed or deleted. Duplicate keys return 409 without partial records. Off-site work classification remains deferred; no device presence is fabricated.

## 5. Event adjudication model

Append-only `Included`/`Excluded` actions reference owned employee/date evidence, with reason, sequence and server UTC timestamp. Latest decision for each event determines the calculation view. Raw evidence stays available separately. Include reverses an exclusion through another action. There is no destructive raw-event endpoint.

## 6. Potential/confirmed absence contract

Only unambiguous D9C PotentialAbsence with an available partition and no other blocking finding may be confirmed. Confirmation is bound to the exact current source fingerprint and must follow the latest reopen. Changed sources invalidate current confirmation. Finalization separately freezes IsConfirmedAbsent; the underlying D9C PotentialAbsence evidence is retained. No Unpaid Leave, deduction, penalty or KPI result is generated.

## 7. Recalculation behavior

D9C source queries were extracted to an internal shared helper under the caller's employee-first transaction. Existing D9C GET behavior and pure calculator are unchanged. D9D passes all validated frozen Approved Leave and the effective event adjudication view into the same exact-tick calculator. Responses contain raw and adjudicated calculations plus original findings/history. Audit CalculationJson records the server calculation before each action.

## 8. Finalization contract

Serializable transaction â†’ lock Employee first â†’ resolve current authoritative sources â†’ compare ExpectedVersion and ExpectedSourceFingerprint â†’ recalculate â†’ reject unresolved blocking findings â†’ freeze a new immutable revision plus a Finalized audit action â†’ commit. Clean/zero-schedule factual days can finalize directly. Confirmed absence can finalize. Missing/ambiguous evidence, configuration conflicts, invalid Leave snapshots, schedule mismatch and presence/Leave overlap cannot be forced through (409).

## 9. Historical expected-work snapshot

Versioned snapshot JSON freezes business date/timezone, effective employment and assignment/calendar/override provenance, schedule kind and exact scheduled intervals/durations. Later live calendar changes do not rewrite stored revisions and are detected by current review reads.

## 10. Attendance-policy snapshot

Each revision preserves D9C-v1, FiveMinuteClockInGrace-v1, 5-minute grace, expected arrival, first relevant presence, exact raw start variance and lateness classification. No generic policy engine or new AttendancePolicy table. Changing the calculator/grace contract requires changing its version identifier; current fingerprints include these identifiers and grace duration.

## 11. Leave snapshot/reference behavior

Snapshots preserve relevant frozen Approved Leave IDs, version, paid/unpaid classification, exact whole-minute charged boundaries, schedule/provenance and coverage. Mutable current LeaveType values are not used. Approved â†’ Cancelled remains allowed in D8; no attendance callback or cancellation guard was added. Cancellation invalidates current source match and returns ApprovedLeaveCancelled with the affected Leave ID while preserving the historical revision.

## 12. Finalized snapshot model

`FinalizedAttendanceRevision`: employee/date/revision, optional scoped review case, server UTC time, nullable actor, confirmed absence, nullable lateness, bigint partition totals/residual, SHA-256 source fingerprint, structured SourcesJson, and version-1 SnapshotJson containing the complete server calculation, raw evidence and correction/adjudication history. Scalar totals support later factual reporting; JSON preserves exact intervals and historical provenance. No reporting implementation yet.

## 13. Revision/reopen lifecycle

Revision 1 â†’ explicit Reopened action with required reason â†’ review/correction â†’ Revision 2. Revision 1 is never overwritten/deleted by the application. Unique employee/date/revision and a filtered unique reopen-per-revision index enforce numbering/reopen uniqueness. Latest historical revision is returned separately from IsCurrentlyValidated. Reopen without an existing closed revision or a competing stale token returns 409. No automatic reopen/refinalization.

## 14. Stale-source protection

Deterministic SHA-256 fingerprint covers effective expected work; all evidence available for the date; effective event adjudications and correction relationships; validated contributing Approved Leave facts and integrity findings; calculation/grace contract. Read timestamps, receipt timestamps, mutable names and employee administrative active status are excluded because they do not affect calculation.

Review returns current token/version, IsStale, RequiresReopen, IsReopened, IsCurrentlyValidated and structured ChangedSources (ExpectedWorkChanged, AttendanceEvidenceChanged, AttendanceDecisionsChanged, ApprovedLeaveCancelled/NoLongerApplicable, ApprovedLeaveSourcesChanged, AttendancePolicyChanged). Stale revisions are historical only, never currently validated. After explicit reopening RequiresReopen becomes false because reopening already occurred; IsCurrentlyValidated remains false until successful refinalization. Reads never write.

## 15. Concurrency/locking

Employee-first UPDLOCK and Serializable transaction reuse existing Attendance/Leave ordering; calendars are acquired through the existing sorted expected-work resolver. Source reads are protected until commit. No Payroll-period locks are introduced. Workflow sequence and fingerprint together reject old screens, concurrent corrections/adjudications/finalizations/reopens. SQL unique constraints provide a backstop; deadlocks/unique conflicts return 409 with rollback. Leave cancellation racing finalization either invalidates the old screen or commits afterward and is detected as stale.

## 16. API

Base: `/api/employees/{employeeId}/attendance-days/{date}`.

| Method | Suffix          | Success                                 |
| ------ | --------------- | --------------------------------------- |
| GET    | review          | 200 current facts, history and validity |
| GET    | history         | 200 immutable revisions in order        |
| POST   | corrections     | 201                                     |
| POST   | adjudications   | 201                                     |
| POST   | confirm-absence | 200                                     |
| POST   | finalize        | 201                                     |
| POST   | reopen          | 200                                     |

Mutations require ExpectedVersion, ExpectedSourceFingerprint and Reason. Invalid requests 400; missing employee/owned evidence 404; stale/conflicting/blocking state 409. Creation responses identify the review GET location. Existing D9C calculated GET remains a calculated read, not a claim of finalization. Swagger documents purposes, contracts and response codes.

## 17. Security/auth boundary

All five mutation routes fail closed outside Development. No authentication/RBAC or fake principal/user was introduced. ActorUserId is nullable GUID, always server-controlled/null now; Origin is DevelopmentUnattributed. Requests reject unmapped fields, including actor IDs, computed totals and forged provenance. Production authorization/identity attachment must be implemented before enabling mutation.

## 18. Precision invariants

Original datetime2(7) evidence and exact .NET ticks are preserved. Snapshots retain exact intervals and grace boundary; aggregate API bigint milliseconds truncate independently without floating point. SQL checks enforce nonnegative totals and scheduled = presence + Approved Leave + unexplained + truncation residual. Residual remains conversion metadata only, never absence/Leave/payroll/KPI time. D8 whole-minute Leave precision is unchanged.

## 19. Leave non-impact

D9D reads frozen Approved Leave facts only. It does not modify entitlements, reservations, paid/unpaid policy, evidence, sandwich cases or lifecycle. Leave cancellation is tested through the existing API, not replaced. All test Leave/calendar fixtures are removed afterward.

## 20. Payroll non-impact

No Payroll calculation, preview, generation, statutory calculator, lifecycle or monetary code was changed. Finalized/stale/reopened attendance creates no payroll records or deductions. Established monetary/provenance/rollback/lifecycle suites are rerun without changing their expected results.

## 21. KPI/disciplinary non-impact

No KPI/gate, warning, misconduct, discipline or termination behavior. Staleness indicates changed authoritative source facts only.

## 22. Legacy Attendance non-impact

No D9D writes to legacy Attendance; existing write retirement remains intact. Raw D9B evidence is unchanged by D9D adjudication. Future legacy read projection is deferred.

## 23. Migration

Applied only to local Development `localhost/SIAMIS`:
`20261004033520_AddAttendanceReviewFinalization`.

Adds AttendanceReviewCases, AttendanceReviewActions, FinalizedAttendanceRevisions; seven NoAction FKs, five checks, unique employee/date case, unique workflow sequence, unique revision and unique reopen-per-revision. Scoped composite case/revision FKs prevent cross-date/employee linking; event ownership is additionally checked by the service. Adds no seeds and changes no existing table/migration. UTC-compatible datetime2(7), GUID identifiers and bigint duration fields verified in actual SQL.

## 24. Test results

All requested suites passed. Primary assertion total: **3,574**, plus 19 regression-wrapper baseline/restoration assertions. These are assertion checks, not xUnit test-case counts. Timing-dependent race branches account for small run-to-run count differences.

| Suite                                        |   Passed assertions |
| -------------------------------------------- | ------------------: |
| Pure regression executable (all checkpoints) |                 813 |
| Of which new D9D pure checks                 | 36 (included above) |
| D9D live API/SQL                             |                 173 |
| Production-mode guard/no-write verification  |                   6 |
| D9B live evidence/schedule                   |                 122 |
| D9C live calculation/precision               |                 116 |
| D8B/D1                                       |                 117 |
| D8C                                          |                 246 |
| D8C lifecycle/concurrency                    |                 230 |
| D8D main evidence/sandwich                   |                 523 |
| D8D capped focus                             |                 391 |
| Eight established payroll suites             |                 837 |

Payroll breakdown: D5A 70; D5C 306; D6B 76; D6C 48; D6D 38; D6E 88; D6E boundaries 128; D7 operations/lifecycle/payslip 83.

Focused D9D verifies normal/early/exact grace/100 ns late; split and non-working schedules; Device/Imported raw evidence preservation; missing IN/OUT corrections; duplicate/Unknown exclusions and reversible inclusion; explicit absence and source invalidation; frozen full Paid/Unpaid and partial Leave; blocking overlap/mismatch/configuration; overlap resolved through explicit corrected evidence, never automatic precedence; calendar and evidence staleness; contributing Leave cancellation; read-only stale detection; audited reopen and revision 2; SQL historical snapshot equality; finalization/finalization, correction/finalization, adjudication/finalization, reopen/reopen and Leave-cancellation/finalization races; failed finalization/duplicate correction rollback; ownership and strict request validation; SQL duration/JSON/action/scope/revision constraints; all seven Swagger routes; and no cross-domain mutations.

A final HTTP check found the new controller's Created Location used DateOnly default formatting. Fixed only the URL date formatting to invariant `yyyy-MM-dd`; verified 201, ISO Location, and successful GET of that URL. This did not change model/calculation/security behavior.

Original regression scenario files are unchanged. The new regression wrapper adapts obsolete table-count assertions only and reuses the previously approved D8 prerequisite adaptations; monetary, Leave and lifecycle expectations are not relaxed.

Reproduction (start the Development API at localhost:5155; local Production-mode API at localhost:5156 only for its guard test):

```powershell
dotnet restore
dotnet build .\SIAMIS.sln -c Release
dotnet run --project tests/SIAMIS.Payroll.RegressionTests -c Release --no-build
python tests/verify_d9d_live.py
python tests/verify_d9d_regressions.py
python tests/verify_d9d_production.py
dotnet ef migrations has-pending-model-changes --project src/SIAMIS.Infrastructure --startup-project src/SIAMIS.Api --configuration Release --no-build -- --environment Development
git diff --check
```

Live verification requires the documented empty transactional Development baseline. Run fixture suites sequentially. Ignored JSON/log evidence is under `tests/SIAMIS.Payroll.RegressionTests/bin/` (`d9d-live-results.json`, `d9d-production-results.json`, `d9d-regressions-results.json`, `d9d-final-database.json`). The original 74-table pre-migration snapshot is retained there for exact comparisons.

## 25. Exact baseline cleanup

Final read-only SQL verification confirms the exact original 74 tables' rows/values/timestamps match the pre-migration snapshot, and all three new D9D tables are empty. Fixture cleanup targets recorded IDs in dependency order. No baseline employee/master row was deleted or changed.

| Table/group                                          |                Final count |
| ---------------------------------------------------- | -------------------------: |
| Employees                                            |   1, TEST-EMP-001 inactive |
| EmploymentRecords                                    |                          1 |
| Original master tables (18)                          |             172 rows total |
| PayrollComponents (included above)                   |                         17 |
| AttendanceEvents                                     |                          0 |
| Legacy Attendance                                    |                          0 |
| AttendanceReviewCases                                |                          0 |
| AttendanceReviewActions                              |                          0 |
| FinalizedAttendanceRevisions                         |                          0 |
| Leave/calendar/evidence/entitlement/sandwich tables  |                      All 0 |
| Payroll transactional/configuration/statutory tables |                      All 0 |
| Other employee child tables / OrganizationProfiles   |                      All 0 |
| Application tables                                   | 77 (20 nonempty, 57 empty) |
| Applied migrations                                   |                         39 |

Final data rows = 174 (172 masters + employee + employment). All five new SQL checks and seven NoAction FKs are enabled/trusted; all 13 new-table physical indexes, including PK/alternate keys, exist. Verification processes were stopped after testing; the user's unrelated processes were not stopped.

## 26. Deferred D9E/D10 work

Monthly attendance reporting and any explicit approved downstream integration; production authentication/RBAC and server-authenticated actors on future actions (existing unattributed history stays immutable); off-site work/reason policy; future versioned attendance policies if needed. No overtime/payroll/Leave-sandwich/KPI/disciplinary effects or frontend implemented. Database administrative direct SQL is outside application immutability enforcement; EF guards and API lifecycle protect application writes without triggers.

## 27. Complete changed-file list

- [D9D-REPORT.md](D9D-REPORT.md)
- [src/SIAMIS.Api/Controllers/AttendanceReviewController.cs](../../../src/SIAMIS.Api/Controllers/AttendanceReviewController.cs)
- [src/SIAMIS.Api/Program.cs](../../../src/SIAMIS.Api/Program.cs)
- [src/SIAMIS.Application/Employees/AttendanceReviewContracts.cs](../../../src/SIAMIS.Application/Employees/AttendanceReviewContracts.cs)
- [src/SIAMIS.Domain/Entities/Employees/AttendanceReview.cs](../../../src/SIAMIS.Domain/Entities/Employees/AttendanceReview.cs)
- [src/SIAMIS.Infrastructure/Configurations/AttendanceReviewConfiguration.cs](../../../src/SIAMIS.Infrastructure/Configurations/AttendanceReviewConfiguration.cs)
- [src/SIAMIS.Infrastructure/Data/SIAMISDbContext.cs](../../../src/SIAMIS.Infrastructure/Data/SIAMISDbContext.cs)
- [src/SIAMIS.Infrastructure/Migrations/20261004033520_AddAttendanceReviewFinalization.Designer.cs](../../../src/SIAMIS.Infrastructure/Migrations/20261004033520_AddAttendanceReviewFinalization.Designer.cs)
- [src/SIAMIS.Infrastructure/Migrations/20261004033520_AddAttendanceReviewFinalization.cs](../../../src/SIAMIS.Infrastructure/Migrations/20261004033520_AddAttendanceReviewFinalization.cs)
- [src/SIAMIS.Infrastructure/Migrations/SIAMISDbContextModelSnapshot.cs](../../../src/SIAMIS.Infrastructure/Migrations/SIAMISDbContextModelSnapshot.cs)
- [src/SIAMIS.Infrastructure/Services/AttendanceDayService.cs](../../../src/SIAMIS.Infrastructure/Services/AttendanceDayService.cs)
- [src/SIAMIS.Infrastructure/Services/AttendanceReviewService.cs](../../../src/SIAMIS.Infrastructure/Services/AttendanceReviewService.cs)
- [src/SIAMIS.Infrastructure/Services/AttendanceReviewSources.cs](../../../src/SIAMIS.Infrastructure/Services/AttendanceReviewSources.cs)
- [tests/SIAMIS.Payroll.RegressionTests/D9DReviewTests.cs](../../../tests/SIAMIS.Payroll.RegressionTests/D9DReviewTests.cs)
- [tests/SIAMIS.Payroll.RegressionTests/Program.cs](../../../tests/SIAMIS.Payroll.RegressionTests/Program.cs)
- [tests/verify_d9d_live.py](../../../tests/verify_d9d_live.py)
- [tests/verify_d9d_production.py](../../../tests/verify_d9d_production.py)
- [tests/verify_d9d_regressions.py](../../../tests/verify_d9d_regressions.py)

18 changed/created files. No existing applied migration was modified; no Payroll or D8 implementation file changed.

## 28. Verification/status table

| Verification                                        | Result                                               |
| --------------------------------------------------- | ---------------------------------------------------- |
| D9D implementation / approved stale-reopen contract | Complete                                             |
| Local Development migration                         | Applied; 39 total migrations                         |
| Live SQL constraints/indexes                        | Verified; trusted NoAction/shape/coverage safeguards |
| Focused pure/live D9D + requested regressions       | Passed                                               |
| Swagger and Created Location                        | Passed                                               |
| Production mutation guards                          | Five routes return 404; no writes                    |
| Exact final baseline                                | Restored; original values/timestamps unchanged       |
| Final dotnet restore                                | Successful                                           |
| Final Release build                                 | 0 warnings, 0 errors                                 |
| EF pending-model-changes                            | No changes since last migration                      |
| git diff --check                                    | Passed                                               |
| Commit / push                                       | Neither performed                                    |
| New unresolved stop conditions                      | None                                                 |

Remaining boundary: mutations remain Development-only and unattributed until real authentication/RBAC exists. Off-site policy, monthly reports and all downstream consequences remain deferred as described above.
