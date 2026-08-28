# GitHub Actions

| Workflow | Trigger | Purpose |
|----------|---------|---------|
| `dotnet.yml` | push/PR | Restore, build, test, publish Host + NuGet audit |
| `tests.yml` | push/PR | All unit test projects + coverage |
| `architecture.yml` | push/PR | Architecture / layer rules |
| `format.yml` | push/PR | `dotnet format --verify-no-changes` (soft fail) |
| `integration.yml` | push/PR | Host smoke with Postgres + RabbitMQ |
| `admin-ci.yml` | paths `apps/admin/**` | Next.js install + production build |
| `docker-build.yml` | push/PR | Build image without push |
| `docker-publish.yml` | main/dev/tags | Push to GHCR |
| `security.yml` | push/PR + weekly | NuGet High/Critical gate, CodeQL, Trivy |
| `sbom-sign.yml` | after publish / manual | SBOM + Cosign (keyless) |
| `release.yml` | tag `v*` | GitHub Release + versioned image |
| `nightly.yml` | daily 03:00 UTC | Full suite + docker |
| `docs.yml` | docs paths | Markdown link check |
| `license.yml` | weekly | Package list artifact |

Dependabot: `.github/dependabot.yml` (NuGet, npm admin, Actions, Docker).
