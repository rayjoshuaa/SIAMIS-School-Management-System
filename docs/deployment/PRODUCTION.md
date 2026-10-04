# Production deployment boundary

HR Backend V1 is frozen, but Production deployment is not claimed. Local D15 verification does not validate a server, proxy, delivery provider or disaster-recovery plan.

## Implemented application behavior

- HTTPS enforcement and secure Production cookies; established Identity/session/CSRF validation.
- Explicit `Security:KnownProxies` trust configuration. Empty configuration does not enable blanket forwarding trust.
- Explicit `Security:AllowedOrigins` for credentialed CORS; Production origins require HTTPS.
- Configurable `Security:DataProtectionKeyDirectory` for persistent keys.
- Private document storage abstraction. `PrivateDocuments:Root` must be absolute and outside the web root; unconfigured Production storage fails closed.
- Development credential delivery is not enabled as a Production fallback.

## Required infrastructure and configuration

| Requirement | Deployment responsibility |
|---|---|
| TLS / reverse proxy | Validate certificates, HTTPS termination, forwarded headers and exact trusted proxy addresses |
| Data Protection | Persist and protect keys; validate permissions and continuity across restarts/instances |
| Credential delivery | Configure and verify a real trusted delivery provider; protect delivery credentials |
| SQL Server | Supply protected connection configuration, appropriate certificate validation and least-privilege access; review migrations separately |
| Private HR documents | Configure storage path and service-account ACLs, integrity operations and recovery; do not serve blobs publicly |
| Malware scanning | Select and integrate scanning before relying on it as a deployed protection |
| Backups / disaster recovery | Coordinate SQL and binary backups; test restore, key recovery and recovery procedures |
| Monitoring | Establish operational/security monitoring and incident handling |

Store environment-specific secrets outside the repository. Production migration and rollout approval remain separate actions. No Production database, infrastructure or service was changed during cleanup.

See the [HR deferred register](../modules/hr/HR-V1-FREEZE.md#deferred-register) and [security foundation](../architecture/SECURITY.md).
