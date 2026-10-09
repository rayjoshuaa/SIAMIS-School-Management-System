# D5A — final implementation and live verification report

## Result

Approved migration **20261001032904_AddSsoWageTreatmentClassification** applied successfully with EF Core 10.0.12 to **localhost / SIAMIS**, using Windows integrated authentication and TrustServerCertificate=True. The Development connection configuration was verified, and the EF command supplied the same explicit local connection. SQL reported server name Ray and database SIAMIS. The migration is recorded in __EFMigrationsHistory (26 entries total).

No production connection, database creation, additional migration, SSO monetary calculation, statutory governing-date/partial-month/rounding decision, geographic handling, or legal earning classification was introduced. No commit/push.

## Exact schema changes and classifications

| Table                | New column               | SQL storage  | Nullable | Default |
| -------------------- | ------------------------ | ------------ | -------- | ------- |
| PayrollComponents    | SsoWageTreatment         | nvarchar(20) | No       | Unknown |
| EmployeePayrollLines | SsoWageTreatmentSnapshot | nvarchar(20) | No       | Unknown |

Check constraints:

- CK_PayrollComponents_SsoWageTreatment
- CK_EmployeePayrollLines_SsoWageTreatmentSnapshot

Both allow Unknown / Included / Excluded, are enabled and trusted (is_disabled=0, is_not_trusted=0), and rejected actual invalid SQL writes with error 547. Invalid writes targeted synthetic fixtures and were rolled back.

The migration also sets only the new field to Unknown for the 17 seeded components. No tables, indexes, relationships, existing provenance checks or monetary columns were added/changed. Existing/unclassified historical lines receive Unknown rather than inferred legal treatment. No preexisting payroll lines were present in Development.

**Final classifications: Unknown=17, Included=0, Excluded=0.** No existing component was assigned Included/Excluded during testing. These values were exercised only on new synthetic components, all removed afterward. Basic Salary retained its existing Unknown classification and non-taxable setting throughout.

## API and historical snapshot contract

- Component create/update accepts optional canonical SsoWageTreatment. Omitted/null create defaults to Unknown; omitted/null update preserves the existing value. Invalid treatment returns HTTP 400. List/detail/create/update responses expose the classification.
- Existing component filters (type, taxable, text search) were verified. No dedicated SsoWageTreatment filter was implemented.
- BasicSalary, Assignment and PayrollRule lines snapshot their source component's treatment. The evaluator carries this metadata without changing applicability/targeting.
- Preview exposes SsoWageTreatmentSnapshot; Generation persists it; stored line reads return historical values rather than current component values.
- Line snapshots preserve SourceType/SourceId and existing rule/Basic Salary metadata. Updating live classification leaves the complete existing generated payroll unchanged. Deliberate permitted regeneration uses current classification.
- Manual creation snapshots the selected component. Amount-only updates preserve that snapshot; explicit component replacement captures the new component's treatment. Manual provenance remains Manual/null. The caller cannot select classification snapshot fields. Generated-line immutability remains in place.
- Metadata is copied on deduction lines for consistent snapshots; D5A does not treat deductions as contribution wages or consume this classification in formulas.

## Verification outcomes

| Verification                                         | Result                                                                                                                                  |
| ---------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------- |
| EF migration application                             | Successful, only approved D5A migration applied                                                                                         |
| Actual SQL columns/defaults/check constraints        | Verified; enabled and trusted                                                                                                           |
| Synthetic component default/explicit classifications | POST 201; GET/update 200; SQL persistence verified                                                                                      |
| Invalid component classification                     | Create/update HTTP 400                                                                                                                  |
| SQL invalid component/line classifications           | Rejected with 547; rolled back                                                                                                          |
| Initial Preview/Generation                           | Matched amounts, classification snapshots, and provenance                                                                               |
| Live component changes                               | Entire stored payroll remained identical                                                                                                |
| Deliberate regeneration                              | Current classifications; unchanged amounts                                                                                              |
| Earning/Deduction Supplement                         | Passed                                                                                                                                  |
| Earning/Deduction ReplaceAssignment                  | Passed; only matching assignment suppressed                                                                                             |
| Earning/Deduction Percentage formulas                | Passed                                                                                                                                  |
| Conflicting replacements, failed NEW generation      | Failed safely, no header/orphan lines                                                                                                   |
| Failed forced regeneration                           | Previous complete header/totals/line IDs/audits preserved                                                                               |
| Manual create/update/component replacement/delete    | Snapshot behavior and totals reconciliation passed                                                                                      |
| Generated-line protection                            | Manual deletion rejected (409)                                                                                                          |
| Payroll lifecycle                                    | Approved/Paid/Cancelled regeneration protected; Approved adjustment/Paid cancellation rejected                                          |
| Payroll-period lifecycle                             | Processing/unfinished-close, Closed/Cancelled mutation guards passed                                                                    |
| D3 salary entitlement                                | Full-month 30,000; September 29 joiner 2,000; Basic Salary audit retained                                                               |
| D4A policy behavior                                  | Incomplete publication rejected; complete synthetic publication/resolution worked; Published edit rejected                              |
| D4B behavior                                         | Unknown absent enrollment; inclusive Applicable resolution; overlapping enrollment rejected; declaration opening/Verified guards passed |
| Applicable enrollment + Published policy             | Preview/Generation amounts unchanged; no SSO calculation                                                                                |
| Focused regression runner                            | 54 assertions passed                                                                                                                    |
| Live Development API/SQL runner                      | 70 assertions passed                                                                                                                    |
| Cleanup                                              | Exact pre-fixture contents restored for all 49 application tables                                                                       |
| dotnet restore SIAMIS.sln                            | Successful                                                                                                                              |
| Release build                                        | Successful, 0 warnings, 0 errors                                                                                                        |
| EF has-pending-model-changes                         | No changes since last migration                                                                                                         |
| git diff --check                                     | Passed                                                                                                                                  |

Live monetary example: BasicSalary=30,000; GrossPay=33,300; TaxableEarnings=3,300; TotalDeductions=500; NetPay=32,800. TaxableEarnings respects the preexisting non-taxable Basic Salary; this setting was not modified. Switching synthetic classifications Included/Excluded did not change these amounts. D3 and existing formulas remain unchanged.

Synthetic D4A policy values were temporary test inputs explicitly labeled non-legal, not seeded/retained statutory parameters. Resolver dates were caller-supplied test dates, not a payroll governing-date decision.

## Cleanup and Development baseline

Cleanup deletes only recorded fixture identifiers, including synthetic finalized payrolls and Published policies that ordinary administrative APIs intentionally protect. It runs in a SQL transaction. A before/after comparison of all row contents across all 49 application tables confirmed exact restoration, including existing employee/core data, component classifications and timestamps. No employee was created or updated. TEST-EMP-001 (433f2c1a-6222-494f-a64f-cd0c31126dc4) remains inactive.

The temporary dedicated API process on localhost:5155 was stopped. Existing API processes were not stopped or restarted. The database intentionally retains the approved D5A schema and migration history entry.

| Table                               | Final rows |
| ----------------------------------- | ---------: |
| AddressTypes                        |          3 |
| Attendance                          |          0 |
| AttendanceStatuses                  |         11 |
| ContractTypes                       |          7 |
| Countries                           |         13 |
| Departments                         |         12 |
| Designations                        |         20 |
| DocumentTypes                       |         15 |
| EmergencyContacts                   |          0 |
| EmployeeAddresses                   |          0 |
| EmployeeCompensations               |          0 |
| EmployeeContacts                    |          0 |
| EmployeeContracts                   |          0 |
| EmployeeDocuments                   |          0 |
| EmployeeHistory                     |          0 |
| EmployeeLeave                       |          0 |
| EmployeePayrollComponentAssignments |          0 |
| EmployeePayrollLines                |          0 |
| EmployeePayrolls                    |          0 |
| EmployeePerformance                 |          0 |
| EmployeeStatutoryEnrollments        |          0 |
| EmployeeTaxClaims                   |          0 |
| EmployeeTaxDeclarationSelections    |          0 |
| EmployeeTaxDeclarations             |          0 |
| EmployeeTaxOpeningBalances          |          0 |
| EmployeeTaxProfiles                 |          0 |
| Employees                           |          1 |
| EmploymentRecords                   |          1 |
| EmploymentStatuses                  |          9 |
| EmploymentTypes                     |          6 |
| Genders                             |          4 |
| HiringSources                       |         10 |
| LeaveTypes                          |         10 |
| Locations                           |          5 |
| MaritalStatuses                     |          6 |
| Nationalities                       |         13 |
| PayTypes                            |          6 |
| PayrollComponents                   |         17 |
| PayrollPeriods                      |          0 |
| PayrollRuleTargets                  |          0 |
| PayrollRules                        |          0 |
| PayrollSettings                     |          0 |
| PerformanceRatings                  |          5 |
| PitPolicyConfigurations             |          0 |
| PitTaxBrackets                      |          0 |
| SocialSecurityPolicyConfigurations  |          0 |
| StatutoryPolicyVersions             |          0 |
| StatutorySchemes                    |          0 |
| TeacherProfiles                     |          0 |
| __EFMigrationsHistory               |         26 |

Application tables=49. Master-data rows=172. Employees=1; EmploymentRecords=1; PayrollComponents=17. Rules/targets/settings/periods/compensations/assignments/payroll headers/lines and all D4A/D4B input tables are empty.

## Test execution and scope

The package-free C# regression runner is included in the solution:

```powershell
.\.dotnet\dotnet.exe run --project tests/SIAMIS.Payroll.RegressionTests -c Release
```

The retained live verification script is `tests/verify_d5a_live.py`. It requires the migrated local Development SIAMIS database and a fresh Development API on localhost:5155 configured for localhost/SIAMIS. It uses standard Python libraries and sqlcmd Windows authentication. It creates synthetic fixtures, validates API/SQL behavior and removes its recorded fixtures in finally. Run it only against the stated clean Development baseline:

```powershell
python tests/verify_d5a_live.py
```

Harness issues encountered during preparation (SQL JSON chunking/truncation, expected taxable totals, and an API launch path containing spaces) were corrected. No application regression was found; no production implementation changes were required by this verification. These are the focused requested regressions, not an exhaustive rerun of every historical checkpoint.

## Complete live assertion record

1. baseline verified before fixtures.
2. D5A migration recorded.
3. live columns required nvarchar(20), default Unknown.
4. live check constraints enabled and trusted.
5. 17 existing components Unknown.
6. component create/detail Unknown.
7. SQL component persistence Unknown.
8. component create/detail Unknown.
9. SQL component persistence Unknown.
10. component create/detail Included.
11. SQL component persistence Included.
12. component create/detail Excluded.
13. SQL component persistence Excluded.
14. invalid create HTTP 400.
15. invalid update HTTP 400.
16. classification update persists.
17. omitted update preserves classification.
18. null update preserves classification.
19. existing type/tax/search filters return classification.
20. initial generation succeeds.
21. baseline salary/gross/tax/deductions/net unchanged.
22. BasicSalary live snapshot Unknown and truthful source.
23. Assignment live snapshot/provenance.
24. PayrollRule live snapshot/provenance.
25. Preview/Generation amounts and classifications parity.
26. SQL generated line snapshots persisted.
27. live classification update leaves entire historical payroll unchanged.
28. deliberate regeneration succeeds.
29. regeneration uses current classification without amount changes.
30. regenerated Preview/Generation parity.
31. D3 full-month entitlement/audit unchanged.
32. D3 September 29 joiner ThirtyDay salary entitlement unchanged.
33. SQL constraint rejects invalid SsoWageTreatment.
34. SQL constraint rejects invalid SsoWageTreatmentSnapshot.
35. earning ReplaceAssignment suppresses matching assignment only.
36. deduction Supplement unchanged.
37. deduction ReplaceAssignment unchanged.
38. failed NEW generation leaves no header.
39. failed NEW generation no orphan lines.
40. failed regeneration preserves complete header/totals/IDs/lines/audits.
41. earning Percentage BasicSalary formula unchanged.
42. deduction Percentage GrossPay formula unchanged.
43. manual creation server source and component snapshot.
44. manual snapshot persisted in SQL.
45. manual amount update preserves historical classification.
46. manual update reconciles totals preserving BasicSalary.
47. manual explicit component replacement snapshots new component without source change.
48. manual deletion restores calculated totals.
49. generated line manual mutation protected.
50. unfinished period closure rejected.
51. Approved payroll regeneration protected.
52. Approved payroll manual adjustment rejected.
53. Paid payroll regeneration protected.
54. Paid payroll cancellation rejected.
55. Closed payroll period mutation protected.
56. new generation after removing conflict succeeds.
57. Cancelled payroll regeneration protected.
58. Cancelled period preview protected.
59. D4A incomplete publication rejected.
60. D4A publication/resolution unchanged; caller date only.
61. D4A Published immutability unchanged.
62. D4B absent enrollment Unknown.
63. D4B inclusive enrollment resolution unchanged.
64. D4B overlapping enrollment rejected.
65. Applicable enrollment plus Published policy introduces no SSO monetary calculation.
66. Generation with Applicable enrollment remains unchanged.
67. D4B declaration needs explicit opening state.
68. D4B Verified declaration immutability unchanged.
69. cleanup restores exact pre-fixture contents of all 49 application tables.
70. TEST-EMP-001 remains inactive.

## Complete changed-file list (D5A implementation and verification)

- [README.md](FOUNDATION-README.md)
- [SIAMIS.sln](../../../SIAMIS.sln)
- [src/SIAMIS.Application/MasterData/PayrollComponentContracts.cs](../../../src/SIAMIS.Application/MasterData/PayrollComponentContracts.cs)
- [src/SIAMIS.Application/Payroll/EmployeePayrollContracts.cs](../../../src/SIAMIS.Application/Payroll/EmployeePayrollContracts.cs)
- [src/SIAMIS.Application/Payroll/PayrollCalculationContracts.cs](../../../src/SIAMIS.Application/Payroll/PayrollCalculationContracts.cs)
- [src/SIAMIS.Application/Payroll/PayrollPreviewContracts.cs](../../../src/SIAMIS.Application/Payroll/PayrollPreviewContracts.cs)
- [src/SIAMIS.Application/Payroll/PayrollRuleEvaluationContracts.cs](../../../src/SIAMIS.Application/Payroll/PayrollRuleEvaluationContracts.cs)
- [src/SIAMIS.Domain/Entities/MasterData/MasterDataEntity.cs](../../../src/SIAMIS.Domain/Entities/MasterData/MasterDataEntity.cs)
- [src/SIAMIS.Domain/Entities/Payroll/EmployeePayrollLine.cs](../../../src/SIAMIS.Domain/Entities/Payroll/EmployeePayrollLine.cs)
- [src/SIAMIS.Infrastructure/Configurations/AdditionalMasterDataConfigurations.cs](../../../src/SIAMIS.Infrastructure/Configurations/AdditionalMasterDataConfigurations.cs)
- [src/SIAMIS.Infrastructure/Configurations/EmployeePayrollConfigurations.cs](../../../src/SIAMIS.Infrastructure/Configurations/EmployeePayrollConfigurations.cs)
- [src/SIAMIS.Infrastructure/Migrations/SIAMISDbContextModelSnapshot.cs](../../../src/SIAMIS.Infrastructure/Migrations/SIAMISDbContextModelSnapshot.cs)
- [src/SIAMIS.Infrastructure/Services/EmployeePayrollService.cs](../../../src/SIAMIS.Infrastructure/Services/EmployeePayrollService.cs)
- [src/SIAMIS.Infrastructure/Services/PayrollCalculationService.cs](../../../src/SIAMIS.Infrastructure/Services/PayrollCalculationService.cs)
- [src/SIAMIS.Infrastructure/Services/PayrollComponentService.cs](../../../src/SIAMIS.Infrastructure/Services/PayrollComponentService.cs)
- [src/SIAMIS.Infrastructure/Services/PayrollGenerationService.cs](../../../src/SIAMIS.Infrastructure/Services/PayrollGenerationService.cs)
- [src/SIAMIS.Infrastructure/Services/PayrollPreviewService.cs](../../../src/SIAMIS.Infrastructure/Services/PayrollPreviewService.cs)
- [src/SIAMIS.Infrastructure/Services/PayrollRuleEvaluator.cs](../../../src/SIAMIS.Infrastructure/Services/PayrollRuleEvaluator.cs)
- [D5A-REPORT.md](D5A-REPORT.md)
- [src/SIAMIS.Infrastructure/Migrations/20261001032904_AddSsoWageTreatmentClassification.Designer.cs](../../../src/SIAMIS.Infrastructure/Migrations/20261001032904_AddSsoWageTreatmentClassification.Designer.cs)
- [src/SIAMIS.Infrastructure/Migrations/20261001032904_AddSsoWageTreatmentClassification.cs](../../../src/SIAMIS.Infrastructure/Migrations/20261001032904_AddSsoWageTreatmentClassification.cs)
- [tests/SIAMIS.Payroll.RegressionTests/Program.cs](../../../tests/SIAMIS.Payroll.RegressionTests/Program.cs)
- [tests/SIAMIS.Payroll.RegressionTests/SIAMIS.Payroll.RegressionTests.csproj](../../../tests/SIAMIS.Payroll.RegressionTests/SIAMIS.Payroll.RegressionTests.csproj)
- [tests/verify_d5a_live.py](../../../tests/verify_d5a_live.py)

## Deferred decisions

SSO wage aggregation, statutory governing date, partial-month treatment, rounding, geographic applicability, and real component Included/Excluded classifications remain unresolved and unimplemented. Future statutory calculation must consume explicit classification safely; ordinary payroll currently remains valid with Unknown. No PIT, provident fund, authentication, or other scope was added.
