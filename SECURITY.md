# Security Policy

## Supported versions

Security fixes are applied to the **`dev`** integration branch and to the latest published release tag (`vMAJOR.MINOR.PATCH`).

| Version | Supported |
|---------|-----------|
| Latest release (`v*`) | ✅ |
| `dev` branch | ✅ |
| Older tags | ❌ Best-effort only |

If you run an older image or build, upgrade to the latest release when a security advisory is published.

## Reporting a vulnerability

**Please do not open a public GitHub Issue for security vulnerabilities.**

Report privately so we can fix the issue before it is disclosed:

1. Use **[GitHub Private Vulnerability Reporting](https://github.com/0x-mhmdnzri/NotificationHub/security/advisories/new)** (preferred), or  
2. Email the maintainer if private reporting is unavailable: contact via the profile of [@0x-mhmdnzri](https://github.com/0x-mhmdnzri).

### What to include

- Description of the issue and impact  
- Affected component (API, Host, plugin, worker, admin UI, dependency, …)  
- Steps to reproduce or a proof of concept  
- Suggested severity (optional)  
- Your preferred credit name (optional)

### What we will do

| Step | Target |
|------|--------|
| Acknowledge receipt | Within **72 hours** |
| Initial assessment | Within **7 days** |
| Fix or mitigation plan | Depends on severity; critical issues prioritized |
| Public disclosure | Coordinated after a fix is available (or risk is accepted) |

We may ask for more detail. Please do not share exploit details publicly until a fixed release is out (or we agree otherwise).

## Scope

In scope examples:

- Authentication / API key handling  
- Injection, SSRF, unsafe deserialization in the Host or plugins  
- Privilege escalation across tenants or roles  
- Secrets leakage in logs or responses  
- High/Critical issues in **direct** production dependencies that we can upgrade or mitigate  

Out of scope (unless there is a clear, exploitable impact on this project):

- Denial of service requiring unrealistic traffic volumes  
- Issues only in outdated/unsupported deployments  
- Vulnerabilities solely in third-party services (SendGrid, Twilio, …) with no project-side misconfiguration  
- Social engineering or physical attacks  

## Safe harbor

We will not pursue legal action against researchers who:

- Make a good-faith effort to avoid privacy violations, data destruction, and service disruption  
- Report findings promptly and privately  
- Do not exploit the issue beyond what is needed to demonstrate it  

## Hardening references

Project security-related notes (operational, not a substitute for this policy):

- [docs/ops/security-hardening-phase0.md](docs/ops/security-hardening-phase0.md)  
- CI: NuGet vulnerability audit, Trivy image scan, CodeQL (see `.github/workflows/security.yml`)

## Preferences for patches

- Prefer minimal, well-tested fixes  
- Follow [Conventional Commits](docs/ops/commit-conventions.md); security fixes are typically `fix(security): ...`  
- Versioning follows [SemVer](docs/ops/versioning.md); security fixes without contract breaks are **PATCH** releases  

Thank you for helping keep NotificationHub and its users safe.
