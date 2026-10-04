# D4B — Minimum employee statutory profile foundation

Implemented the original checkpoint and the approved stop decisions. The focused migration
was applied only to Development SQL Server `localhost`, database `SIAMIS`, using Windows
integrated authentication and TrustServerCertificate=True. No commit or push was performed.

## 1. Files created or modified

Created:

- `src/SIAMIS.Domain/Entities/Payroll/EmployeeStatutoryProfiles.cs` — six entity classes.
- `src/SIAMIS.Application/Payroll/EmployeeStatutoryContracts.cs` — focused request/response DTOs and IEmployeeStatutoryService.
- `src/SIAMIS.Infrastructure/Configurations/EmployeeStatutoryConfigurations.cs` — six EF mappings.
- `src/SIAMIS.Infrastructure/Services/EmployeeStatutoryService.cs` — employee input storage, lifecycle and resolution.
- `src/SIAMIS.Api/Controllers/EmployeeStatutoryEnrollmentsController.cs`.
- `src/SIAMIS.Api/Controllers/EmployeeTaxController.cs`.
- `src/SIAMIS.Infrastructure/Migrations/20261001025119_AddEmployeeStatutoryProfiles.cs` and its `.Designer.cs`.
- `D4B-REPORT.md`.

Modified:

- `src/SIAMIS.Api/Program.cs` — one scoped registration.
- `src/SIAMIS.Infrastructure/Data/SIAMISDbContext.cs` — six DbSets.
- `src/SIAMIS.Infrastructure/Migrations/SIAMISDbContextModelSnapshot.cs` — D4B mappings.
- `README.md` — documentation link.

Temporary verification scripts, SQL and logs were removed after cleanup. Existing entities,
employee write contracts, D4A services and payroll calculation services were not modified.

## 2. Migration and exact schema additions

`20261001025119_AddEmployeeStatutoryProfiles` adds only these six tables and their
constraints/indexes. No existing table changes, inserts, seeds, backfills or employee/payroll
rewrites occur in its Up method. SIAMIS now has 49 application tables, excluding migration history.

| Table | Columns |
|---|---|
| EmployeeStatutoryEnrollments | EmployeeStatutoryEnrollmentId, EmployeeId, StatutorySchemeId, EffectiveFrom, EffectiveTo, Applicability, MembershipNumber, Remarks, CreatedAt, UpdatedAt |
| EmployeeTaxProfiles | EmployeeTaxProfileId, EmployeeId, TaxpayerIdentificationNumber, CreatedAt, UpdatedAt |
| EmployeeTaxDeclarations | EmployeeTaxDeclarationId, EmployeeId, TaxYear, RevisionNumber, ReplacesDeclarationId, Status, TaxpayerIdentificationNumberSnapshot, VerifiedAt, Remarks, CreatedAt, UpdatedAt |
| EmployeeTaxDeclarationSelections | EmployeeId, TaxYear, CurrentDeclarationId |
| EmployeeTaxClaims | EmployeeTaxClaimId, EmployeeTaxDeclarationId, ClaimType, Amount, Quantity, Reference, Remarks, CreatedAt, UpdatedAt |
| EmployeeTaxOpeningBalances | EmployeeTaxDeclarationId, State, Currency, PriorTaxableEmploymentIncome, PriorTaxWithheld, PriorSocialSecurityContribution, AsOfDate, Remarks, VerifiedAt, CreatedAt, UpdatedAt |

GUID identities, SQL date effective/cutoff fields, application UTC timestamps and SQL datetime2
follow existing conventions. Opening amounts and declared claim Amount use decimal(19,4);
supplied values must be nonnegative and fit that precision/range. Quantity is an optional positive
integer count. Nothing translates it into a legal entitlement.

## 3. Enrollment model

Enrollment references Employee identity and the existing D4A StatutoryScheme. Creation requires
an existing employee and an active Thai SocialSecurity scheme; no TH-SSO-33 code is hard-coded
into the entity. MembershipNumber is optional, up to 100 characters, and is not unique.
No legal identifier equivalence or exemption reason is inferred.

EffectiveFrom/EffectiveTo are inclusive. Per employee/scheme intervals cannot overlap; next-day
adjacency is allowed. Create never shortens a predecessor. History has no generic PUT/DELETE
API. An explicit end action closes an open interval on today or a future date at/after its start.
Closed intervals and already elapsed coverage cannot be rewritten through that action. Retroactive
correction would require a separately approved workflow; this checkpoint does not silently erase history.

## 4. Applicability semantics

Applicable and NotApplicable are explicit stored inputs. Missing coverage resolves as Unknown,
not NotApplicable or a zero contribution. The date resolver rejects corrupt overlapping coverage
with 409. The caller supplies the date; D5 must define its calculation governing-date rules.
Employee activity, nationality, employment type, department, salary and other core fields do not
create or change applicability. Membership is omitted from list/resolution summaries.

## 5. Tax profile

One optional profile per Employee stores only a nullable taxpayer identifier (maximum 100 characters)
and timestamps. It is not related to a single EmploymentRecord. Profile reads do not create rows.
The focused PUT authors the profile; no other identifier is copied/inferred. There are no residency
or tax-treatment fields. Ordinary employment/nationality changes leave it untouched.

Verification captures the current nullable taxpayer identifier on the declaration. A later profile
change does not change that historical snapshot. Missing taxpayer identification is not inferred from
nationality or another document; D6 must enforce whatever additional inputs its approved method requires.

## 6. Declaration model and current selection

Each Employee/TaxYear has revisioned declarations. TaxYear is 1–9999. RevisionNumber is positive
and server-assigned. Unique employee/year/revision and a filtered unique employee/year Draft index
allow multiple Verified historical revisions and at most one Draft.

Creating a new Draft automatically references the current Verified declaration, if any, using
ReplacesDeclarationId. Claims and opening amounts are not copied automatically. A composite
self-FK confines the replacement link to the same employee/year; self-replacement is forbidden.

Current selection is a separate table keyed by EmployeeId/TaxYear. Its composite FK points to a
declaration belonging to that same employee/year. Verification atomically changes the pointer;
the preceding Verified row is never updated. IsCurrentVerified in DTOs is derived selection metadata,
not a mutable field on historical declaration inputs. D6 must record the exact selected revision in
its future historical results. No statutory result linkage was added now.

## 7. Claim model

Manual types are Spouse, Child and Parent only. Each claim has optional declared Amount/Quantity,
reference (500 characters) and remarks (2000 characters). Values remain nullable when not supplied;
the service does not invent a count or allowance. Type, range/precision and ownership are validated.
Multiple claims of the same type are permitted; legal category limits are not guessed.

No manual Personal or SocialSecurity type is accepted. Personal remains a PIT policy concept for
D6. Pre-SIAMIS Social Security is an opening aggregate; SIAMIS-era contributions belong to future
D5 results. D6 must combine them without double counting. No monetary allowance values are seeded.

## 8. Opening-balance model

At most one explicit aggregate opening state belongs to each declaration revision. It stores THB
PriorTaxableEmploymentIncome, PriorTaxWithheld and PriorSocialSecurityContribution with inclusive
AsOfDate. It does not model prior employers or mutable SIAMIS-era YTD counters.

PriorTaxableEmploymentIncome means the approved verified pre-SIAMIS Thai employment-income
aggregate relevant to the supported PIT calculation for the same tax year through the inclusive
cutoff. It is not GrossPay and is not automatically generic TaxableEarnings. D6 owns final statutory
interpretation and cutoff/YTD aggregation. D6 must stop for an extension if the approved aggregate
proves legally insufficient.

| Collective state | Stored contract |
|---|---|
| Unknown | All three amounts and VerifiedAt remain null; optional Remarks |
| ConfirmedZero | Explicit state canonicalizes all three amounts to zero; supplied nonzero amounts are rejected; nonblank Remarks and server UTC VerifiedAt required |
| VerifiedAmount | All three values explicitly supplied and nonnegative; nonblank Remarks and server UTC VerifiedAt required |

Unknown/null never becomes zero. VerifiedAmount may contain supplied zeros. Known opening state
is an explicit internal confirmation on the focused PUT; timestamps/actors are not caller-editable.
AsOfDate must be within the declaration year or the previous December 31 opening instant. That
previous-year cutoff cannot carry positive current-year YTD amounts. It represents an empty
current-year interval, allowing explicit ConfirmedZero at adoption on January 1.

## 9. Verification lifecycle

Draft → Verified is an explicit action, with server UTC VerifiedAt. It validates claim structure,
an explicit opening state and its monetary/audit consistency, and the current replacement chain.
It captures the taxpayer-ID snapshot and updates selection in one employee-locked transaction.
Year/revision/replacement/status/timestamps cannot be supplied in ordinary update requests.
No submission workflow or SubmittedAt field was introduced; there is no authenticated VerifiedBy.

A declaration with explicit Unknown opening state can become Verified, as approved. This means
internal review of supplied inputs, not legal certification or completeness for every D6 calculation.
D6 must reject calculations requiring unresolved opening inputs. Absent opening configuration
blocks verification rather than fabricating a state or balance.

## 10. Immutability and corrections

Verified declaration inputs, claims and opening balance cannot be updated/deleted through the service/API.
Republishing/reverification or status reassignment is rejected. Correction uses the next Draft revision,
then switches current selection on verification while preserving all preceding rows/timestamps/snapshots.
Existing payroll and statutory results are never automatically recalculated.

Draft remarks and owned inputs are editable. Safe Draft deletion removes its owned children in the
same transaction; referenced/Verified history is protected. All relationships use NoAction.
Immutability and Verified selection-state enforcement are service boundaries, not SQL triggers.
Privileged direct database writers can bypass lifecycle rules and must be restricted in production.

## 11. API endpoints/contracts

Routes below start with `/api/employees/{employeeId}`. Controllers use Application contracts and
IEmployeeStatutoryService, not DbContext. Reads use AsNoTracking; ordering/filtering are database-level
where applicable. Full declaration detail loads only its owned claims/opening balance. Unknown JSON
fields are rejected. Swagger exposes purposes, schemas and 400/404/409 outcomes.

| Method | Suffix | Successful response |
|---|---|---|
| GET | /statutory-enrollments | 200 summary array |
| GET | /statutory-enrollments/{id} | 200 owned enrollment detail |
| POST | /statutory-enrollments | 201 enrollment detail |
| POST | /statutory-enrollments/{id}/end | 200 ended enrollment detail |
| GET | /statutory-enrollments/resolve?schemeId=...&date=YYYY-MM-DD | 200 explicit applicability/Unknown; corrupt coverage 409 |
| GET | /tax-profile | 200 focused profile or JSON null |
| PUT | /tax-profile | 200 focused profile |
| GET | /tax-declarations?taxYear=... | 200 revision summaries with current-selection flag |
| GET | /tax-declarations/{id} | 200 complete owned declaration DTO |
| POST | /tax-declarations | 201 new Draft revision DTO |
| PUT | /tax-declarations/{id} | 200 updated Draft remarks DTO |
| DELETE | /tax-declarations/{id} | 204 safe Draft deletion |
| GET | /tax-declarations/{id}/claims | 200 declared claims |
| POST | /tax-declarations/{id}/claims | 201 updated declaration DTO |
| PUT | /tax-declarations/{id}/claims/{claimId} | 200 updated declaration DTO |
| DELETE | /tax-declarations/{id}/claims/{claimId} | 204 Draft claim deletion |
| GET | /tax-declarations/{id}/opening-balance | 200 opening DTO or JSON null |
| PUT | /tax-declarations/{id}/opening-balance | 200 updated declaration DTO |
| DELETE | /tax-declarations/{id}/opening-balance | 204 Draft opening deletion |
| POST | /tax-declarations/{id}/verify | 200 Verified declaration DTO |

Missing employee or wrong resource parent returns 404. Invalid structure returns 400. Overlap,
Draft-slot and immutable lifecycle conflicts return 409. No EF entities are exposed.

## 12. Concurrency

All existing-employee enrollment/profile/declaration writes start Serializable transactions and
acquire the established Employees UPDLOCK before child reads/writes. Ownership/lifecycle is rechecked
inside the transaction. Revision responses capture selection while the transaction still owns the lock.
No global RowVersion, trigger or new concurrency framework was introduced.

Concurrent overlapping enrollment POSTs produced 201/409; concurrent same-year Draft creation
produced 201/409; concurrent verification of the same Draft produced 200/409. Exactly one current
selection survived and the previous Verified revision remained untouched.

## 13. Privacy/security limitations

Taxpayer identifiers are exposed only in focused profile and complete declaration detail/write DTOs,
not broad employee or declaration-summary lists. Membership is available in focused enrollment detail
but excluded from enrollment list/resolution summaries. All test identifiers were synthetic.

There is no authentication/RBAC or new encryption infrastructure. Database strings are not encrypted
by this checkpoint. Employee-scoped routes alone do not authorize access. Production must restrict
these sensitive endpoints and database access and establish appropriate identifier/logging protection.
No actor identities were invented from Employee IDs.

## 14. Database constraints/indexes

Verified all 13 D4B checks and eight FKs exist, are enabled/trusted, with no cascading delete actions.
Checks protect enrollment dates/applicability; declaration year/revision/status/verification metadata
and no self-replacement; selection year; claim types/values; opening THB/state/amounts/audit consistency.

Unique indexes protect one profile per employee, employee/year/revision, and one Draft per employee/year.
Selection's employee/year primary key permits one current pointer. Composite replacement/selection
FKs prevent cross-employee or cross-year links. Enrollment employee/scheme/date and other FK indexes
support reads. MembershipNumber has no unique constraint. Cross-row overlap and Verified-only current
selection are validated in the serialized service, not guessed from unique indexes.

FKs: enrollment→Employee/Scheme; profile→Employee; declaration→Employee/prior declaration;
claim→declaration; opening→declaration; selection→same-employee/year declaration.

## 15. No calculation boundary

No SSO/PIT/provident-fund, employer contribution, monetary allowance, annualization or YTD calculation
was implemented. No EmployeePayrollStatutoryResult/table, statutory payroll line, tax-treatment field
or employee enrollment/declaration seed was introduced. No payroll totals or formulas were changed.
PayrollCalculationService, PayrollGenerationService, PayrollPreviewService, BasicSalaryEntitlementService
and PayrollRuleEvaluator remain unchanged, as do D4A policy services.

## 16. Focused verification

Final successful run: 132 focused assertions, 120 D1–D4A/payroll regression assertions and three cleanup
integrity comparisons, for 255 passing checks. Counts include HTTP result checks, not only distinct
business scenarios.

Verified Applicable/NotApplicable/absence Unknown, inclusive adjacency/overlap rejection, invalid
status/dates/references, ownership, immutable past intervals, enrollment end rules, sensitive-list
omission, tax-profile storage/length/no treatment fields, revision identity/Draft slot, claim vocabulary/
range/precision/ownership, all opening states and audit/null consistency, cutoff/currency validation,
verification prerequisites/Unknown allowance, immutable inputs, taxpayer-ID snapshots, replacement
selection/row preservation, safe Draft deletion, three concurrency scenarios, Swagger and trusted
database constraints. Rolled-back SQL tests rejected invalid fields, cross-parent selection, self-
replacement, duplicate revision/Draft/current selection, and forbidden FK deletions.

## 17. D1–D4A/payroll regressions

Passed D1 employment change/end/rehire with preserved tax profile; nationality changes neither created
nor changed statutory treatment. D2 retained first-eligible historical targeting, inactive historical
selection and future-employment exclusion. D3 retained full 28/29/31-day salary, inclusive leaver,
joiner/gap and salary increase/decrease segment amounts and versioned audit snapshots.

D4A SSO/PIT typed publication, incomplete/overlap rejection, historical resolution, no fallback and
Published deletion protection passed with enrollment relationships present.

| Synthetic payroll scenario | GrossPay | Deductions | NetPay |
|---|---:|---:|---:|
| THB 30,000 Basic Salary, no rules | 30,000 | 0 | 30,000 |
| Generic earning/deduction Supplement | 35,227.5 | 3,822.75 | 31,404.75 |
| Matching earning/deduction ReplaceAssignment | 35,122.5 | 3,512.25 | 31,610.25 |
| Manual earning adjustment +125 | 35,247.5 | 3,512.25 | 31,735.25 |

Preview/Generation parity and BasicSalary/Assignment/PayrollRule/Manual provenance remained correct.
Invalid pay type and replacement conflicts preserved complete prior payroll/audits on failed regeneration;
failed NEW generation left no header/lines. Manual create/update/delete reconciled totals. Payroll
approval/payment and period processing/close/cancel guards passed, including Paid/Closed/Cancelled
regeneration blocks. Statutory profile data had no monetary effect.

## 18. Final Development baseline

| Data | Count |
|---|---:|
| Employees | 1 |
| EmploymentRecords | 1 |
| PayrollComponents | 17 |
| PayrollRules / PayrollRuleTargets / PayrollSettings / PayrollPeriods | 0 each |
| EmployeeCompensations / EmployeePayrollComponentAssignments | 0 each |
| EmployeePayrolls / EmployeePayrollLines | 0 each |
| All five D4A statutory tables | 0 each |
| All six D4B employee statutory tables | 0 each |

TEST-EMP-001 remains inactive. Original Employee/EmploymentRecord/PayrollComponent rows were compared
before/after and are unchanged. Existing payroll baseline was zero rows; failure preservation was also
tested against temporary generated payroll. No real employee inputs or legal numeric defaults remain.
All temporary fixtures were removed using explicitly scoped local verification cleanup.

## 19. Build/EF/diff checks

- Restore succeeded.
- Release build: zero warnings, zero errors.
- EF reports no pending model changes.
- git diff --check passed.
- One focused D4B migration applied locally; no earlier migration modified.
- No commit/push.

## 20. Remaining decisions before D5/D6

- D5 must choose the approved enrollment/policy governing date, validate calculator method compatibility,
  combine explicit applicability with employment eligibility, and define employee/employer outputs.
- D6 must define statutory interpretation, withholding/annualization/YTD methodology, legal claim inputs,
  and the handling of unknown/missing required information. No declaration status implies universal completeness.
- D6 must select the current Verified declaration for new calculations and snapshot its exact revision;
  finalized historical results must retain their original inputs.
- D6 must prevent double counting across inclusive opening cutoff and SIAMIS-era finalized history, and
  request a model extension if the approved aggregate is insufficient.
- Personal allowance and current-year SSO are not manual claims. No legal amounts/eligibility have been approved here.
- Controlled retroactive enrollment correction, production authorization and identifier protection remain future work.

D4B is an input foundation only. It does not certify legal eligibility or implement the future calculators.
