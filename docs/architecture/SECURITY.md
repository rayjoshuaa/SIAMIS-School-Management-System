# Security foundation

SIAMIS uses ASP.NET Core Identity with GUID `ApplicationUser` identifiers and cookie authentication. Account state, security stamps and current role grants participate in request validation. Mutations use the established CSRF protection. Audit actors come from authenticated server context; descriptive fields such as ChangedBy are not proof of actor identity.

Accounts are distinct from employees. Provisioning, activation, role changes, recovery and account status are explicit workflows. Recovery uses a trusted verified contact. Last-SystemAdmin protection prevents removing the final active system administrator. Rehire does not implicitly enable an account.

[Capability policies and ownership](AUTHORIZATION.md) govern API access. System administration does not grant confidential HR document access. Salary Change history requires Payroll capability independently of ordinary employee history access.

HR document binaries use private storage, opaque server-generated keys, SHA-256 integrity, immutable versions and archival lifecycle. Uploads support PDF/JPEG/PNG with a 20 MiB limit. Storage/metadata failure handling is established by D14; malware scanning integration remains deferred.

Employee photos use dedicated private metadata/storage and Employee.Read/Manage, without broadening HRDocuments access. Actual JPEG/PNG decoding validates the 5 MiB and 4096 × 4096 limits. Superseded/removed revisions are retained; no automatic purge or EXIF stripping is claimed. Coordinate privacy/retention and binary/database backups before deployment.

Employee identity reservations prevent renumbering/reuse. Controlled permanent deletion fails closed on protected dependencies and keeps permanent reservations/audit evidence. Durable registration receipts prevent duplicate creation without person matching. Public login remains available during an initial probe outage; protected authenticated refresh outages block access/commands while preserving RAM-only edits. See [current authentication lifecycle](../frontend/AUTHENTICATION.md).

Production enforces HTTPS and secure cookies. Proxy trust and credentialed CORS require explicit configuration. Production credential delivery and private storage fail closed when suitable configuration is unavailable. [Production requirements](../deployment/PRODUCTION.md) are deployment obligations, not completed infrastructure.

The [D15 audit](../archive/checkpoints/d15/D15-REPORT.md) records the historical foundation. [Current release notes](../releases/v0.5.1.md) distinguish subsequent verification, limitations and pending release approval. This document introduces no new security behavior.
