# D5B — Section 33 contribution contract / calculator foundation

## Outcome

The legally independent contribution-wage candidate foundation is implemented and verified. Monetary SSO work is **stopped** at the governing-date and partial-month minimum-base gates. Rounding evidence was found, but the exact SSO-TH-V1 monetary contract remains unapproved. No contribution base, employee/employer contribution, statutory result entity/table, payroll integration or migration was implemented.

## 1. Repository findings

D5A was committed before this checkpoint; the starting working tree was clean. Final calculation lines already carry immutable-value classification/provenance metadata. PayrollCalculationService produces final BasicSalary/Assignment/PayrollRule earnings after replacement suppression and rule ordering. Its calculated-line DTO and stored EmployeePayrollLineDto both expose SsoWageTreatmentSnapshot. The existing monetary services and all entity/configuration/migration files remain unchanged by D5B.

Key evidence:

- [Basic Salary snapshot source](<C:/Users/USER/Documents/ChatGPT/SIAMIS School Management System/src/SIAMIS.Infrastructure/Services/PayrollCalculationService.cs:24>)
- [Replacement and final earning processing](<C:/Users/USER/Documents/ChatGPT/SIAMIS School Management System/src/SIAMIS.Infrastructure/Services/PayrollCalculationService.cs:33>)
- [Stored Manual snapshot boundary](<C:/Users/USER/Documents/ChatGPT/SIAMIS School Management System/src/SIAMIS.Infrastructure/Services/EmployeePayrollService.cs:417>)
- [Regeneration replaces existing lines](<C:/Users/USER/Documents/ChatGPT/SIAMIS School Management System/src/SIAMIS.Infrastructure/Services/PayrollGenerationService.cs:126>)
- [Existing four-decimal rounding](<C:/Users/USER/Documents/ChatGPT/SIAMIS School Management System/src/SIAMIS.Infrastructure/Services/PayrollCalculationService.cs:226>)
- [Complete calendar month restriction](<C:/Users/USER/Documents/ChatGPT/SIAMIS School Management System/src/SIAMIS.Application/Payroll/BasicSalaryEntitlementContracts.cs:26>)
- [D4A caller-supplied governing date](<C:/Users/USER/Documents/ChatGPT/SIAMIS School Management System/src/SIAMIS.Infrastructure/Services/StatutoryPolicyService.cs:158>)
- [D4A method/publication contract](<C:/Users/USER/Documents/ChatGPT/SIAMIS School Management System/src/SIAMIS.Infrastructure/Services/StatutoryPolicyService.cs:211>)
- [D4B caller-supplied enrollment date](<C:/Users/USER/Documents/ChatGPT/SIAMIS School Management System/src/SIAMIS.Infrastructure/Services/EmployeeStatutoryService.cs:68>)

## 2. Contribution-wage contract

Section33ContributionWageResolver is a standalone pure service behind an Application interface. It consumes the complete final line set and explicit applicability, supplied by a future authorized caller. It is deliberately not called from Preview, Generation, manual reconciliation, or an HTTP endpoint. It does not resolve enrollment, policy or dates.

For Applicable, sum only final Earning lines whose stored snapshot is Included. Excluded earnings contribute nothing. Deduction lines never contribute and their classification cannot block this wage resolution. The resolver consumes each supplied final line once; it does not separately enumerate configurations or group/deduplicate by component ID. Distinct final assignment/rule/manual lines may share a component and must remain distinct amounts. The caller must supply one complete final line set, without duplicated rows or a second copy of the same earnings.

Result statuses: Resolved, NotApplicable, Unresolved, InvalidInput. Only Resolved has ContributionWageCandidate; this may explicitly be zero when every earning is Excluded. No minimum floor, maximum cap, currency conversion, statutory rounding, rate or financial contribution is evaluated. Candidate arithmetic uses the supplied decimal line amounts without further rounding. Invalid earning type/treatment, negative earning amount and decimal overflow fail safely. It does not claim that a candidate is the legally approved contribution base.

## 3. BasicSalary classification source

The engine selects the single existing configurable PayrollComponent named Basic Salary, then copies that component's SsoWageTreatment into the generated BasicSalary line. No new configuration source is needed. Unknown remains Unknown. All actual Development components, including Basic Salary, remain Unknown. Synthetic in-memory tests exercise explicit Included/Excluded without assigning legal treatment to any existing component.

## 4. Assignment / PayrollRule / Manual treatment

Assignments copy component metadata. The evaluator carries the rule's component metadata without changing targeting/applicability; the calculator copies it into the rule line. Both use final snapshots, not a later live join.

Manual creation copies the selected component's treatment into its historical line; same-component amount updates preserve that snapshot. Explicit manual component replacement captures the new component's treatment. SourceType=Manual and SourceId=null remain truthful. The new FromStoredLine adapter uses that stored treatment/amount rather than looking up current component values. A Manual earning with Unknown is unresolved for Applicable, just like any other earning.

## 5. Final earning-line semantics

The candidate is derived after existing entitlement, assignment processing, Earning rules, Supplement, ReplaceAssignment and rule ordering. The tests pass actual final outputs from the existing calculator: Supplement counts distinct assignment and rule earnings once each; ReplaceAssignment excludes the suppressed assignment. No original assignment is added back from configuration. Manual earnings can be consumed from a stored final payroll; current Preview/Generation do not merge existing stored manual adjustments into their newly calculated line set. A future caller must choose the correct complete final set for that workflow.

## 6. Unknown handling

Applicable plus any Unknown earning returns Unresolved and a null candidate, never a partial sum or zero. Issues identify line index, component ID/code/name, source type and source ID. Invalid snapshot values are rejected rather than treated as Excluded.

NotApplicable returns NotApplicable/null without requiring irrelevant classifications and without calculating a deduction. Unknown enrollment returns Unresolved/null. Nationality is not an input. Ordinary payroll remains unchanged because this foundation has not been integrated; it does not yet block existing payroll API generation on statutory Unknown.

## 7. Governing-date findings — STOP

PayrollPeriod has StartDate, EndDate and PayDate. D3 restricts calculation to one complete calendar month. D4A policy and D4B enrollment intervals are inclusive and resolve a caller-provided date. Neither defines a statutory monthly selector. D4A allows adjacent non-overlapping versions to transition mid-month; D4B can also change applicability mid-month.

For example, synthetic Policy A ending October 14 and Policy B beginning October 15 make StartDate and EndDate select different policies. Applicable enrollment ending mid-month and NotApplicable following it similarly changes applicability. A PayDate in a later month adds another ambiguity. Monthly period labels alone do not settle these cases.

Smallest proposal for approval: identify the contribution month from the complete calendar-month payroll period, require a single Published policy covering that entire month, and reject mid-month/mixed policy or enrollment coverage until a specific approved rule exists. Do not use PayDate as a default. A monthly effective enrollment selector (including mid-month joiners/leavers) still needs approval; no selector was implemented.

## 8. Partial-month findings — STOP

D3 ThirtyDay proration determines the earning amount; it does not establish an SSO minimum-base rule. An Included prorated earning can safely enter the candidate, but a wage below the configured minimum could be floored, treated as actual wages under an approved exception, or handled under another explicit rule. These produce different contributions. The retrieved standard form does not separately establish treatment for joiners/leavers, employment gaps, rehires or zero-wage cases.

Approve the statutory minimum-base behavior for those scenarios before D5C. Until then, the candidate is not clamped or converted into ContributionBase. Do not scale minimum, rates or caps using D3 payable days.

## 9. Rounding findings

The official SSO-hosted [SP.S.1-10 instructions, page 2](https://catalog.sso.go.th/dataset/d66067d6-cce6-42e6-b479-8a4e958338eb/resource/1841f115-b4c2-4aeb-a120-966caf5461f1/download/.pdf) label the filing by wage month and distinguish actual paid wages. They instruct per-person contribution rounding to whole baht: fractions at least 0.50 round upward; smaller fractions are discarded. They also describe the employer remittance matching the rounded insured-person contribution. This form contains the older 15,000 ceiling, so it is not adopted as current numeric policy or proof of every modern exception. A [second official SSO copy](https://catalog.sso.go.th/dataset/cc980e7a-5b8c-43dc-90e4-5a2d95a14fc1/resource/43508f84-8dac-4a12-9046-acdedee508a6/download/.pdf?preview=1) carries the same instructions.

Illustrative competing rounding outcomes (raw contributions, not implemented formulas):

| Raw amount | Whole-baht half-up | Whole-baht to-even | Truncate | Existing payroll four decimals |
|---|---:|---:|---:|---:|
| 82.49 | 82 | 82 | 82 | 82.4900 |
| 82.50 | 83 | 82 | 82 | 82.5000 |

Recommendation, not implemented: adopt an explicitly approved per-insured-person whole-baht half-up contract for V1, retain raw and remitted amounts plus rounding identity in the future snapshot, and prohibit implicit intermediate rounding. Confirm current applicability and employee/employer calculation ordering, including whether unequal configured rates are in V1 scope, before monetary implementation. The source's equal-remittance wording is not permission to alias employer cost to employee deductions for every policy. No rounding decision is encoded in D5B.

## 10. D4A policy compatibility

D4A's canonical configuration identifier is SSO-TH-V1. Publication requires OfficialReference and complete rates, minimum/maximum base and insured-person classification; Published rows are immutable through the service. The wage resolver accepts no policy/method identifier and does not certify runtime support for a monetary calculator. D5C must explicitly accept only SSO-TH-V1 and fail unknown versions before monetary calculation.

D4A does not currently store separate employee/employer amount caps, statutory rounding mode, minimum-base exception strategy or geographic applicability. Do not reinterpret existing columns. If V1 requires independent caps beyond policy wage bounds/rates, return for approval of the missing policy contract. No new field or legal values were introduced/seeded.

## 11. D4B enrollment compatibility

For a supplied date, the existing resolver returns exactly one effective record, Unknown when absent, and conflict when coverage is ambiguous. The pure candidate resolver accepts the resulting explicit applicability without inference. Compatibility with a contribution month cannot be certified until the governing selector and mid-month handling are approved. Enrollment history was not rewritten.

## 12. Proposed statutory result design for D5C

Recommendation only, no entities/DDL: a small EmployeePayrollStatutoryResult parent with GUID result ID, EmployeePayrollId, scheme/policy/enrollment historical identifiers, method version, approved contribution-period/date metadata, currency, CreatedAt and versioned CalculationSnapshotJson. Use NoAction historical relationships where safe and retain snapshot values independently of live joins.

An SSO-specific one-to-one child should hold the explicit monetary fields: ContributionWage, ContributionBase, MinimumBase, MaximumBase, EmployeeRate, EmployerRate, EmployeeAmount, EmployerAmount. This keeps monetary meaning typed without adding nullable SSO fields to a generic future PIT record. The snapshot should retain earning input identities/amounts/treatments, raw contributions, approved rounding identity, policy/enrollment context and any approved base-treatment explanation.

Uniqueness depends on approved monthly segmentation: one payroll/scheme result is reasonable only if one complete monthly context is guaranteed. If mixed-period policy/enrollment calculation is required, decide the segment/result contract before migration. EmployeeAmount will affect deductions/net only; EmployerAmount remains separate employer cost. No result migration is created before these decisions.

## 13. Proposed payroll-line provenance for D5C

Add SourceType=Statutory deliberately, with non-null SourceId holding the statutory result's preallocated GUID as historical snapshot data, not a new SourceId FK. The result belongs to the payroll; the generated employee deduction line carries that result ID. Preallocation avoids a circular FK/persistence dependency. Extend the existing source-consistency check accordingly. Employer cost remains in the typed statutory result, never an employee deduction line.

Persist the header, generated lines and result atomically using existing lock order. Forced regeneration must replace old calculated snapshots only after full calculation succeeds; Approved/Paid/Cancelled protections remain. No such persistence/source changes were made in D5B.

## 14. Temporary / geographic limitation

D4A has no geographic statutory targeting and rejects overlapping Published versions per scheme. D5B does not implement disaster relief or infer applicability from location/nationality. Nationwide versus scoped exception resolution requires a future explicit configuration/context contract. No Pathum Thani constant was added.

## 15. Code/schema changes

Created Application input/result/issue/interface contracts; created the pure Infrastructure resolver; created focused test cases and invoked them from the existing regression runner. Added this report and a README entry. No DI/HTTP route integration, EF entity/configuration/model change, migration, database schema modification or existing payroll service behavior change.

## 16. Verification performed

23 new focused D5B assertions cover Included/Excluded/Unknown, issue provenance, NotApplicable/Unknown enrollment, deduction exclusion, exact decimal sum, invalid/negative/overflow safety, final Supplement/ReplaceAssignment sets, configurable BasicSalary, snapshot preservation/current recalculation, stored Manual input, nationality independence and absence of contribution/base fields. Combined runner: 77 assertions (54 D5A + 23 D5B), all passed.

## 17. Regression results

The existing Development API/SQL suite was rerun unchanged: all 70 assertions passed. It covers D5A SQL constraints/persistence and historical regeneration, inactive employee period eligibility (D2), D3 full-month and mid-month salary entitlement, D4A publication/resolution/immutability, D4B enrollment/declaration guards, generic rule Supplement/ReplaceAssignment and percentage formulas, failed new generation/failed regeneration preservation, manual reconciliation, generated provenance and payroll/period lifecycle protection. Applicable enrollment plus Published synthetic policy still produces no SSO monetary change.

D1 lifecycle production code is unchanged and its employee/employment baseline was preserved exactly; this is not a claim that the entire historical D1 test matrix was rerun. No PIT/provident fund or employee/employer contribution was introduced. Synthetic test policy values were temporary non-legal fixtures and were removed.

## 18. Unresolved decisions requiring approval

1. Contribution-month governing policy/enrollment selector, including mid-month transitions; recommended conservative full-month uniform coverage guard above.
2. Minimum-base behavior for joiners/leavers, gaps, rehires and zero Included wages; do not infer from D3.
3. Approve the V1 remittance rounding/ordering contract and its current scope using official evidence; do not reuse four-decimal payroll rounding.
4. Confirm independent monetary caps are unnecessary or approve explicit D4A fields if required; method compatibility remains an explicit D5C gate.
5. Future manual earning mutation consistency: manual APIs currently reconcile totals while regeneration removes/rebuilds all lines, including Manual. Decide whether statutory recalculation is atomic on manual earning mutation, manual changes are blocked after statutory calculation, or another explicit invalidation workflow is required. A stale statutory deduction must not silently survive a wage change. This decision is deferred, not a D5B workaround.

The broader payroll line contracts have no currency field. D5C must validate the approved THB payroll context before using this arithmetic candidate for statutory money; D5B does not infer currency from component/classification.

## 19. Final Development baseline

The live suite restored exact pre-fixture row contents of all 49 application tables, not just counts. All 17 components remain Unknown; Included=0 / Excluded=0. TEST-EMP-001 remains inactive; Employees=1 and EmploymentRecords=1. No temporary data remains. There are 26 migrations; latest remains 20261001032904_AddSsoWageTreatmentClassification. Only the dedicated temporary verification API process was stopped.

| Table | Final rows |
|---|---:|
| AddressTypes | 3 |
| Attendance | 0 |
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
| EmployeePayrollComponentAssignments | 0 |
| EmployeePayrollLines | 0 |
| EmployeePayrolls | 0 |
| EmployeePerformance | 0 |
| EmployeeStatutoryEnrollments | 0 |
| EmployeeTaxClaims | 0 |
| EmployeeTaxDeclarationSelections | 0 |
| EmployeeTaxDeclarations | 0 |
| EmployeeTaxOpeningBalances | 0 |
| EmployeeTaxProfiles | 0 |
| Employees | 1 |
| EmploymentRecords | 1 |
| EmploymentStatuses | 9 |
| EmploymentTypes | 6 |
| Genders | 4 |
| HiringSources | 10 |
| LeaveTypes | 10 |
| Locations | 5 |
| MaritalStatuses | 6 |
| Nationalities | 13 |
| PayTypes | 6 |
| PayrollComponents | 17 |
| PayrollPeriods | 0 |
| PayrollRuleTargets | 0 |
| PayrollRules | 0 |
| PayrollSettings | 0 |
| PerformanceRatings | 5 |
| PitPolicyConfigurations | 0 |
| PitTaxBrackets | 0 |
| SocialSecurityPolicyConfigurations | 0 |
| StatutoryPolicyVersions | 0 |
| StatutorySchemes | 0 |
| TeacherProfiles | 0 |

## 20. Build / EF / diff status

- Restore succeeded.
- Release build succeeded with 0 warnings and 0 errors.
- Combined focused regressions: 77 passed.
- Live API/SQL regressions: 70 passed; exact baseline restored.
- EF reports no changes since the last migration.
- git diff --check passed.
- No commit or push.

Focused test command:

```powershell
.\.dotnet\dotnet.exe run --project tests/SIAMIS.Payroll.RegressionTests -c Release
```

## Complete D5B changed-file list

- [src/SIAMIS.Application/Payroll/Section33ContributionWageContracts.cs](<C:/Users/USER/Documents/ChatGPT/SIAMIS School Management System/src/SIAMIS.Application/Payroll/Section33ContributionWageContracts.cs>)
- [src/SIAMIS.Infrastructure/Services/Section33ContributionWageResolver.cs](<C:/Users/USER/Documents/ChatGPT/SIAMIS School Management System/src/SIAMIS.Infrastructure/Services/Section33ContributionWageResolver.cs>)
- [tests/SIAMIS.Payroll.RegressionTests/Section33WageRegressionTests.cs](<C:/Users/USER/Documents/ChatGPT/SIAMIS School Management System/tests/SIAMIS.Payroll.RegressionTests/Section33WageRegressionTests.cs>)
- [tests/SIAMIS.Payroll.RegressionTests/Program.cs](<C:/Users/USER/Documents/ChatGPT/SIAMIS School Management System/tests/SIAMIS.Payroll.RegressionTests/Program.cs>)
- [D5B-REPORT.md](<C:/Users/USER/Documents/ChatGPT/SIAMIS School Management System/D5B-REPORT.md>)
- [README.md](<C:/Users/USER/Documents/ChatGPT/SIAMIS School Management System/README.md>)
