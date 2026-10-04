# D15 role → capability → route matrix

Generated from current controller actions and Swagger; six role combinations tested with real Identity cookies and CSRF. Allowed probes use invalid/nonowned IDs or invalid shapes, so they exercise gates without changing HR data. Successful business behavior is verified separately.

| Role | Capabilities |
|---|---|
| SystemAdmin | Attendance.Finalize, Attendance.Manage, Attendance.Read, Employee.Manage, Employee.Read, Leave.Evidence, Leave.Manage, Leave.Read, Leave.Review, MasterData.Read, Payroll.Manage, Payroll.Read, Reporting.Read, Security.Manage |
| HRAdmin | Attendance.Finalize, Attendance.Manage, Attendance.Read, Employee.Manage, Employee.Read, HRDocuments.Manage, HRDocuments.Read, Leave.Evidence, Leave.Manage, Leave.Read, Leave.Review, MasterData.Read, Reporting.Read |
| PayrollAdmin | MasterData.Read, Payroll.Manage, Payroll.Read |
| Management | MasterData.Read, Reporting.Read |
| Employee | MasterData.Read, SelfService |

Employee own-record exceptions: own Leave list/detail/create/cancel and balances; own Attendance history/summary; own Approved/Paid payroll, lines/PIT/payslip through server User→Employee linkage. No Employee document access; self-approval prohibited. Linked evidence content also requires Leave.Evidence.

EmployeeHistory route admission uses either capability (`|` means OR). Service-level EventType gates: ordinary events require Employee.Read/Manage; Salary Change requires Payroll.Read/Manage independently. Lists filter in SQL before sorting/projection, with no placeholders or total. Unauthorized/nonowned direct IDs return 404; unauthorized creates return 403. Payroll-only callers cannot access ordinary events.

| Method | Route | Capability | Default roles / combined role |
|---|---|---|---|
| DELETE | `/api/employee-payrolls/{id}` | Payroll.Manage | SystemAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| DELETE | `/api/employee-payrolls/{payrollId}/lines/{lineId}` | Payroll.Manage | SystemAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| DELETE | `/api/employees/{employeeId}/addresses/{addressId}` | Employee.Manage | SystemAdmin, HRAdmin, SystemAdmin+HRAdmin |
| DELETE | `/api/employees/{employeeId}/attendance/{attendanceId}` | Attendance.Manage | SystemAdmin, HRAdmin, SystemAdmin+HRAdmin |
| DELETE | `/api/employees/{employeeId}/compensations/{compensationId}` | Payroll.Manage | SystemAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| DELETE | `/api/employees/{employeeId}/contacts/{contactId}` | Employee.Manage | SystemAdmin, HRAdmin, SystemAdmin+HRAdmin |
| DELETE | `/api/employees/{employeeId}/contracts/{contractId}` | Employee.Manage | SystemAdmin, HRAdmin, SystemAdmin+HRAdmin |
| DELETE | `/api/employees/{employeeId}/documents/{documentId}` | HRDocuments.Manage | HRAdmin, SystemAdmin+HRAdmin |
| DELETE | `/api/employees/{employeeId}/emergency-contacts/{emergencyContactId}` | Employee.Manage | SystemAdmin, HRAdmin, SystemAdmin+HRAdmin |
| DELETE | `/api/employees/{employeeId}/history/{historyId}` | Employee.Manage or Payroll.Manage | SystemAdmin, HRAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| DELETE | `/api/employees/{employeeId}/payroll-component-assignments/{assignmentId}` | Payroll.Manage | SystemAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| DELETE | `/api/employees/{employeeId}/performance/{performanceRecordId}` | Employee.Manage | SystemAdmin, HRAdmin, SystemAdmin+HRAdmin |
| DELETE | `/api/employees/{employeeId}/tax-declarations/{id}` | Payroll.Manage | SystemAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| DELETE | `/api/employees/{employeeId}/tax-declarations/{id}/claims/{claimId}` | Payroll.Manage | SystemAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| DELETE | `/api/employees/{employeeId}/tax-declarations/{id}/opening-balance` | Payroll.Manage | SystemAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| DELETE | `/api/payroll-components/{id}` | Payroll.Manage | SystemAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| DELETE | `/api/payroll-periods/{id}` | Payroll.Manage | SystemAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| DELETE | `/api/payroll-rules/{id}` | Payroll.Manage | SystemAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| DELETE | `/api/payroll-rules/{payrollRuleId}/targets/{targetId}` | Payroll.Manage | SystemAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| DELETE | `/api/payroll-settings/{id}` | Payroll.Manage | SystemAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| DELETE | `/api/statutory-policy-versions/{id}` | Payroll.Manage | SystemAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| DELETE | `/api/statutory-schemes/{id}` | Payroll.Manage | SystemAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| GET | `/api/admin/users` | Security.Manage | SystemAdmin, SystemAdmin+HRAdmin |
| GET | `/api/admin/users/{id}` | Security.Manage | SystemAdmin, SystemAdmin+HRAdmin |
| GET | `/api/attendance/days/{date}` | Reporting.Read | SystemAdmin, HRAdmin, Management, SystemAdmin+HRAdmin |
| GET | `/api/attendance/review-queue` | Attendance.Read | SystemAdmin, HRAdmin, SystemAdmin+HRAdmin |
| GET | `/api/attendance/today` | Reporting.Read | SystemAdmin, HRAdmin, Management, SystemAdmin+HRAdmin |
| GET | `/api/employee-payrolls` | Payroll.Read | SystemAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| GET | `/api/employee-payrolls/{id}` | Payroll.Read | SystemAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| GET | `/api/employee-payrolls/{id}/payslip` | Payroll.Read | SystemAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| GET | `/api/employee-payrolls/{id}/review` | Payroll.Read | SystemAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| GET | `/api/employee-payrolls/{payrollId}/lines` | Payroll.Read | SystemAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| GET | `/api/employee-payrolls/{payrollId}/lines/{lineId}` | Payroll.Read | SystemAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| GET | `/api/employee-payrolls/{payrollId}/pit-result` | Payroll.Read | SystemAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| GET | `/api/employees` | Employee.Read | SystemAdmin, HRAdmin, SystemAdmin+HRAdmin |
| GET | `/api/employees/{employeeId}/account-lifecycle` | Employee.Read | SystemAdmin, HRAdmin, SystemAdmin+HRAdmin |
| GET | `/api/employees/{employeeId}/addresses` | Employee.Read | SystemAdmin, HRAdmin, SystemAdmin+HRAdmin |
| GET | `/api/employees/{employeeId}/attendance` | Attendance.Read | SystemAdmin, HRAdmin, SystemAdmin+HRAdmin |
| GET | `/api/employees/{employeeId}/attendance-days/{date}` | Attendance.Read | SystemAdmin, HRAdmin, SystemAdmin+HRAdmin |
| GET | `/api/employees/{employeeId}/attendance-days/{date}/history` | Attendance.Read | SystemAdmin, HRAdmin, SystemAdmin+HRAdmin |
| GET | `/api/employees/{employeeId}/attendance-days/{date}/review` | Attendance.Read | SystemAdmin, HRAdmin, SystemAdmin+HRAdmin |
| GET | `/api/employees/{employeeId}/attendance-events` | Attendance.Read | SystemAdmin, HRAdmin, SystemAdmin+HRAdmin |
| GET | `/api/employees/{employeeId}/attendance-events/{eventId}` | Attendance.Read | SystemAdmin, HRAdmin, SystemAdmin+HRAdmin |
| GET | `/api/employees/{employeeId}/attendance-expected-work` | Attendance.Read | SystemAdmin, HRAdmin, SystemAdmin+HRAdmin |
| GET | `/api/employees/{employeeId}/attendance-history` | Reporting.Read | SystemAdmin, HRAdmin, Management, SystemAdmin+HRAdmin |
| GET | `/api/employees/{employeeId}/attendance-summary` | Reporting.Read | SystemAdmin, HRAdmin, Management, SystemAdmin+HRAdmin |
| GET | `/api/employees/{employeeId}/attendance/{attendanceId}` | Attendance.Read | SystemAdmin, HRAdmin, SystemAdmin+HRAdmin |
| GET | `/api/employees/{employeeId}/compensations` | Payroll.Read | SystemAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| GET | `/api/employees/{employeeId}/compensations/{compensationId}` | Payroll.Read | SystemAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| GET | `/api/employees/{employeeId}/contacts` | Employee.Read | SystemAdmin, HRAdmin, SystemAdmin+HRAdmin |
| GET | `/api/employees/{employeeId}/contracts` | Employee.Read | SystemAdmin, HRAdmin, SystemAdmin+HRAdmin |
| GET | `/api/employees/{employeeId}/contracts/{contractId}` | Employee.Read | SystemAdmin, HRAdmin, SystemAdmin+HRAdmin |
| GET | `/api/employees/{employeeId}/documents` | HRDocuments.Read | HRAdmin, SystemAdmin+HRAdmin |
| GET | `/api/employees/{employeeId}/documents/{documentId}` | HRDocuments.Read | HRAdmin, SystemAdmin+HRAdmin |
| GET | `/api/employees/{employeeId}/documents/{documentId}/content` | HRDocuments.Read | HRAdmin, SystemAdmin+HRAdmin |
| GET | `/api/employees/{employeeId}/emergency-contacts` | Employee.Read | SystemAdmin, HRAdmin, SystemAdmin+HRAdmin |
| GET | `/api/employees/{employeeId}/employment-effective` | Employee.Read | SystemAdmin, HRAdmin, SystemAdmin+HRAdmin |
| GET | `/api/employees/{employeeId}/employment-history` | Employee.Read | SystemAdmin, HRAdmin, SystemAdmin+HRAdmin |
| GET | `/api/employees/{employeeId}/history` | Employee.Read or Payroll.Read | SystemAdmin, HRAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| GET | `/api/employees/{employeeId}/history/{historyId}` | Employee.Read or Payroll.Read | SystemAdmin, HRAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| GET | `/api/employees/{employeeId}/leave` | Leave.Read | SystemAdmin, HRAdmin, SystemAdmin+HRAdmin |
| GET | `/api/employees/{employeeId}/leave-balances` | Leave.Read | SystemAdmin, HRAdmin, SystemAdmin+HRAdmin |
| GET | `/api/employees/{employeeId}/leave-entitlements` | Leave.Read | SystemAdmin, HRAdmin, SystemAdmin+HRAdmin |
| GET | `/api/employees/{employeeId}/leave-entitlements/{entitlementId}/adjustments` | Leave.Read | SystemAdmin, HRAdmin, SystemAdmin+HRAdmin |
| GET | `/api/employees/{employeeId}/leave-sandwich-cases` | Leave.Evidence | SystemAdmin, HRAdmin, SystemAdmin+HRAdmin |
| GET | `/api/employees/{employeeId}/leave/{leaveId}` | Leave.Read | SystemAdmin, HRAdmin, SystemAdmin+HRAdmin |
| GET | `/api/employees/{employeeId}/leave/{leaveId}/evidence` | Leave.Evidence | SystemAdmin, HRAdmin, SystemAdmin+HRAdmin |
| GET | `/api/employees/{employeeId}/payroll-component-assignments` | Payroll.Read | SystemAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| GET | `/api/employees/{employeeId}/payroll-component-assignments/{assignmentId}` | Payroll.Read | SystemAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| GET | `/api/employees/{employeeId}/payroll-periods/{payrollPeriodId}/pit-preview` | Payroll.Read | SystemAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| GET | `/api/employees/{employeeId}/performance` | Employee.Read | SystemAdmin, HRAdmin, SystemAdmin+HRAdmin |
| GET | `/api/employees/{employeeId}/performance/{performanceRecordId}` | Employee.Read | SystemAdmin, HRAdmin, SystemAdmin+HRAdmin |
| GET | `/api/employees/{employeeId}/pit-payment-schedules/current/{taxYear}` | Payroll.Read | SystemAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| GET | `/api/employees/{employeeId}/pit-payment-schedules/{id}` | Payroll.Read | SystemAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| GET | `/api/employees/{employeeId}/statutory-enrollments` | Payroll.Read | SystemAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| GET | `/api/employees/{employeeId}/statutory-enrollments/resolve` | Payroll.Read | SystemAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| GET | `/api/employees/{employeeId}/statutory-enrollments/{id}` | Payroll.Read | SystemAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| GET | `/api/employees/{employeeId}/tax-declarations` | Payroll.Read | SystemAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| GET | `/api/employees/{employeeId}/tax-declarations/{id}` | Payroll.Read | SystemAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| GET | `/api/employees/{employeeId}/tax-declarations/{id}/claims` | Payroll.Read | SystemAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| GET | `/api/employees/{employeeId}/tax-declarations/{id}/opening-balance` | Payroll.Read | SystemAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| GET | `/api/employees/{employeeId}/tax-profile` | Payroll.Read | SystemAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| GET | `/api/employees/{employeeId}/tax-treatment` | Payroll.Read | SystemAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| GET | `/api/employees/{employeeId}/work-calendar` | Leave.Read | SystemAdmin, HRAdmin, SystemAdmin+HRAdmin |
| GET | `/api/employees/{employeeId}/work-calendar-assignments` | Leave.Read | SystemAdmin, HRAdmin, SystemAdmin+HRAdmin |
| GET | `/api/employees/{id}` | Employee.Read | SystemAdmin, HRAdmin, SystemAdmin+HRAdmin |
| GET | `/api/hr-documents/{documentId}` | HRDocuments.Read | HRAdmin, SystemAdmin+HRAdmin |
| GET | `/api/hr-documents/{documentId}/content` | HRDocuments.Read | HRAdmin, SystemAdmin+HRAdmin |
| GET | `/api/hr/leave-status` | Reporting.Read | SystemAdmin, HRAdmin, Management, SystemAdmin+HRAdmin |
| GET | `/api/hr/staff-overview` | Reporting.Read | SystemAdmin, HRAdmin, Management, SystemAdmin+HRAdmin |
| GET | `/api/leave-policies` | Leave.Read | SystemAdmin, HRAdmin, SystemAdmin+HRAdmin |
| GET | `/api/leave-policies/resolve` | Leave.Read | SystemAdmin, HRAdmin, SystemAdmin+HRAdmin |
| GET | `/api/leave-requests` | Leave.Read | SystemAdmin, HRAdmin, SystemAdmin+HRAdmin |
| GET | `/api/leave-requests/pending` | Leave.Read | SystemAdmin, HRAdmin, SystemAdmin+HRAdmin |
| GET | `/api/master-data/address-types` | MasterData.Read | SystemAdmin, HRAdmin, PayrollAdmin, Management, Employee, SystemAdmin+HRAdmin |
| GET | `/api/master-data/countries` | MasterData.Read | SystemAdmin, HRAdmin, PayrollAdmin, Management, Employee, SystemAdmin+HRAdmin |
| GET | `/api/master-data/departments` | MasterData.Read | SystemAdmin, HRAdmin, PayrollAdmin, Management, Employee, SystemAdmin+HRAdmin |
| GET | `/api/master-data/designations` | MasterData.Read | SystemAdmin, HRAdmin, PayrollAdmin, Management, Employee, SystemAdmin+HRAdmin |
| GET | `/api/master-data/employment-statuses` | MasterData.Read | SystemAdmin, HRAdmin, PayrollAdmin, Management, Employee, SystemAdmin+HRAdmin |
| GET | `/api/master-data/employment-types` | MasterData.Read | SystemAdmin, HRAdmin, PayrollAdmin, Management, Employee, SystemAdmin+HRAdmin |
| GET | `/api/master-data/genders` | MasterData.Read | SystemAdmin, HRAdmin, PayrollAdmin, Management, Employee, SystemAdmin+HRAdmin |
| GET | `/api/master-data/hiring-sources` | MasterData.Read | SystemAdmin, HRAdmin, PayrollAdmin, Management, Employee, SystemAdmin+HRAdmin |
| GET | `/api/master-data/locations` | MasterData.Read | SystemAdmin, HRAdmin, PayrollAdmin, Management, Employee, SystemAdmin+HRAdmin |
| GET | `/api/master-data/marital-statuses` | MasterData.Read | SystemAdmin, HRAdmin, PayrollAdmin, Management, Employee, SystemAdmin+HRAdmin |
| GET | `/api/master-data/nationalities` | MasterData.Read | SystemAdmin, HRAdmin, PayrollAdmin, Management, Employee, SystemAdmin+HRAdmin |
| GET | `/api/organization-profile` | Payroll.Read | SystemAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| GET | `/api/payroll-component-assignments` | Payroll.Read | SystemAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| GET | `/api/payroll-components` | Payroll.Read | SystemAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| GET | `/api/payroll-components/{id}` | Payroll.Read | SystemAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| GET | `/api/payroll-periods` | Payroll.Read | SystemAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| GET | `/api/payroll-periods/{id}` | Payroll.Read | SystemAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| GET | `/api/payroll-periods/{id}/summary` | Payroll.Read | SystemAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| GET | `/api/payroll-rules` | Payroll.Read | SystemAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| GET | `/api/payroll-rules/evaluation/{payrollPeriodId}/{employeeId}` | Payroll.Read | SystemAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| GET | `/api/payroll-rules/{id}` | Payroll.Read | SystemAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| GET | `/api/payroll-rules/{payrollRuleId}/targets` | Payroll.Read | SystemAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| GET | `/api/payroll-rules/{payrollRuleId}/targets/{targetId}` | Payroll.Read | SystemAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| GET | `/api/payroll-settings` | Payroll.Read | SystemAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| GET | `/api/payroll-settings/{id}` | Payroll.Read | SystemAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| GET | `/api/self/payrolls` | SelfService | Employee |
| GET | `/api/self/profile` | SelfService | Employee |
| GET | `/api/status` | MasterData.Read | SystemAdmin, HRAdmin, PayrollAdmin, Management, Employee, SystemAdmin+HRAdmin |
| GET | `/api/statutory-policy-versions` | Payroll.Read | SystemAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| GET | `/api/statutory-policy-versions/{id}` | Payroll.Read | SystemAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| GET | `/api/statutory-schemes` | Payroll.Read | SystemAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| GET | `/api/statutory-schemes/{id}` | Payroll.Read | SystemAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| GET | `/api/statutory-schemes/{id}/resolve` | Payroll.Read | SystemAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| GET | `/api/work-calendars` | Leave.Read | SystemAdmin, HRAdmin, SystemAdmin+HRAdmin |
| GET | `/api/work-calendars/{calendarId}/overrides` | Leave.Read | SystemAdmin, HRAdmin, SystemAdmin+HRAdmin |
| GET | `/api/work-calendars/{calendarId}/weekly-intervals` | Leave.Read | SystemAdmin, HRAdmin, SystemAdmin+HRAdmin |
| PATCH | `/api/admin/users/{id}/status` | Security.Manage | SystemAdmin, SystemAdmin+HRAdmin |
| PATCH | `/api/employees/{id}/status` | Employee.Manage | SystemAdmin, HRAdmin, SystemAdmin+HRAdmin |
| PATCH | `/api/payroll-components/{id}/status` | Payroll.Manage | SystemAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| PATCH | `/api/payroll-rules/{id}/status` | Payroll.Manage | SystemAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| PATCH | `/api/payroll-settings/{id}/status` | Payroll.Manage | SystemAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| POST | `/api/admin/users` | Security.Manage | SystemAdmin, SystemAdmin+HRAdmin |
| POST | `/api/admin/users/{id}/credential-delivery` | Security.Manage | SystemAdmin, SystemAdmin+HRAdmin |
| POST | `/api/admin/users/{id}/issue-credentials` | Security.Manage | SystemAdmin, SystemAdmin+HRAdmin |
| POST | `/api/employee-payrolls` | Payroll.Manage | SystemAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| POST | `/api/employee-payrolls/{id}/approve` | Payroll.Manage | SystemAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| POST | `/api/employee-payrolls/{id}/cancel` | Payroll.Manage | SystemAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| POST | `/api/employee-payrolls/{id}/mark-paid` | Payroll.Manage | SystemAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| POST | `/api/employee-payrolls/{payrollId}/lines` | Payroll.Manage | SystemAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| POST | `/api/employees` | Employee.Manage | SystemAdmin, HRAdmin, SystemAdmin+HRAdmin |
| POST | `/api/employees/{employeeId}/addresses` | Employee.Manage | SystemAdmin, HRAdmin, SystemAdmin+HRAdmin |
| POST | `/api/employees/{employeeId}/attendance` | Attendance.Manage | SystemAdmin, HRAdmin, SystemAdmin+HRAdmin |
| POST | `/api/employees/{employeeId}/attendance-days/{date}/adjudications` | Attendance.Manage | SystemAdmin, HRAdmin, SystemAdmin+HRAdmin |
| POST | `/api/employees/{employeeId}/attendance-days/{date}/confirm-absence` | Attendance.Finalize | SystemAdmin, HRAdmin, SystemAdmin+HRAdmin |
| POST | `/api/employees/{employeeId}/attendance-days/{date}/corrections` | Attendance.Manage | SystemAdmin, HRAdmin, SystemAdmin+HRAdmin |
| POST | `/api/employees/{employeeId}/attendance-days/{date}/finalize` | Attendance.Finalize | SystemAdmin, HRAdmin, SystemAdmin+HRAdmin |
| POST | `/api/employees/{employeeId}/attendance-days/{date}/reopen` | Attendance.Finalize | SystemAdmin, HRAdmin, SystemAdmin+HRAdmin |
| POST | `/api/employees/{employeeId}/attendance-events/manual` | Attendance.Manage | SystemAdmin, HRAdmin, SystemAdmin+HRAdmin |
| POST | `/api/employees/{employeeId}/compensations` | Payroll.Manage | SystemAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| POST | `/api/employees/{employeeId}/contacts` | Employee.Manage | SystemAdmin, HRAdmin, SystemAdmin+HRAdmin |
| POST | `/api/employees/{employeeId}/contracts` | Employee.Manage | SystemAdmin, HRAdmin, SystemAdmin+HRAdmin |
| POST | `/api/employees/{employeeId}/documents` | HRDocuments.Manage | HRAdmin, SystemAdmin+HRAdmin |
| POST | `/api/employees/{employeeId}/documents/{documentId}/archive` | HRDocuments.Manage | HRAdmin, SystemAdmin+HRAdmin |
| POST | `/api/employees/{employeeId}/documents/{documentId}/replace` | HRDocuments.Manage | HRAdmin, SystemAdmin+HRAdmin |
| POST | `/api/employees/{employeeId}/emergency-contacts` | Employee.Manage | SystemAdmin, HRAdmin, SystemAdmin+HRAdmin |
| POST | `/api/employees/{employeeId}/employment-changes` | Employee.Manage | SystemAdmin, HRAdmin, SystemAdmin+HRAdmin |
| POST | `/api/employees/{employeeId}/end-employment` | Employee.Manage | SystemAdmin, HRAdmin, SystemAdmin+HRAdmin |
| POST | `/api/employees/{employeeId}/history` | Employee.Manage or Payroll.Manage | SystemAdmin, HRAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| POST | `/api/employees/{employeeId}/leave` | Leave.Manage | SystemAdmin, HRAdmin, SystemAdmin+HRAdmin |
| POST | `/api/employees/{employeeId}/leave-entitlements` | Leave.Manage | SystemAdmin, HRAdmin, SystemAdmin+HRAdmin |
| POST | `/api/employees/{employeeId}/leave-entitlements/{entitlementId}/adjustments` | Leave.Manage | SystemAdmin, HRAdmin, SystemAdmin+HRAdmin |
| POST | `/api/employees/{employeeId}/leave-sandwich-cases/{caseId}/review` | Leave.Evidence | SystemAdmin, HRAdmin, SystemAdmin+HRAdmin |
| POST | `/api/employees/{employeeId}/leave/{leaveId}/approve` | Leave.Review | SystemAdmin, HRAdmin, SystemAdmin+HRAdmin |
| POST | `/api/employees/{employeeId}/leave/{leaveId}/cancel` | Leave.Manage | SystemAdmin, HRAdmin, SystemAdmin+HRAdmin |
| POST | `/api/employees/{employeeId}/leave/{leaveId}/evidence` | Leave.Evidence | SystemAdmin, HRAdmin, SystemAdmin+HRAdmin |
| POST | `/api/employees/{employeeId}/leave/{leaveId}/evidence/{evidenceId}/accept` | Leave.Evidence | SystemAdmin, HRAdmin, SystemAdmin+HRAdmin |
| POST | `/api/employees/{employeeId}/leave/{leaveId}/evidence/{evidenceId}/reject` | Leave.Evidence | SystemAdmin, HRAdmin, SystemAdmin+HRAdmin |
| POST | `/api/employees/{employeeId}/leave/{leaveId}/reject` | Leave.Review | SystemAdmin, HRAdmin, SystemAdmin+HRAdmin |
| POST | `/api/employees/{employeeId}/payroll-component-assignments` | Payroll.Manage | SystemAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| POST | `/api/employees/{employeeId}/performance` | Employee.Manage | SystemAdmin, HRAdmin, SystemAdmin+HRAdmin |
| POST | `/api/employees/{employeeId}/pit-payment-schedules` | Payroll.Manage | SystemAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| POST | `/api/employees/{employeeId}/pit-payment-schedules/{id}/verify` | Payroll.Manage | SystemAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| POST | `/api/employees/{employeeId}/rehire` | Employee.Manage | SystemAdmin, HRAdmin, SystemAdmin+HRAdmin |
| POST | `/api/employees/{employeeId}/statutory-enrollments` | Payroll.Manage | SystemAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| POST | `/api/employees/{employeeId}/statutory-enrollments/{id}/end` | Payroll.Manage | SystemAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| POST | `/api/employees/{employeeId}/tax-declarations` | Payroll.Manage | SystemAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| POST | `/api/employees/{employeeId}/tax-declarations/{id}/claims` | Payroll.Manage | SystemAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| POST | `/api/employees/{employeeId}/tax-declarations/{id}/verify` | Payroll.Manage | SystemAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| POST | `/api/employees/{employeeId}/work-calendar-assignments` | Leave.Manage | SystemAdmin, HRAdmin, SystemAdmin+HRAdmin |
| POST | `/api/hr-documents/{documentId}/archive` | HRDocuments.Manage | HRAdmin, SystemAdmin+HRAdmin |
| POST | `/api/hr-documents/{documentId}/replace` | HRDocuments.Manage | HRAdmin, SystemAdmin+HRAdmin |
| POST | `/api/leave-policies` | Leave.Manage | SystemAdmin, HRAdmin, SystemAdmin+HRAdmin |
| POST | `/api/leave-policies/{policyId}/publish` | Leave.Manage | SystemAdmin, HRAdmin, SystemAdmin+HRAdmin |
| POST | `/api/master-data/employment-statuses` | Employee.Manage | SystemAdmin, HRAdmin, SystemAdmin+HRAdmin |
| POST | `/api/payroll-components` | Payroll.Manage | SystemAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| POST | `/api/payroll-periods` | Payroll.Manage | SystemAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| POST | `/api/payroll-periods/{id}/cancel` | Payroll.Manage | SystemAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| POST | `/api/payroll-periods/{id}/close` | Payroll.Manage | SystemAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| POST | `/api/payroll-periods/{id}/start-processing` | Payroll.Manage | SystemAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| POST | `/api/payroll-periods/{payrollPeriodId}/generate` | Payroll.Manage | SystemAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| POST | `/api/payroll-periods/{payrollPeriodId}/preview` | Payroll.Manage | SystemAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| POST | `/api/payroll-rules` | Payroll.Manage | SystemAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| POST | `/api/payroll-rules/{payrollRuleId}/targets` | Payroll.Manage | SystemAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| POST | `/api/payroll-settings` | Payroll.Manage | SystemAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| POST | `/api/statutory-policy-versions` | Payroll.Manage | SystemAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| POST | `/api/statutory-policy-versions/{id}/publish` | Payroll.Manage | SystemAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| POST | `/api/statutory-schemes` | Payroll.Manage | SystemAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| POST | `/api/work-calendars` | Leave.Manage | SystemAdmin, HRAdmin, SystemAdmin+HRAdmin |
| POST | `/api/work-calendars/{calendarId}/overrides` | Leave.Manage | SystemAdmin, HRAdmin, SystemAdmin+HRAdmin |
| POST | `/api/work-calendars/{calendarId}/weekly-intervals` | Leave.Manage | SystemAdmin, HRAdmin, SystemAdmin+HRAdmin |
| PUT | `/api/admin/users/{id}/roles` | Security.Manage | SystemAdmin, SystemAdmin+HRAdmin |
| PUT | `/api/employee-payrolls/{id}` | Payroll.Manage | SystemAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| PUT | `/api/employee-payrolls/{payrollId}/lines/{lineId}` | Payroll.Manage | SystemAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| PUT | `/api/employees/{employeeId}/addresses/{addressId}` | Employee.Manage | SystemAdmin, HRAdmin, SystemAdmin+HRAdmin |
| PUT | `/api/employees/{employeeId}/attendance/{attendanceId}` | Attendance.Manage | SystemAdmin, HRAdmin, SystemAdmin+HRAdmin |
| PUT | `/api/employees/{employeeId}/compensations/{compensationId}` | Payroll.Manage | SystemAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| PUT | `/api/employees/{employeeId}/contacts/{contactId}` | Employee.Manage | SystemAdmin, HRAdmin, SystemAdmin+HRAdmin |
| PUT | `/api/employees/{employeeId}/contracts/{contractId}` | Employee.Manage | SystemAdmin, HRAdmin, SystemAdmin+HRAdmin |
| PUT | `/api/employees/{employeeId}/documents/{documentId}` | HRDocuments.Manage | HRAdmin, SystemAdmin+HRAdmin |
| PUT | `/api/employees/{employeeId}/emergency-contacts/{emergencyContactId}` | Employee.Manage | SystemAdmin, HRAdmin, SystemAdmin+HRAdmin |
| PUT | `/api/employees/{employeeId}/payroll-component-assignments/{assignmentId}` | Payroll.Manage | SystemAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| PUT | `/api/employees/{employeeId}/performance/{performanceRecordId}` | Employee.Manage | SystemAdmin, HRAdmin, SystemAdmin+HRAdmin |
| PUT | `/api/employees/{employeeId}/pit-payment-schedules/{id}` | Payroll.Manage | SystemAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| PUT | `/api/employees/{employeeId}/tax-declarations/{id}` | Payroll.Manage | SystemAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| PUT | `/api/employees/{employeeId}/tax-declarations/{id}/claims/{claimId}` | Payroll.Manage | SystemAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| PUT | `/api/employees/{employeeId}/tax-declarations/{id}/opening-balance` | Payroll.Manage | SystemAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| PUT | `/api/employees/{employeeId}/tax-declarations/{id}/treatment` | Payroll.Manage | SystemAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| PUT | `/api/employees/{employeeId}/tax-profile` | Payroll.Manage | SystemAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| PUT | `/api/employees/{id}` | Employee.Manage | SystemAdmin, HRAdmin, SystemAdmin+HRAdmin |
| PUT | `/api/leave-policies/{policyId}` | Leave.Manage | SystemAdmin, HRAdmin, SystemAdmin+HRAdmin |
| PUT | `/api/master-data/employment-statuses/{id}` | Employee.Manage | SystemAdmin, HRAdmin, SystemAdmin+HRAdmin |
| PUT | `/api/organization-profile` | Payroll.Manage | SystemAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| PUT | `/api/payroll-components/{id}` | Payroll.Manage | SystemAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| PUT | `/api/payroll-periods/{id}` | Payroll.Manage | SystemAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| PUT | `/api/payroll-rules/{id}` | Payroll.Manage | SystemAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| PUT | `/api/payroll-rules/{payrollRuleId}/targets/{targetId}` | Payroll.Manage | SystemAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| PUT | `/api/payroll-settings/{id}` | Payroll.Manage | SystemAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| PUT | `/api/statutory-policy-versions/{id}` | Payroll.Manage | SystemAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| PUT | `/api/statutory-policy-versions/{id}/personal-income-tax` | Payroll.Manage | SystemAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| PUT | `/api/statutory-policy-versions/{id}/social-security` | Payroll.Manage | SystemAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| PUT | `/api/statutory-schemes/{id}` | Payroll.Manage | SystemAdmin, PayrollAdmin, SystemAdmin+HRAdmin |
| PUT | `/api/work-calendars/{calendarId}` | Leave.Manage | SystemAdmin, HRAdmin, SystemAdmin+HRAdmin |
