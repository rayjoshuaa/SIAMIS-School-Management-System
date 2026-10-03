# D7 — Payroll operations and payslip foundation

Completed 2026-10-03. Implementation follows the original D7 request and the approved contract review.
No commit or push was performed. Only localhost/SIAMIS Development was migrated and tested.

## Result

D7 is complete. Generated Calculated payroll has a structured payslip before approval/payment when employer configuration is present. Historical presentation is frozen; successful manual monetary changes refresh only the stored financial content. Regeneration replaces the editable payroll and payslip atomically. Approval/payment never recalculate or refresh payslips.

## API additions and read models

| Operation | Behavior |
|---|---|
| GET /api/organization-profile | Configured singleton profile; 404 if unconfigured. |
| PUT /api/organization-profile | Creates/updates the singleton. DisplayName is required; address lines, phone and email optional. Identity and timestamps are server controlled. |
| GET /api/employee-payrolls/{id}/payslip | Structured snapshot and separate current payroll status/timestamps; 404 missing payroll, 409 ineligible/inconsistent/not ready. |
| GET /api/employee-payrolls/{id}/review | Machine-readable finding codes, human explanations, CanApprove and PayslipReady. No writes/calculators. |
| GET /api/payroll-periods/{id}/summary | Actual header/employee counts, status counts, monetary groups by D3 currency and unresolved-currency count. |

Existing payroll detail gains an Operations object: frozen presentation when available, authoritative currency, grouped earning/deduction lines, concise SSO/PIT amounts and review findings. Existing detail/history uses frozen employee/period labels where available. Without a snapshot, current display facts are explicitly marked HasFrozenPresentation=false. Existing filtered/paginated history is reused; no duplicate employee history route was created. Ordering remains deterministic.

Swagger verified all five new operations, purpose text and HTTP response codes, plus the extended detail schema.

## Organization identity

OrganizationProfiles has one fixed singleton GUID, required DisplayName, optional AddressLine1/AddressLine2/Phone/Email and UTC-compatible timestamps. Its primary key and check constraint permit zero or one configured profile. No tenants, campuses, tax identifiers, real identity seeds or hard-coded employer defaults were added.

No profile: payroll generation succeeds without a payslip; review/GET/approval clearly report missing formal snapshot/configuration. Configuring a profile later does not backfill existing payroll. Deliberate successful regeneration is required for an editable payroll lacking a snapshot.

The final Development profile is intentionally unconfigured (zero rows) after fixture cleanup. Configure the real employer through PUT /api/organization-profile before generating payroll intended for a formal payslip.

## Payslip storage and eligibility

EmployeePayslips stores EmployeePayslipId, EmployeePayrollId, SnapshotVersion, SnapshotJson, CreatedAt and UpdatedAt. A unique owner index enforces one current snapshot per payroll. JSON and format-version checks are enforced by SQL Server. The payroll FK uses NoAction. Version 1 identifies the JSON format; UpdatedAt records financial refreshes. No PDF/HTML binary, snapshot configuration FKs or historical backfill was added.

Calculated/Approved/Paid payrolls require a complete consistent snapshot for formal access. Draft is ineligible. Cancelled payroll retains an existing snapshot and returns it with separate Cancelled lifecycle status; cancellation does not create or delete calculation evidence.

Snapshot content includes:

- frozen organization reference/name/address;
- employee ID/code and canonical display name;
- first-eligible employment record/context date, department/designation/type/location IDs and labels;
- period ID/code/name/dates;
- D3 currency;
- authoritative line IDs, component labels/type, amount and source metadata;
- employee SSO, employer SSO separately, and PIT;
- BasicSalary/GrossPay/TaxableEarnings/TotalDeductions/NetPay;
- owning payroll, format version and server-created timestamp.

No tax declarations or claims are copied into the payslip. D3 entitlement segments and full statutory evidence remain in their existing historical storage. Component/source GUIDs are API audit metadata; D7 does not render an employee-facing document.

Canonical display name uses nonblank PreferredName, otherwise FirstName, then nonblank MiddleName/LastName with normalized whitespace. Generation, preview, detail/history and payslip share this convention. Preview changes are display-only.

## Manual changes, regeneration and lifecycle

Generation saves payroll/lines/statutory results and captures the payslip before the same transaction commits. Missing organization configuration deliberately skips formal snapshot creation without blocking calculation.

Allowed Calculated manual create/update/delete refreshes lines, totals and stored statutory summary atomically with existing reconciliation. Frozen employee, employer, employment, period, currency and original snapshot timestamp are retained. Missing identity is never manufactured during a manual adjustment. Existing D5/D6 earning/PIT guards remain enforced.

Successful regeneration captures current presentation facts and replaces the old editable header, lines, statutory results and payslip in one transaction. The existing replacement-header design creates a new payroll ID. Failed regeneration preserves the previous complete state. Approved/Paid/Cancelled generation protection remains authoritative.

Approval/payment read consistency findings before changing lifecycle metadata. They never invoke salary, SSO or PIT calculators, change amounts, or refresh payslip identity/content. Cancel preserves the snapshot unchanged.

## Stored-integrity review

Checks include stored totals, positive line amounts/types, source-ID consistency, generated Basic Salary evidence, D3 currency, PIT owner/amount/provenance, SSO ownership/evidence/amount/provenance, and payslip owner/currency/line/statutory/total consistency. Existing monetary range checks remain in the lifecycle service.

SSO checks compare persisted result/evidence and employee deduction without recalculating a contribution. Positive SSO requires the matching single Statutory DEDUCT-001 line; zero creates no line. Without an Applicable stored result, ordinary/manual DEDUCT-001 retains D5 semantics; an orphan Statutory deduction is rejected. Employer SSO remains outside employee deductions. No current statutory enrollment/policy eligibility is inferred anew during rendering or approval.

Review exposes actual status and parent-period approval readiness. It does not approve automatically or introduce an expected employee population.

## Period summaries and currency

Summaries include all represented headers, including Cancelled; status counts provide lifecycle separation. Monetary totals are grouped by stored D3 currency. Draft/missing currency is counted separately and excluded from monetary currency groups. SSO/PIT aggregates use stored results. No FX conversion, hard-coded THB fallback, or current-compensation/PayrollSettings lookup is used. Summary queries batch the required currency/statutory data without loading full related entities or tax declarations.

Generation failures/skips are not persisted by the existing engine, so historical counts are not exposed or fabricated.

## Migration and SQL verification

Applied: **20261003061140_AddPayrollOperationsAndPayslips**.

Inspected Up/Down before applying. Up creates only OrganizationProfiles and EmployeePayslips and their constraints/index. No existing table alterations, seeds or data backfill. Applied only with Development configuration targeting localhost/SIAMIS using Windows integrated authentication and TrustServerCertificate=True.

Actual SQL verified four enabled/trusted check constraints, the unique payslip owner index and NoAction FK. Deliberate fixture-only duplicate payslip/singleton inserts were rejected and rolled back. All 55 original application tables retained exact rows/timestamps; the two new tables are empty after cleanup. Total application tables: 57.

## Verification

| Suite/check | Result |
|---|---|
| Pure model/calculation regressions including D7 | 400 passed (386 prior + 14 D7). |
| Focused D7 live API/SQL | 81 passed. |
| D5A live | 70 passed. |
| D5C live salary/employment/SSO | 306 passed. |
| D6B live | 76 passed. |
| D6C live | 48 passed. |
| D6D live | 38 passed. |
| D6E live | 88 passed. |
| D6E boundaries/concurrency | 128 passed. |
| Swagger | Five operations plus detail schema verified. |
| Final restore | Succeeded; dependencies up to date. |
| Final Release build | Succeeded, 0 warnings / 0 errors. |
| EF pending-model-changes | None. |
| git diff --check | Passed. |

Total: 1,235 pure/live assertions, plus Swagger verification. Existing suites cover relevant D1/D2/D3 history/eligibility/proration, D4A policy and D4B employee statutory contracts, D5/D6 financial behavior, Supplement/ReplaceAssignment, rollback, manual reconciliation/provenance, approval/payment and period lifecycle. No separate obsolete D1–D4 test suites were invented.

Focused cases verify pre-payment availability; Draft/missing configuration; successful/failed regeneration; exact Approved/Paid/Cancelled snapshot preservation; mutable employee/employment/location/employer/period/component labels; allowed/failed manual adjustments; statutory stale-input guards; positive/zero/NotApplicable statutory display; currency grouping/unresolved currency; totals, PIT and SSO corruption; missing snapshot; singleton/unique constraints; deterministic frozen history; read-only GET behavior.

Concurrency verifies regeneration/approval, regeneration/payment, manual adjustment/approval and concurrent reads/regeneration. Multi-query reads reuse the existing parent-first period locking convention and Serializable transactions to return one coherent committed state. A replaced old payroll ID may correctly return 404; no mixed snapshot is returned. No new server/database isolation setting or broad locking subsystem was introduced.

Early test failures were resolved: organization UTC serialization mismatch was fixed; ordinary-paid/PIT fixture histories were separated to preserve existing review rules; deleted temporary components were recreated for later test phases; legacy saved-baseline files were recaptured for the two D7 tables. Final runs have no errors.

## Final baseline

Independent final comparison matched every original row and timestamp plus two empty D7 tables. TEST-EMP-001 remains inactive. All 17 real payroll components retain Unknown SSO/PIT classifications. Employee API totalCount=1; filtered employee payroll history totalCount=0. No temporary employee, organization, payroll, payslip, policy, declaration or schedule remains.

| Table | Rows |
|---|---|
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
| EmployeePayrollPitResults | 0 |
| EmployeePayrollSocialSecurityResults | 0 |
| EmployeePayrollStatutoryResults | 0 |
| EmployeePayrolls | 0 |
| EmployeePayslips | 0 |
| EmployeePerformance | 0 |
| EmployeePitPaymentScheduleEntries | 0 |
| EmployeePitPaymentScheduleSelections | 0 |
| EmployeePitPaymentSchedules | 0 |
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
| OrganizationProfiles | 0 |
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

172 original master-data rows remain.

## Monetary behavior and remaining boundaries

PayrollCalculationService, BasicSalaryEntitlementService, Section33Calculator, PitCalculator, PayrollRuleEvaluator, Section33PayrollService and PitPayrollService have no changes. D3/D5/D6 monetary formulas, legal eligibility, rounding and PIT/SSO policy behavior are unchanged. Generation changes concern snapshot persistence/display only; manual financial reconciliation uses the existing stored-line behavior.

No PDF, frontend, authentication/RBAC, Leave/Attendance, Provident Fund, P.N.D.1, payment gateway or multi-level approval was added. Administrative authorization/actor attribution remains deferred as instructed. Real organization identity must be configured explicitly; D7 does not manufacture it. No unresolved contract decision remains.

## Repeating verification

Use local Development only, with a clean baseline and the API running on localhost:5155. The test scripts intentionally create synthetic fixtures and restore them. The API must be launched with ASPNETCORE_ENVIRONMENT=Development and the API project content root.

```powershell
.\.dotnet\dotnet.exe restore
.\.dotnet\dotnet.exe build .\SIAMIS.sln -c Release --no-restore
.\.dotnet\dotnet.exe tests/SIAMIS.Payroll.RegressionTests/bin/Release/net10.0/SIAMIS.Payroll.RegressionTests.dll
python tests/verify_d7_live.py --capture-before
python tests/verify_d7_live.py
python tests/verify_d7_regressions.py
.\.dotnet\dotnet.exe ef migrations has-pending-model-changes --project src/SIAMIS.Infrastructure --startup-project src/SIAMIS.Api --configuration Release --no-build
git diff --check
```

The baseline capture is read-only. It can capture the existing migrated baseline for later reruns; the original D7 migration verification used the separately captured 55-table pre-migration state. No migration reapplication is necessary for rerunning tests. The owned verification API process was stopped after testing.

## Complete changed-file list

- [D7-REPORT.md](<C:/Users/USER/Documents/ChatGPT/SIAMIS School Management System/D7-REPORT.md>)
- [src/SIAMIS.Api/Controllers/OrganizationProfileController.cs](<C:/Users/USER/Documents/ChatGPT/SIAMIS School Management System/src/SIAMIS.Api/Controllers/OrganizationProfileController.cs>)
- [src/SIAMIS.Api/Controllers/PayrollOperationsController.cs](<C:/Users/USER/Documents/ChatGPT/SIAMIS School Management System/src/SIAMIS.Api/Controllers/PayrollOperationsController.cs>)
- [src/SIAMIS.Api/Program.cs](<C:/Users/USER/Documents/ChatGPT/SIAMIS School Management System/src/SIAMIS.Api/Program.cs>)
- [src/SIAMIS.Application/Payroll/EmployeePayrollContracts.cs](<C:/Users/USER/Documents/ChatGPT/SIAMIS School Management System/src/SIAMIS.Application/Payroll/EmployeePayrollContracts.cs>)
- [src/SIAMIS.Application/Payroll/PayrollOperationsContracts.cs](<C:/Users/USER/Documents/ChatGPT/SIAMIS School Management System/src/SIAMIS.Application/Payroll/PayrollOperationsContracts.cs>)
- [src/SIAMIS.Domain/Entities/OrganizationProfile.cs](<C:/Users/USER/Documents/ChatGPT/SIAMIS School Management System/src/SIAMIS.Domain/Entities/OrganizationProfile.cs>)
- [src/SIAMIS.Domain/Entities/Payroll/EmployeePayslip.cs](<C:/Users/USER/Documents/ChatGPT/SIAMIS School Management System/src/SIAMIS.Domain/Entities/Payroll/EmployeePayslip.cs>)
- [src/SIAMIS.Infrastructure/Configurations/PayrollOperationsConfigurations.cs](<C:/Users/USER/Documents/ChatGPT/SIAMIS School Management System/src/SIAMIS.Infrastructure/Configurations/PayrollOperationsConfigurations.cs>)
- [src/SIAMIS.Infrastructure/Data/SIAMISDbContext.cs](<C:/Users/USER/Documents/ChatGPT/SIAMIS School Management System/src/SIAMIS.Infrastructure/Data/SIAMISDbContext.cs>)
- [src/SIAMIS.Infrastructure/Migrations/20261003061140_AddPayrollOperationsAndPayslips.Designer.cs](<C:/Users/USER/Documents/ChatGPT/SIAMIS School Management System/src/SIAMIS.Infrastructure/Migrations/20261003061140_AddPayrollOperationsAndPayslips.Designer.cs>)
- [src/SIAMIS.Infrastructure/Migrations/20261003061140_AddPayrollOperationsAndPayslips.cs](<C:/Users/USER/Documents/ChatGPT/SIAMIS School Management System/src/SIAMIS.Infrastructure/Migrations/20261003061140_AddPayrollOperationsAndPayslips.cs>)
- [src/SIAMIS.Infrastructure/Migrations/SIAMISDbContextModelSnapshot.cs](<C:/Users/USER/Documents/ChatGPT/SIAMIS School Management System/src/SIAMIS.Infrastructure/Migrations/SIAMISDbContextModelSnapshot.cs>)
- [src/SIAMIS.Infrastructure/Services/EmployeePayrollService.cs](<C:/Users/USER/Documents/ChatGPT/SIAMIS School Management System/src/SIAMIS.Infrastructure/Services/EmployeePayrollService.cs>)
- [src/SIAMIS.Infrastructure/Services/OrganizationProfileService.cs](<C:/Users/USER/Documents/ChatGPT/SIAMIS School Management System/src/SIAMIS.Infrastructure/Services/OrganizationProfileService.cs>)
- [src/SIAMIS.Infrastructure/Services/PayrollGenerationService.cs](<C:/Users/USER/Documents/ChatGPT/SIAMIS School Management System/src/SIAMIS.Infrastructure/Services/PayrollGenerationService.cs>)
- [src/SIAMIS.Infrastructure/Services/PayrollOperationsService.cs](<C:/Users/USER/Documents/ChatGPT/SIAMIS School Management System/src/SIAMIS.Infrastructure/Services/PayrollOperationsService.cs>)
- [src/SIAMIS.Infrastructure/Services/PayrollPreviewService.cs](<C:/Users/USER/Documents/ChatGPT/SIAMIS School Management System/src/SIAMIS.Infrastructure/Services/PayrollPreviewService.cs>)
- [tests/SIAMIS.Payroll.RegressionTests/D7OperationsContractTests.cs](<C:/Users/USER/Documents/ChatGPT/SIAMIS School Management System/tests/SIAMIS.Payroll.RegressionTests/D7OperationsContractTests.cs>)
- [tests/SIAMIS.Payroll.RegressionTests/Program.cs](<C:/Users/USER/Documents/ChatGPT/SIAMIS School Management System/tests/SIAMIS.Payroll.RegressionTests/Program.cs>)
- [tests/verify_d5a_live.py](<C:/Users/USER/Documents/ChatGPT/SIAMIS School Management System/tests/verify_d5a_live.py>)
- [tests/verify_d5c_live.py](<C:/Users/USER/Documents/ChatGPT/SIAMIS School Management System/tests/verify_d5c_live.py>)
- [tests/verify_d6b_live.py](<C:/Users/USER/Documents/ChatGPT/SIAMIS School Management System/tests/verify_d6b_live.py>)
- [tests/verify_d6c_live.py](<C:/Users/USER/Documents/ChatGPT/SIAMIS School Management System/tests/verify_d6c_live.py>)
- [tests/verify_d6d_live.py](<C:/Users/USER/Documents/ChatGPT/SIAMIS School Management System/tests/verify_d6d_live.py>)
- [tests/verify_d6e_boundaries_live.py](<C:/Users/USER/Documents/ChatGPT/SIAMIS School Management System/tests/verify_d6e_boundaries_live.py>)
- [tests/verify_d6e_live.py](<C:/Users/USER/Documents/ChatGPT/SIAMIS School Management System/tests/verify_d6e_live.py>)
- [tests/verify_d7_live.py](<C:/Users/USER/Documents/ChatGPT/SIAMIS School Management System/tests/verify_d7_live.py>)
- [tests/verify_d7_regressions.py](<C:/Users/USER/Documents/ChatGPT/SIAMIS School Management System/tests/verify_d7_regressions.py>)
