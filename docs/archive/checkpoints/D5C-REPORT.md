# D5C - Section 33 monetary integration and verification

## 1. Completion

D5C is implemented and verified against local Development **localhost/SIAMIS** only.
The existing D5B working tree was retained. No commit or push was performed.
No legal numeric values or legal Included/Excluded classifications were seeded.
All live monetary inputs were temporary synthetic verification fixtures.

## 2. Shared calculator and resolution

`Section33Calculator` implements the approved SSO-TH-V1 contract using the D5B
contribution-wage resolver. `Section33PayrollService` resolves configuration and
composes that result onto generic payroll. Preview and Generation share both.

The complete calendar-month requirement remains. PayrollPeriod.EndDate resolves
Published TH-SSO-33 policy and explicit enrollment; PayDate is never used.
ContributionMonth is stored as YYYY-MM. EndDate represents the contribution month
in this repository, not a legal claim that the last day itself creates liability.

Applicable requires effective enrollment, Published SocialSecurity policy under
TH-SSO-33/TH, currency THB, classification 33 and exact SSO-TH-V1 support.
Unknown/missing enrollment or missing/Draft/unsupported policy fails clearly.
Explicit NotApplicable creates no automatic SSO result/deduction and preserves
generic behavior. No nationality inference or currency conversion is introduced.

## 3. Approved money contract

- Wage is the sum of final Included Earning amounts, once per final line.
- Excluded earnings and deductions do not contribute. Applicable Unknown fails
  with component code, source identity and resolver issue details.
- Positive wage uses `min(max(wage, policy minimum), policy maximum)`.
- Zero wage uses zero base and contribution with explicit ZeroWage metadata.
- D3 determines partial-month earnings; SSO monthly bounds are not prorated.
- D4A stores percentage points: raw employee amount = base * EmployeeRate / 100.
- An explicit decimal helper discards fractions below .50 and rounds .50 or above
  upward to a whole baht. Raw and final amounts are both preserved.
- Compatible V1 EmployerAmount equals final rounded EmployeeAmount.
- Decimal/range overflow or total deductions exceeding gross pay fails safely.

D4A retains independent employee/employer rate fields. Publication validates exact
equality only for SSO-TH-V1, after existing numeric validation. The calculator
rejects already Published incompatible policies without modifying them. Both
rates are preserved historically. No equality constraint was added to D4A policy
configuration. A focused check in the new typed V1 result enforces its equal
snapshotted rates and final employee/employer amounts; it does not constrain
future policy configurations.

## 4. Canonical deduction ownership and totals

DEDUCT-001 presents a positive automatic employee deduction. Resolution uses code,
never display name or component amount. Exactly one active Deduction component
is required before producing that line; missing/inactive/wrong-type configuration
fails without automatic repair or fallback.

For Applicable employees, a final Assignment/PayrollRule deduction targeting
DEDUCT-001 fails clearly. This is checked after existing rule formulas,
Supplement/ReplaceAssignment and financial eligibility. Skipped rules do not
conflict. Nothing is silently suppressed, reinterpreted or counted twice.
NotApplicable retains generic DEDUCT-001 behavior.

Positive employee amount creates one Deduction line with SourceType Statutory
and SourceId equal to its actual historical result ID. Component classification
flags remain copied snapshots, not formula authority. Zero contribution stores
a result but creates no zero-value line, matching the existing AmountPositive
constraint. Employer contribution creates no employee line.

Employee amount alone increases TotalDeductions and decreases NetPay. It does
not change BasicSalary, GrossPay or TaxableEarnings. Employer amount changes none
of the employee payroll totals.

## 5. Migration and exact database changes

Applied **20261001044414_AddSection33PayrollResults** through EF Core, explicitly
targeting localhost/SIAMIS Development with Windows integrated authentication.
Application tables increased from 49 to 51. No other migration was created or
applied in this task, and existing migrations were not changed.

- EmployeePayrollStatutoryResults: result/payroll/scheme/policy/enrollment GUIDs,
  method version, month, governing date, currency, versioned JSON and UTC CreatedAt.
- EmployeePayrollSocialSecurityResults: shared primary key, wage/base/bounds,
  both rates, raw employee amount and final employee/employer amounts.
- Monetary/rate fields use decimal(19,4); raw uses decimal(38,10), preserving
  four-decimal base/rate multiplication and division precision.
- Five NoAction foreign keys: payroll, scheme, policy, enrollment and typed
  child to parent result. No accidental cascade deletes.
- Unique payroll/scheme index and supporting policy/enrollment/scheme indexes.
- Six result checks: JSON, V1/THB, month/date agreement, nonnegative values and
  bounds, zero/positive base behavior and V1 final amounts.
- Existing line source/ID check now allows Statutory with non-null SourceId.
- SourceId remains snapshot data with **no foreign key**. No circular persistence.
- No employee or master-data seed changes.

Calculation JSON also preserves exact policy version/reference/effective dates,
enrollment interval/identity, classification, both rates, raw/final amounts,
zero-wage behavior, base/rounding method identifiers, and final input
classification/provenance. History does not rely solely on live joins. NoAction
also prevents referenced configuration from being hard-deleted.

## 6. Transactions and historical behavior

Generation retains per-employee Serializable transactions and parent period and
employee locks. Basic Salary classification is refreshed inside that transaction,
so an earlier batch read is not treated as a committed audit snapshot. SSO is
validated before removal of an old payroll. New header, lines and parent/child
statutory results are committed atomically. Failed regeneration preserves the
complete previous header, totals, lines and statutory audit snapshots.

Forced regeneration replaces the header and all lines/results, **including Manual
adjustments**. It does not preserve Manual lines. Finalized payroll and period
protections remain in place.

Preview persists nothing. Its result identity is transient, EmployeePayrollId is
empty and CreatedAt is not a persisted audit timestamp. Monetary fields, final
earning inputs and calculation JSON match Generation. Generation's Statutory
SourceId references the actual persisted result.

## 7. API, manual protection and Swagger

Existing payroll detail GET adds `statutoryResults`; Preview adds `socialSecurity`.
Responses use DTOs. There is no writable statutory-result API. Swagger exposes
the response extensions and documents manual conflicts and regeneration.

Once an SSO result exists, manual earning create, amount/component update,
type conversion involving an old or proposed Earning, and earning deletion return
409 with instructions to change underlying inputs and regenerate. Rejected
mutations preserve the entire stored snapshot. Manual deductions retain existing
lifecycle/reconciliation behavior. Generated Statutory lines cannot be updated
or deleted through Manual APIs or masqueraded as manually generated provenance.

## 8. Verification results

| Check                                                        | Result                       |
| ------------------------------------------------------------ | ---------------------------- |
| Restore                                                      | Passed                       |
| Final Release build                                          | Passed: 0 warnings, 0 errors |
| Focused D5A/D5B/D5C assertions                               | 111 passed                   |
| Existing live payroll regression checks on final Release API | 70 passed                    |
| D5C live API/SQL assertions                                  | 306 passed                   |
| Actual schema, FKs, checks and migration history             | Verified                     |
| Swagger response metadata and behavior documentation         | Verified                     |
| EF pending-model-changes                                     | None                         |
| git diff --check                                             | Passed                       |

D5C live coverage includes equal/unequal publication, future method validation,
defensive incompatible Published policy rejection, Included/Excluded/Unknown,
NotApplicable/missing enrollment, Published/Draft/missing/unsupported policy,
THB enforcement, EndDate versus PayDate, every base bound and zero wage,
rounding 1.49/1.50/1.51, joiner/leaver/gap salary and unprorated SSO bounds.

Also verified canonical component missing/inactive/wrong type, final assignment
and rule conflicts, financially skipped rule, NotApplicable generic deduction,
exactly one employee deduction, employer isolation, structured SQL persistence,
Preview read-only behavior/parity, complete history, current-config regeneration,
failed NEW generation and failed regeneration complete-snapshot preservation.

Manual tests cover creation, amount/component changes, both type directions,
deletion, generated Statutory immutability, allowed deduction mutations, unchanged
state after rejection, SQL source-ID/fractional-contribution rejection and removal
of Manual adjustments on regeneration. Both Earning and Deduction Supplement and
ReplaceAssignment were exercised under Applicable SSO, including matching-only
suppression, source metadata and duplicate replacement failures. Approved/Paid
regeneration protection was verified with persisted SSO results.

The existing 70 live checks additionally verify component classification CRUD,
BasicSalary/Assignment/PayrollRule snapshots, percentage formulas, manual
reconciliation, Approved/Paid/Cancelled payroll, processing/closed/cancelled
periods, D4A publication/immutability and D4B enrollment/declaration behavior.
Those non-statutory fixtures now configure explicit canonical NotApplicable.
The independent D4A/D4B enrollment test remains separate from that canonical
applicability. D1/D2 period eligibility/context and D3 full/partial/gap scenarios
were exercised; unchanged workflow implementation files were confirmed by diff.

Initial test-harness errors were corrected: expected schema column count and
omitted-null restoration. Unknown diagnostics were improved. The null cleanup
failure was repaired with only the known original employment EndDate/IsCurrent
and Basic Salary classification. The final complete rerun compared all 51
application tables exactly, including nulls and timestamps, and passed.

## 9. Final baseline and remaining boundaries

Employees 1, EmploymentRecords 1, PayrollComponents 17 (all Unknown).
TEST-EMP-001 is inactive with its original open/current employment record.
All 172 master rows remain. Payroll processing, assignments, compensation,
statutory configuration/enrollments/results and employee child fixtures are zero.
The approved D5C schema and migration history remain applied. No orphan lines.

No implementation blocker remains within D5C. Real operation still requires
externally reviewed Published policy inputs, explicit enrollment and approved
wage classifications. The clean baseline contains none; missing configuration
fails instead of silently calculating or exempting employees.
Geographic targeting, PIT, Provident Fund, payslips, authentication/RBAC and
configurable scheme/component binding remain deferred. Published policies are
never repaired automatically. No production/VPS access, commit or push.
Only the temporary verification API process was restarted and stopped.

## 10. Complete final application-table counts

The following counts were verified by both live suites after cleanup.

| Application table                    | Final rows |
| ------------------------------------ | ---------: |
| AddressTypes                         |          3 |
| Attendance                           |          0 |
| AttendanceStatuses                   |         11 |
| ContractTypes                        |          7 |
| Countries                            |         13 |
| Departments                          |         12 |
| Designations                         |         20 |
| DocumentTypes                        |         15 |
| EmergencyContacts                    |          0 |
| EmployeeAddresses                    |          0 |
| EmployeeCompensations                |          0 |
| EmployeeContacts                     |          0 |
| EmployeeContracts                    |          0 |
| EmployeeDocuments                    |          0 |
| EmployeeHistory                      |          0 |
| EmployeeLeave                        |          0 |
| EmployeePayrollComponentAssignments  |          0 |
| EmployeePayrollLines                 |          0 |
| EmployeePayrollSocialSecurityResults |          0 |
| EmployeePayrollStatutoryResults      |          0 |
| EmployeePayrolls                     |          0 |
| EmployeePerformance                  |          0 |
| EmployeeStatutoryEnrollments         |          0 |
| EmployeeTaxClaims                    |          0 |
| EmployeeTaxDeclarationSelections     |          0 |
| EmployeeTaxDeclarations              |          0 |
| EmployeeTaxOpeningBalances           |          0 |
| EmployeeTaxProfiles                  |          0 |
| Employees                            |          1 |
| EmploymentRecords                    |          1 |
| EmploymentStatuses                   |          9 |
| EmploymentTypes                      |          6 |
| Genders                              |          4 |
| HiringSources                        |         10 |
| LeaveTypes                           |         10 |
| Locations                            |          5 |
| MaritalStatuses                      |          6 |
| Nationalities                        |         13 |
| PayTypes                             |          6 |
| PayrollComponents                    |         17 |
| PayrollPeriods                       |          0 |
| PayrollRuleTargets                   |          0 |
| PayrollRules                         |          0 |
| PayrollSettings                      |          0 |
| PerformanceRatings                   |          5 |
| PitPolicyConfigurations              |          0 |
| PitTaxBrackets                       |          0 |
| SocialSecurityPolicyConfigurations   |          0 |
| StatutoryPolicyVersions              |          0 |
| StatutorySchemes                     |          0 |
| TeacherProfiles                      |          0 |

## 11. Complete working-tree changed-file list

Includes preserved D5B work; no claim that it was reimplemented. Ignored build, log and result artifacts are excluded.

- [D5B-REPORT.md](D5B-REPORT.md) (preserved D5B foundation)
- [D5C-REPORT.md](D5C-REPORT.md)
- [README.md](FOUNDATION-README.md)
- [src/SIAMIS.Api/Controllers/EmployeePayrollsController.cs](../../../src/SIAMIS.Api/Controllers/EmployeePayrollsController.cs)
- [src/SIAMIS.Api/Controllers/PayrollPeriodsController.cs](../../../src/SIAMIS.Api/Controllers/PayrollPeriodsController.cs)
- [src/SIAMIS.Api/Program.cs](../../../src/SIAMIS.Api/Program.cs)
- [src/SIAMIS.Application/Payroll/EmployeePayrollContracts.cs](../../../src/SIAMIS.Application/Payroll/EmployeePayrollContracts.cs)
- [src/SIAMIS.Application/Payroll/PayrollPreviewContracts.cs](../../../src/SIAMIS.Application/Payroll/PayrollPreviewContracts.cs)
- [src/SIAMIS.Application/Payroll/Section33CalculationContracts.cs](../../../src/SIAMIS.Application/Payroll/Section33CalculationContracts.cs)
- [src/SIAMIS.Application/Payroll/Section33ContributionWageContracts.cs](../../../src/SIAMIS.Application/Payroll/Section33ContributionWageContracts.cs) (preserved D5B foundation)
- [src/SIAMIS.Domain/Entities/Payroll/EmployeePayrollStatutoryResult.cs](../../../src/SIAMIS.Domain/Entities/Payroll/EmployeePayrollStatutoryResult.cs)
- [src/SIAMIS.Infrastructure/Configurations/EmployeePayrollConfigurations.cs](../../../src/SIAMIS.Infrastructure/Configurations/EmployeePayrollConfigurations.cs)
- [src/SIAMIS.Infrastructure/Configurations/EmployeePayrollStatutoryConfigurations.cs](../../../src/SIAMIS.Infrastructure/Configurations/EmployeePayrollStatutoryConfigurations.cs)
- [src/SIAMIS.Infrastructure/Data/SIAMISDbContext.cs](../../../src/SIAMIS.Infrastructure/Data/SIAMISDbContext.cs)
- [src/SIAMIS.Infrastructure/Migrations/20261001044414_AddSection33PayrollResults.Designer.cs](../../../src/SIAMIS.Infrastructure/Migrations/20261001044414_AddSection33PayrollResults.Designer.cs)
- [src/SIAMIS.Infrastructure/Migrations/20261001044414_AddSection33PayrollResults.cs](../../../src/SIAMIS.Infrastructure/Migrations/20261001044414_AddSection33PayrollResults.cs)
- [src/SIAMIS.Infrastructure/Migrations/SIAMISDbContextModelSnapshot.cs](../../../src/SIAMIS.Infrastructure/Migrations/SIAMISDbContextModelSnapshot.cs)
- [src/SIAMIS.Infrastructure/Services/EmployeePayrollService.cs](../../../src/SIAMIS.Infrastructure/Services/EmployeePayrollService.cs)
- [src/SIAMIS.Infrastructure/Services/PayrollGenerationService.cs](../../../src/SIAMIS.Infrastructure/Services/PayrollGenerationService.cs)
- [src/SIAMIS.Infrastructure/Services/PayrollPreviewService.cs](../../../src/SIAMIS.Infrastructure/Services/PayrollPreviewService.cs)
- [src/SIAMIS.Infrastructure/Services/Section33Calculator.cs](../../../src/SIAMIS.Infrastructure/Services/Section33Calculator.cs)
- [src/SIAMIS.Infrastructure/Services/Section33ContributionWageResolver.cs](../../../src/SIAMIS.Infrastructure/Services/Section33ContributionWageResolver.cs) (D5B resolver retained; integration documentation updated)
- [src/SIAMIS.Infrastructure/Services/Section33PayrollService.cs](../../../src/SIAMIS.Infrastructure/Services/Section33PayrollService.cs)
- [src/SIAMIS.Infrastructure/Services/StatutoryPolicyService.cs](../../../src/SIAMIS.Infrastructure/Services/StatutoryPolicyService.cs)
- [tests/SIAMIS.Payroll.RegressionTests/Program.cs](../../../tests/SIAMIS.Payroll.RegressionTests/Program.cs)
- [tests/SIAMIS.Payroll.RegressionTests/Section33CalculationRegressionTests.cs](../../../tests/SIAMIS.Payroll.RegressionTests/Section33CalculationRegressionTests.cs)
- [tests/SIAMIS.Payroll.RegressionTests/Section33WageRegressionTests.cs](../../../tests/SIAMIS.Payroll.RegressionTests/Section33WageRegressionTests.cs) (preserved D5B foundation)
- [tests/verify_d5a_live.py](../../../tests/verify_d5a_live.py)
- [tests/verify_d5c_live.py](../../../tests/verify_d5c_live.py)
