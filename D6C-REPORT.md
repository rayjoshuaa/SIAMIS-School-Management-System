# D6C — PIT policy and claims contract complete

Final result, 2026-10-02: **D6C complete**, including the approved final actual-cumulative employee SSO recognition contract. PIT-TH-V1 publication accepts a structurally complete policy. Foundation migration was applied previously to **localhost/SIAMIS Development only**; this final decision required no additional migration. No PIT calculator, annualization, YTD calculation, tax line or monetary change was introduced. The initial review below is retained as an explicitly superseded historical appendix.

## Current 1. Official-source findings and approved interpretation

The approved employee attestation contract resolves the prior spouse/child/parent modeling stops. Current code stores typed facts; policy owns amounts. Sources and eligibility evidence remain documented in the historical appendix.

Additional official research found [Revenue Department ruling 0811/08327, 13 August 1999](https://www.rd.go.th/23655.html). It specifically discusses social-security deductions for monthly withholding and states that actual paid contributions accumulate each month. This supports **actual employee-side cumulative SSO**, rather than an assumed full-year projection. [P.96/2543 clause 1(2)](https://www.rd.go.th/3558.html) separately addresses employee-declared allowances and evidence. The [2026 Revenue Department withholding training](https://interweb1.rd.go.th/publish/seminar/training/RD19.pdf) was also inspected; searchable text did not supply an SSO-specific timing contract.

The final user decision defines Paid as SIAMIS V1's historical recognition event. Prior Draft/Calculated/Approved/Cancelled payrolls are excluded. Current transaction D5 EmployeeAmount is a separate source that need not already be Paid. Use opening employee SSO plus eligible prior Paid sources strictly after cutoff plus current employee SSO; no projection or independent PIT cap. Paid is an internal application convention and is **not proof of employer SSO remittance**. No remittance tracking was built. Legal guidance and approved software convention remain distinct.

## Current 2. Complete changed-file list

Modified:

- `src/SIAMIS.Api/Controllers/EmployeeTaxController.cs`
- `src/SIAMIS.Api/Controllers/StatutoryPolicyVersionsController.cs`
- `src/SIAMIS.Application/Payroll/EmployeeStatutoryContracts.cs`
- `src/SIAMIS.Application/Payroll/StatutoryPolicyContracts.cs`
- `src/SIAMIS.Domain/Entities/Payroll/EmployeeStatutoryProfiles.cs`
- `src/SIAMIS.Domain/Entities/Payroll/StatutoryPolicies.cs`
- `src/SIAMIS.Infrastructure/Configurations/EmployeeStatutoryConfigurations.cs`
- `src/SIAMIS.Infrastructure/Configurations/StatutoryPolicyConfigurations.cs`
- `src/SIAMIS.Infrastructure/Migrations/SIAMISDbContextModelSnapshot.cs`
- `src/SIAMIS.Infrastructure/Services/EmployeeStatutoryService.cs`
- `src/SIAMIS.Infrastructure/Services/StatutoryPolicyService.cs`
- `tests/SIAMIS.Payroll.RegressionTests/Program.cs`

Created in this D6C checkpoint:

- `src/SIAMIS.Infrastructure/Migrations/20261002061558_AddPitClaimsAndOpeningContract.cs`
- `src/SIAMIS.Infrastructure/Migrations/20261002061558_AddPitClaimsAndOpeningContract.Designer.cs`
- `src/SIAMIS.Application/Payroll/PitSsoRecognitionContract.cs`
- `tests/SIAMIS.Payroll.RegressionTests/D6CContractRegressionTests.cs`
- `tests/SIAMIS.Payroll.RegressionTests/PitSsoRecognitionContractTests.cs`
- `tests/verify_d6c_live.py`
- `D6C-REPORT.md` (initial stop report now updated with implementation/results).

## Current 3. Migration

`20261002061558_AddPitClaimsAndOpeningContract` generated, inspected and applied successfully in the preceding approved foundation step using an explicit localhost/SIAMIS integrated-auth connection. Exactly 12 columns and four structural CHECK constraints were added to four existing tables. No new tables, foreign keys, legal seeds, deletes, history updates or automatic publication. EF history has 29 migrations. Actual SQL columns and enabled/trusted checks were verified. Existing rows receive null metadata/false completeness, never fabricated facts. **No new migration was created/applied for the final SSO decision**; existing method-version metadata identifies the approved recognition contract.

## Current 4. Typed PIT policy contract

Added nullable Draft fields: SpouseAllowanceAmount, ChildAllowanceAmount, AdditionalChildAllowanceAmount, ParentAllowanceAmount, AdoptedChildCombinedCountLimit and MaximumEligibleParentCount. Existing expense/personal/year/method/bracket fields remain. Amounts use decimal(19,4); limits are positive integers if supplied. Responses explicitly expose RateUnit=PercentagePoints, consistent with existing statutory configuration. PIT-TH-V1 method metadata now identifies ActualCumulative recognition, historical Paid-only input, current transaction input, no projection and no independent PIT cap. Metadata is response-only; callers cannot select a different recognition rule while claiming PIT-TH-V1.

## Current 5. Expense contract

Rate uses percentage points, structurally 0–100; supplied expense cap must be positive. Published V1 requires both. No current legal rate/cap is seeded or embedded in services.

## Current 6. Personal allowance

Existing required policy amount retained. No Personal claim introduced, no entitlement inferred or calculated.

## Current 7. Spouse claim

One reviewed presence claim per revision, serialized under the existing employee lock. Omitted Quantity becomes one; other quantities rejected. Evidence Reference required. Legal Amount rejected, including zero. No marital-status inference or spouse-income aggregation.

## Current 8. Child claim

Aggregate positive eligible Quantity is partitioned by explicit ChildRelationshipType (Lawful/Adopted) and required AdditionalChildAllowanceEligible boolean. Adopted + true is rejected. Declaration TotalLivingLawfulChildren includes noneligible living lawful children and is required when adopted claims exist. Verification checks consistent lawful counts. Typed facts are attested, never derived from birthdays/order/HR fields. Additional allowance is separate from the ordinary allowance in policy. Future policy compatibility must validate adopted capacity against AdoptedChildCombinedCountLimit; no legal count constant or allowance calculation is implemented here.

## Current 9. Parent claim

Explicit positive eligible Quantity and reviewed Reference required. Policy owns ParentAllowanceAmount and MaximumEligibleParentCount. Eligibility/exclusive entitlement is the approved employee attestation/review responsibility. Future policy compatibility must check aggregate count against the exact policy limit. No cross-employer spouse reconciliation or family registry.

## Current 10. SSO PIT recognition contract

`PitSsoRecognitionContract` is a pure source/metadata contract, not wired into payroll calculation. It returns individual unaggregated input records; it does not add amounts, project contributions or calculate a deduction. Historical source requires Paid, same tax year, strictly after opening cutoff and before current PayDate. Future-date and cutoff-covered sources are excluded; same-day historical/current ambiguity fails clearly. Current source is distinct and rejects overlap with opening. Opening source requires the exact selected Verified revision, known state, reviewed CurrentEmployer meaning and completeness. Unknown/Draft/stale inputs fail. Source records preserve exact D5 result/payroll IDs, EmployeeAmount and business date. EmployerAmount is not read or exposed. THB, supported D5 method and source ownership/identity are validated. Future D6D still owns complete employee/period/history selection, duplicate-source checks and aggregation safeguards.

## Current 11. Opening compatibility

Added OpeningBalanceScope, CompletenessAttested and server-owned InputContractVersion. Known states require explicit CurrentEmployer and completeness; prior-employer scopes return HTTP 400 with review explanation. Stored version PIT-TH-V1 marks the approved same-year pre-expense Section 40(1), employee-side recognized SSO and PIT withholding meaning. Legacy null version is not silently reinterpreted. Unknown stays null/unresolved; ConfirmedZero explicit; VerifiedAmount requires all values. Inclusive cutoff and prior December 31 zero rules retained. Strictly-after-cutoff aggregation and same-day ambiguity rejection are documented future requirements; no history aggregation exists in D6C.

## Current 12. Brackets

Existing ordering/contiguity/overlap/final-unbounded validation retained. Rate units explicit; supplied rates over 100 rejected. First zero-rate band remains sufficient; no duplicate exemption field or legal bracket seeds.

## Current 13. Dates/year

V1 publication now requires a bounded inclusive interval within the existing configured TaxYear. TH/THB unchanged. Future resolution must independently require both effectiveness on PayDate and policy TaxYear == Gregorian PayDate year; the generic D4A date resolver was not repurposed as a PIT calculator resolver. No redundant year column.

## Current 14. Publication

V1 completeness adds policy-owned allowances and limits, numeric representation validation and year compatibility. The obsolete SSO stop was removed after approval; a structurally complete synthetic PIT-TH-V1 policy published successfully during verification. Incomplete policy publication still returns HTTP 400. No legal numeric policy was seeded or published and no policy was auto-created. All test policies were removed during exact cleanup. SSO publication/immutability/serialization remain unchanged; unknown future method IDs are not interpreted as V1. Future monetary calculator compatibility must be validated independently.

## Current 15. Immutability / compatibility

Published policies and Verified declaration inputs remain immutable. Replacement revision begins without claims/opening inheritance; selection switches at verification. Generic legacy Amount column/response remains for historical compatibility but new/updated claim requests with non-null Amount return HTTP 400. Legacy incomplete claims cannot newly verify as V1-ready input. Existing Verified rows are not rewritten or retrospectively certified calculator-compatible. Absence of supported claims on a fully reviewed Verified declaration means none requested; Draft never provides automatic PIT input.

## Current 16. API / Swagger

Extended existing Draft PIT configuration and employee declaration/claim/opening endpoints. No parallel API. Typed fields are visible in Swagger; InputContractVersion and method-owned PitSsoRecognition metadata are response-only. Existing unknown-member rejection retained. Controller descriptions explain attested facts, rejected legal Amount, opening meaning and final approved SSO recognition. No monetary route added.

## Current 17. Constraints

Added CK_PitPolicyConfigurations_ClaimValues, CK_EmployeeTaxDeclarations_LivingChildren, CK_EmployeeTaxClaims_ChildMetadata and CK_EmployeeTaxOpeningBalances_Scope. Legal amounts/counts are policy data, not CHECK constants. Nullable legacy metadata is deliberately permitted; application verification enforces current contract completeness. No existing historical row was fabricated to satisfy new requirements.

## Current 18. Future historical result

Separate EmployeePayrollPitResult remains deferred to D6D/D6E. Snapshot requirements in the historical appendix remain applicable, now including typed child categories/eligibility/counts, policy quantity limits, living-lawful count, opening scope/completeness/meaning/cutoff, exact D5 recognized result IDs/EmployeeAmount and recognition evidence. Preserve exact revision/treatment and denormalized policy/input explanation. DEDUCT-002 is documented as approved future presentation identity only; this checkpoint adds no reservation/collision processing, PIT line or result table. Future collision validation follows the approved D5 fail-clearly pattern.

## Current 19. Verification totals

Final verification: **245 pure assertions** (195 existing + 28 D6C policy/claim checks + 22 SSO recognition checks) and **48 D6C live assertions** passed. The live suite now includes successful synthetic PIT publication, rejected incomplete publication and Published parameter/bracket/reference/deletion protection. Existing live suites are rerun against this final implementation: D5A 70, D5C 306, D6B 76, for **745 total assertions**. Synthetic fixture values are not Thai legal policy values. All live suites compare exact application-table contents/timestamps after cleanup. No future monetary or remittance/YTD calculation is represented as implemented/tested.

## Current 20. D1–D6B regression results

Existing live suites verify salary/full-month/joiner/leaver/gap entitlement, enrollment/effective policy behavior, SSO, Earning/Deduction Supplement and ReplaceAssignment, conflicting replacement failures, failed-new rollback, failed-regeneration complete preservation, provenance/snapshots, manual reconciliation, Approved/Paid/Cancelled payroll guards, Closed/Cancelled period guards, tax classification/treatment, revision selection and privacy. All passed. Standalone historical D1/D2 acceptance scripts are not newly claimed as executed. PayrollCalculationService, PayrollGenerationService and PayrollPreviewService were not changed.

## Current 21. Final Development baseline

51 application tables and 172 master rows retained. Employees=1, EmploymentRecords=1, PayrollComponents=17. TEST-EMP-001 remains inactive. All 17 components retain PIT Unknown and SSO Unknown. PayrollRules/Targets/Settings/Periods, compensation/assignment/header/lines/SSO results, statutory schemes/policies/PIT configuration/brackets, tax profiles/declarations/selections/claims/opening and other operational tables remain zero. The full table-count matrix in the appendix remains accurate except __EFMigrationsHistory is now **29**, not 28. Final read-only SQL confirmed these counts and all 12 new columns.

## Current 22. Restoration

All temporary fixtures removed. Four live suites compare exact baseline application contents/timestamps; no employee core change remains. The final temporary API process is stopped after verification. Migration/history/schema intentionally remain applied. Synthetic Published policy cleanup uses SQL only for tracked test IDs, without enabling a production hard-delete API.

## Current 23. Restore / Release

Restore succeeded. Final Release build succeeded: zero warnings, zero errors. An intermediate nullable-reference warning was fixed before live verification. No remaining build error.

## Current 24. EF check

Final Release Development check passed: no pending model changes after the focused applied migration.

## Current 25. Diff / Git

git diff --check passed. No commit or push. Changed-file list is Current 2; ignored runtime logs/results are verification artifacts.

## Current 26. D6C completion and remaining D6D work

D6C is complete within the approved policy/claims/recognition foundation. No recognition decision remains pending. D6D owns the independently supported PIT calculator, income annualization, expense/allowance/bracket monetary calculations, Paid-history aggregation/current-period deduplication, immutable PIT result persistence and DEDUCT-002 presentation/collision enforcement. Future snapshot must distinguish OpeningBalance, HistoricalPaidPayroll and CurrentPayroll sources with exact D5 IDs/EmployeeAmount. Paid-history corrections need a later controlled workflow; no historical recalculation/remittance tracking was added. No payroll financial behavior changed, no commit and no push.

---

# Historical appendix — initial D6C stop review (superseded by current results above)

Reviewed 2026-10-02. **Stopped before implementation under sections 7, 12 and 24 of the request.** The current generic Child claim cannot represent distinct legal categories. Spouse/Parent eligibility and opening-balance meanings also require explicit contract decisions. No application, model, migration or database changes were made. Recommendations below are proposals for review, not implemented contracts or calculator behavior.

## 1. Official-source findings

Sources were opened and inspected during this checkpoint:

- [Revenue Code sections 38–64](https://www.rd.go.th/5937.html): section 39 defines a calendar tax year; section 42 bis provides a combined 40(1)/(2) expense deduction of 50%, capped at THB 100,000. Section 47 provides personal allowance and insured-person social-security contributions actually paid. These are policy facts, not constants added to code. V1 remains 40(1) only; mixed 40(2) inputs cannot silently consume a separate expense cap.
- [Official 2025 annual-return instructions, pp. 7–8](https://www.rd.go.th/fileadmin/tax_pdf/pit/2568/Ins90_241268.pdf): spouse allowance depends on the spouse having no income. Lawful children receive a basic allowance; a second or subsequent lawful child born in/after 2018 receives an additional allowance. Birth order includes deceased children. Adopted children have separate limits and interaction with living lawful children, including children ineligible for deduction. Child dependency/education/income and nonresident location conditions matter. These annual instructions establish claim eligibility, not a monthly withholding algorithm. The 2025 document does not by itself certify all 2026 numerical policy values.
- [Revenue Department parent guidance](https://www.rd.go.th/60056.html), corroborated by the annual instructions: parent eligibility includes age, support, income, lawful relationship, and one claimant supported by L.Y.03 evidence. Nonresident location and spouse-parent conditions require attention. The older FAQ is supporting interpretation, not a standalone current numeric policy source.
- [P.96/2543, clause 1(2)](https://www.rd.go.th/3558.html): withholding allowances use the employee's declaration and supporting evidence; changed declarations affect subsequent calculations. Its old numerical examples must not populate current policy. Projection, withholding order and rounding remain future method behavior.
- [Official SSO deduction guidance](https://www.rd.go.th/60061.html): actual insured-person payments qualify subject to social-security law; its historical numeric examples cannot establish a permanent current PIT cap. Neither that guidance nor the inspected withholding instruction resolves the proposed SIAMIS annual-projection/remittance contract.

Interpretation boundary: legal values belong in Published policy; eligibility facts belong in the verified employee revision; calculation order/projection belongs in the future supported method. No legal values were seeded, and no PIT money was calculated.

## 2. Files created/modified

Created only this report: `D6C-REPORT.md`. Initial working tree was clean. No source files, contracts, tests, configurations or migrations were modified.

## 3. Migration

None created or applied. Latest applied migration remains `20261002042159_AddPitIncomeClassificationAndTaxTreatment`; EF history contains 28 entries. Typed claim/policy extensions will require a focused migration after the stopped contracts are approved.

## 4. PIT-TH-V1 typed policy contract

Repository: `src/SIAMIS.Domain/Entities/Payroll/StatutoryPolicies.cs:50`. Existing configuration contains TaxYear, EmploymentExpenseDeductionRate/Cap, PersonalAllowanceAmount, WithholdingMethodIdentifier and Brackets. It lacks spouse, child-category, parent and SSO recognition configuration.

Recommendation: extend this existing typed configuration; retain decimal(19,4). Add spouse and parent per-person allowance amounts, lawful/adopted child basic amounts and lawful-child additional-tier configuration. Store any legally relevant numeric category thresholds/limits in versioned policy. Settle SSO recognition/projection before adding cap fields. Do not publish this incomplete contract as a calculator-ready PIT-TH-V1 policy. Dispatch publication completeness by method version; future calculators must independently reject unsupported versions.

## 5. Expense deduction contract

Existing rate/cap fields are suitable for the narrow scope. Proposed structural validation: explicit percentage-point units, rate between 0 and 100, required positive cap, supported precision. Keep legal numeric values in policy. This strengthening was not implemented while the complete V1 contract is stopped.

## 6. Personal allowance contract

Retain the existing required policy amount; no Personal claim type. Future application depends on the supported method and verified treatment. Do not infer eligibility from employee HR data.

## 7. Spouse claim contract — decision required

Repository: `EmployeeStatutoryProfiles.cs:64`, `EmployeeStatutoryContracts.cs:44`, `EmployeeStatutoryService.cs:149`. A Spouse claim currently has optional Amount/Quantity, Reference and Remarks. Verification checks structure, not spouse income or eligibility attestation. A Verified generic claim therefore does not presently promise legal eligibility.

Affected calculation: whether to apply the spouse allowance. Smallest proposal: one spouse claim per revision, no client-authored allowance amount; explicit employee attestation of applicable marriage/no-income conditions and Thailand-location fact when relevant to residency, with evidence reference. Existing declaration verification supplies the immutable verification event. Quantity is one/presence, not arbitrary multiplication. Approve the exact attestation wording and required facts before implementation.

## 8. Child claim contract — mandatory stop

The source in section 1 proves a generic Child plus one amount is insufficient. Existing ClaimType/Quantity cannot distinguish lawful/adopted children or the additional lawful-child tier. Reference/Remarks must not become hidden calculation inputs.

Affected calculation: basic child allowance, additional child allowance and adopted-child capacity. Smallest recommended extension: one typed child claim per person, explicit lawful/adopted category, birth date and declared lawful-child birth order, year-specific eligibility attestation and identity/evidence reference. Capture the declared total living lawful-child count, including nonqualifying children, once per revision to validate adopted capacity without building a general family registry. Include the location fact needed for nonresident treatment. Policy owns basic/additional amounts and the numeric tier/limit parameters; the method owns their approved interaction. Prevent duplicate claimed persons within a revision. Quantity for a person is one, derived by the server.

An aggregate category-count alternative would need a separately approved verification/deduplication contract. It must not silently approximate the legally relevant facts. No historical claim was reclassified or fabricated.

## 9. Parent claim contract — decision required

Existing generic Parent claims lack typed own-parent/spouse-parent distinction, individual identity, relevant eligibility attestation and exclusive-claim evidence. Arbitrary Quantity is not a safe allowance multiplier.

Affected calculation: eligible parent count and spouse-parent entitlement. Smallest proposal: one typed person claim, relationship role, identity/evidence reference and explicit attestations covering applicable age/support/income/lawful-relationship/exclusive-claim conditions; location where residency requires it. Reuse declaration verification. Policy owns the per-person amount and any approved numeric eligibility parameters. Agree whether attestations suffice or exact age/income facts must be stored; do not infer them from emergency contacts or HR relationships.

## 10. SSO PIT deduction contract — decision required

Repository: `src/SIAMIS.Domain/Entities/Payroll/EmployeePayrollStatutoryResult.cs:20` stores distinct EmployeeAmount and EmployerAmount. Future PIT input must use historical employee-side results; never EmployerAmount or recomputed live SSO.

Affected calculation: annual deductible SSO and withholding timing. Actual contribution results are not proof of payment/remittance. Approve how eligible actual payments are recognized, how future monthly withholding represents annual SSO, and whether a separate PIT cap exists for the supported policy/year. Do not copy an old FAQ annual maximum or derive a PIT cap from gross salary. Any cap, if established, belongs in policy. No SSO projection method was selected.

## 11. Opening-balance compatibility — mandatory stop

Repository: `EmployeeStatutoryProfiles.cs:78–93`, request at `EmployeeStatutoryContracts.cs:54`, service `SetOpeningAsync`. Existing shared-key balance has State, THB, PriorTaxableEmploymentIncome, PriorTaxWithheld, PriorSocialSecurityContribution, inclusive AsOfDate and audit explanation. Its comment expressly defers income interpretation to D6. It does not establish pre-expense income versus net taxable income, payer scope, contribution payment recognition, or exclusion of overlapping SIAMIS history.

Affected calculation: annual income, deductible employee SSO and prior withholding can be misstated or double-counted. Smallest proposal: an explicitly versioned opening-input meaning for eligible 40(1) income before expense/allowance deductions, actual employee-side deductible SSO paid, and actual PIT withheld, all in the declaration's TaxYear through inclusive cutoff. Approve same-employer versus prior-employer scope and the SIAMIS-history cutoff rule first. Define treatment of current-period entries and unpaid/cancelled payroll separately; a cutoff date alone does not resolve payer scope.

Preserve existing verified balances verbatim; do not silently reinterpret legacy fields. If introducing typed replacement inputs, legacy/unresolved meanings must block automatic PIT. Retain Unknown/null, ConfirmedZero/explicit zeros and VerifiedAmount/explicit values. Opening identity is the declaration ID, not a nonexistent separate opening GUID.

## 12. PIT bracket contract

Repository: `StatutoryPolicyService.cs:236` validates consecutive ordering, nonnegative values, positive bounded widths, no overlaps, and on publication contiguous zero-to-infinity coverage with a final unbounded bracket. Retain LowerBoundInclusive/UpperBoundExclusive. A first zero-rate band represents the initial exemption without a duplicate field. Existing bracket tests were rerun; no legal schedule was seeded. Proposed V1 strengthening: percentage-point rates no greater than 100, without hard-coding a statutory schedule.

## 13. Effective-date/tax-year contract

PayDate and its Gregorian year are approved future business inputs. Existing inclusive effective dates plus the already present TaxYear are sufficient with validation; no duplicate TaxYear is needed. Current publication checks TaxYear range, not compatibility with effective dates.

Recommended PIT-TH-V1 rule: require a bounded effective interval wholly within its declared TaxYear; permit partial-year policy versions and reject incompatible ranges. Future resolver must also require PayDate.Year == TaxYear. This is a proposed SIAMIS compatibility rule, not a claim that all legislation starts January 1. No date rule was implemented, no predecessor shortened, and no Published policy rewritten.

## 14. Publication validation

Repository: `StatutoryPolicyService.cs:211`. Current D4A publication requires reference, method identifier, the existing PIT fields and complete brackets. It does not guarantee the future expanded V1 contract. Retain Serializable publication, scheme-row UPDLOCK and overlap rejection. After decisions, require all V1 parameters and year compatibility only for the supported method. No policy exists in Development and none was auto-published.

## 15. Immutability behavior

Published mutation/deletion is rejected by `StatutoryPolicyService.cs:184`. `EmployeeStatutoryService.cs:251–258` permits only Draft mutation and protects Verified claims/opening/treatment. Corrections remain replacement revisions; selection changes atomically at verification. Future calculations use the exact selected Verified revision; historical generated/finalized payroll must retain its original snapshot. No trigger or new immutability mechanism was introduced.

## 16. API/Swagger changes

None. Extend existing policy and declaration/claim endpoints after approval. Keep server-owned verification/selection/audit fields response-only. Existing claim request still accepts generic Amount; this must not be relabeled a legal allowance input. Proposal: reject client-authored allowance amounts in the future V1 claim contract while retaining legacy stored values without using them for V1 money. Agree the compatibility behavior before changing the existing API.

## 17. Database constraints

Unchanged. Existing constraints protect claim vocabulary/nonnegative supplied amounts/positive supplied quantities and opening state consistency. They do not establish legal claim eligibility. Future structural constraints should protect typed categories, person uniqueness and numeric ranges; publication handles complete policy schedules. Do not encode current legal amounts as CHECK constants.

## 18. Future PIT-result snapshot requirements

Do not create a PIT result in this stopped checkpoint. D6D must preserve exact policy ID, supported method version, THB, PayDate/TaxYear, declaration ID/revision and treatment values/verification, profile identity where available plus verified tax-ID snapshot, opening shared-key identity/state/meaning/cutoff/input values, classified income with line provenance, expense rate/cap, personal allowance, spouse/child/parent typed facts and amounts actually used, employee SSO source-result identities/payment basis/deductible amount/cap, and bracket schedule. Preserve future annualized income, net taxable income, annual tax, prior/current withholding and versioned CalculationSnapshotJson. Snapshot facts and values so explanation does not depend on mutable live joins. Do not reuse the SSO-required result shape as if it already supported PIT.

## 19. Verification counts/results

195 existing focused regression assertions passed, with no database connections/writes. No new D6C implementation tests were added. The requested 48-case D6C acceptance suite is **not complete**: new policy/claim persistence and publication cases are blocked by the contract stop. Existing pure probes cover brackets, opening structural states, classification, treatment and monetary parity. Source inspection and read-only SQL are reported separately from executable acceptance tests.

## 20. D1–D6B regressions

Reran the existing focused D5A/D5B/D5C/D6A/D6B runner: 195 passed. It includes calculation/classification/provenance probes and existing D3 calculation use. Full live D1/D2/D3/lifecycle/rollback/privacy HTTP suites were not rerun in this stopped checkpoint; prior report results are not counted as new tests. No source changes can intentionally alter those behaviors. No PIT lines or money exist in the checked database.

## 21. Final Development baseline

Read-only `sqlcmd` used Windows authentication and trusted local certificate against localhost/SIAMIS; SQL reports server Ray. 51 application tables plus EF history. 172 master rows, Employees 1, EmploymentRecords 1. TEST-EMP-001 ID `433f2c1a-6222-494f-a64f-cd0c31126dc4` remains inactive. All 17 components retain PIT Unknown, SSO Unknown and legacy IsTaxable=false.

| Table | Rows | Table | Rows |
|---|---:|---|---:|
| AddressTypes | 3 | Attendance | 0 |
| AttendanceStatuses | 11 | ContractTypes | 7 |
| Countries | 13 | Departments | 12 |
| Designations | 20 | DocumentTypes | 15 |
| EmergencyContacts | 0 | EmployeeAddresses | 0 |
| EmployeeCompensations | 0 | EmployeeContacts | 0 |
| EmployeeContracts | 0 | EmployeeDocuments | 0 |
| EmployeeHistory | 0 | EmployeeLeave | 0 |
| EmployeePayrollComponentAssignments | 0 | EmployeePayrollLines | 0 |
| EmployeePayrolls | 0 | EmployeePayrollSocialSecurityResults | 0 |
| EmployeePayrollStatutoryResults | 0 | EmployeePerformance | 0 |
| Employees | 1 | EmployeeStatutoryEnrollments | 0 |
| EmployeeTaxClaims | 0 | EmployeeTaxDeclarations | 0 |
| EmployeeTaxDeclarationSelections | 0 | EmployeeTaxOpeningBalances | 0 |
| EmployeeTaxProfiles | 0 | EmploymentRecords | 1 |
| EmploymentStatuses | 9 | EmploymentTypes | 6 |
| Genders | 4 | HiringSources | 10 |
| LeaveTypes | 10 | Locations | 5 |
| MaritalStatuses | 6 | Nationalities | 13 |
| PayrollComponents | 17 | PayrollPeriods | 0 |
| PayrollRules | 0 | PayrollRuleTargets | 0 |
| PayrollSettings | 0 | PayTypes | 6 |
| PerformanceRatings | 5 | PitPolicyConfigurations | 0 |
| PitTaxBrackets | 0 | SocialSecurityPolicyConfigurations | 0 |
| StatutoryPolicyVersions | 0 | StatutorySchemes | 0 |
| TeacherProfiles | 0 | __EFMigrationsHistory | 28 |

## 22. Exact restoration status

No fixtures created, no database writes performed, and no restoration necessary. Read-only checks confirm the baseline. No API process was started/stopped. Employee core data, policy/declaration history and component classifications were untouched.

## 23. Restore / Release build

`dotnet restore SIAMIS.sln`: succeeded. `dotnet build SIAMIS.sln -c Release --no-restore`: succeeded, 0 warnings, 0 errors. Commands used the workspace SDK at `.dotnet/dotnet.exe`.

## 24. EF pending-model-changes

Development Release EF check succeeded: “No changes have been made to the model since the last migration.” No migration operation was run.

## 25. git diff --check

Passed. Final task change is this report only. No commit or push.

## 26. Remaining decisions / next step

Before resuming D6C, review the proposed typed child facts/category and adoption-capacity declaration, spouse/parent eligibility attestations, legacy claim Amount compatibility, explicit opening input meaning/payer/cutoff, and SSO actual-payment recognition/annual projection/cap contract. These can change money; implementation has stopped rather than selected them implicitly.

D6D additionally needs an approved supported monthly method: regular versus irregular income behavior, joining/leaving payment counts, actual-paid history eligibility, prior withholding ownership, rounding/residual handling and correction behavior, canonical PIT deduction ownership and fail-closed snapshot compatibility. PayDate governing-date choice is already approved. No D6D calculation work was performed.
