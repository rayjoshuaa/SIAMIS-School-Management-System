# Controlled Development administrator recovery

This explicit operator tool implements the product owner's approved reset of
`f4-bootstrap-helper` only. It is not included in API startup, the solution,
or an HTTP endpoint. It creates no accounts and changes no role grants.

## Private interactive use

From the repository root in your own PowerShell terminal:

```powershell
dotnet build tools/development/ResetDevelopmentAdministrator/ResetDevelopmentAdministrator.csproj -c Release
$env:DOTNET_ENVIRONMENT = 'Development'
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet ./tools/development/ResetDevelopmentAdministrator/bin/Release/net10.0/ResetDevelopmentAdministrator.dll
```

Choose and confirm the password privately at the two hidden prompts. Escape
cancels. Do not paste the password into chat or pass it on the command line.
The tool does not read the superseded encrypted QA credential files, save a
password, print tokens, or create a browser session.

It rejects redirected input, arbitrary arguments, non-Development environments,
and any effective database connection except Windows-authenticated
`localhost/SIAMIS`. Unexpected account role, active-state or Employee-link drift
also aborts the transaction. Identity enforces the application password policy.

## Reset semantics

The existing application Identity registration supplies the real password
validators, hasher and token provider. GeneratePasswordResetTokenAsync and
ResetPasswordAsync perform the reset; no hash is manually constructed. Identity
rotates the security stamp so existing application cookie validation rejects
previous sessions. AdministrationVersion also rotates. A serializable transaction
locks the existing SystemAdmin role gate before the helper user and atomically
records DevelopmentAdministratorCredentialReset in SecurityAuditEvents.

ActorUserId is null because this is explicitly authorized local operator recovery,
not an authenticated HTTP action. ResourceId identifies the existing target.
No authenticated actor is fabricated. No audit record is deleted.

Roles, Employee linkage, email trust, active state, forced-password state and
lockout policy are preserved. Normal subsequent login may clear the existing
failed-login counter and creates the established security audit events.

Afterward sign in normally, verify administration access, sign out, and sign in
again. No QA persona provisioning is performed by this tool.

## Isolation checks (no database access)

```powershell
dotnet ./tools/development/ResetDevelopmentAdministrator/bin/Release/net10.0/ResetDevelopmentAdministrator.dll --verify-guards
```

The checks accept Development/local/integrated authentication and reject
Production, Staging, a remote server, another database and SQL credentials.
The reset tool is intentionally separate from the superseded unverified
Provision-QaPersonas.ps1; do not run that provisioner.
