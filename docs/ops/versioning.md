# Versioning (SemVer 2.0.0)

**Source of truth:** `version.json` + `Directory.Build.props` (`VersionPrefix`).

## Scheme

```text
MAJOR.MINOR.PATCH
```

| Change | Bump |
|--------|------|
| Breaking public API / contract | MAJOR |
| Backward-compatible feature | MINOR |
| Bug fix / internal / docs | PATCH |

Pre-1.0: `0.x.y` — breaking changes may appear between MINOR versions.

## Conventional Commits → bump

| Commit prefix | Bump |
|---------------|------|
| `feat!:` / `fix!:` / `BREAKING CHANGE:` | MAJOR |
| `feat:` | MINOR |
| `fix:` / `perf:` / `refactor:` / `docs:` / `test:` / `chore:` / `ci:` / `build:` / `style:` | PATCH |

## Workflows

1. **`.github/workflows/version.yml`** — on merge to `dev` (PR closed + merged, or push to `dev`):
   - reads commits since last `v*` tag
   - computes next SemVer
   - updates `version.json` + `Directory.Build.props`
   - creates annotated tag `vX.Y.Z`
   - pushes tag (triggers Release)

2. **`.github/workflows/release.yml`** — on tag `v*`:
   - publishes GitHub Release
   - builds & pushes container `ghcr.io/...:X.Y.Z`

## Runtime

```http
GET /api/v1/version
```

Returns `{ version, commit, product, environment }` (no secrets).

## Rules

- Tags are immutable (`v1.2.3` never moved).
- Wrong release → ship a new PATCH.
- API path stays `/api/v1` independently of app SemVer.
