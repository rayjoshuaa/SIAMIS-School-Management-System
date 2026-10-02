# D6A — Thailand employment PIT withholding contract analysis

Reviewed 2 October 2026 against the current repository and local Development `localhost/SIAMIS`.

**D6A analysis is complete. Monetary PIT integration is stopped pending the decisions in section 20.** No application behavior, payroll totals, database data, schema, migration, legal numeric seed, authentication, or SSO calculation was changed. Recommendations below are proposed contracts, not approved legal classifications or implemented calculator behavior.

## 1. Official-source findings

The relevant official sources are:

| Source | Finding used in this review |
|---|---|
| [Revenue Code, sections 39–50](https://www.rd.go.th/5937.html) | Employment assessable income, calendar tax year, source/residency, expenses/allowances, actual insured-person SSO deduction, and withholding framework. |
| [Order P.96/2543](https://www.rd.go.th/3558.html) | Separate regular, special-payment, indeterminate-frequency, employer-paid-tax, December reconciliation, and leaver adjustment paths. |
| [Order P.16/2530](https://www.rd.go.th/3613.html) | Mid-year joining uses the actual number of payments for that first tax year. |
| [Official P.N.D.1 instructions, page 2](https://www.rd.go.th/fileadmin/tax_pdf/withhold/200360_WHT1.pdf) | Regular annualization, declared deductions, allocation, and separate special-payment calculation. |
| [Revenue Department 2023 withholding training, slides 16–19](https://interweb1.rd.go.th/publish/seminar/training/RD06.pdf) | Corroborates annualization, mid-year count, residual carry and the distinct indeterminate-frequency method. |
| [Ruling 0811/09662](https://www.rd.go.th/23727.html) | Joining and leaving must not be treated as the same payment-count reduction. |
| [2025 individual-return instructions](https://www.rd.go.th/fileadmin/tax_pdf/pit/2568/Ins90_241268.pdf) | Supports inspection of claim eligibility/allowances; does not replace employment-withholding instructions. |
| [Parent-allowance guidance](https://www.rd.go.th/60056.html) | Eligibility requires evidence beyond a raw parent count. |
| [Ruling 0702/637](https://www.rd.go.th/67605.html) | Special foreign-worker treatment exists for qualifying circumstances; nationality alone is insufficient. |

For ordinary regular pay, the instructions annualize the payment using its applicable payment count, deduct expenses and declared allowances, calculate progressive annual tax, and divide by that count. Changed regular pay is recalculated. Special payments have their own incremental-tax path. These instructions do not establish a universal `(projected annual tax - YTD withholding) / remaining months` algorithm. [P.N.D.1 instructions, clauses 2.1–2.6](https://www.rd.go.th/fileadmin/tax_pdf/withhold/200360_WHT1.pdf)

P.96's worked examples use historical legal amounts. They are evidence of calculation order and precision behavior, not current numeric policy seeds. No numeric statutory values were entered into SIAMIS.

## 2. Repository findings

The following are the main inspected sources (paths relative to the solution root):

| File | Relevant evidence |
|---|---|
| `src/SIAMIS.Infrastructure/Services/PayrollCalculationService.cs:95` | TaxableEarnings sums final earning-line amounts whose tax snapshot is true. |
| `src/SIAMIS.Infrastructure/Services/EmployeePayrollService.cs:338` | Manual mutations reconcile totals from stored snapshots. |
| `src/SIAMIS.Infrastructure/Services/EmployeePayrollService.cs:429` | Manual component changes snapshot classification; amount-only updates retain it. |
| `src/SIAMIS.Domain/Entities/Payroll/StatutoryPolicies.cs:49` | Existing typed PIT configuration. |
| `src/SIAMIS.Infrastructure/Services/StatutoryPolicyService.cs:211` | Publication and complete bracket validation. |
| `src/SIAMIS.Domain/Entities/Payroll/EmployeeStatutoryProfiles.cs:22` | Profile, declarations, claims, selection and opening amounts. |
| `src/SIAMIS.Infrastructure/Services/EmployeeStatutoryService.cs:198` | Verification and atomic selected-revision replacement. |
| `src/SIAMIS.Infrastructure/Configurations/EmployeeStatutoryConfigurations.cs` | Ownership, revision uniqueness and opening-state checks. |
| `src/SIAMIS.Infrastructure/Services/PayrollGenerationService.cs:75` | Serializable per-employee generation transaction. |
| `src/SIAMIS.Infrastructure/Services/PayrollGenerationService.cs:130` | SSO runs before existing payroll deletion/replacement. |
| `src/SIAMIS.Infrastructure/Services/PayrollPeriodService.cs:89` | Dates cannot change once the period has a payroll header. |
| `src/SIAMIS.Infrastructure/Configurations/EmployeePayrollStatutoryConfigurations.cs` | Current result storage is constrained to SSO-TH-V1. |

No PIT calculator, PIT result, PIT deduction ownership resolver, tax payment ledger or employee-year withholding resolver currently exists. D4A's `PIT-TH-V1` is configuration-contract metadata, not an implemented monetary method.

## 3. Foreign/Thai employee treatment

Thailand-source employment and tax residence are distinct concepts. The Revenue Code's nonresident flat-rate provision in section 50(1) concerns section 40(2), not a blanket rule for foreign section 40(1) employees. Residence also affects allowance eligibility. [Revenue Code, sections 41, 47 and 50](https://www.rd.go.th/5937.html)

Special qualifying foreign-worker regimes cannot be identified by nationality alone. [Official ruling 0702/637](https://www.rd.go.th/67605.html)

`EmployeeTaxProfile` stores only an identifier and audit timestamps. It lacks residence, employment-income treatment, exceptional regime and employer-paid-tax/gross-up information. Enrollment in SSO supplies none of those facts.

**Stop applicability resolution.** Smallest proposed addition: verified employee/tax-year treatment metadata, with `ResidencyStatus = Unknown / Resident / NonResident` and `EmploymentTaxTreatment = Unknown / StandardSection40_1 / RequiresReview`, plus evidence reference and verification timestamp. Capture this in immutable declaration/result snapshots; do not rely on a mutable profile alone. Define what establishes residence for prospective payroll and how later corrections work. Unknown and RequiresReview block V1. NonResident must receive an explicit reviewed treatment or be outside V1; it must never mean zero tax automatically.

## 4. TaxableEarnings assessment

Current semantics are **classified earning amounts before PIT deductions**, not final net taxable income. Generic deductions and SSO do not reduce TaxableEarnings. Basic Salary, Assignment and PayrollRule generation copy `IsTaxable` into `IsTaxableSnapshot`; storage and reads retain that value. Explicit regeneration uses current classification. Manual creation copies classification, amount-only update preserves it, and changing its component copies the new component's classification.

The boolean cannot distinguish reviewed exclusion from unreviewed false. SQL confirmed all 17 baseline components have `IsTaxable=0`, including Basic Salary. That is not evidence that these earnings are legally excluded.

**Stop assessable-income input.** Recommend an explicit `PitIncomeTreatment = Unknown / Included / Excluded` component field and earning-line snapshot, independent of SsoWageTreatment. Preserve the existing boolean and historical totals during a reviewed transition. Existing records must not acquire a legal classification merely from either boolean value. Unknown must block future PIT when relevant. No backfill classification was performed here.

Even a complete Included/Excluded classification is insufficient to distinguish recurring salary from irregular bonus/overtime. A minimal separate payment-kind contract is needed. Noncash taxable benefits and gross-up payments must be represented truthfully or explicitly rejected as outside V1; GrossPay is not automatically the complete legal assessable-income ledger.

## 5. D4A PIT policy completeness

| Required contract | Existing representation | Assessment |
|---|---|---|
| Expense rate and cap | Nullable fields; required for publication | Present structurally. Future calculator must define rate units and supported ranges. |
| Personal allowance | Nullable amount; required for publication | Present structurally. |
| Spouse allowance | None | Missing. |
| Child allowance and relevant tiers | None | Missing. A single count may not determine the allowance. |
| Parent allowance/eligibility limits | None | Missing. |
| Employee SSO deduction mode | None | Missing projection/recognition contract. |
| Independent PIT SSO cap | None | Do not invent one; establish whether the supported method needs one. |
| Progressive brackets | Ordered typed rows | Structurally adequate. |
| Tax-exempt income band | Can be a zero-rate bracket | No separate threshold is inherently needed for that band. |
| Withholding/rounding method | Free method identifier plus `PIT-TH-V1` metadata | Does not establish a supported monetary algorithm. |
| TaxYear and effective dates | Stored separately | Future resolver must require both date applicability and matching tax year. |

Current law separates employment expenses from allowances. Insured-person contributions are deductible as actually paid; the cited provision does not supply an independent fixed PIT SSO cap. [Revenue Code, sections 42 bis and 47(1)(ฌ)](https://www.rd.go.th/5937.html)

Publication currently requires tax year, expense rate/cap, personal allowance, reference and method metadata, but can succeed without the missing claim parameters. It also accepts nonnegative numeric rates without defining percentage versus fraction for a future calculator. Published D4A policies must remain immutable; extending the monetary contract should use an explicitly supported new configuration/method version, never silently reinterpret an old Published policy.

**Stop policy completeness.** Add only reviewed parameters/typed rules for the supported allowance cases and SSO recognition/projection. Do not fill them with calculator constants.

## 6. Bracket assessment

Keep `LowerBoundInclusive`, nullable `UpperBoundExclusive`, and ordered contiguous coverage from zero to infinity. For annual net income `x`, the amount within a bracket is `max(0, min(x, upper) - lower)`, with no upper bound for the last bracket. Apply the explicitly defined rate unit to that slice. A boundary has zero width in the adjacent bracket; there is no duplicate taxation at an exact boundary.

Publication rejects missing coverage, gaps, overlaps, negative values, zero-width bounded intervals and premature unbounded intervals. Draft gaps may remain unfinished. Focused tests executed these conditions. No bracket schema or interpretation change is needed. Legal bracket values/exemption bands still require reviewed policy data.

## 7. Declaration selection

The selection primary key is `(EmployeeId, TaxYear)`; its composite FK ensures the selected revision belongs to that employee/year. Revision numbers are unique; at most one Draft exists for an employee/year. Correction creates a fresh Draft linked to the selected predecessor, without copying inputs silently. Verification uses a serializable transaction and employee lock, checks the predecessor, verifies the revision and changes selection atomically. Verified revision, claims and opening balance are immutable through the service/API.

Important limitations: Verified-only selection is enforced by the service, not a cross-table SQL status constraint; verification establishes structural validity, not legal allowance eligibility. A declaration can legitimately be Verified with an **Unknown opening state**. Tax ID can be absent. There is no effective-from date on a selected declaration and no current authenticated VerifiedBy.

Proposed future behavior:

| Situation | PIT action |
|---|---|
| No declaration/selection | Fail with missing verified-input explanation. |
| Only Draft | Fail; never consume Draft claims. |
| Selected Verified with Unknown opening | Fail at the history boundary. |
| Selected Verified with resolved inputs | Use exact revision GUID and revision number. |
| Correction selected mid-year | New calculation explicitly uses that revision; preserve already stored payroll results. |

Resolve whether future calculations always use selection at calculation time, or require a business-effective declaration date for backdated payroll. Record the exact revision either way. Do not retroactively change approved/paid results.

## 8. Opening balance / YTD contract

Opening balance has **no separate OpeningBalanceId**: its identity is the shared `EmployeeTaxDeclarationId`. Stored amounts are `PriorTaxableEmploymentIncome`, `PriorTaxWithheld`, and `PriorSocialSecurityContribution`, plus THB, State, inclusive AsOfDate, Remarks and verification/audit timestamps.

| State | Existing invariant |
|---|---|
| Unknown | All three amounts null; no verification timestamp. |
| ConfirmedZero | Three exact zeros, explanation and verification timestamp. |
| VerifiedAmount | Three explicit nonnegative amounts, explanation and verification timestamp; zero is permitted. |

The service restricts cutoff to the declaration year or immediately preceding December 31; a preceding-year cutoff cannot carry positive YTD amounts. The current model comment deliberately leaves “taxable Thai employment income” for D6 to interpret. It does not establish pre-expense assessable income versus post-deduction net income, payer provenance, recurring/special-payment composition, or whether the aggregate includes SIAMIS payments through the cutoff.

**Stop history interpretation.** Proposed minimum contract: opening is a verified, same-tax-year **pre-expense assessable employment-income aggregate**, with separately verified prior withholding and employee SSO, and explicit payer/same-employer migration scope. Previous-employer tax is not automatically a school-withholding credit. Reject unsupported mixed-payer/mixed-payment balances until reviewed. Require an explicit completeness attestation and a fresh complete opening balance on a replacement declaration.

After approval of those meanings, the exact candidate boundary for each aggregate is:

`opening amount through inclusive cutoff + authoritative same-scope payments strictly after cutoff and strictly before current payment + current payment where required by the method`.

Require `year start - 1 day <= cutoff < current payment date`. Exclude the payroll being replaced by employee/period identity, not merely its old GUID. Require all same-day payments through cutoff to be included in the opening aggregate, or reject ambiguous same-day ordering. For December 31 preceding the year, opening must be zero. Never add the opening income indiscriminately to every annualized monthly salary: its use depends on the chosen official branch.

## 9. SSO deduction contract

Use exact historical D5 **EmployeeAmount**, never EmployerAmount and never a recalculation under live SSO policy. Opening SSO and SIAMIS contributions must use the same inclusive cutoff/exclusive-after boundary as section 8. Employee SSO and PIT have separate governing-date concepts; contribution month must not silently determine PIT deduction year.

D5 provides contribution calculation results, not proof of actual remittance. Likewise Paid is an employee-payroll lifecycle state, not a Revenue Department/SSO remittance ledger. Establish when deduction is considered paid and what evidence/attestation suffices. Annualized payroll also needs an approved expected annual employee SSO deduction: actual-to-date alone versus actual plus reviewed future projection can change withholding. No such projection contract exists.

**Stop annual SSO allowance input.** Recommend versioned deduction-recognition/projection rules and a documented verification boundary, retaining opening amounts and historical D5 result IDs in the PIT snapshot. Do not assume a historical FAQ's fixed annual ceiling remains valid after contribution-policy changes. Do not create a second SSO calculator inside PIT.

## 10. Claim vocabulary

Spouse, Child and Parent are an adequate minimum vocabulary, with Personal policy-driven. Their current nullable Amount/Quantity/Reference/Remarks structure is not a legal entitlement calculation contract. Child tiers, dependency and adoption conditions can differ; parent eligibility requires supporting evidence and prevention of duplicate entitlement. [2025 official return instructions](https://www.rd.go.th/fileadmin/tax_pdf/pit/2568/Ins90_241268.pdf), [parent guidance](https://www.rd.go.th/60056.html)

Smallest proposed approach: reviewed annual claim amounts with evidence and policy validation, plus the minimum tier/quantity facts needed to validate them. Decide whether Amount is the approved total annual deduction or whether Quantity selects a policy amount; do not multiply one by the other without a contract. Missing claim entries should mean explicitly declared no claim only after an attestation, not an inferred entitlement or inferred zero.

Keep other optional deductions outside V1. If an employee requests one, return an unsupported-declaration review outcome; do not silently discard the request. No additional universally mandatory deduction category was established for the narrow ordinary salary scope.

## 11. Tax year and governing date

The legal tax year is the calendar year, and withholding is tied to payment. [Revenue Code, sections 39 and 50](https://www.rd.go.th/5937.html), [P.N.D.1 payment-month fields](https://www.rd.go.th/fileadmin/tax_pdf/withhold/200360_WHT1.pdf)

Period has StartDate, EndDate and PayDate. D5 uses EndDate. PaidAt is an administrative UTC timestamp assigned by mark-paid; it is not a captured business payment date. Period PayDate becomes protected once any header exists, but its planned/actual meaning is not established. Period end and pay date can fall in different years.

**Stop governing-date selection.** Recommend using a confirmed business payment date, stored immutably on the PIT result, for Gregorian TaxYear, policy date, declaration year and YTD inclusion. The smallest option is explicitly making protected PayDate the true payment date and rejecting mark-paid mismatches; if it is only scheduled, add a confirmed payment-date contract before PIT. Buddhist year conversion should be display/import behavior, never inferred from an unrestricted TaxYear integer. Policy resolution must additionally match its PIT TaxYear.

## 12. Annualization findings

For regular monthly payments beginning at the start of the year, the normal count is 12. [Official withholding training, slide 17](https://interweb1.rd.go.th/publish/seminar/training/RD06.pdf)

For mid-year joining, P.16 uses the actual payments for that first year; its October-start monthly example uses three. [P.16/2530](https://www.rd.go.th/3613.html)

The official leaver ruling says a January starter leaving in June still uses the normal annual count; a mid-year starter uses the count from starting through year-end, irrespective of when employment ends. Do not shrink the divisor to actual months worked at exit. [Ruling 0811/09662](https://www.rd.go.th/23727.html)

Repository periods are independent date ranges, not a tax payment schedule. D3 computes payable days and entitled salary, not payment frequency/count. Mid-month starts, employment gaps/rehires, unpaid periods, arrears and payments shifted between years are not resolved merely by counting PayrollPeriods or dividing D3 days by 30.

**Stop those cases.** A supported V1 must explicitly represent/verify the regular monthly payment schedule and applicable joining count. Initially reject partial/irregular schedules pending a reviewed contract rather than annualize a partial first wage as a full recurring salary. A recurring/special/unknown payment-kind distinction must be available for all taxable earning origins, including rules and manual earnings. Bonus and intermittent overtime use a separate method; either implement that reviewed branch later or reject such payrolls in salary-only V1.

## 13. YTD payroll-state recommendation

| State | Recommended use in authoritative actual-payment history |
|---|---|
| Draft | Exclude. |
| Calculated | Exclude from actual paid/withheld history. |
| Approved | Exclude until actual payment is confirmed. |
| Paid | Include only with confirmed payment timing and required withholding/deduction evidence. |
| Cancelled | Always exclude. |

For prospective calculation, separately identify current/proposed payment inputs; do not silently merge unpaid earlier payroll into paid history. An unresolved earlier payment should block dependent reconciliation, or be explicitly handled by a reviewed forecast contract.

Database uniqueness is `(PayrollPeriodId, EmployeeId)`, so one employee cannot have duplicate current headers for a period. It does not guarantee one payment date or an employee-year payment sequence. Forced regeneration replaces the header GUID and complete calculated snapshot; Approved/Paid/Cancelled are protected. Querying history is feasible by EmployeePayroll -> PayrollPeriod.PayDate plus Status, but no dedicated PIT YTD resolver exists. The proposed actual-date contract is required before that join is financially authoritative.

Serialize future PIT calculations by employee/year as well as the existing period locks. Establish deterministic same-date ordering and prevent a backdated history change from leaving a later PIT snapshot stale. Paid-only history avoids regeneration duplicates, but does not by itself solve cross-period dependencies.

## 14. Proposed V1 withholding algorithm

This is a **conditional implementation plan**, not an executable approved monetary contract:

1. Verify ordinary section 40(1) treatment, confirmed payment date/year, supported monthly schedule and legal classification of every relevant earning. Reject unknown/unsupported inputs.
2. Resolve exactly one Published TH-PIT policy with matching date, year, supported configuration and calculator method version. Resolve the selected Verified declaration and complete opening state.
3. Finalize D3 salary, assignments/rules, stored tax-classification metadata and current employee SSO. Build the authoritative history/opening inputs under sections 8–9 and 13.
4. For **regular salary**, let `R` be the supported regular assessable payment and `N` the approved applicable count. Use projected income `R × N`; expense deduction is bounded by the policy rate/cap. Subtract verified annual allowance amounts and the approved annual employee-SSO deduction. Calculate nonnegative net income and progressive annual tax from policy brackets; allocate ordinary current withholding using that same `N` and the approved precision/residual contract.
5. Salary changes and revised declarations affect the appropriate new calculation; do not rewrite previous results. Do not subtract all YTD tax and divide by remaining months as a replacement for ordinary allocation.
6. Separate irregular/special-payment and indeterminate-frequency branches must remain unsupported until their input and method contracts are approved. Never fold a one-off amount into `R × N`.
7. For December's last payment, decide whether V1 adopts the permitted actual-year reconciliation; for a leaver, decide the permitted true-allowance under/over-withholding adjustment and refund handling. Neither can be guessed from salary proration.
8. Emit only a positive employee withholding deduction with Statutory provenance. Preserve a zero-result explanation if tax is zero; do not insert a zero-value line.

The regular annualize/deduct/calculate/divide sequence is corroborated by [official withholding training, slides 16–17](https://interweb1.rd.go.th/publish/seminar/training/RD06.pdf); the special-payment distinction is documented in the [P.N.D.1 instructions](https://www.rd.go.th/fileadmin/tax_pdf/withhold/200360_WHT1.pdf). December and exit adjustments are permitted under P.96 clauses 2–3; December uses annual tax less tax already withheld and remitted, while the exit clause adjusts previous under/over-withholding based on actual allowance evidence. [P.96/2543](https://www.rd.go.th/3558.html)

A negative reconciliation cannot be silently clamped to zero or inserted into the existing positive-only deduction schema. Its refund/credit procedure is a required decision before supporting that path.

## 15. Rounding findings

P.96 demonstrates satang precision and last-payment residual carry: its example divides 9,777.78 by 12, with 814.81 for earlier payments and 814.87 for the last. This differs from ordinary two-decimal half-up allocation and whole-baht SSO rounding. [P.96 worked example 5](https://www.rd.go.th/3558.html)

The inspected sources do not establish every intermediate precision/tie rule needed for all supported cases. **Stop monetary rounding.** Proposed versioned contract for review: explicit decimal precision for income/deductions/bracket accumulation, annual-tax rounding point/mode, satang truncation for ordinary allocations, exact residual carry/reconciliation and treatment of negative adjustments. Confirm each stage against official examples before implementation. Preserve raw and final values plus rounding version in the result snapshot. Do not use generic four-decimal AwayFromZero or D5 whole-baht rounding by default.

## 16. Proposed historical PIT result

Current `EmployeePayrollStatutoryResult` is SSO-specific despite its generic name: required EnrollmentId and ContributionMonth, required SocialSecurity child, SSO-only SQL method check, and SSO-specific DTO/removal logic. A PIT row cannot truthfully be inserted there today.

Smallest recommended future persistence is a separate typed `EmployeePayrollPitResult`, linked to EmployeePayroll and exact policy/declaration/profile identities with NoAction, unique per payroll/scheme. Keep SSO storage intact. If a common result root is later generalized, migrate its SSO-only requirements into the SSO child explicitly; do not add a collection of unrelated nullable PIT fields to the SSO child.

Suggested PIT fields:

| Category | Data |
|---|---|
| Identity | Result GUID, EmployeePayrollId, StatutorySchemeId, StatutoryPolicyVersionId, EmployeeTaxProfileId, EmployeeTaxDeclarationId and revision number. |
| Opening identity | Declaration shared key identifies the opening row; no invented independent OpeningBalanceId. |
| Timing/method | TaxYear, confirmed GoverningDate, calculation/configuration/rounding version, THB, UTC CreatedAt. |
| Income | Current assessable TaxableEarnings, opening and SIAMIS YTD income separately, regular/special composition, projected income, applicable payment count. |
| Deductions | Expense deduction; Personal/Spouse/Child/Parent amounts separately; actual and projected employee SSO separately. |
| Tax | Net annual taxable income, annual tax, opening/YTD withholding, raw current amount, final withholding and residual/reconciliation amounts. |
| Explanation | Versioned JSON with exact policy values/brackets, selected declaration inputs, opening cutoff/provenance and historical payroll/SSO result IDs and values used. |

Use typed searchable monetary fields and JSON for reproduction detail. Snapshot profile treatment because the live profile remains mutable. Do not duplicate the sensitive tax identifier in broad payroll lists or logs. SourceId on its deduction remains a snapshot identifier, not an FK; linked result/configuration relationships preserve ownership independently. The eventual schema work needs review and a migration; none was created here.

## 17. Deduction component and provenance

SQL confirms seeded stable code **DEDUCT-002**, name Withholding Tax, category Deduction. The seed source establishes its stable identity; current code does not designate it as a canonical TH-PIT-owned component. D5 reserves DEDUCT-001 for SSO.

Recommend explicitly approving DEDUCT-002 as V1 PIT ownership, with active/category/identity validation and collision checks against final Assignment/PayrollRule/generated/manual lines. Do not select by display name. Clarify whether manual PIT adjustments are prohibited or separately audited, and never treat them automatically as historical statutory withholding.

Future positive PIT line: `SourceType=Statutory`, SourceId=PIT result GUID. It changes TotalDeductions and NetPay only. It leaves BasicSalary, GrossPay and TaxableEarnings intact. There is no employer PIT counterpart to employer SSO in this design; employer-paid employee-tax gross-up is a different, excluded method.

## 18. Manual mutation policy

Existing D5 guard blocks earning mutations when an SSO result exists; permitted manual deductions reconcile payroll totals. Manual endpoints cannot edit generated lines, and terminal lifecycle/period guards remain in place.

After PIT persistence, recommend blocking earning creation/deletion/amount/component changes and any deduction change that alters a PIT-recognized input or claims statutory PIT/SSO ownership. Allow unrelated manual deductions only where existing lifecycle permits and where totals reconciliation remains safe. A loan or late deduction is not automatically a statutory tax allowance. Do not automatically recalculate PIT.

Require explicit complete regeneration for supported mutable calculated payroll; preserve failure rollback and finalized-history protection. Combine SSO/PIT guards by dependency, rather than allowing an operation blocked by either. Historical input changes affecting later employee-year PIT results require an explicit dependency/reconciliation policy.

## 19. Preview/Generation ordering

Use one Application calculator contract with Infrastructure input resolution. Both Preview and Generation should use the same resolved immutable calculation inputs:

`D3 salary -> assignments/rules -> finalized earning classifications/TaxableEarnings -> D5 employee SSO -> PIT -> employee deduction totals/NetPay`.

Preview persists nothing and labels all proposed values provisional. Generation resolves and validates every dependent input inside its serialized transaction before destructive replacement. Persist header, lines, SSO and PIT results together. Failed new generation leaves no partial rows; failed forced regeneration preserves the prior complete header, totals, lines and both statutory explanations. Resolve exact selected revision/policy and employee-year history under appropriate locks to prevent inconsistent reads.

Current generation already validates SSO before replacement and commits all D5 state transactionally. Adding PIT requires extending complete regeneration/removal and result DTO handling explicitly. No such integration was made in D6A.

## 20. Missing contracts / decisions requiring review

| Boundary | Smallest decision needed before money |
|---|---|
| Tax classification | Approve explicit Unknown/Included/Excluded and historical transition rules. |
| Applicability | Verified employee/year residency and standard section 40(1) treatment; unsupported/special regimes fail explicitly. |
| Policy | Approved spouse/child/parent parameters, legal claim validation, rate units and a supported monetary method version. |
| Payment date/year | True payment-date contract; Gregorian year; snapshot; mismatch/cross-year handling. |
| Schedule | Regular payment kind/count and mid-year joining; partial month, rehire/gap and irregular pay either supported explicitly or rejected. |
| Opening | Pre-expense assessable meaning, payer scope, completeness and same-day cutoff ownership; no prior-employer credit assumption. |
| SSO deduction | Paid/recognized employee contribution evidence and approved annual projection, without live historical recalculation. |
| Claims | Meaning of Amount/Quantity, eligibility proof, tier validation and explicit unsupported-claim behavior. |
| History | Paid-only actual ledger, employee/year serialization, same-date ordering and backdated dependency handling. |
| Reconciliation | Whether December/exit adjustments are in V1; negative refund/credit handling. |
| Precision | Versioned intermediate/annual/period rounding and residual carry. |
| Ownership/result | Approve DEDUCT-002 ownership and separate typed PIT result schema. |

These stop affected PIT calculation boundaries. No partial PIT monetary path was introduced. V1 should remain ordinary supported monthly Thailand employment; section 40(2)–(8), termination special taxation, foreign-source/treaty relief, investments, insurance, donations, provident fund, spouse aggregation, annual filing and P.N.D.1 submission remain outside scope.

## 21. Files changed

- `D6A-REPORT.md` — this analysis and decision record.
- `tests/SIAMIS.Payroll.RegressionTests/D6AContractRegressionTests.cs` — 29 dependency-free repository-contract assertions using synthetic inputs; no database calls.
- `tests/SIAMIS.Payroll.RegressionTests/Program.cs` — invokes those probes and updates the runner summary.

No production/application source, configuration, entity, EF configuration, migration or snapshot file changed. No commit or push.

## 22. Verification results

| Requested check | Evidence / result |
|---|---|
| Historical tax classification | Executed generated snapshot and live flag-change/recalculation probes. |
| Manual classification | Executed creation, amount-only retention and explicit component-change probes. |
| PIT publication | Inspected required fields, immutability and serialized overlap checks; no policy published here. |
| Bracket coverage | Executed valid coverage plus gap/overlap/order/negative/width/unbounded rejection probes. |
| Declaration selection / Draft | Executed model key/FK/unique-Draft probes; inspected Verified-only atomic selection service. |
| Opening invariants | Executed Unknown, ConfirmedZero, VerifiedAmount, missing/nonzero amounts and explanation probes; inspected date validation and SQL checks. |
| Privacy | Inspected narrow profile/detail endpoints and summary omission of identifier. No authentication middleware or authorization is present: ownership is not caller authorization. No new privacy feature added. |
| Historical employee SSO | Inspected immutable typed EmployeeAmount availability, exact result IDs and snapshots; existing D5 calculator assertions passed. |
| No employer SSO deduction | Inspected separate employee/employer fields and proposed consumer boundary; no PIT consumer exists. |
| Year/state/history feasibility | Inspected period/date joins, statuses, uniqueness and protected dates; no monetary YTD query implemented. |
| Regeneration | Inspected transaction and validation-before-replacement, terminal protections and snapshot persistence. |
| PIT deduction inventory | Read-only SQL confirmed DEDUCT-002; canonical ownership is still a decision. |
| No PIT money / unchanged totals | Only report/tests changed. Existing calculator totals and classification independence assertions passed. |
| D1–D5 regression coverage | Re-ran all 111 existing dependency-free D5A/B/C assertions; added 29 D6A probes, total 140 passed. D1/D2/D3 and D4A/B integration boundaries were inspected unchanged. |

**Verification limits:** no new live fixtures or payrolls were created. The committed D4B report records 132 focused and 120 earlier regression assertions, and D5C records 306 live API/SQL assertions. Those historical live results were inspected, not presented as rerun in D6A. Atomic declaration replacement and SQL rollback/lifecycle regressions were reviewed in source; this checkpoint's new executable probes do not simulate database concurrency or replay every D1–D4 live workflow. The database has no payroll history to exercise an actual PIT year aggregation. This does not establish monetary PIT correctness.

## 23. Final Development baseline

Read-only SQL connected successfully to `localhost/SIAMIS` using Windows authentication. SQL's instance server name is Ray. There are **51 application tables**, plus EF migration history. Latest applied migration remains `20261001044414_AddSection33PayrollResults`.

| Tables / checks | Count |
|---|---:|
| Employees | 1 |
| EmploymentRecords | 1 |
| EmployeeContacts / EmployeeAddresses / EmergencyContacts | 0 each |
| EmployeeCompensations / EmployeePayrollComponentAssignments | 0 each |
| PayrollComponents | 17 |
| PayrollRules / PayrollRuleTargets / PayrollSettings / PayrollPeriods | 0 each |
| EmployeePayrolls / EmployeePayrollLines | 0 each |
| EmployeePayrollStatutoryResults / EmployeePayrollSocialSecurityResults | 0 each |
| EmployeeStatutoryEnrollments / EmployeeTaxProfiles | 0 each |
| EmployeeTaxDeclarations / EmployeeTaxDeclarationSelections | 0 each |
| EmployeeTaxClaims / EmployeeTaxOpeningBalances | 0 each |
| StatutorySchemes / StatutoryPolicyVersions | 0 each |
| PitPolicyConfigurations / PitTaxBrackets / SocialSecurityPolicyConfigurations | 0 each |
| Other non-master operational tables | 0 each |

Master counts: AddressTypes 3; AttendanceStatuses 11; ContractTypes 7; Countries 13; Departments 12; Designations 20; DocumentTypes 15; EmploymentStatuses 9; EmploymentTypes 6; Genders 4; HiringSources 10; LeaveTypes 10; Locations 5; MaritalStatuses 6; Nationalities 13; PayrollComponents 17; PayTypes 6; PerformanceRatings 5. Total **172 master rows**.

Only employee: `433f2c1a-6222-494f-a64f-cd0c31126dc4`, TEST-EMP-001, IsActive=false. All 17 component SsoWageTreatment values remain Unknown; all baseline IsTaxable values remain false without any legal inference. No fixture cleanup was necessary because no database writes were performed.

## 24. Build / EF / diff status

| Command | Result |
|---|---|
| `.\.dotnet\dotnet.exe restore SIAMIS.sln` | Succeeded. Initial sandbox NuGet access denial resolved by approved unrestricted restore. |
| `.\.dotnet\dotnet.exe build .\SIAMIS.sln -c Release --no-restore` | Succeeded; 0 warnings, 0 errors. |
| `.\.dotnet\dotnet.exe run --project tests/SIAMIS.Payroll.RegressionTests -c Release --no-build --no-restore` | 140 assertions passed; no database connections/writes. |
| `dotnet ef migrations has-pending-model-changes` with Infrastructure/API, Release and Development | No model changes since the last migration. |
| `git diff --check` | Passed. |

No migration created or applied. No SSO/PIT amounts, legal classifications, payroll monetary behavior, authentication or unrelated features changed. Stop here for review of the missing contracts.
