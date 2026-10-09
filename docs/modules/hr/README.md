# Human Resources implementation status

The [HR V1 freeze](HR-V1-FREEZE.md) is the historical business baseline. Separately approved checkpoints added employee clocking and employee-management improvements without automatic attendance/leave payroll deductions. Current status is summarized here; detailed dated milestone reports preserve evidence.

| Area                                      | Current implementation                                                                                                       | Evidence / boundary                                                                                                     |
| ----------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------- |
| Identity and accounts                     | Real Identity sessions, CSRF, capability authorization, passwordless provisioning/activation, recovery and account lifecycle | [Authentication](../../frontend/AUTHENTICATION.md); User Accounts requires Security.Manage                              |
| HR Dashboard                              | Bounded real backend metrics and operational links                                                                           | [F4](../../frontend/F4-REPORT.md), later premium UI refinements                                                         |
| Employee Directory / 360                  | Registration, profile/supporting records, employment history and account context                                             | [F5](../../frontend/F5-EMPLOYEE-DIRECTORY-AND-360.md), [F5.1B](../../frontend/F5.1B-EMPLOYEE-PROFILE-AND-ONBOARDING.md) |
| Numbering                                 | Automatic immutable numbers from 100000; permanent reservations and legacy identity preservation                             | [F5.1A](../../development/F5.1A-EMPLOYEE-NUMBERING.md)                                                                  |
| Photos                                    | Private JPEG/PNG upload, replacement, removal and authenticated retrieval; initials fallback                                 | [F5.1C](../../frontend/F5.1C-SECURE-EMPLOYEE-PHOTOS.md)                                                                 |
| Employment lifecycle                      | Existing End Employment and rehire; employee identity/history retained; account decisions separate                           | [F6](../../frontend/F6-EMPLOYMENT-AND-ACCOUNT-LIFECYCLE.md)                                                             |
| Permanent deletion                        | Restricted mistaken-registration eligibility, dependency blockers, audit and permanent number protection                     | [F5.1D](../../frontend/F5.1D-CONTROLLED-EMPLOYEE-DELETION.md); not routine offboarding                                  |
| Registration idempotency                  | Durable actor/operation-scoped receipts, 30-day replay, permanent expired-key protection                                     | [F5.1E.1](../../frontend/F5.1E1-EMPLOYEE-REGISTRATION-IDEMPOTENCY.md)                                                   |
| Attendance administration                 | Evidence, corrections, review, explicit reopen/finalization and historical revisions                                         | [F7](../../frontend/F7-ATTENDANCE-MANAGEMENT.md)                                                                        |
| Employee clocking                         | Owned sessions/history, UTC server timestamps, On Campus / Online Class / Remote Work                                        | [F7.2 backend](../../development/F7.2-EMPLOYEE-CLOCKING-BACKEND.md); My Attendance frontend implemented subsequently    |
| Leave, Payroll, confidential HR Documents | Established backend workflows and privacy boundaries                                                                         | Full operational frontend workspaces remain outside this release scope                                                  |

Implementation/test evidence is distinct from final release approval. F5.1A/B/C approvals and Development deployments are recorded in their reports. F5.1E verification was accepted; idempotency addressed the registration blocker. See [v0.5.1 verification and release gates](../../releases/v0.5.1.md) for current readiness.

## Identity and retention rules

Registration atomically creates an employee and initial employment; Active is assigned automatically, and effective hire dates still govern current eligibility. Account provisioning remains separate. Do not conflate employment, account activation, approved leave or employee-record activation.

Normal offboarding uses End Employment and preserves rehire/history. Permanent deletion is for eligible mistakes, not historical-data cleanup. Legacy records lacking trusted creation provenance and records with protected dependencies/account links are not eligible. Photo revisions, including removed/superseded versions, are retained and block deletion; no silent blob purge is provided.

Photos require Employee.Read to view and Employee.Manage to change; these do not confer HRDocuments access. Photo content is validated as JPEG/PNG, at most 5 MiB and decoded dimensions at most 4096 × 4096. Originals may retain metadata such as EXIF. Retention and privacy handling require deployment review.

## Deferred

F5.2 QR credentials, kiosk scanning, monthly timesheets, future module UIs and Production rollout are not implemented here. Clocking is allowed from any location without GPS/webcam verification. Missing clock-out is reviewable, not automatic misconduct or salary deduction. Attendance/leave/payroll calculations retain their established contracts.
