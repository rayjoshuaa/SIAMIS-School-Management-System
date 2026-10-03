# D6D implementation and verification complete

Final result, 2026-10-02: D6D pure regular-monthly PIT-TH-V1 calculator and payment-treatment classification foundation are implemented and verified. The final user rounding decision supersedes both historical stops below. No unresolved rounding mode was selected. D6E persistence/payroll integration remains deferred.

## 1. Changed files (28)

API:

- src/SIAMIS.Api/Controllers/EmployeePitCalculationController.cs (new)
- src/SIAMIS.Api/Program.cs

Application:

- src/SIAMIS.Application/MasterData/PayrollComponentContracts.cs
- src/SIAMIS.Application/Payroll/EmployeePayrollContracts.cs
- src/SIAMIS.Application/Payroll/PayrollCalculationContracts.cs
- src/SIAMIS.Application/Payroll/PayrollPreviewContracts.cs
- src/SIAMIS.Application/Payroll/PayrollRuleEvaluationContracts.cs
- src/SIAMIS.Application/Payroll/PitFoundationContracts.cs
- src/SIAMIS.Application/Payroll/PitCalculationContracts.cs (new)

Domain:

- src/SIAMIS.Domain/Entities/MasterData/MasterDataEntity.cs
- src/SIAMIS.Domain/Entities/Payroll/EmployeePayrollLine.cs

Infrastructure:

- src/SIAMIS.Infrastructure/Configurations/AdditionalMasterDataConfigurations.cs
- src/SIAMIS.Infrastructure/Configurations/EmployeePayrollConfigurations.cs
- src/SIAMIS.Infrastructure/Migrations/20261002084445_AddPitPaymentTreatmentClassification.cs (new)
- src/SIAMIS.Infrastructure/Migrations/20261002084445_AddPitPaymentTreatmentClassification.Designer.cs (new)
- src/SIAMIS.Infrastructure/Migrations/SIAMISDbContextModelSnapshot.cs
- src/SIAMIS.Infrastructure/Services/EmployeePayrollService.cs
- src/SIAMIS.Infrastructure/Services/PayrollCalculationService.cs
- src/SIAMIS.Infrastructure/Services/PayrollComponentService.cs
- src/SIAMIS.Infrastructure/Services/PayrollGenerationService.cs
- src/SIAMIS.Infrastructure/Services/PayrollPreviewService.cs
- src/SIAMIS.Infrastructure/Services/PayrollRuleEvaluator.cs
- src/SIAMIS.Infrastructure/Services/PitCalculator.cs (new)
- src/SIAMIS.Infrastructure/Services/PitCalculationPreviewService.cs (new)

Verification/report:

- tests/SIAMIS.Payroll.RegressionTests/D6DCalculatorTests.cs (new)
- tests/SIAMIS.Payroll.RegressionTests/Program.cs
- tests/verify_d6d_live.py (new)
- D6D-REPORT.md (new, includes the earlier stopped reviews)

The temporary mechanical editing script was removed. Verification artifacts are under the ignored regression bin directory. No commit or push occurred.

## 2. Migration and exact database changes

Inspected and applied **20261002084445_AddPitPaymentTreatmentClassification** only to **localhost/SIAMIS Development**, Windows integrated authentication.

- PayrollComponents.PitPaymentTreatment: required nvarchar(20), default Unknown.
- EmployeePayrollLines.PitPaymentTreatmentSnapshot: required nvarchar(20), default Unknown.
- Both tables have trusted, enabled checks allowing Unknown/Regular/Special.
- Seventeen component seed updates set only the new classification to Unknown.
- No new tables, monetary columns, legal numeric seeds or PIT results. Migration history now has 30 entries.

Existing components remain Unknown for PIT payment treatment, PIT income treatment and SSO wage treatment. No existing component was legally classified. Synthetic classifications used during tests were restored.

## 3-4. Resolver/calculator architecture

Application contracts define typed policy/declaration/opening/schedule/current-line/history/PIT-result/SSO inputs and versioned explanations. PitCalculator is a shared pure service depending only on the existing pure IPitIncomeResolver. It has no DbContext, clock, random result IDs, binary floating point or writes. PitCalculationPreviewService resolves available live context with AsNoTracking reads and existing read-only policy/declaration/payroll-preview services. Controllers use the application interface; no direct DbContext access.

The live resolver does not manufacture an annual schedule or prior PIT ledger. Missing inputs return RequiresReview. Monetary mathematics exists only in PitCalculator; no duplicate preview mathematics or D6E integration was introduced.

## 5-10. Resolution and recognition contracts

| Topic | Implementation |
|---|---|
| Date/year | PayrollPeriod.PayDate; Gregorian PayDate year. Pure input preserves GoverningDate. |
| Policy | Compatible Published TH-PIT, TH, THB, PersonalIncomeTax, PIT-TH-V1, inclusive effective dates and exact TaxYear; invalid/incomplete input requires review. |
| Treatment/declaration | Exact selected Verified revision; treatment identity/revision/year/employee checked. StandardSection40_1 plus resolved residence required; no nationality inference. |
| Opening | Known, complete, verified CurrentEmployer/PIT-TH-V1 statement; inclusive cutoff; Unknown is not zero. Pre-expense prior income, withheld and employee SSO preserved. |
| Current income | Existing Included/Excluded/Unknown resolver plus Included + Regular gate. Included Special/Unknown payment character requires review rather than exemption. Deductions excluded. Legacy IsTaxable is not legal authority. |
| History | Same employee/year, Paid-only, strictly after opening cutoff, before current PayDate and excluding current payroll identity. Stored amount/income/payment snapshots only. Same-day and duplicate identities require review. |
| Prior withholding | Opening plus typed immutable Paid PIT-result inputs; identity/method/currency/satang checks and matching payroll/date income authority. No manual/generic deduction fallback. |
| SSO | Opening plus resolved historical/current exact employee-side sources; dates/identities/duplicates checked. Current Resolved requires a current source; NotApplicable requires none. No employer amount, projection, independent cap or SSO formula. |

## 11-17. Monetary method

- Projected regular income = current resolved regular income R × explicitly resolved N. N and ordinal require reviewed schedule evidence; no default/inference from period dates, payable days or D3 proration.
- Expense = min(projected income × configured percentage-point rate / 100, configured cap), without premature rounding.
- Exact policy-owned personal/spouse/ordinary-child/additional-child/parent allowances use selected verified typed claims. Adopted capacity uses declared living lawful children and the policy limit; parent limit enforced. Impossible/unsupported/duplicate structures require review; no silent trimming or client legal Amount.
- Net taxable income is floored at zero after approved expenses/allowances/recognized employee SSO. No unsupported deductions.
- Progressive ordered brackets must cover zero to infinity contiguously, with inclusive lower/exclusive upper bounds and final unbounded bracket. Each band's raw taxable amount/rate/tax is retained.
- RawAnnualTax retains decimal precision. AllocatableAnnualWithholding truncates toward zero to two decimals; SubSatangRemainder remains separately explained.
- RawRegularAllocation = allocatable annual withholding / N. Ordinary allocation truncates toward zero to satang. Available liability after authoritative prior withholding limits the amount and never becomes negative.
- Proven final scheduled payment receives allocatable annual withholding minus authoritative prior allocation, floored at zero. FinalAllocationResidual distinguishes its difference from ordinary allocation. No recovery of sub-satang fractions or automatic refund; over-withheld amount is informational.
- Remuneration changes are recalculated from current regular R; no frozen first calculation or generic remaining-month formula. Special/indeterminate payments, ambiguous partial first payments, final-leaver and full optional year-end reconciliation remain unsupported.

## 18-19. Result and advisory API

PitCalculationSnapshot preserves version PIT-TH-V1-D6D-1, complete resolved input identities/facts, classification/provenance, N/ordinal/evidence, expense and allowance breakdowns, SSO sources, brackets, raw/allocatable/sub-satang tax, regular/current/prior allocation, final residual and over-withholding. CalculationSnapshotJson serializes that representation deterministically for identical typed input; it is not persisted.

New Swagger-documented endpoint:

`GET /api/employees/{employeeId}/payroll-periods/{payrollPeriodId}/pit-preview`

Returns 200 with typed advisory result, including RequiresReview reasons, or 404 for missing employee/period. No caller-selected N, source provenance or financial input contract is exposed. Current SIAMIS has no authoritative annual payment schedule, so live previews cannot yet claim a Calculated withholding amount. Where prior Paid SIAMIS history exists, the missing D6E authoritative PIT ledger is additionally reported. The pure engine calculates fully resolved synthetic inputs now. This live limitation is deliberate and visible, not guessed financial behavior.

## 20-21. Tests and regressions

| Suite | Assertions | Result |
|---|---:|---|
| Existing pure D5A-D6C plus focused D6D | 365 | Passed, no DB connections/writes |
| D6D live API/SQL | 38 | Passed |
| D5A live regression | 70 | Passed |
| D5C live regression | 306 | Passed |
| D6B live regression | 76 | Passed |
| D6C live regression | 48 | Passed |
| Total | 903 | Passed |

D6D covers raw tax 12.065/12.064/12.069, exact cents and zero; 100/N=3 allocations 33.33 with final 33.34; deterministic JSON; N=12 and known midyear N; unknown schedule/partial/leaver/year-end gates; current and historical payment classifications; Paid recognition and excluded statuses; income/ledger reconciliation; opening states; SSO employee-only inputs; policy/treatment compatibility; progressive boundaries and invalid brackets; expenses/caps; all typed allowance categories/limits; no negative/refund; generated/manual snapshots and schema checks.

Live D6D verifies default/explicit Unknown/Regular/Special persistence, invalid API/SQL classification rejection, omitted-update preservation, BasicSalary/Assignment/PayrollRule snapshots, historical snapshot preservation after live changes, regeneration using current classification, manual update preservation, preview/generation parity, Swagger, read-only advisory behavior, no PIT table/line and exact cleanup.

Existing live suites cover D2 period context and D3 full/joiner/leaver/gap entitlement, D4A publication/immutability, D4B declarations/opening/enrollments, D5 calculations, D6B treatment/classification, D6C claims/recognition, earning/deduction Supplement/ReplaceAssignment, conflict rollback, failed new generation cleanup, failed regeneration full preservation, provenance, manual reconciliation, Approved/Paid/Cancelled protection and period lifecycle. Standalone D1-D3 acceptance scripts were not newly run; no additional claim is made about those scripts.

The first D6D live run failed because its fixture omitted the existing required explicit SSO NotApplicable enrollment. Its cleanup passed. The test prerequisite was corrected and the final 38-check run passed; no application monetary workaround was added. The initial EF scaffold attempt's implicit build failed; an explicit Release build succeeded and scaffolding with --no-build then succeeded. No corrective migration was stacked.

## 22-24. Unchanged payroll money and restored baseline

PayrollCalculationService, PayrollGenerationService and PayrollPreviewService changes are limited to additive payment snapshot propagation. Their monetary formulas, D3 entitlement and D5 calculation/integration are unchanged. No DEDUCT-002 lines, EmployeePayrollPitResult, refund workflow or persisted PIT monetary integration exists.

Pre-migration exact row snapshots were captured. Post-migration comparison confirmed only the approved new Unknown component column differed. Every live suite restored all 51 application tables, including original timestamps. Final read-only comparison matched the original rows plus that column. No temporary fixtures remain.

Final baseline: Employees 1 (TEST-EMP-001, 433f2c1a-6222-494f-a64f-cd0c31126dc4, inactive); EmploymentRecords 1; PayrollComponents 17, all three treatments Unknown; all 172 master-data rows retained. EmployeeCompensations, assignments, PayrollPeriods/Rules/Targets/Settings, EmployeePayrolls/Lines, statutory schemes/policies/configurations/results/enrollments, tax profiles/declarations/selections/claims/openings and all other operational tables remain zero. The full table counts in the historical baseline section below were reverified and remain accurate; EF history alone intentionally increased to 30.

## 25-28. Final checks and remaining work

- Restore succeeded.
- Release build succeeded: 0 warnings, 0 errors.
- Development EF pending-model-changes check: no changes since last migration.
- git diff --check passed.
- Verification API process started for this task was stopped after cleanup.
- No commit/push; no production/staging access.
- D6E owns immutable PIT result/history persistence, canonical PIT deduction ownership and atomic payroll integration. Authoritative annual monthly schedules/final-payment facts are also needed before live automatic calculation can resolve N. No schedule redesign or D6E work was included in D6D.

---

# Historical D6D contract review continuation: remaining mandatory rounding stop (superseded)

Date: 2026-10-02. The user's D6D Contract Review was read in full. It supersedes the original stop decisions in the historical report below. Historical PIT income and prior withholding are now separately Paid-only; regular monthly R × N / annual-tax allocation is approved; Special/Unknown payment character and ambiguous schedules require review; D6E owns the PIT result ledger. These decisions have not been reopened.

**Current result: STOP under the new contract's sections 9 and 19 before implementing monetary outputs.** One exact final-satang boundary remains unresolved. This report remains the only changed file; no classification implementation, source edits, schema changes, migration, database writes, commit or push occurred.

## Exact remaining boundary

Configured four-decimal policy values can produce a progressive annual-tax liability with more than two decimal places. The approved contract retains intermediate precision and requires a two-decimal employee-facing amount plus an exact final residual; it does not specify conversion of sub-satang annual liability into an allocatable satang total.

Synthetic, nonlegal example: net taxable income 1,206.50 and a single applicable 1% band produce raw annual tax **12.065 THB**. With N=12, truncating the ordinary allocation to satang yields 1.00 per payment. After eleven such payments, the exact remaining amount is **1.065 THB**. A two-decimal final payment cannot preserve the raw 12.065 total. Rounding 1.065 with ToEven yields 1.06; AwayFromZero yields 1.07. Selecting either mode changes withholding and is expressly prohibited without approval. Truncation also changes the total and must be an explicit annual-tax contract, not an undocumented workaround.

The [Revenue Department P.96/2543 allocation examples](https://www.rd.go.th/3558.html) support retaining a division residual for the last payment. They do not settle the treatment of every sub-satang annual liability produced by configurable policy decimals. Existing D3 salary and D5 SSO modes are not PIT authority.

**Smallest recommended approval:** keep and expose raw annual tax at decimal precision; define `AllocatableAnnualTax = decimal.Truncate(RawAnnualTax * 100m) / 100m` for nonnegative liability; define regular allocation as `decimal.Truncate((AllocatableAnnualTax / N) * 100m) / 100m`; allocate the resulting whole-satang division residual only to the provable final supported payment. Preserve raw tax, allocatable tax, discarded sub-satang amount and residual in the explanation. This is a proposed SIAMIS versioned precision contract, not a claim that the source explicitly mandates annual-tax truncation. It has not been implemented. If a different annual-tax conversion is desired, approve its rounding point and exact mode instead.

Annual-payment schedule data remains absent from the current model, but the new contract explicitly permits RequiresReview when N/final-payment identity cannot be proven. That is an unresolved input for live preview, rather than permission to invent schedules. The focused payment-treatment migration fits the existing string-classification/snapshot architecture, but has not been started while the monetary checkpoint is stopped.

## Current full requested report

| Item | Current continuation result |
|---|---|
| 1. Files | D6D-REPORT.md updated only; it was already untracked when this continuation began. |
| 2. Migration | None created/applied. Latest remains 20261002061558_AddPitClaimsAndOpeningContract. |
| 3. Resolver architecture | Existing D6B income/treatment and D6C SSO contracts inspected. New typed monetary resolver deferred. |
| 4. Calculator architecture | Shared pure resolver/calculator/presentation separation approved; no calculator implemented before the rounding stop. |
| 5. Date/year | PayDate/Gregorian year approved and unchanged. |
| 6. Policy | Published TH-PIT, TH/THB, PIT-TH-V1, effective-date and year checks remain required; no new resolution path. |
| 7. Treatment/declaration | Exact selected Verified revision and StandardSection40_1 remain required; no nationality inference. |
| 8. Opening | Reviewed CurrentEmployer opening and inclusive cutoff retained; no mutation. |
| 9. Income | Historical Paid-only independently approved. PitPaymentTreatment Unknown/Regular/Special and generated snapshots approved but not yet implemented. Existing classification resolver unchanged. |
| 10. SSO | Approved actual opening + prior Paid exact employee D5 amounts + current exact employee D5 amount retained. No projection or employer deduction. |
| 11. Expense | Policy-owned rate/cap and no premature rounding approved; no calculation added. |
| 12. Allowances | Policy-owned amounts and typed verified claims retained; no calculation added. |
| 13. Brackets | Published progressive bracket contract retained; no monetary implementation. |
| 14. Annualization | Regular monthly R × N and annual tax / N approved. No remaining-month formula; no invented N or special-payment branch. |
| 15. Prior withholding | Paid-only exact PIT result authority approved. Pure typed inputs allowed; live required history unresolved until D6E ledger exists. |
| 16. Rounding | Current STOP: raw annual tax may contain fractions of a satang; its conversion to the allocatable annual total requires approval. |
| 17. Zero/negative | Zero floor/no refund requirement retained; no amounts produced. |
| 18. Typed result | No new result or calculation snapshot implemented. |
| 19. Preview | No endpoint added; no payroll headers/lines/results or other database writes. |
| 20. Vectors | No D6D tests added while stopped. Concrete synthetic sub-satang case documented above. |
| 21. Regressions | Existing pure suite rerun: 245 focused D5A/D5B/D5C/D6A/D6B/D6C assertions passed, without DB connections/writes. Live mutation suites and standalone D1-D3 suites not rerun; no newly implemented behavior to verify. |
| 22. Totals | No source/database changes; BasicSalary/GrossPay/TaxableEarnings/TotalDeductions/NetPay behavior unchanged. |
| 23. Baseline | Fresh read-only SQL verifies all counts below, 51 application tables and 29 migration entries; 17 PIT/SSO Unknown components. |
| 24. Restoration | No fixtures created, no cleanup writes required; employee remains inactive. No claim of a new before/after full-row comparison. |
| 25. Build | Restore succeeded; fresh Release build succeeded with 0 warnings and 0 errors. |
| 26. EF | Fresh Development Release pending-model-changes check: no changes since last migration. |
| 27. Diff | git diff --check passed after report update. |
| 28. D6E | Remains deferred: authoritative immutable PIT result ledger, DEDUCT-002 ownership and atomic payroll integration; no D6E work. |

Read-only table-count verification initially failed because the diagnostic query used an unquoted reserved alias. It was corrected to [RecordCount] and rerun successfully. This was a SELECT-query issue, not an application or database failure; it caused no database writes.

## Historical first-inspection report (superseded decisions)

The following original stop findings are retained as history. The current approved decisions and single remaining rounding stop above take precedence. Its database count table also matches the fresh read-only continuation verification.

# D6D inspection and mandatory stop report

Date: 2026-10-02. **D6D monetary implementation is stopped under the requested stop conditions.** D6A-D6C were inspected; their completed implementation was not restarted. The working tree was clean at inspection. This report is the only new file. No application code, database data, schema, migration, or Git history was changed.

## Decisions required before implementation

### 1. Historical PIT income and withholding recognition

`D6A-REPORT.md:170-184` recommends excluding Approved payroll until actual payment, but identifies the payment/evidence contract as a prerequisite. `D6C-REPORT.md:11,70` and `src/SIAMIS.Application/Payroll/PitSsoRecognitionContract.cs` explicitly settle historical **employee SSO** as Paid-only. The contract returns SSO sources; it does not resolve PIT income or prior PIT withholding.

`D6C-REPORT.md:134` mentions future Paid-history aggregation, but does not independently approve PIT income and withholding recognition. Under D6D section 8, that wording cannot be treated as permission to infer PIT recognition from SSO. The completed D6C SSO decision remains settled.

**Smallest recommended decision:** explicitly approve Paid-only recognition separately for historical PIT income and historical PIT withheld, within the same Gregorian tax year and CurrentEmployer scope. Use business PayDate, immutable stored income classification snapshots and exact PIT results; exclude Draft/Calculated/Approved/Cancelled history. Apply the inclusive opening cutoff and exclude the current payroll identity. Reject unresolved same-day ownership rather than invent ordering. This recommendation is not implemented.

### 2. Annualization and withholding formula

`D6A-REPORT.md:188-196` labels its plan conditional, not an executable approved monetary contract. `src/SIAMIS.Domain/Entities/Payroll/StatutoryPolicies.cs:64-65` explicitly describes WithholdingMethodIdentifier as an audit identifier without assigned annualization/cumulative semantics. D6C added policy/claim structure and SSO recognition, not a withholding algorithm.

`PayrollPeriod` contains StartDate, EndDate and PayDate, but no reviewed annual payment schedule/count. `PitIncomeLine` contains income classification and provenance, but no regular/special/indeterminate-frequency classification. BasicSalary/Assignment/PayrollRule/Manual provenance cannot establish payment frequency. Included does not mean regular salary.

Official evidence rechecked during this inspection:

- [Revenue Department P.96/2543](https://www.rd.go.th/3558.html), clauses 1-3, distinguishes regular-payment annualization/allocation, special payments, indeterminate payment frequency and optional year-end/exit adjustment. Its allocation examples carry a residual into a final payment; they do not establish one universal cumulative formula.
- [Revenue Department P.16/2530](https://www.rd.go.th/3613.html) describes the applicable payment count for a midyear starter and final-payment residual handling.
- [Revenue Department ruling 0811/09662](https://www.rd.go.th/23727.html) distinguishes a year-start employee leaving midyear from a midyear starter; actual months worked alone do not determine the annualization count.

**Smallest recommended contract for review:** initially support an explicitly defined regular monthly-payment branch with reviewed current regular income R and applicable annual payment count N. Specify R × N, policy-owned expenses/allowances, approved actual-cumulative employee SSO, progressive annual tax, allocation and residual handling as one versioned contract. Define precisely when prior withholding reduces the current payment. Do not introduce a universal `(projected tax - prior withholding) / remaining months` formula. Reject unsupported special/indeterminate-frequency payments and unresolved partial-first-pay, rejoiner, leaver or reconciliation cases. Decide the supported cases and required schedule/payment-kind inputs before code. Do not infer N from D3 proration or assume N=12.

### 3. PIT rounding

`D6A-REPORT.md:207` explicitly stops monetary rounding pending a reviewed stage-specific contract. D6C does not supersede that boundary. D3 uses four-decimal AwayFromZero salary rounding; D5 has its own whole-baht SSO rule. Neither is PIT authority.

**Smallest recommended decision:** approve a PIT-TH-V1 precision/rounding table covering annualization, expense deduction, allowances, each bracket, annual tax, current withholding and final residual. Specify precision, rounding/truncation direction, tie behavior and the rounding point for each stage. Validate the allocation/residual rule against official examples. The official allocation examples do not settle every intermediate decimal/tie rule. No mode was selected or implemented.

### 4. Prior PIT result persistence

There is no EmployeePayrollPitResult entity/DbSet. The existing EmployeePayrollStatutoryResult has required SSO-specific enrollment/contribution-month structure and an employee/employer SocialSecurity result. It must not be reused as though it already stores PIT withholding. Opening PriorTaxWithheld exists, but does not supply subsequent SIAMIS PIT results. Generic/manual deduction lines are not a historical PIT ledger.

**Smallest recommended separation:** keep D6D's future engine pure, accepting typed, provenance-bearing historical inputs for deterministic tests. D6E should introduce the dedicated immutable PIT result persistence before enabling live history-dependent withholding. D6D preview must report unresolved history when required results do not exist. If full live cumulative history is required in D6D, approve that persistence dependency and checkpoint change first; no table or migration has been created.

## Required final-report coverage

| Item | Result |
|---|---|
| 1. Files | Created D6D-REPORT.md only; no source changes. |
| 2. Migration | None created/applied. Latest existing migration: 20261002061558_AddPitClaimsAndOpeningContract. |
| 3. Resolver architecture | Existing PIT income/treatment and SSO contracts inspected. New complete monetary-input resolver deferred pending decisions above. |
| 4. Calculator architecture | Shared pure typed-input engine remains the intended design; no monetary engine implemented around unresolved semantics. |
| 5. Governing date/year | Approved PayDate and its Gregorian year preserved; no new execution path. |
| 6. Policy | Existing Published TH-PIT, TH/THB, effective dates, year and PIT-TH-V1 contracts preserved. No runtime calculator compatibility claimed. |
| 7. Treatment/declaration | Exact selected Verified revision and StandardSection40_1 foundation preserved; no nationality inference added. |
| 8. Opening | Existing CurrentEmployer scope, inclusive cutoff, completeness and Known/Unknown states preserved. |
| 9. Income | Existing final-line Included/Excluded/Unknown resolver preserved. Historical PIT lifecycle remains a stop decision. |
| 10. SSO | Existing opening + prior Paid + current exact employee-side sources preserved; no employer amount, projection or independent PIT cap added. |
| 11. Expense | Policy parameters inspected; no monetary calculation implemented. |
| 12. Allowances | Typed selected-declaration claims and policy parameters preserved; no monetary calculation implemented. |
| 13. Brackets | Existing inclusive-lower/exclusive-upper publication structure preserved; no progressive calculator added. |
| 14. Annualization | Stopped; conditional prior recommendations are not an approved executable method. |
| 15. Prior withholding | Stopped; historical recognition and exact PIT result storage not defined sufficiently for implementation. |
| 16. Rounding | Stopped; no D3/D5 rounding reused. |
| 17. Zero/negative | Required no-negative-withholding/no-refund behavior acknowledged; no output produced. |
| 18. Result | No new monetary result/snapshot contract or persisted result added. |
| 19. Preview | No new endpoint; existing APIs unchanged. |
| 20. D6D vectors | Not created/executed because monetary implementation is stopped. Existing focused assertions listed below passed. |
| 21. Regressions | Existing pure D5A/D5B/D5C/D6A/D6B/D6C suite: 245 assertions passed. Full live D1-D6C/payroll lifecycle suites were not rerun in this stopped inspection; previous checkpoint results are not counted as new results. |
| 22. Payroll totals | Calculation/generation/preview services unchanged; no payroll records or monetary values written. |
| 23. Baseline | Read-only SQL counts and classifications verified below. |
| 24. Restoration | No fixtures created; no cleanup/restoration writes needed. Test employee remains inactive. |
| 25. Release build | Restore succeeded; Release build succeeded with 0 warnings and 0 errors. |
| 26. EF check | Development Release has-pending-model-changes: no changes since last migration. |
| 27. Diff check | git diff --check passed after report creation. |
| 28. D6E | After approving the D6D contract and proving its engine: dedicated PIT result/history persistence, final statutory deduction ownership, atomic payroll integration and historical traceability. No D6E work performed. |

## Read-only Development baseline

Target: localhost / SIAMIS, Windows integrated authentication. No secrets exposed. SQL confirms 51 application tables plus __EFMigrationsHistory; 29 existing migration entries.

| Table/check | Count/result |
|---|---:|
| Employees | 1 |
| EmploymentRecords | 1 |
| Only employee | TEST-EMP-001 / 433f2c1a-6222-494f-a64f-cd0c31126dc4 |
| Employee IsActive | false |
| PayrollComponents | 17 |
| PayrollComponents PIT classification | All 17 Unknown |
| PayrollComponents SSO classification | All 17 Unknown |
| All master-data rows | 172 |
| PayrollPeriods, PayrollRules, PayrollRuleTargets, PayrollSettings | 0 each |
| EmployeeCompensations, EmployeePayrollComponentAssignments | 0 each |
| EmployeePayrolls, EmployeePayrollLines | 0 each |
| EmployeePayrollStatutoryResults, EmployeePayrollSocialSecurityResults | 0 each |
| StatutorySchemes, StatutoryPolicyVersions | 0 each |
| SocialSecurityPolicyConfigurations, PitPolicyConfigurations, PitTaxBrackets | 0 each |
| EmployeeStatutoryEnrollments | 0 |
| EmployeeTaxProfiles, EmployeeTaxDeclarations, EmployeeTaxDeclarationSelections | 0 each |
| EmployeeTaxClaims, EmployeeTaxOpeningBalances | 0 each |
| Attendance, EmergencyContacts, EmployeeAddresses, EmployeeContacts | 0 each |
| EmployeeContracts, EmployeeDocuments, EmployeeHistory, EmployeeLeave | 0 each |
| EmployeePerformance, TeacherProfiles | 0 each |

Master-table counts: AddressTypes 3; AttendanceStatuses 11; ContractTypes 7; Countries 13; Departments 12; Designations 20; DocumentTypes 15; EmploymentStatuses 9; EmploymentTypes 6; Genders 4; HiringSources 10; LeaveTypes 10; Locations 5; MaritalStatuses 6; Nationalities 13; PayrollComponents 17; PayTypes 6; PerformanceRatings 5.

Only read-only SQL was used. No temporary policies, declarations, employees, payrolls or lines were created. No commit or push was performed. D6D remains incomplete until the stopped monetary contracts are approved.
