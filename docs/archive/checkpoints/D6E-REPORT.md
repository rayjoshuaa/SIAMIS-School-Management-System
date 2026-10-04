# D6E — PIT payroll integration and historical persistence

Completed 2026-10-03. The two stopped contracts were approved and implemented within D6E. No commit or push was performed.

## 1. Files created/modified

The complete working-tree inventory appears at the end of this report. Changes cover the explicit payment schedule, PIT result ledger, shared integration, focused APIs, guards, migration and regression coverage.

## 2. Migration name/status

`20261003042439_AddPitPayrollIntegration` was generated, inspected and applied only to Development `localhost/SIAMIS` using Windows integrated authentication and the existing configuration.

The migration adds exactly four tables: `EmployeePitPaymentSchedules`, `EmployeePitPaymentScheduleEntries`, `EmployeePitPaymentScheduleSelections`, and `EmployeePayrollPitResults`. It has no seeds, backfills or alterations to existing business tables. No historical PIT results, enrollments, schedules or ordinals were invented.

## 3. EmployeePayrollPitResult schema

| Group | Stored columns |
|---|---|
| Identity/ownership | EmployeePayrollPitResultId, EmployeePayrollId, EmployeeId, PayrollPeriodId |
| Business date | GoverningDate, TaxYear, Currency |
| Configuration identity | StatutorySchemeId, StatutoryPolicyVersionId, EmployeeStatutoryEnrollmentId, EmployeeTaxDeclarationId, CalculationMethodVersion |
| Schedule facts | EmployeePitPaymentScheduleId, ScheduleRevisionNumber, ApplicablePaymentCount, PaymentOrdinal, IsFinalScheduledPayment |
| Income | CurrentRegularIncome, PriorRecognizedIncome, ProjectedRegularIncome |
| Expense/allowances | EmploymentExpenseDeduction, PersonalAllowance, SpouseAllowance, ChildAllowance, ParentAllowance |
| SSO/tax | RecognizedEmployeeSso, NetTaxableIncome, RawAnnualTax, AllocatableAnnualWithholding, SubSatangRemainder, PriorRecognizedWithholding, CurrentWithholding, FinalAllocationResidual, OverWithheldAmount |
| Historical evidence | CalculationSnapshotJson, CreatedAt |

GUID identities, `date` business dates, UTC application timestamps in SQL `datetime2`, and `decimal(38,18)` monetary evidence. The JSON preserves the exact D6D calculation, including raw allocation precision not duplicated as a rounded database field.

## 4. Uniqueness and relationships

One PIT result per payroll is enforced by a unique EmployeePayrollId index. An EmployeeId/TaxYear/GoverningDate index supports history reads. Result FKs reference the owning payroll, employee, period, scheme, policy, enrollment, declaration and schedule. All use NoAction.

Schedule revisions are unique by employee/year/revision; only one Draft may exist per employee/year. Entries have unique schedule/date and schedule/ordinal indexes. Selection has an employee/year primary key and a composite FK enforcing schedule ownership/year. Replacement references also enforce employee/year ownership.

Actual SQL verification confirmed all 12 new FKs are NoAction and all eight new check constraints are enabled and trusted. Checks cover schedule status/evidence/year/ordinal and result year/payment bounds, satang withholding, final-payment consistency and valid JSON. Payroll-line SourceId remains historical snapshot data without an FK.

## 5. Generation order

Existing employment resolution and D3 salary entitlement → final generic rule/assignment calculation → D5 SSO → explicit PIT enrollment on PayDate → selected Verified schedule/declaration and Published policy → unchanged D6D calculator → persist payroll, SSO, PIT result and lines in the same employee transaction.

An intended payroll identity is allocated before calculation so current SSO/PIT evidence matches the eventual owner. Generation re-resolves all inputs inside its authoritative transaction.

## 6. DEDUCT-002 ownership

Canonical identity is `f1000000-0000-0000-0000-000000000002` / `DEDUCT-002`, active and Deduction. Display name is not used as identity. Conflicting assignment/rule-generated DEDUCT-002 lines fail generation before replacement/persistence. Manual DEDUCT-002 create/update/delete is prohibited. Positive PIT produces one Statutory line whose SourceId is the exact PIT result ID.

## 7. Positive, zero, NotApplicable and RequiresReview

Positive withholding persists a result and one deduction. Zero persists the result without a zero line. Explicit NotApplicable enrollment creates neither result nor PIT line. Absent/ambiguous applicability and unresolved required inputs fail that employee's generation; Preview explains RequiresReview. Other employees retain the existing independent processing behavior.

An active TH-PIT PersonalIncomeTax scheme is required for applicability resolution. Existing SocialSecurity enrollment validation is retained. Nationality, taxpayer ID, declaration presence, employee activity and salary do not infer PIT enrollment.

## 8. Historical income recognition

Only Paid payrolls for this employee and tax year, after the inclusive opening cutoff and before the current payment, supply historical income. Same-day ambiguous ownership requires review through D6D. Current/replaced payrolls are excluded. Historical earning/payment classifications come from stored lines, not live components.

## 9. Historical PIT recognition

Only exact Paid EmployeePayrollPitResult.CurrentWithholding values are authoritative. Draft, Calculated, Approved and Cancelled payrolls are excluded. No deduction-line or manual-line fallback exists. Missing result authority fails review rather than fabricating zero. Opening withholding plus post-cutoff Paid results is recognized once.

## 10. Historical SSO recognition

Inputs are the exact reviewed opening employee amount, Paid D5 employee amounts and current D5 employee amount when Applicable. Employer amounts and future projections are excluded. Historical results are not recomputed using live rates.

## 11. Regeneration

Only editable Draft/Calculated payrolls regenerate. All calculations must succeed before old snapshots are removed. Successful regeneration replaces header, lines, SSO and PIT coherently. Failed regeneration preserves complete prior IDs, totals, lines and audit JSON. Approved/Paid/Cancelled payroll protections remain.

## 12. Manual mutation protections

Any persisted PIT result, including zero, blocks earning creation/update/deletion and conversion between earnings and deductions. Generated lines remain protected. Unrelated manual deductions retain existing reconciliation behavior without recalculating PIT. Failed mutations preserve stored data.

## 13. Transactions and locking

Generation retains Serializable isolation and the established parent-period → employee lock order. Schedule revision creation/edit/verification uses Serializable and the existing employee UPDLOCK convention. Selection and declaration writers serialize on that same employee row. No new broad isolation mechanism was introduced.

## 14. Preview

Ordinary payroll Preview and focused PIT Preview use the same read-only integration/calculator path as Generation. Preview performs zero writes, shows applicability and calculation evidence, and exposes no purported persisted PIT result ID. Its PIT line SourceId is null; Generation supplies the authoritative ID. Preview state never authorizes later Generation.

## 15. Payroll totals before/after PIT

PIT leaves BasicSalary, GrossPay, legacy TaxableEarnings and assessable-income classification unchanged. TotalDeductions adds exact employee PIT once; NetPay remains GrossPay minus TotalDeductions. Employer SSO remains outside employee totals. A deduction exceeding available GrossPay fails atomically.

Synthetic examples verified:

| Scenario | GrossPay | Employee SSO | PIT | TotalDeductions | NetPay |
|---|---:|---:|---:|---:|---:|
| Monthly base 30,000; N=12; synthetic flat PIT 1%; SSO NotApplicable | 30,000 | 0 | 300 | 300 | 29,700 |
| Same base; synthetic employee SSO 300 | 30,000 | 300 | 299.75 | 599.75 | 29,400.25 |
| Earning Supplement; Included 31,300 plus Excluded 200; other deduction 100 | 31,500 | 0 | 313 | 413 | 31,087 |
| Earning ReplaceAssignment; Included 30,500 plus Excluded 200; other deduction 100 | 30,700 | 0 | 305 | 405 | 30,295 |
| Deduction Supplement adds 20 to that scenario | 30,700 | 0 | 305 | 425 | 30,275 |
| Deduction ReplaceAssignment substitutes 40 for matching 100 | 30,700 | 0 | 305 | 345 | 30,355 |

These are software fixtures, not legal Thai rates or component classifications. All were cleaned.

## 16. Lifecycle protections

Approval/payment validate stored PIT owner, amount and statutory provenance alongside existing stored totals. Neither recalculates PIT. Approved/Paid/Cancelled generation protections and parent-period Processing/Close/Cancel guards passed. Close/payment preserve historical results; no expected-population completeness rule was added.

## 17. Result/schedule audit

Version `D6E-V1` wraps the unchanged D6D snapshot, exact enrollment identity and selected schedule DTO. It preserves schedule revision/year, complete entries/evidence, exact PayDate/ordinal/count/final flag, policy/reference/rates/brackets, declaration/treatment/claims/opening cutoff, final line provenance, employee SSO sources, historical PIT sources, raw tax/allocation and final residual/over-withheld values.

Verified schedules are immutable; corrections create new Draft revisions. Later selection does not rewrite stored evidence. Schedule N is the verified entry count; ordinal is an exact date match. V1 validation permits 1–12 ordered consecutive monthly payments, with no duplicates, ordinal gaps or wrong-year dates. No count/ordinal is inferred from hire dates, D3 days, month number or current date.

## 18. API and Swagger

New focused routes:

- POST `/api/employees/{employeeId}/pit-payment-schedules` — create Draft/replacement, 201.
- GET `/api/employees/{employeeId}/pit-payment-schedules/{id}` — inspect revision.
- PUT `/api/employees/{employeeId}/pit-payment-schedules/{id}` — correct Draft evidence/entries only.
- POST `/api/employees/{employeeId}/pit-payment-schedules/{id}/verify` — verify/select atomically.
- GET `/api/employees/{employeeId}/pit-payment-schedules/current/{taxYear}` — selected revision or 200 null.
- GET `/api/employee-payrolls/{payrollId}/pit-result` — stored historical evidence or 200 null.

Existing focused PIT Preview now uses the shared integration. Existing statutory enrollment API accepts active TH-PIT PersonalIncomeTax as well as its existing SocialSecurity behavior. DTOs remain separate from EF entities; no broad employee-list exposure or caller-selected provenance fields were added. Swagger routes/schemas were verified.

Testing found and fixed two new implementation issues: null-read responses needed explicit 200 JSON rather than framework 204 formatting; Draft replacement entries needed explicit EF Added tracking because their GUIDs were preassigned. Repeated Draft edits now pass. No D6D monetary fix was necessary.

## 19. Focused test results

| Suite | Passed assertions |
|---|---:|
| D6E live schedule/applicability/integration | 88 |
| D6E boundary/history/rules/lifecycle/concurrency | 128 |
| Existing and new pure/model regression runner | 386 |

Verified twelve- and six-payment schedules, exact/final/non-final dates, missing date, invalid structures, Draft restrictions, ownership, replacement history, explicit/Unknown/ambiguous applicability, PayDate selection, nationality independence, positive/zero behavior, SSO/PIT preservation, statutory collisions and manual guards. No legal statutory values were seeded.

## 20. Concurrency results

Concurrent new generation yielded one Generated and one Skipped result. Concurrent editable regeneration succeeded serially and left one header/result/PIT line. Concurrent replacement creation admitted one Draft. Generation racing declaration verification retained one coherent declaration/opening/history state. Opening change racing verification yielded a complete committed revision; subsequent Verified mutation failed without writes. Generation racing prior payment recognized consistently either the before-Paid or after-Paid state. No duplicate results, mixed recognition or deadlock was observed.

## 21. D1–D6D regression results

| Existing live suite | Passed assertions |
|---|---:|
| D5A generic payroll/classification/lifecycle | 70 |
| D5C SSO monetary and employment salary scenarios | 306 |
| D6B PIT classification/tax treatment | 76 |
| D6C policy/claims/opening/revisions | 48 |
| D6D payment classification/Preview | 38 |

Relevant employment/context, D3 full-month/joiner/leaver/gap entitlement, D4A policy, D4B enrollment/declaration, D5 resolver/calculator, D6 classifications/claims/calculator, Supplement/ReplaceAssignment, manual reconciliation, provenance and rollback coverage passed. Generic fixtures explicitly opt out of PIT. The SSO suite's opt-out covers its deliberately tested 2027 PayDate; SSO still resolves on EndDate.

Total: **754 live assertions plus 386 pure/model assertions = 1,140**. Independent final baseline checks also passed.

## 22. Cleanup and Development baseline

Every suite compared all application-table rows/timestamps after cleanup. An independent final comparison matched the pre-migration 51-table baseline plus exactly four empty new tables. TEST-EMP-001 is unchanged and inactive; the known legacy employment inconsistency was not repaired.

| Final data | Count |
|---|---:|
| Application tables | 55 |
| Employees | 1 |
| EmploymentRecords | 1 |
| PayrollComponents | 17 |
| Master-data rows, including PayrollComponents | 172 |
| PayrollRules / targets / settings / periods | 0 each |
| Compensation / component assignments | 0 each |
| Payroll headers / lines / SSO results / PIT results | 0 each |
| PIT schedules / entries / selections | 0 each |
| Statutory schemes / policies / enrollment | 0 each |
| Tax profiles / declarations / claims / opening / selections | 0 each |
| Contacts / addresses / emergency contacts | 0 each |

All 17 component SSO, PIT income and PIT payment classifications remain Unknown. No real statutory applicability or payment facts were backfilled. Other non-master employee/operational tables remain empty. The owned verification API was stopped. Temporary D6E SQL input files and logs were removed; reusable regression scripts remain under tests.

## 23. Restore and Release build

Final restore succeeded. `dotnet build SIAMIS.sln -c Release --no-restore` succeeded with **0 warnings and 0 errors**. SDK 10.0.401 / EF 10.0.12. Restore and Windows-authenticated SQL/API checks needed execution outside the desktop sandbox; no dependency/security workaround was introduced.

## 24. EF model check

`dotnet ef migrations has-pending-model-changes --project src/SIAMIS.Infrastructure --startup-project src/SIAMIS.Api --configuration Release --no-build` passed: no changes since the last migration.

## 25. Git checks

`git diff --check` passed cleanly. Existing `PitCalculator`, `PayrollCalculationService`, `Section33Calculator`, `BasicSalaryEntitlementService` and `PayrollRuleEvaluator` files have no diff. D6D formulas, rounding, claims semantics and generic formulas were not changed. No commit or push.

## 26. Remaining boundaries

No unresolved implementation stop remains. PIT-TH-V1 still requires review for unsupported partial/irregular payments, ambiguous employment spells, leaver and optional year-end reconciliation. A Paid predecessor lacking authoritative PIT history cannot be silently treated as zero; a reviewed opening cutoff/contract is required. Authentication/RBAC and broader payroll scope remain deferred.

The restored baseline intentionally has no real PIT enrollment, policy, declaration or schedule. Automatic generation therefore requires explicit reviewed administrative inputs before it can calculate PIT. No production configuration or legal numeric values were chosen in D6E.

## Complete changed-file inventory

- Modified: src/SIAMIS.Api/Controllers/EmployeeStatutoryEnrollmentsController.cs
- Modified: src/SIAMIS.Api/Program.cs
- Modified: src/SIAMIS.Application/Payroll/PayrollPreviewContracts.cs
- Modified: src/SIAMIS.Infrastructure/Data/SIAMISDbContext.cs
- Modified: src/SIAMIS.Infrastructure/Migrations/SIAMISDbContextModelSnapshot.cs
- Modified: src/SIAMIS.Infrastructure/Services/EmployeePayrollService.cs
- Modified: src/SIAMIS.Infrastructure/Services/EmployeeStatutoryService.cs
- Modified: src/SIAMIS.Infrastructure/Services/PayrollGenerationService.cs
- Modified: src/SIAMIS.Infrastructure/Services/PayrollPreviewService.cs
- Modified: src/SIAMIS.Infrastructure/Services/PitCalculationPreviewService.cs
- Modified: tests/SIAMIS.Payroll.RegressionTests/D6DCalculatorTests.cs
- Modified: tests/SIAMIS.Payroll.RegressionTests/Program.cs
- Modified: tests/verify_d5a_live.py
- Modified: tests/verify_d5c_live.py
- Modified: tests/verify_d6b_live.py
- Modified: tests/verify_d6c_live.py
- Modified: tests/verify_d6d_live.py
- Created: D6E-REPORT.md
- Created: src/SIAMIS.Api/Controllers/EmployeePayrollPitResultsController.cs
- Created: src/SIAMIS.Api/Controllers/EmployeePitPaymentSchedulesController.cs
- Created: src/SIAMIS.Application/Payroll/PitPaymentScheduleContracts.cs
- Created: src/SIAMIS.Application/Payroll/PitPayrollContracts.cs
- Created: src/SIAMIS.Domain/Entities/Payroll/EmployeePayrollPitResult.cs
- Created: src/SIAMIS.Domain/Entities/Payroll/EmployeePitPaymentSchedule.cs
- Created: src/SIAMIS.Infrastructure/Configurations/EmployeePayrollPitResultConfiguration.cs
- Created: src/SIAMIS.Infrastructure/Configurations/PitPaymentScheduleConfigurations.cs
- Created: src/SIAMIS.Infrastructure/Migrations/20261003042439_AddPitPayrollIntegration.Designer.cs
- Created: src/SIAMIS.Infrastructure/Migrations/20261003042439_AddPitPayrollIntegration.cs
- Created: src/SIAMIS.Infrastructure/Services/PitPaymentScheduleService.cs
- Created: src/SIAMIS.Infrastructure/Services/PitPayrollService.cs
- Created: tests/SIAMIS.Payroll.RegressionTests/D6EIntegrationContractTests.cs
- Created: tests/verify_d6e_boundaries_live.py
- Created: tests/verify_d6e_live.py
