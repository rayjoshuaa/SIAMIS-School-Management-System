# D4A — Thailand statutory policy and version foundation

Completed against local Development SQL Server `localhost`, database `SIAMIS`, using
Windows integrated authentication and `TrustServerCertificate=True`.
No commit or push was performed.

## 1. Files created or modified

Created:

- `src/SIAMIS.Domain/Entities/Payroll/StatutoryPolicies.cs`: five typed entities.
- `src/SIAMIS.Application/Payroll/StatutoryPolicyContracts.cs`: requests, DTOs, service and resolver interfaces.
- `src/SIAMIS.Infrastructure/Configurations/StatutoryPolicyConfigurations.cs`: five EF mappings.
- `src/SIAMIS.Infrastructure/Services/StatutoryPolicyService.cs`: configuration CRUD, publication and resolution.
- `src/SIAMIS.Api/Controllers/StatutoryConfigurationController.cs`: shared HTTP error mapping for the two focused controllers.
- `src/SIAMIS.Api/Controllers/StatutorySchemesController.cs`.
- `src/SIAMIS.Api/Controllers/StatutoryPolicyVersionsController.cs`.
- `src/SIAMIS.Infrastructure/Migrations/20261001020915_AddStatutoryPolicyFoundation.cs` and its `.Designer.cs`.
- `D4A-REPORT.md`.

Modified:

- `src/SIAMIS.Api/Program.cs`: scoped service/resolver registration only.
- `src/SIAMIS.Infrastructure/Data/SIAMISDbContext.cs`: five DbSets only.
- `src/SIAMIS.Infrastructure/Migrations/SIAMISDbContextModelSnapshot.cs`: D4A model additions.
- `README.md`: D4A documentation link.

Temporary verification scripts, SQL files and logs were removed after successful cleanup.

## 2. Migration and exact schema additions

`20261001020915_AddStatutoryPolicyFoundation` was generated, inspected and applied only
to local Development SIAMIS. Its Up method creates the following five tables and their
keys, indexes and constraints. It contains no alterations to existing tables and no data
inserts, backfills, or destructive rewriting.

| Table | Columns |
|---|---|
| StatutorySchemes | StatutorySchemeId, Code, Name, Jurisdiction, SchemeType, IsActive, CreatedAt, UpdatedAt |
| StatutoryPolicyVersions | StatutoryPolicyVersionId, StatutorySchemeId, SchemeType, Version, EffectiveFrom, EffectiveTo, Currency, Status, OfficialReference, CalculationMethodVersion, CreatedAt, UpdatedAt, PublishedAt |
| SocialSecurityPolicyConfigurations | StatutoryPolicyVersionId, SchemeType, EmployeeContributionRate, EmployerContributionRate, MinimumContributionBase, MaximumContributionBase, InsuredPersonClassification |
| PitPolicyConfigurations | StatutoryPolicyVersionId, SchemeType, TaxYear, EmploymentExpenseDeductionRate, EmploymentExpenseDeductionCap, PersonalAllowanceAmount, WithholdingMethodIdentifier |
| PitTaxBrackets | PitTaxBracketId, StatutoryPolicyVersionId, SortOrder, LowerBoundInclusive, UpperBoundExclusive, Rate |

IDs are GUIDs. Effective dates are SQL `date`; audit timestamps use `DateTime.UtcNow`
and SQL `datetime2`. Numeric policy fields use `decimal(19,4)`. API validation rejects
negative values, excess precision and values outside that storage range.

## 3. Scheme model

Scheme types: `SocialSecurity` and `PersonalIncomeTax`. Jurisdiction canonicalizes to
`TH`; other jurisdictions are rejected. Code canonicalizes to uppercase and is unique.
The approved initial codes are `TH-SSO-33` and `TH-PIT`; neither is seeded.
Other Social Security categories can have their own stable scheme identities and explicit
insured-person classification; this design does not merge all categories into Section 33.

Code/type/jurisdiction cannot be reassigned. Name can change before any version is
Published. IsActive controls new Draft creation and publication; inactive Published
history remains readable and resolvable. Schemes with any policy versions cannot be deleted.

## 4. Policy/version model

Each scheme/version pair is unique. A version starts as Draft. Version, effective dates,
currency, source reference and method metadata are explicit fields. Scheme assignment
and lifecycle audit are server-controlled. Drafts may be incomplete; publication checks
completeness. The SchemeType discriminator is copied by the server and enforced through
composite foreign keys so typed configuration cannot attach to the wrong scheme type.

## 5. Typed Social Security configuration

Employee/employer rates, minimum/maximum contribution bases and insured-person
classification are nullable during drafting and required for publication. Rates are stored
as percentage points, matching existing SIAMIS rate representation; no contributions are
calculated. Both bases must be nonnegative and maximum must be at least minimum.
No legal rates, bases or employee enrollment are inferred.

## 6. Typed PIT configuration and brackets

TaxYear, expense deduction rate/cap, personal allowance and withholding-method identifier
are explicit configuration fields. Publication requires all of them. TaxYear fits 1–9999.
The withholding identifier remains metadata; D4A selects no annualization or cumulative method.

Bracket rows have consecutive SortOrder starting at 1, lower-inclusive and nullable
upper-exclusive bounds, and nonnegative rates. Drafts may have gaps/incomplete coverage,
but invalid boundaries, overlaps, duplicate order and early unbounded ranges are rejected.
Publication requires contiguous coverage from zero to infinity, with exactly one final
unbounded bracket. These are approved SIAMIS storage rules, not assertions about legal
Thai bracket values. No legal brackets are seeded.

## 7. Publication lifecycle

An explicit Publish action validates typed completeness, meaningful nonblank
OfficialReference, supported configuration-method metadata, THB, active scheme and
non-overlapping Published coverage. Successful publication sets PublishedAt in UTC.
Requests cannot set Status or PublishedAt. Invalid input returns 400; lifecycle/overlap
conflicts return 409. No actor ID is invented while authentication remains deferred.

## 8. Immutability and historical safety

All Published policy metadata and typed parameters, including OfficialReference, are
immutable through the service/API. Republish, return to Draft and hard deletion are
rejected. Published scheme names also cannot change. Draft deletion explicitly removes
owned configuration/brackets inside the same transaction and rolls back if references
prevent deletion. There are no cascading FKs.

This is an application/service enforcement boundary, not a SQL immutability trigger.
Privileged direct SQL can bypass application publication/immutability rules. Production
must restrict direct database writes and add authentication/RBAC to these admin endpoints.
Database constraints provide structural protection but do not certify publication legality.

## 9. Effective dates and overlap

EffectiveFrom/EffectiveTo are inclusive. To cannot precede From. Publication uses a
Serializable transaction and scheme-row UPDLOCK, then checks all Published coverage for
that scheme. Conflicting concurrent publishers serialize; only one succeeds. Touching
inclusive dates overlap. A successor starting the day after a bounded predecessor is valid.
An overlapping successor to an open-ended predecessor is rejected; the predecessor is
never automatically shortened or rewritten.

## 10. Currency and calculation method

Currency canonicalizes to THB; unsupported currencies are rejected by API and database.
There is no FX behavior. Publication recognizes `SSO-TH-V1` / `PIT-TH-V1` as typed
configuration contracts only. Unknown/missing method metadata prevents publication.
No runtime calculator compatibility is claimed. D5/D6 must explicitly check their
implementation supports the policy's method version before calculating.

## 11. Resolver

`IStatutoryPolicyResolver` accepts scheme ID and caller-supplied governing date. It returns
Resolved, NoApplicablePolicy, Ambiguous or SchemeNotFound. There is no fallback rate or
silent choice between overlaps. Inactive schemes remain resolvable for historical inspection.
The resolver is not wired into Preview/Generation/calculation in D4A.

## 12. Database constraints and indexes

Verified all 13 check constraints and four foreign keys exist, are enabled and trusted.
All foreign keys use NoAction:

- Policy (SchemeId, SchemeType) → Scheme (SchemeId, SchemeType).
- SSO configuration (PolicyId, SchemeType) → Policy (PolicyId, SchemeType).
- PIT configuration (PolicyId, SchemeType) → Policy (PolicyId, SchemeType).
- PIT bracket PolicyId → PIT configuration PolicyId.

Alternate composite identity keys support those typed relationships. Each configuration
uses PolicyId as its primary key, enforcing at most one configuration of its type per policy.

Business unique indexes: scheme Code; scheme/version; bracket policy/order; bracket
policy/lower bound. Resolution index: scheme/status/effective dates. Additional FK indexes
are generated by EF. Checks protect jurisdiction/type, status/publication timestamps and
required reference metadata, dates, THB, tax year, nonnegative numeric fields and base ranges.
Cross-row full bracket coverage and publication overlap are service validations protected
by the scheme transaction lock rather than ordinary unique indexes.

## 13. API endpoints and contracts

All endpoints use DTOs, asynchronous services and AsNoTracking reads; controllers do not
access DbContext. Swagger includes purposes, request/response schemas and HTTP outcomes.

| Method | Route | Purpose |
|---|---|---|
| GET | /api/statutory-schemes | Active schemes; optional includeInactive=true |
| GET | /api/statutory-schemes/{id} | Scheme including inactive history |
| POST | /api/statutory-schemes | Create scheme (201) |
| PUT | /api/statutory-schemes/{id} | Allowed name/activation updates (200) |
| DELETE | /api/statutory-schemes/{id} | Delete unreferenced empty scheme (204) |
| GET | /api/statutory-schemes/{id}/resolve?governingDate=YYYY-MM-DD | Resolve supplied date; missing date 400, missing scheme 404, ambiguity 409 |
| GET | /api/statutory-policy-versions | List; optional schemeId filter |
| GET | /api/statutory-policy-versions/{id} | Complete typed policy DTO |
| POST | /api/statutory-policy-versions | Create Draft (201) |
| PUT | /api/statutory-policy-versions/{id} | Replace Draft metadata (200) |
| PUT | /api/statutory-policy-versions/{id}/social-security | Replace Draft SSO configuration (200) |
| PUT | /api/statutory-policy-versions/{id}/personal-income-tax | Replace Draft PIT configuration/brackets atomically (200) |
| POST | /api/statutory-policy-versions/{id}/publish | Publish once (200) |
| DELETE | /api/statutory-policy-versions/{id} | Delete safe Draft (204) |

Unknown JSON fields are rejected. Status, timestamps and scheme reassignment are not
editable policy request fields. Lists have deterministic ordering. Typed request lengths
match storage. Responses contain no internal EF navigation properties.

## 14. Statutory results

EmployeePayrollStatutoryResult is deferred to D5/D6 because its required output/audit shape
depends on the actual calculators. No employer results, statutory payroll lines or employee
statutory-profile tables were added.

## 15. Focused verification

The final successful run passed 134 focused API/SQL/Swagger assertions, 97 regression
assertions, and three cleanup integrity comparisons: 234 total checks. These counts include
HTTP response checks as well as semantic assertions; they are not 234 distinct business cases.

Verified scheme uniqueness/type/jurisdiction/activation, Draft CRUD/deletion, date/currency
validation, version uniqueness, incomplete publication, negative/scaled numeric rejection,
typed scheme mismatch, all bracket structure/coverage cases, required references/method
identifiers, immutable Published operations, inactive publication rejection, inclusive
resolution, expired/future no fallback, overlapping/open-ended publication rejection,
competing publishers (200/409), deterministic scheme-lock blocking, and simulated corrupt
coverage (Ambiguous 409 with no chosen policy).

SQL tests rolled back invalid currency/date/rate/type/reference/FK mutations and duplicate
scheme/version/bracket keys. New constraints/indexes were inspected for existence/trust.
Synthetic fixtures were explicitly nonlegal and removed after testing. A PIT bracket EF
tracking defect discovered during verification was fixed with explicit Add tracking; final
creation and repeated replacement/publication tests passed. No schema correction was needed.

## 16. Payroll regressions

Passed D1 change/end/rehire history, D2 first eligible historical targeting/inactive historical
selection/future exclusion, and D3 full-month 28/29/31-day entitlement, joiner/leaver/gaps,
salary increase/decrease segmentation and versioned BasicSalary snapshot checks.

For a synthetic THB 30,000 salary, 100/250 earning assignments and 300 deduction assignment:

| Scenario | GrossPay | Deductions | NetPay |
|---|---:|---:|---:|
| Supplement rules | 35,227.5 | 3,822.75 | 31,404.75 |
| Matching earning/deduction ReplaceAssignment | 35,122.5 | 3,512.25 | 31,610.25 |
| Manual earning adjustment +125 | 35,247.5 | 3,512.25 | 31,735.25 |

Preview and generation agree. Unmatched assignments survive replacement. Generated
BasicSalary/Assignment/PayrollRule provenance and audit metadata remain correct; Manual
uses null SourceId. Manual deletion restores totals. Unknown pay type and replacement
conflicts fail regeneration without changing the complete prior header/lines/audits. Failed
NEW generation leaves no header/lines. Approval/payment and period processing/close/cancel
guards remain intact; Paid payroll cannot regenerate and Closed/Cancelled periods block
operations. Published statutory configuration has no monetary effect.

PayrollCalculationService, BasicSalaryEntitlementService, PayrollRuleEvaluator,
PayrollPreviewService and PayrollGenerationService were not modified.

## 17. Final local database baseline

| Entity | Count |
|---|---:|
| Employees | 1 |
| EmploymentRecords | 1 |
| PayrollComponents | 17 |
| PayrollRules / PayrollRuleTargets / PayrollSettings / PayrollPeriods | 0 each |
| EmployeeCompensations / EmployeePayrollComponentAssignments | 0 each |
| EmployeePayrolls / EmployeePayrollLines | 0 each |
| StatutorySchemes / StatutoryPolicyVersions | 0 each |
| SocialSecurityPolicyConfigurations / PitPolicyConfigurations / PitTaxBrackets | 0 each |

TEST-EMP-001 remains inactive. Full original employee, employment-record and payroll-component
rows were compared before/after and are unchanged. Existing historical payroll baseline
was zero headers/lines. No legal statutory values remain in the database.

## 18. Build, EF and Git checks

- Restore succeeded.
- Release build succeeded with zero warnings and zero errors.
- EF reports no pending model changes.
- git diff --check passed.
- Only the D4A migration was created/applied; prior migrations were not modified.
- No commit or push.

## 19. Remaining D4B/D5/D6 decisions

- D4B: employee enrollment/classification and tax declarations/claims, eligibility and history.
- D5: SSO calculator compatibility, approved legal sources/parameters and governing date,
  employee/employer output and immutable statutory result shape.
- D6: PIT annualization versus cumulative withholding, YTD/opening balances, claims,
  withholding-method compatibility and rounding/boundary interpretation by the calculator.
- Future controlled successor workflow if an open-ended Published version must be ended.
- Production authentication/RBAC and restricted direct database writes.

Legal truth is not validated by D4A; official numeric policy data must be separately reviewed
before use. Future calculators must reject incompatible method versions instead of silently
reinterpreting old policy contracts.
