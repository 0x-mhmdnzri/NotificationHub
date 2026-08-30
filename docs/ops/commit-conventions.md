# Commit message conventions

NotificationHub uses **Conventional Commits** so history stays readable and [SemVer automation](versioning.md) can classify changes.

## Format

```text
type(scope): subject

[optional body]

[optional footer]
```

- **type** — required (see table below)  
- **scope** — optional area (`api`, `host`, `campaigns`, `ef`, `ci`, `deps`, …)  
- **subject** — imperative, concise, no trailing period  
- **Breaking change** — `!` after type/scope and/or footer `BREAKING CHANGE: ...`

### Examples

```text
feat(auth): add password recovery
fix(api): fix connection timeout error
chore(deps): upgrade all dependencies
docs(api): update developer guide
test(e2e): add checkout flow test
refactor(core): simplify cache-lookup logic
style(ui): fix mobile CSS alignment
perf(sql): index complex queries for speed
build(deps): update all packages
ci(travis): add new node environment
revert: add login logic
chore: update code generation script
```

Breaking:

```text
feat(api)!: replace payment contract

BREAKING CHANGE: PaymentRequest.amount is now mandatory.
```

---

## Types

| Type | Meaning | Typical SemVer |
|------|---------|----------------|
| **feat** | New user-facing capability | MINOR |
| **fix** | Bug fix / unintended behavior | PATCH |
| **perf** | Performance improvement | PATCH |
| **refactor** | Restructure without behavior change | PATCH |
| **style** | Formatting only (no logic) | PATCH |
| **test** | Add or fix tests | PATCH |
| **docs** | Documentation only | PATCH |
| **build** | Build system / external build deps | PATCH |
| **ci** | CI config and scripts | PATCH |
| **chore** | Maintenance, tooling, non-src noise | PATCH |
| **revert** | Reverts a previous commit | PATCH (or match original impact) |

Any type with **`!`** or a **`BREAKING CHANGE:`** footer → **MAJOR** (after 1.0; still signal clearly under 0.x).

---

## Scopes (suggested)

| Scope | Area |
|-------|------|
| `host` | API host / Program / middleware |
| `api` | HTTP contracts / endpoints |
| `campaigns` | Broadcast / batch campaigns |
| `messaging` | Outbox, RabbitMQ, consumers |
| `ef` / `db` | EF Core, migrations, schema |
| `plugins` | Channel plugins |
| `admin` | Admin UI |
| `ci` | GitHub Actions |
| `deps` | Dependency bumps |
| `security` | Auth, hardening, scanners |

Use a new scope when it helps filtering; omit scope when the change is cross-cutting.

---

## Relation to pull requests

- Prefer **one logical change per commit** on feature branches; squash/rebase is fine if the final messages on `dev` stay conventional.
- PR title should itself be conventional — it often becomes the merge commit subject.
- Merges into **`dev`** feed [version.yml](../../.github/workflows/version.yml) for the next tag.

---

## What not to do

- Vague subjects: `update`, `fix stuff`, `WIP`
- Commit secrets or large generated blobs
- Mix unrelated features in one commit when it muddies SemVer classification
- Use `feat` for internal-only refactors (prefer `refactor` / `chore`)

---

## See also

- [versioning.md](versioning.md) — SemVer rules and release workflow  
- Root [CONTRIBUTING.md](../../CONTRIBUTING.md)  
