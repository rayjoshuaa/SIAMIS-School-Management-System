# D9B — Attendance Evidence and Expected-Work Foundation

Date: 2026-10-04, Asia/Bangkok. Local Development verification: localhost / SIAMIS, Windows integrated authentication.

## 1. Executive summary

**D9B implementation and verification completed successfully.** D9B implements immutable observed attendance evidence and purpose-specific expected-work resolution. It does not pair events, confirm absence, finalize attendance, integrate devices, or change Leave/Payroll calculations. Legacy Attendance reads/storage remain; legacy writes are retired explicitly.

## 2. Approved contracts implemented

- Explicit Asia/Bangkok business dates; UTC instants retain seven fractional digits.
- Direction In/Out/Unknown; source ManualAuthorized/Device/Imported.
- Server-controlled manual provenance, receipt time, null actor and required reason/request key.
- Evidence retained for inactive and not-employed existing employees; receipt anomalies frozen.
- Effective employment -> explicit effective assignment -> calendar -> override/weekly intervals; no default fallback.
- Immutable API/EF evidence; no update/delete routes or correction fields in this checkpoint.
- Development-only manual mutation; external provenance capability reserved without device/import routes.

No duration columns were introduced. The approved canonical calculated-duration unit remains wide integer milliseconds for D9C; evidence capture performs no minute/millisecond rounding. Leave remains minute-based. Submillisecond event evidence is preserved for future explicitly defined calculation conversion.

## 3. Legacy attendance transition

Both legacy GET routes retain their original ownership, filters, projection and current status-label behavior. Valid legacy POST/PUT/DELETE requests return **410 Gone** with a ProblemDetails retirement explanation. Normal ASP.NET request validation can still return 400 for malformed legacy POST/PUT bodies before the action executes.

The Infrastructure legacy service also returns retirement failures without database writes, preventing internal callers from bypassing controller retirement. There are no other repository callers of these mutation methods. Legacy Attendance and AttendanceStatuses are retained unchanged; no dual writes or status reinterpretation.

## 4. AttendanceEvent schema

New table: **AttendanceEvents**.

| Column | SQL representation / meaning |
|---|---|
| AttendanceEventId | uniqueidentifier PK |
| EmployeeId | required uniqueidentifier; Employees ownership FK, NoAction |
| OccurredAtUtc | required datetime2(7), normalized source instant |
| BusinessDate | required date, derived explicitly in Asia/Bangkok |
| BusinessTimeZone | required nvarchar(50), constrained Asia/Bangkok |
| Direction | required nvarchar(20), constrained In/Out/Unknown |
| Source | required nvarchar(30), constrained ManualAuthorized/Device/Imported |
| SourceKey | nullable nvarchar(100), external namespace |
| ExternalEventId | nullable nvarchar(200), external replay identity |
| ManualRequestKey | nullable uniqueidentifier; required/nonempty for manual evidence |
| OriginalSourceTimestamp | nullable nvarchar(100); manual endpoint preserves original explicit-offset text |
| ReceivedAtUtc | required datetime2(7), server receipt timestamp |
| Reason | nullable nvarchar(2000); nonblank required for manual evidence |
| ActorId | nullable uniqueidentifier; manual constrained null, no user/Employee actor FK |
| EmployeeWasInactive | required bit; frozen receipt fact |
| EmploymentReadiness | required nvarchar(30); Ready/NotEmployed/ConfigurationConflict receipt fact |

Five enabled/trusted checks: Direction, Source, TimeZone, EmploymentReadiness, SourceFields. SourceFields enforces manual key/reason, null external identity and actor; Device/Imported require nonblank namespace/external ID and null manual key. Controlled text and opaque external identities use Latin1_General_100_BIN2 to preserve exact values and case-sensitive external identity.

Indexes: PK; (EmployeeId, BusinessDate, OccurredAtUtc, AttendanceEventId) lookup; filtered unique ManualRequestKey; filtered unique (SourceKey, ExternalEventId). EmployeeId is deliberately excluded from external replay uniqueness. There are no actor FKs, cascades, duration/settings/policy tables or daily/finalization tables.

## 5. Timestamp/timezone behavior

Manual `occurredAt` is an ISO 8601 string requiring seconds, explicit Z or numeric offset and at most seven fractional digits. Offsetless and overprecision inputs return 400. DateTimeOffset normalization produces a UTC DateTime; original text is retained. ReceivedAtUtc uses DateTime.UtcNow. DTO timestamps explicitly serialize as UTC Z after SQL materialization.

BusinessDate uses TimeZoneInfo with the existing Leave Asia/Bangkok constant, not machine-local time or a hard-coded SQL offset calculation. SQL datetime2 stores precision, not a timezone tag; the application owns UTC normalization and business-date derivation. No organization timezone configuration or Leave precision changes.

## 6. Employment/inactive behavior

The manual endpoint requires an existing employee, not an active one. It locks the employee before checking effective employment for the normalized event date. EmployeeWasInactive and EmploymentReadiness are captured once; replay returns the original receipt even if live state later differs.

DTO IntakeAnomalies derives EmployeeInactiveAtReceipt and non-Ready employment findings from those frozen fields. No employment repair or clock discarding. Expected work uses inclusive effective bounds and StartDate ?? HireDate, independent of current IsActive/IsCurrent selection. The original inactive/open TEST-EMP-001 baseline is preserved exactly.

## 7. Expected-work resolver

GET resolves valid date-effective employment first, then exactly one explicit assignment. Missing employment -> NotEmployed; missing assignment -> WorkCalendarNotConfigured; ambiguous/invalid employment, assignments, calendars, overrides or intervals -> ConfigurationConflict. Only a fully resolved schedule is Ready.

Returns employee/date/timezone/active flag, employment context, assignment/calendar IDs/code/name, override ID, schedule kind and exact ordered interval IDs/times. Missing readiness is distinguished from a valid zero-work day by readiness and null schedule kind. Multiple intervals preserve gaps. PublicHoliday/SchoolHoliday/RestDay have no working intervals. ExceptionalWorkingDay replaces the weekly schedule. An established assignment remains usable when its calendar is currently inactive; IsDefault is never consulted.

Pure AttendanceFoundationResolver reuses existing employment and whole-minute calendar conventions. AttendanceFoundationService uses coherent Serializable reads with employee first, then sorted calendar locks. No LeaveType/LeavePolicy dependency, balance access, event pairing, schedule duration column or daily-result creation.

## 8. Development-only security boundary

All new mutation routes consist of the manual evidence POST; it returns 404 outside Development before entering the service. Unknown request fields are rejected, including source, actor/user/reviewer IDs, receipt time, business date and external identifiers. Source is server-selected ManualAuthorized; actor is null.

Reads follow existing project conventions. Employee ownership validation is not caller authorization. No production authentication/RBAC or trusted anonymous HR identity is claimed.

## 9. Idempotency/concurrency

ManualRequestKey is globally unique. Same key + same employee, normalized UTC instant, direction and trimmed reason returns **200** with the original event/receipt. New evidence returns **201**. A different normalized payload returns **409**. Equivalent offset representations of the same instant are replay-compatible; the first original timestamp remains intact.

Serializable intake acquires Employees UPDLOCK first, then a locked key lookup, inserts one event and commits. Unique/deadlock races are handled as replay/conflict with a retry message if no committed receipt exists. No SQL exception is returned as a successful partial intake. Payroll lock ordering is unchanged.

External replay uniqueness is schema-only in D9B: no import endpoint or runtime provider adapter exists. Ordinary tracked EF updates/deletes are rejected by a focused DbContext guard before SQL; APIs provide no raw update/delete. Privileged direct SQL/bulk operations are not made immutable with triggers. Verification cleanup removes only its recorded temporary fixture IDs directly through SQL.

## 10. Migration details

Migration: **20261003175821_AddAttendanceEvidenceFoundation**.

Generated and inspected before local application: one CreateTable, one employee NoAction FK, five checks, three secondary indexes plus PK. Model snapshot diff contains only the new event entity/relationship. No applied migration was rewritten; no D1–D8 schema operation or legacy drop was generated.

Applied only via explicit local localhost/SIAMIS connection and Development environment. Application table count increases 73 -> 74; migration history 37 -> 38. Exact prior table rows/timestamps are compared to a pre-migration snapshot.

The first application attempt used an assembly built before the newly generated migration file existed and reported migration not found; no migration was applied by that attempt. Rebuilding included the migration, and the explicit target application succeeded. No corrective migration was created.

## 11. API changes

| Method / route | Result |
|---|---|
| POST /api/employees/{employeeId}/attendance-events/manual | Development-only: 201 new / 200 replay / 400 invalid / 404 missing employee or outside Development / 409 conflicting key |
| GET /api/employees/{employeeId}/attendance-events/{eventId} | 200 owned evidence / 404 missing or wrong parent |
| GET /api/employees/{employeeId}/attendance-events | Paged DTOs, inclusive business-date filtering, stable date/instant/ID order; pageSize1–100 |
| GET /api/employees/{employeeId}/attendance-expected-work?date= | 200 readiness/context/schedule DTO / 400 missing/invalid date / 404 employee missing |
| Existing legacy collection/item GET | Preserved |
| Existing legacy POST/PUT/DELETE | Valid requests410; no mutation |

Example manual request:

```json
{
  "occurredAt": "2026-10-04T08:00:00.1234567+07:00",
  "direction": "In",
  "manualRequestKey": "11111111-2222-4333-8444-555555555555",
  "reason": "Authorized Development verification entry"
}
```

Use a new request key for different evidence; reuse it only to retry the same normalized payload. Controllers call Application interfaces; Infrastructure owns EF queries/transactions. No entities or navigation graphs are exposed. XML Swagger descriptions cover provenance, timezone, query parameters, readiness and response codes.

## 12. Test results

All final successful runs passed:

| Suite | Recorded checks |
|---|---:|
| Pure payroll/statutory/Leave/D9B runner | 691 (649 existing + 42 D9B) |
| Focused D9B live API/SQL/Swagger | 122 |
| Local Production-mode manual mutation rejection | 1 |
| D8B/D1 live | 117 |
| D8C live | 246 |
| D8C ExpectedStatus concurrency | 230 |
| D8D full evidence/sandwich live | 523 |
| D8D capped-consumption focus | 391 |
| D5A payroll live | 70 |
| D5C SSO/monetary live | 306 |
| D6B tax classification live | 76 |
| D6C claims/policy live | 48 |
| D6D calculator contract live | 38 |
| D6E payroll/PIT live | 88 |
| D6E boundary/regeneration live | 128 |
| D7 operations/manual reconciliation/payroll and period lifecycle live | 84 |

**3,159 recorded suite checks**, plus exact-baseline wrapper/final verification guards. Counts describe final successful runs, not the earlier interrupted focused attempt or duplicate pure-run executions. Prior payroll live suites total 838. Existing wrappers were reused with obsolete application-table counts adapted in memory to 74; no expected financial, leave or lifecycle outcome was altered. Original regression scenario files remain unchanged.

Focused live results cover precise IN/OUT/Unknown evidence, explicit-offset normalization, original metadata, Bangkok UTC-day boundary, required reason/key, spoofed fields, inactive/not-employed intake, wrong-parent reads, API immutability, replay conflicts, SQL source-specific checks and both external replay uniqueness cases. Eight concurrent identical manual requests returned one201/seven200 with one ID; conflicting simultaneous payloads returned one201/one409.

Schedule checks cover explicit assignment, default non-fallback, inclusive bounds, gaps, ordered split intervals, all three non-working override kinds, exceptional replacement, inactive calendar and deliberate temporary ambiguity. Swagger exposes all four new route templates, correct manual fields and 410 retirement responses. No daily/finalization table, Leave write or Payroll write arose from D9B.

The first focused run stopped on a test helper passing a UUID object to a string-only formatter during an external uniqueness assertion. Its finally cleanup restored the complete baseline. The helper was corrected and the entire 122-check run passed. No application workaround was needed. There are no outstanding test/build errors.

Artifacts under `tests/SIAMIS.Payroll.RegressionTests/bin`: `d9b-live-results.json`, `d9b-regressions-results.json`, `d9b-production-boundary.json`, `d9b-final-verification.json`, `d9b-before-migration.json`, and `d9b-*.log` / nested existing regression outputs. Test API logging was reduced through temporary process arguments on the final rerun; configuration files were not changed.

## 13. Leave non-impact

No Leave entity, configuration, service, API or calculation source was changed. Minute precision, explicit assignments, replacement overrides, lifecycle concurrency, frozen paid classifications, evidence prerequisites, normal insufficient-balance409 and capped non-working sandwich consequences remain unchanged. D9B does not read/write leave balances or approve/cancel leave.

## 14. Payroll non-impact

No PayrollCalculationService, PayrollGenerationService, PayrollPreviewService, salary entitlement, rule evaluator, SSO/PIT or payroll lifecycle source was changed. No deductions, overtime, wage-time formula, KPI or unpaid leave conversion was introduced. D9B has no payroll calls or relationships. Existing pure and live monetary/lifecycle/snapshot tests verify their approved expectations unchanged.

## 15. Legacy compatibility status

Legacy table/entity/status seeds retained. GETs still expose legacy summaries exactly as before; they are not converted into events or authoritative attendance calculations. A temporary synthetic legacy summary verifies both GET routes and is removed afterward. Other environments were not queried or migrated; no assumptions about their row counts were made. Legacy storage removal requires a later explicit decision.

## 16. Security/privacy deferrals

No credentials, face images/templates, biometric payloads, vendor SDK, device ingestion endpoint, user model, production roles, retention/export policy or correction-review authority was implemented. Actor placeholder has no Employee FK. Production mutation remains gated.

## 17. Remaining D9C/D9D/D9E decisions

- D9C: event pairing/ties/duplicates/missing events; interval coverage and Leave/calendar conflicts; objective variances; precise duration conversion. No policy grace/late count or off-site inference.
- D9D: correction requests/review/apply, stale input revisions, absence review, finalization/reopening/cutoff and handling later Leave/source changes.
- D9E: actual provider IDs/direction/timestamps/mapping, replay/batch/drift contracts and secure adapters; operational roster/month reads.
- Later independent contracts: overnight work, automatic paid-to-unpaid exhaustion, payroll monetary attendance effects, KPI/discipline and production auth/privacy.

No new material D9B stop condition was found. None of these deferred decisions was silently implemented.

## 18. Exact baseline cleanup

Verification compared every application table's sorted complete row JSON, including timestamps and audit/snapshot fields. Final data equals the pre-migration 73-table snapshot exactly, plus the new empty AttendanceEvents table. All temporary clocks, synthetic employee, legacy summary, calendars/intervals/overrides/assignments and prior-suite fixtures were removed. No baseline employee fields/timestamps needed to be changed by D9B.

| Final Development count | Actual |
|---|---:|
| Application tables / migration history rows | 74 / 38 |
| Employees / EmploymentRecords | 1 / 1 |
| Legacy Attendance / AttendanceEvents / orphan events | 0 / 0 / 0 |
| AttendanceStatuses / PayrollComponents | 11 / 17 |
| Master-data rows across original 18 tables | 172, unchanged |
| WorkCalendars / weekly intervals / overrides / override intervals / employee assignments | All 0 |
| LeavePolicies / EmployeeLeave / allocations / entitlements / adjustments | All 0 |
| Seven D8D evidence/sandwich tables | All 0 |
| PayrollRules / PayrollRuleTargets / PayrollSettings / PayrollPeriods | All 0 |
| EmployeeCompensations / payroll assignments / payroll headers / payroll lines | All 0 |
| Statutory/PIT profiles/configuration/results, payment schedules, payslips, OrganizationProfiles | All 0 |
| Other operational child tables | All 0 |

TEST-EMP-001 remains inactive, ID `433f2c1a-6222-494f-a64f-cd0c31126dc4`. Its original current open employment is unchanged. Final GET employees returns200/totalCount1; legacy Attendance GET returns200/[]; event collection GET returns200/totalCount0. Final read-only verification confirms zero event orphans and exactly the approved new migration as history row38.

## 19. Complete changed-file list

Created:

- D9B-REPORT.md
- src/SIAMIS.Api/Controllers/AttendanceFoundationController.cs
- src/SIAMIS.Application/Employees/AttendanceFoundationContracts.cs
- src/SIAMIS.Domain/Entities/Employees/AttendanceEvent.cs
- src/SIAMIS.Infrastructure/Configurations/AttendanceEventConfiguration.cs
- src/SIAMIS.Infrastructure/Services/AttendanceFoundationResolver.cs
- src/SIAMIS.Infrastructure/Services/AttendanceFoundationService.cs
- src/SIAMIS.Infrastructure/Migrations/20261003175821_AddAttendanceEvidenceFoundation.cs
- src/SIAMIS.Infrastructure/Migrations/20261003175821_AddAttendanceEvidenceFoundation.Designer.cs
- tests/SIAMIS.Payroll.RegressionTests/D9BFoundationTests.cs
- tests/verify_d9b_live.py
- tests/verify_d9b_regressions.py

Modified:

- src/SIAMIS.Api/Controllers/EmployeeAttendanceController.cs
- src/SIAMIS.Api/Program.cs
- src/SIAMIS.Infrastructure/Data/SIAMISDbContext.cs
- src/SIAMIS.Infrastructure/Migrations/SIAMISDbContextModelSnapshot.cs
- src/SIAMIS.Infrastructure/Services/EmployeeAttendanceService.cs
- tests/SIAMIS.Payroll.RegressionTests/Program.cs

D9A-ANALYSIS.md was already untracked at D9B start and was not modified by D9B. Verification logs/results/baseline JSON are generated under the existing ignored tests/SIAMIS.Payroll.RegressionTests/bin directory; they are not production or migration files.

## 20. Verification/status table

| Check | Result |
|---|---|
| Initial tree inspection | Prior D9A-ANALYSIS.md untracked; no prior D9B implementation or code changes to discard |
| Scope/model inspection | Only evidence foundation; no existing internal legacy mutation callers outside its controller/service |
| Migration inspection/application | One focused migration, one new table; localhost/SIAMIS only; no original schema/data rewrite |
| Actual SQL integrity | 16 columns, datetime2(7), five trusted/enabled checks, PK/lookup/two filtered unique indexes, one trusted NoAction FK |
| Manual source/identity protection | Server source, null actor, strict request fields; normal validated Production-mode POST404 |
| Idempotency/races | Original receipt preserved; normalized payload conflicts409; concurrent tests passed |
| Readiness/calendar resolution | Ready/NotEmployed/WorkCalendarNotConfigured/ConfigurationConflict verified; no fallback or result writes |
| Legacy compatibility | GET behavior retained; POST/PUT/DELETE410; table/statuses preserved |
| Swagger | Four new templates and retired-write responses verified live |
| Pure/live/regression tests | All final runs passed; section12 totals |
| Exact cleanup | All 73 original tables' complete rows/timestamps unchanged; new event table empty; employee inactive |
| dotnet restore | Succeeded, projects up-to-date |
| Release build | Succeeded, **0 warnings / 0 errors** |
| EF pending model changes | **None** |
| git diff --check | Passed; new untracked files separately checked for trailing whitespace |
| SQL/API final read-only checks | 74 tables / 38 migrations, event orphans0, employees200/totalCount1 |
| Temporary test API processes | Task-owned Development and Production-mode verification processes stopped after checks |
| Leave/Payroll production source | Unchanged; approved regression expectations passed |
| D9C/D9D/D9E / auth/vendor/monetary behavior | Not implemented |

Initial sandbox restore could not read the installed user NuGet configuration; the permitted host restore succeeded. Local HTTP-only verification processes emitted the existing HTTPS-port discovery warning; requests completed as expected. Git reported LF-to-CRLF normalization notices, not whitespace failures. The resolved migration-assembly and test-helper issues are documented above; no unresolved errors remain.

No commit or push. No D9C/D9D/D9E implementation.
