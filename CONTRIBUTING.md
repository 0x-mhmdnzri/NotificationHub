# Contributing to NotificationHub

Thanks for helping improve the project.

## Branching

- **`dev`** — integration branch (protected: required status checks, no force-push/delete).
- Feature work: branch from `dev`, open a PR **into `dev`**.
- Do not force-push `dev`.

## Commit messages

Follow **Conventional Commits**. Full guide: [docs/ops/commit-conventions.md](docs/ops/commit-conventions.md).

```text
type(scope): subject
```

Examples: `feat(campaigns): add CSV import`, `fix(ef): idempotent Broadcast migration`, `ci(security): pin trivy-action`.

Breaking changes: `feat(api)!: ...` and/or footer `BREAKING CHANGE:`.

## Versioning

Product version is **SemVer 2.0.0**. Guide: [docs/ops/versioning.md](docs/ops/versioning.md).

- Source of truth: `version.json` + `Directory.Build.props`
- On merge to `dev`, [version.yml](.github/workflows/version.yml) may create tag `vX.Y.Z`
- Tags trigger [release.yml](.github/workflows/release.yml) (GitHub Release + GHCR image)
- Runtime: `GET /api/v1/version`

| Signal | Bump |
|--------|------|
| Breaking (`!` / `BREAKING CHANGE`) | MAJOR |
| `feat` | MINOR |
| `fix` / `perf` / `docs` / … | PATCH |

## Checks required on `dev`

PRs must pass (among others):

- Build and Test  
- Unit tests (all)  
- dotnet format verify  
- Build Docker Image  
- Trivy scan (Docker image)  
- NuGet vulnerability audit  

Locally:

```bash
dotnet restore
dotnet format --verify-no-changes --severity error
dotnet test
```

## Docs

- Architecture: [docs/README.md](docs/README.md) (ADRs)
- Ops runbooks: [docs/ops/](docs/ops/)
- Plugin SDK: [docs/sdk/plugin-sdk.md](docs/sdk/plugin-sdk.md)

When you change architecture, update or add an ADR. When you change process (commits, versioning, CI), update the ops docs in the same PR.

## License

By contributing, you agree that your contributions are licensed under the same **MIT** license as the repository.
