# D6B — PIT classification and employee/year treatment foundation

Completed 2 October 2026. Local Development target: `localhost/SIAMIS`, Windows authentication, trusted development certificate.

## 1. Files created/modified

The complete file list is appended below. D6A report and contract probes are preserved; the existing runner continues to execute them. D6B changes production code only for classification metadata, focused administration, historical snapshots and classification/treatment resolution. No PIT monetary path was introduced.

## 2. Migration details

Created, inspected and applied **20261002042159_AddPitIncomeClassificationAndTaxTreatment** only to local Development `localhost/SIAMIS`.

Up contains four additive non-null string columns, each defaulting to Unknown; four vocabulary checks; and 17 seed updates setting only the new component classification to Unknown. It contains no table creation/deletion, financial column updates, legal classification assignment or numeric legal seed. EF emitted its scaffold warning because of data operations; the generated Up was reviewed before application. Down removes the new fields/checks and was not executed.

## 3. PitIncomeTreatment storage/API contract

`PayrollComponents.PitIncomeTreatment`: required nvarchar(20), default Unknown; allowed Unknown/Included/Excluded. The existing component create, update, detail and list APIs expose it through DTOs. Create omission/null gives Unknown; update omission/null preserves the stored value. Empty, unsupported and noncanonical values are rejected by application validation. No automatic name/code/type/SSO/IsTaxable mapping exists. Existing filters still concern their existing flags; no new PIT filter was added.

All 17 existing components migrated to Unknown and remain Unknown after verification cleanup. Temporary Included/Excluded classifications were synthetic test fixtures, including a temporary Basic Salary metadata change; they are not legal determinations.

## 4. Relationship to legacy IsTaxable

IsTaxable and IsTaxableSnapshot continue to control the existing TaxableEarnings sum and existing API/filter behavior. PitIncomeTreatment is separate future legal PIT authority and does not alter BasicSalary, GrossPay, TaxableEarnings, TotalDeductions or NetPay. They can disagree: legacy false + PIT Included, or legacy true + PIT Excluded, is preserved deliberately during this transition.

Smallest future path: require reviewed explicit earning classifications; make the future PIT engine consume the separate candidate from historical PIT snapshots; preserve old line flags/totals. Any retirement or redefinition of legacy TaxableEarnings requires a separately reviewed compatibility decision. No historical reclassification or financial backfill is permitted merely from a boolean.

## 5. Payroll-line snapshots

`EmployeePayrollLines.PitIncomeTreatmentSnapshot`: required nvarchar(20), default Unknown, same allowed vocabulary. Existing rows receive only Unknown. BasicSalary, Assignment and PayrollRule calculations copy component classification into value snapshots. Rule evaluation carries component metadata without changing applicability or formulas. Preview and persisted detail/line DTOs expose the new snapshot. Generation persists it.

Live component edits leave stored snapshots unchanged. Explicit regeneration captures current component classification. SourceType/SourceId, D3 salary audit JSON and all prior metadata remain intact. Deduction lines do not become PIT earnings regardless of their classification.

## 6. PIT income resolver

`IPitIncomeResolver` / `PitIncomeResolver` is a shared pure classification service registered in DI. It is not called from monetary generation/preview.

| Final line | Result |
|---|---|
| Earning + Included | Final Amount contributes once. |
| Earning + Excluded | Does not contribute. |
| Earning + Unknown | Unresolved; no complete candidate exposed; issue identifies line/component/source. |
| Deduction | Ignored, including Unknown classifications. |
| Invalid type/treatment, negative earning, overflow | InvalidInput; no candidate. |
| Empty/all Excluded valid set | Resolved with explicit zero candidate. |

Mixed Included/Excluded/Unknown is Unresolved with null candidate rather than a misleading partial resolved base. DTO adapters consume calculated or stored line snapshots. It resolves no policy, year, opening balance, annualization, expense, allowance or tax and writes nothing.

## 7. EmployeeTaxProfile treatment design

The existing profile remains the employee-scoped taxpayer identifier. Treatment belongs to `EmployeeTaxDeclaration`, already the employee/year immutable revision architecture. Added ResidencyStatus and EmploymentTaxTreatment there, avoiding a second competing profile-revision/selection system. Focused declaration detail includes a Treatment DTO with exact declaration GUID, employee GUID, Gregorian year, revision number, values, verification status/timestamp and supporting declaration remarks.

No new table or identity was needed. Future PIT history can reference the exact declaration/treatment revision instead of reading mutable current flags. D4B's verification-time taxpayer identifier snapshot remains unchanged.

## 8. Tax-year semantics

Treatment inherits the declaration TaxYear; API/storage interpret it as Gregorian 1–9999 without Buddhist-year conversion. The existing `(EmployeeId, TaxYear)` selection key and `(EmployeeId, TaxYear, RevisionNumber)` uniqueness prevent ambiguous selected revisions. Only one Draft per year is permitted; multiple immutable historical Verified revisions remain allowed. No PayDate-to-TaxYear integration was implemented.

## 9. Verification and immutability

Treatment can be edited only on an owned Draft through the same serializable employee-lock/DraftMutation boundary as D4B. Existing declaration verification is the explicit verification action: it captures VerifiedAt and atomically updates selection. A verified declaration's treatment cannot be edited; corrections use a replacement Draft and the existing predecessor check. A replacement Draft does not affect selected treatment until verification.

`EmployeeTaxTreatmentResolver` consumes only the selected revision. Approved requires Verified status, VerifiedAt, known residence and StandardSection40_1. RequiresReview remains Blocked; Unknown residence/treatment remains Unresolved; Draft/missing selection remains Unresolved. Approved is only an employee-treatment gate, not proof of complete calculator inputs or authorization/RBAC.

D4B still permits verification with explicit Unknown opening balance and Unknown treatment. This preserves its structural verification contract; future PIT must separately require resolved monetary/history inputs. Remarks remain supporting declaration remarks and are optional; omitted/null treatment remarks preserve them. No VerifiedBy was added. Immutability is enforced at service/API boundaries, not through database triggers.

## 10. Foreign/Thai employee behavior

The gate has no nationality, citizenship, SSO, employment status, location or taxpayer-ID input. Explicit Resident and NonResident standard verified treatments can both be Approved. Tests temporarily changed the test employee's nationality to Filipino and Thai: Standard remained Approved, RequiresReview remained Blocked and Unknown remained Unresolved in both cases. The exact original employee row was restored.

This establishes independence from nationality, not legal approval for every nonresident or foreign regime. The administrator's verified treatment supplies the contract; unresolved or exceptional treatment is never inferred as standard or exempt.

## 11. Privacy

No treatment/taxpayer fields were added to general employee DTOs. The live employee list was checked with a synthetic taxpayer identifier present and contained neither that identifier nor treatment fields. Treatment metadata is exposed only through focused employee tax/declaration endpoints. Broad declaration summaries remain unchanged. Authentication/RBAC is still absent as previously agreed; parent ownership does not establish caller authorization.

## 12. Manual-line behavior

Manual creation snapshots the selected component's PIT classification on the server. Same-component updates preserve the stored snapshot even if live classification changed. Explicit component changes refresh it. Manual provenance remains Manual/null. Existing SSO earning-mutation guards and payroll lifecycle guards remain unchanged.

No writable snapshot/source fields were added. The manual request now explicitly rejects unmapped JSON members; attempted PIT snapshot submission returns 400 on create and update. This also rejects other unrecognized request properties rather than silently ignoring them. Amount edits still use the established reconciliation behavior; PIT classification itself changes no money.

## 13. API/Swagger changes

| Endpoint | Purpose / principal responses |
|---|---|
| Existing POST/PUT `/api/payroll-components` | Configure optional PitIncomeTreatment; existing 201/200 and validation responses. |
| Existing GET component list/detail | Returns PitIncomeTreatment. |
| PUT `/api/employees/{employeeId}/tax-declarations/{id}/treatment` | Owned Draft treatment; 200, 400 invalid, 404 ownership/missing, 409 immutable Verified. |
| Existing declaration verify endpoint | Explicitly verifies the revision containing treatment and updates selection. |
| GET `/api/employees/{employeeId}/tax-treatment?taxYear=2026` | Resolves selected year treatment; 200 Approved/Blocked/Unresolved, 400 invalid year, 404 missing employee. |
| Existing declaration detail / verify response | Adds focused Treatment DTO. |
| Existing preview / payroll-line detail | Adds read-only PitIncomeTreatmentSnapshot. |

Live Swagger exposes both new routes and component request classification. Manual request schema contains no snapshot field. The existing Application/Infrastructure separation is retained; controllers do not use DbContext.

## 14. Database constraints

| Table / column | Required type/default | Added check |
|---|---|---|
| PayrollComponents.PitIncomeTreatment | nvarchar(20), Unknown | CK_PayrollComponents_PitIncomeTreatment |
| EmployeePayrollLines.PitIncomeTreatmentSnapshot | nvarchar(20), Unknown | CK_EmployeePayrollLines_PitIncomeTreatmentSnapshot |
| EmployeeTaxDeclarations.ResidencyStatus | nvarchar(20), Unknown | CK_EmployeeTaxDeclarations_ResidencyStatus |
| EmployeeTaxDeclarations.EmploymentTaxTreatment | nvarchar(30), Unknown | CK_EmployeeTaxDeclarations_EmploymentTaxTreatment |

Checks allow only the corresponding approved vocabulary under the database's existing SQL collation conventions. Live SQL confirmed all four enabled/trusted; invalid-value attempts failed and rolled back. Existing ownership FKs, filtered Draft index, unique revision/selection keys and NoAction behaviors were preserved. No nationality constraint exists.

## 15. Verification counts/results

| Suite | Final result |
|---|---:|
| Dependency-free D5A/B/C + D6A/B runner | 195 assertions passed |
| Existing D5A live API/SQL suite | 70 passed |
| Existing D5C live API/SQL suite | 306 passed |
| New D6B live API/SQL suite | 76 passed |
| Total across final successful runs | 647 passed |

D6B covers default/explicit classifications, invalid create/update, SQL persistence and constraints, all three generated origins, historical retention/regeneration, preview parity, manual snapshot/forgery behavior, candidate resolution including mixed Unknown, every residence/treatment gate, year ownership, immutable correction/selection, foreign/Thai independence and privacy/Swagger.

During test development, a manual-request probe exposed ignored unknown properties; this was fixed as described in section 12. History comparison assertions were corrected to compare persisted GET/SQL snapshots: IsCurrentVerified is derived selection metadata, and in-memory UTC mutation responses differ in timestamp serialization from SQL datetime2 reads. The underlying Verified database row remained unchanged. Failed test attempts also ran cleanup successfully before the final complete rerun.

## 16. D1–D6A regression results

Existing dependency-free tests and both committed live suites were rerun. Covered boundaries include D3 full/partial salary entitlement and employment timeline cases, D4A publication/resolution/immutability, D4B declaration/opening/selection and enrollment behavior, D5 employee/employer isolation and historical snapshots, Supplement/ReplaceAssignment/conflict, failed new generation rollback, failed regeneration preservation, source provenance, manual totals reconciliation, payroll approval/payment/cancellation and period close/cancellation protections. D6A's 29 probes remain in the runner and pass.

There is no separate complete D1–D4 live suite in the current tree; this report does not claim every historical test from those checkpoints was replayed. Their relevant current regression boundaries are covered by the retained suites and unchanged source. No PIT deduction was produced and all financial invariance comparisons passed.

## 17. Final Development table counts

There remain **51 application tables** plus EF history, **172 master rows**, one employee and one employment record. The full per-table counts are appended below.

TEST-EMP-001 is the only employee, ID `433f2c1a-6222-494f-a64f-cd0c31126dc4`, IsActive=false. All 17 components have PitIncomeTreatment=Unknown and SsoWageTreatment=Unknown. Existing IsTaxable values remain false. Payrolls, lines, statutory results, policies, employee treatment/declaration/profile records and test fixtures all end at zero.

## 18. Exact baseline restoration

Each live suite compared the complete serialized contents of every application table to its post-migration, pre-fixture baseline, not just row counts. D6B restores all original component fields/timestamps and the exact employee row, and removes recorded fixture IDs, synthetic profile/declarations, selections, balances, compensation, assignments, rules, periods and generated rows. Final comparison passed for all 51 tables. The intended schema additions and EF history entry remain; no permanent legal classifications or employee core changes remain.

## 19. Release build

Restore succeeded. Final `.\.dotnet\dotnet.exe build SIAMIS.sln -c Release --no-restore` succeeded with **0 warnings and 0 errors**. No new packages were required.

## 20. EF pending model changes

Development Release `ef migrations has-pending-model-changes` reports no changes since the last migration. Only the focused D6B migration was created/applied; older migrations were preserved.

## 21. Git diff check

`git diff --check` passed. Existing D6A work is preserved. No commit or push.

The temporary Development API used on port 5155 was stopped after verification. Ignored logs/results remain available locally; temporary edit/report helper scripts were removed.

## 22. Remaining D6C decisions

Classification/treatment foundations are now explicit, but automatic PIT is still absent. D6C must resolve confirmed payment-date/Gregorian year semantics, annualization and regular/special payment kinds, partial/rehire/irregular schedules, complete allowance policy/claim contracts, opening payer/income/cutoff meanings, authoritative YTD payment/remittance history and ordering, actual/projected employee SSO deduction recognition, supported method/precision/rounding and residual/refund rules, DEDUCT-002 ownership and typed historical PIT result design. A selected Verified standard treatment does not bypass any of these boundaries.

No annualization, PIT brackets/calculator, PIT deduction, policy resolution, opening/YTD consumption, Provident Fund, payslip, authentication or monetary PIT changes were implemented. Stop after D6B.

## Complete working-tree file list

- [D6A-REPORT.md](<C:/Users/USER/Documents/ChatGPT/SIAMIS School Management System/D6A-REPORT.md>) (preserved D6A)
- [D6B-REPORT.md](<C:/Users/USER/Documents/ChatGPT/SIAMIS School Management System/D6B-REPORT.md>)
- [src/SIAMIS.Api/Controllers/EmployeeTaxController.cs](<C:/Users/USER/Documents/ChatGPT/SIAMIS School Management System/src/SIAMIS.Api/Controllers/EmployeeTaxController.cs>)
- [src/SIAMIS.Api/Program.cs](<C:/Users/USER/Documents/ChatGPT/SIAMIS School Management System/src/SIAMIS.Api/Program.cs>)
- [src/SIAMIS.Application/MasterData/PayrollComponentContracts.cs](<C:/Users/USER/Documents/ChatGPT/SIAMIS School Management System/src/SIAMIS.Application/MasterData/PayrollComponentContracts.cs>)
- [src/SIAMIS.Application/Payroll/EmployeePayrollContracts.cs](<C:/Users/USER/Documents/ChatGPT/SIAMIS School Management System/src/SIAMIS.Application/Payroll/EmployeePayrollContracts.cs>)
- [src/SIAMIS.Application/Payroll/EmployeeStatutoryContracts.cs](<C:/Users/USER/Documents/ChatGPT/SIAMIS School Management System/src/SIAMIS.Application/Payroll/EmployeeStatutoryContracts.cs>)
- [src/SIAMIS.Application/Payroll/PayrollCalculationContracts.cs](<C:/Users/USER/Documents/ChatGPT/SIAMIS School Management System/src/SIAMIS.Application/Payroll/PayrollCalculationContracts.cs>)
- [src/SIAMIS.Application/Payroll/PayrollPreviewContracts.cs](<C:/Users/USER/Documents/ChatGPT/SIAMIS School Management System/src/SIAMIS.Application/Payroll/PayrollPreviewContracts.cs>)
- [src/SIAMIS.Application/Payroll/PayrollRuleEvaluationContracts.cs](<C:/Users/USER/Documents/ChatGPT/SIAMIS School Management System/src/SIAMIS.Application/Payroll/PayrollRuleEvaluationContracts.cs>)
- [src/SIAMIS.Application/Payroll/PitFoundationContracts.cs](<C:/Users/USER/Documents/ChatGPT/SIAMIS School Management System/src/SIAMIS.Application/Payroll/PitFoundationContracts.cs>)
- [src/SIAMIS.Domain/Entities/MasterData/MasterDataEntity.cs](<C:/Users/USER/Documents/ChatGPT/SIAMIS School Management System/src/SIAMIS.Domain/Entities/MasterData/MasterDataEntity.cs>)
- [src/SIAMIS.Domain/Entities/Payroll/EmployeePayrollLine.cs](<C:/Users/USER/Documents/ChatGPT/SIAMIS School Management System/src/SIAMIS.Domain/Entities/Payroll/EmployeePayrollLine.cs>)
- [src/SIAMIS.Domain/Entities/Payroll/EmployeeStatutoryProfiles.cs](<C:/Users/USER/Documents/ChatGPT/SIAMIS School Management System/src/SIAMIS.Domain/Entities/Payroll/EmployeeStatutoryProfiles.cs>)
- [src/SIAMIS.Infrastructure/Configurations/AdditionalMasterDataConfigurations.cs](<C:/Users/USER/Documents/ChatGPT/SIAMIS School Management System/src/SIAMIS.Infrastructure/Configurations/AdditionalMasterDataConfigurations.cs>)
- [src/SIAMIS.Infrastructure/Configurations/EmployeePayrollConfigurations.cs](<C:/Users/USER/Documents/ChatGPT/SIAMIS School Management System/src/SIAMIS.Infrastructure/Configurations/EmployeePayrollConfigurations.cs>)
- [src/SIAMIS.Infrastructure/Configurations/EmployeeStatutoryConfigurations.cs](<C:/Users/USER/Documents/ChatGPT/SIAMIS School Management System/src/SIAMIS.Infrastructure/Configurations/EmployeeStatutoryConfigurations.cs>)
- [src/SIAMIS.Infrastructure/Migrations/20261002042159_AddPitIncomeClassificationAndTaxTreatment.Designer.cs](<C:/Users/USER/Documents/ChatGPT/SIAMIS School Management System/src/SIAMIS.Infrastructure/Migrations/20261002042159_AddPitIncomeClassificationAndTaxTreatment.Designer.cs>)
- [src/SIAMIS.Infrastructure/Migrations/20261002042159_AddPitIncomeClassificationAndTaxTreatment.cs](<C:/Users/USER/Documents/ChatGPT/SIAMIS School Management System/src/SIAMIS.Infrastructure/Migrations/20261002042159_AddPitIncomeClassificationAndTaxTreatment.cs>)
- [src/SIAMIS.Infrastructure/Migrations/SIAMISDbContextModelSnapshot.cs](<C:/Users/USER/Documents/ChatGPT/SIAMIS School Management System/src/SIAMIS.Infrastructure/Migrations/SIAMISDbContextModelSnapshot.cs>)
- [src/SIAMIS.Infrastructure/Services/EmployeePayrollService.cs](<C:/Users/USER/Documents/ChatGPT/SIAMIS School Management System/src/SIAMIS.Infrastructure/Services/EmployeePayrollService.cs>)
- [src/SIAMIS.Infrastructure/Services/EmployeeStatutoryService.cs](<C:/Users/USER/Documents/ChatGPT/SIAMIS School Management System/src/SIAMIS.Infrastructure/Services/EmployeeStatutoryService.cs>)
- [src/SIAMIS.Infrastructure/Services/EmployeeTaxTreatmentResolver.cs](<C:/Users/USER/Documents/ChatGPT/SIAMIS School Management System/src/SIAMIS.Infrastructure/Services/EmployeeTaxTreatmentResolver.cs>)
- [src/SIAMIS.Infrastructure/Services/PayrollCalculationService.cs](<C:/Users/USER/Documents/ChatGPT/SIAMIS School Management System/src/SIAMIS.Infrastructure/Services/PayrollCalculationService.cs>)
- [src/SIAMIS.Infrastructure/Services/PayrollComponentService.cs](<C:/Users/USER/Documents/ChatGPT/SIAMIS School Management System/src/SIAMIS.Infrastructure/Services/PayrollComponentService.cs>)
- [src/SIAMIS.Infrastructure/Services/PayrollGenerationService.cs](<C:/Users/USER/Documents/ChatGPT/SIAMIS School Management System/src/SIAMIS.Infrastructure/Services/PayrollGenerationService.cs>)
- [src/SIAMIS.Infrastructure/Services/PayrollPreviewService.cs](<C:/Users/USER/Documents/ChatGPT/SIAMIS School Management System/src/SIAMIS.Infrastructure/Services/PayrollPreviewService.cs>)
- [src/SIAMIS.Infrastructure/Services/PayrollRuleEvaluator.cs](<C:/Users/USER/Documents/ChatGPT/SIAMIS School Management System/src/SIAMIS.Infrastructure/Services/PayrollRuleEvaluator.cs>)
- [src/SIAMIS.Infrastructure/Services/PitIncomeResolver.cs](<C:/Users/USER/Documents/ChatGPT/SIAMIS School Management System/src/SIAMIS.Infrastructure/Services/PitIncomeResolver.cs>)
- [tests/SIAMIS.Payroll.RegressionTests/D6AContractRegressionTests.cs](<C:/Users/USER/Documents/ChatGPT/SIAMIS School Management System/tests/SIAMIS.Payroll.RegressionTests/D6AContractRegressionTests.cs>) (preserved D6A)
- [tests/SIAMIS.Payroll.RegressionTests/D6BFoundationRegressionTests.cs](<C:/Users/USER/Documents/ChatGPT/SIAMIS School Management System/tests/SIAMIS.Payroll.RegressionTests/D6BFoundationRegressionTests.cs>)
- [tests/SIAMIS.Payroll.RegressionTests/Program.cs](<C:/Users/USER/Documents/ChatGPT/SIAMIS School Management System/tests/SIAMIS.Payroll.RegressionTests/Program.cs>)
- [tests/verify_d6b_live.py](<C:/Users/USER/Documents/ChatGPT/SIAMIS School Management System/tests/verify_d6b_live.py>)

## Complete final application table counts

| Table | Rows |
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
| EmployeePayrollSocialSecurityResults | 0 |
| EmployeePayrollStatutoryResults | 0 |
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
