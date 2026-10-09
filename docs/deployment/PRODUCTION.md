# Production deployment boundary

HR Backend V1 is frozen, but Production deployment is not claimed. Local D15 verification does not validate a server, proxy, delivery provider or disaster-recovery plan.

## Implemented application behavior

- HTTPS enforcement and secure Production cookies; established Identity/session/CSRF validation.
- Explicit `Security:KnownProxies` trust configuration. Empty configuration does not enable blanket forwarding trust.
- Explicit `Security:AllowedOrigins` for credentialed CORS; Production origins require HTTPS.
- Configurable `Security:DataProtectionKeyDirectory` for persistent keys.
- Private document storage abstraction. `PrivateDocuments:Root` must be absolute and outside the web root; unconfigured Production storage fails closed.
- Separate employee-photo storage: `EmployeePhotos:Root` must be absolute, outside the web root and outside the document root. Unconfigured Production photo operations fail closed; authenticated retrieval must not become public image hosting.
- Development credential delivery is not enabled as a Production fallback.

## Required infrastructure and configuration

| Requirement                 | Deployment responsibility                                                                                                              |
| --------------------------- | -------------------------------------------------------------------------------------------------------------------------------------- |
| TLS / reverse proxy         | Validate certificates, HTTPS termination, forwarded headers and exact trusted proxy addresses                                          |
| Data Protection             | Persist and protect keys; validate permissions and continuity across restarts/instances                                                |
| Credential delivery         | Configure and verify a real trusted delivery provider; protect delivery credentials                                                    |
| SQL Server                  | Supply protected connection configuration, appropriate certificate validation and least-privilege access; review migrations separately |
| Private HR documents        | Configure storage path and service-account ACLs, integrity operations and recovery; do not serve blobs publicly                        |
| Employee photos             | Configure separate private storage, least-privilege ACLs, explicit retention and coordinated SQL/binary backups                        |
| Malware scanning            | Select and integrate scanning before relying on it as a deployed protection                                                            |
| Backups / disaster recovery | Coordinate SQL and binary backups; test restore, key recovery and recovery procedures                                                  |
| Monitoring                  | Establish operational/security monitoring and incident handling                                                                        |

Store environment-specific secrets outside the repository. Production migration and rollout approval remain separate actions. No Production database, infrastructure or service was changed during cleanup.

The 46-migration Development baseline does not authorize Production migration. QA identities, old credential files, raw authenticated screenshots, local artifacts/certificates and SQL backups must not be published. Ignore rules do not remove already-tracked files; the owner must review release scope/history. The API's public legacy AdminLTE landing page remains present and requires a hosting-disposition decision, not a Stage 3 redesign.

See the [HR deferred register](../modules/hr/HR-V1-FREEZE.md#deferred-register) and [security foundation](../architecture/SECURITY.md).
