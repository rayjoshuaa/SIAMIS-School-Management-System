# HR Backend V1 freeze

> Historical baseline: the freeze below predates separately approved clocking and employee-management extensions. Use [current HR status](README.md) and [v0.5.1 release notes](../../releases/v0.5.1.md) for current implementation/deployment status. Tag/hash references below are preserved historical evidence, not freshly Git-verified in Stage 3.

**Status: Frozen.** Authoritative Git tag: `hr-backend-v1`, targeting `74907b66417c8f18e14f116ed27e28e8ce492d45`.

The [D15 freeze audit](../../archive/checkpoints/d15/D15-REPORT.md) is the primary supporting record. Its original checkpoint baseline and working-tree wording remain historical evidence; the tag above identifies the accepted frozen implementation. Local verification does not constitute Production deployment approval.

## Included domains

| Area                | Frozen domains                                                                                                                                |
| ------------------- | --------------------------------------------------------------------------------------------------------------------------------------------- |
| Employee foundation | Employee; Employment; WorkCalendar                                                                                                            |
| Leave               | Leave; entitlement; evidence; Sandwich Leave; Paid/Unpaid exhaustion                                                                          |
| Attendance          | Evidence; calculation; review/finalization; reporting                                                                                         |
| Finance             | Payroll; Payslips; current Section 33 Social Security and PIT statutory/tax implementation                                                    |
| Identity/security   | Identity/authentication; RBAC/ownership; account lifecycle; credential lifecycle; offboarding/rehire; HR Documents; audit/security foundation |

These cover the 22 areas in D15's freeze manifest. Exact API groups and persistence boundaries remain in that manifest and the [authorization matrix](../../archive/checkpoints/d15/D15-AUTHORIZATION-MATRIX.md).

## Major invariants

- **Employee/employment:** stable unique employee identity; owned child records; inclusive date-effective employment history with one current record; explicit termination/account decisions. Rehire preserves history and does not implicitly enable an account.
- **Calendars:** explicit EmployeeWorkCalendarAssignment only; no automatic IsDefault fallback. ExceptionalWorkingDay intervals replace the weekly schedule. No assumed eight-hour day.
- **Leave:** versioned immutable Published policies; calendar-year entitlement with whole-minute precision; append-only adjustments; frozen evidence and paid/unpaid allocations. Sandwich cases use explicit capped review and approved timing. Leave cancellation remains independent of Attendance finalization.
- **Attendance:** exact timestamp/tick coverage partition. Independently truncated integer-millisecond totals expose a precision residual that is not absence, work, Leave or payroll time. Finalized revisions are immutable. Source changes produce structured stale reads without mutation; reopening is explicit, reasoned and preserves previous revisions.
- **Payroll:** established D3 entitlement/proration and D5/D6 statutory contracts remain unchanged. Supported calculation-method versions and explicit classifications are required. No legal numeric values or classifications are inferred. Supplement/ReplaceAssignment behavior, matching-assignment suppression and conflict rejection remain established. Failed new generation rolls back; failed regeneration preserves the previous complete result. Generated provenance and calculation snapshots are historical, and SourceId is not a live foreign key. Manual adjustments retain Manual provenance and remarks. Payroll and period lifecycle guards remain intact.
- **Payslips:** historical payroll identity/data are retained; employee self-service sees only own Approved/Paid information.
- **Security:** authenticated server-derived actors, CSRF, session/account validation, explicit capabilities, optional unique User → Employee linkage and last-admin protection. Employment position never implies a security role. Salary Change history is Payroll-protected.
- **Documents:** confidential HRDocuments capabilities; private immutable binary versions; opaque keys, integrity checking, archive and compensated failures. SystemAdmin alone has no confidential document access.

No automatic Attendance/Leave → Payroll deductions, KPI or disciplinary consequences are part of this freeze. Domain audit histories remain preserved; no universal retention/purge rule was invented.

## Deferred register

| Category                         | Deferred work                                                                                                                                                                                                       |
| -------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Deployment / Operations          | TLS/reverse-proxy validation; persistent Data Protection keys; Production credential delivery; private document storage path/ACL; malware scanning; coordinated SQL/document backups; monitoring; disaster recovery |
| Integrations                     | Facial-recognition/biometric attendance import; email provider; accounting/payment integrations                                                                                                                     |
| Future HR                        | Employee document self-service; scheduled termination; final pay/severance; retention/purge automation; teacher KPI/appraisal; advanced analytics; Provident Fund                                                   |
| Business Policy Not Yet Approved | Attendance → Payroll deductions; automatic Leave cancellation at termination; maker-checker Payroll approval; disciplinary consequences                                                                             |

## Change gate

Frozen business semantics require a **new explicitly approved checkpoint** before modification. School Management and frontend work must preserve these boundaries. Repository cleanup changes documentation only.
