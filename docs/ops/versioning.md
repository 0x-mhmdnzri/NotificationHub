# Versioning (SemVer 2.0.0)

Unified product version for NotificationHub (application, packages built from this repo, and container tags).

**API route version** (`/api/v1/...`) is independent of application SemVer. Do not equate `app 2.0.0` with `API v2`.

---

## Source of truth

| Location | Role |
|----------|------|
| [`version.json`](../../version.json) | Canonical product version (`MAJOR.MINOR.PATCH`) |
| [`Directory.Build.props`](../../Directory.Build.props) | `VersionPrefix`, `AssemblyVersion`, `FileVersion`, `InformationalVersion` |
| Git tag `vMAJOR.MINOR.PATCH` | Immutable release identity |
| GitHub Release | Notes + publish artifacts |

Avoid diverging versions across `.csproj` files, Docker tags, and packages. CI derives artifacts from the tag / `VersionPrefix`.

---

## Git tag naming (required)

Tags **must** match SemVer 2.0.0 with a `v` prefix:

```text
vMAJOR.MINOR.PATCH
```

Valid examples:

```text
v0.1.0
v0.2.0
v1.0.0
v1.4.2
v2.0.0-rc.1
```

Invalid (do not create):

```text
0.1.0              ← missing v prefix
v1.0               ← incomplete
v1.0.0-final       ← non-SemVer label
v1.0.0-production
2026.08.1          ← CalVer (not used)
latest
release-1
```

- Tags are **immutable**: never move `v1.2.3` to another commit.
- Wrong release → publish a new **PATCH** (e.g. `v1.2.4`), do not rewrite history.
- GitHub Release **title** uses the bare version (`1.2.3`); the **tag** keeps the `v` prefix (`v1.2.3`).
- Container images use the bare version: `ghcr.io/.../notificationhub:1.2.3`.


## SemVer rules

```text
MAJOR.MINOR.PATCH
```

| Bump | When |
|------|------|
| **MAJOR** | Existing consumers can break (public API, contracts, auth, events, required fields, incompatible defaults) |
| **MINOR** | Backward-compatible functionality (new endpoints, optional fields, features) |
| **PATCH** | Backward-compatible fixes, security patches without contract change, perf, refactor, docs, tests, internal deps |

### Pre-1.0 (`0.x.y`)

Until the first stable public contract:

- Start from `0.1.0` for the first meaningful feature set.
- Breaking changes **may** occur between MINOR versions under `0.x`.
- Prefer explicit `feat!:` / `BREAKING CHANGE` so automation still bumps MAJOR when you intend a hard break after 1.0.

### Pre-release (optional)

```text
2.0.0-alpha.1
2.0.0-beta.1
2.0.0-rc.1
2.0.0
```

Use only when intentionally shipping non-production builds. Tags: `v2.0.0-rc.1`.

### Build metadata

```text
2.4.1+sha.8f31c2a
```

Carried in `InformationalVersion`. Does **not** affect SemVer precedence.

---

## Conventional Commits → version bump

Commit messages follow [commit conventions](commit-conventions.md). Automation maps them as:

| Commit signal | Version bump |
|---------------|--------------|
| `type!:` or `type(scope)!:` or footer `BREAKING CHANGE:` | **MAJOR** |
| `feat:` / `feat(scope):` | **MINOR** |
| `fix`, `perf`, `refactor`, `docs`, `test`, `chore`, `ci`, `build`, `style`, `revert` | **PATCH** |

Do not rely on prefixes alone for public package contracts — still review API/event/DB compatibility before release.

---

## Workflows

### 1. Version bump — `.github/workflows/version.yml`

**Triggers**

- Pull request **merged** into `dev`
- Manual `workflow_dispatch` (optional force: `major` \| `minor` \| `patch` \| `auto`)

**Steps**

1. Read current version from `version.json`.
2. Inspect commits since last `v*` tag (conventional subjects).
3. Compute next SemVer.
4. Update `version.json` and `Directory.Build.props`.
5. Create annotated tag `vX.Y.Z` (immutable — never move).
6. Push tag (triggers Release).  
   - Optional repo secret **`VERSION_BUMP_TOKEN`**: PAT that can push the version commit to protected `dev`. Without it, tag push is still attempted; file updates may need a follow-up PR if branch protection blocks the bot.

### 2. Release — `.github/workflows/release.yml`

**Triggers:** push of tag `v*`

**Produces**

- GitHub Release (generated notes)
- Container image on GHCR: `ghcr.io/<org>/NotificationHub:X.Y.Z` (and related tags)

Create releases from tags, not by hand-editing an untagged draft only:  
https://github.com/0x-mhmdnzri/NotificationHub/releases

### 3. Branch protection (`dev`)

Merges into `dev` require green checks (build, format, tests, Trivy, NuGet audit, Docker build). Force-push and branch deletion are disabled.

---

## Runtime identity

```http
GET /api/v1/version
```

Example response:

```json
{
  "version": "0.1.0",
  "commit": "aee74c8",
  "product": "NotificationHub",
  "environment": "Development"
}
```

No secrets, connection strings, or infrastructure credentials.

---

## Containers

Prefer immutable tags:

```text
ghcr.io/0x-mhmdnzri/notificationhub:0.1.0
```

Production should pin to SemVer or digest, not only `latest`.

---

## Database & messages

- **Schema** version = EF migrations / history table — not app SemVer.
- **Integration events** — additive evolution preferred; breaking event shapes need explicit contract versioning (see [INTEGRATION-EVENT-VERSIONING.md](../INTEGRATION-EVENT-VERSIONING.md)).

---

## Anti-patterns

- PATCH for a breaking API change
- MAJOR for a trivial internal fix
- Moving or reusing tag `v1.2.3` on a different commit
- Different versions in `version.json`, image tag, and assembly with no single source
- Treating `/api/v1` as the application SemVer

---

## Checklist before a release

1. Previous tag / version identified  
2. Changes classified (breaking / feature / fix)  
3. API, package, event, and migration compatibility reviewed  
4. Tag `vX.Y.Z` created from the intended commit  
5. Release notes list Added / Fixed / Breaking  
6. Runtime `GET /api/v1/version` matches the tag  

When uncertain, choose the bump by **consumer compatibility**, not by size of the diff.
