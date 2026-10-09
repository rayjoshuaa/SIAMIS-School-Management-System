# Authorization

Policies use explicit capabilities. Role grants are independent of employee department, designation or management position. A user can hold multiple explicitly assigned roles.

| Role         | Default boundary                                                                                                                    |
| ------------ | ----------------------------------------------------------------------------------------------------------------------------------- |
| SystemAdmin  | Employee read/manage/permanent-delete, Leave, Attendance, Payroll, Reporting and Security capabilities; no HRDocuments access alone |
| HRAdmin      | Employee, Leave, Attendance, Reporting and HRDocuments; no Payroll access by default                                                |
| PayrollAdmin | Payroll read/manage and supporting master-data reads                                                                                |
| Management   | Reporting and supporting master-data reads; no detailed Payroll or HRDocuments access                                               |
| Employee     | Authenticated self-service and supporting master-data reads; no organization-wide HR/Payroll access                                 |

Current grants are authoritative in [SecurityCapabilities](../../src/SIAMIS.Application/Security/SecurityContracts.cs). Every recognized role also receives `MasterData.Read`. The [archived D15 authorization matrix](../archive/checkpoints/d15/D15-AUTHORIZATION-MATRIX.md) records the freeze before the approved permanent-deletion extension.

## Ownership and confidential information

- Employee self-service identity comes from authenticated User → Employee linkage. Client-supplied EmployeeId does not establish ownership.
- Photos require `Employee.Read` to view and `Employee.Manage` to upload/replace/remove. HRAdmin and SystemAdmin have these explicit grants; neither gets a hidden bypass. PayrollAdmin/Employee do not manage photos by default. Photo capabilities do not confer HRDocuments access.
- Permanent deletion requires `Employee.DeletePermanent` plus established read/eligibility checks. Only SystemAdmin has the default deletion grant; Employee.Manage alone is insufficient. Protected dependencies and linked accounts block deletion.
- User Accounts requires `Security.Manage`. Passwordless provisioning and secure activation remain separate from employee registration; account and employment states are distinct.
- Payroll/payslip self-service is limited to the employee's own Approved/Paid information.
- EmployeeDocuments metadata and secure binary operations require `HRDocuments.Read` or `HRDocuments.Manage`. `Employee.Read/Manage` is insufficient. SystemAdmin + HRAdmin obtains document access through HRAdmin, with no hidden system-admin bypass.
- Leave evidence retains its established authorization/lifecycle. Associated confidential binary access requires the compatible document gate as well.
- EmployeeHistory distinguishes ordinary events from Salary Change events. Financial history requires Payroll.Read/Manage. Authorized lists filter in SQL; unauthorized direct event access returns 404, and denied creates return 403.

Per-user capability overrides, new approval stages and maker-checker rules are outside the frozen scope. See [HR V1 Freeze](../modules/hr/HR-V1-FREEZE.md).
