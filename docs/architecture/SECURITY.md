# Security foundation

SIAMIS uses ASP.NET Core Identity with GUID `ApplicationUser` identifiers and cookie authentication. Account state, security stamps and current role grants participate in request validation. Mutations use the established CSRF protection. Audit actors come from authenticated server context; descriptive fields such as ChangedBy are not proof of actor identity.

Accounts are distinct from employees. Provisioning, activation, role changes, recovery and account status are explicit workflows. Recovery uses a trusted verified contact. Last-SystemAdmin protection prevents removing the final active system administrator. Rehire does not implicitly enable an account.

[Capability policies and ownership](AUTHORIZATION.md) govern API access. System administration does not grant confidential HR document access. Salary Change history requires Payroll capability independently of ordinary employee history access.

HR document binaries use private storage, opaque server-generated keys, SHA-256 integrity, immutable versions and archival lifecycle. Uploads support PDF/JPEG/PNG with a 20 MiB limit. Storage/metadata failure handling is established by D14; malware scanning integration remains deferred.

Production enforces HTTPS and secure cookies. Proxy trust and credentialed CORS require explicit configuration. Production credential delivery and private storage fail closed when suitable configuration is unavailable. [Production requirements](../deployment/PRODUCTION.md) are deployment obligations, not completed infrastructure.

The [D15 audit](../archive/checkpoints/d15/D15-REPORT.md) records verification and practical limits. This document summarizes the frozen foundation; it does not introduce new security behavior.
