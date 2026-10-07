# PR rules for Aspid.Core.HSM

Checked by `.github/workflows/pr-checks.yml`; keep the types and scopes in sync with it.

- Types: `feat` `fix` `perf` `refactor` `docs` `test` `chore` `ci` `style`.
- Scopes (or none): `hsm` `unity` `generator` `samples` `tests` `package` · `readme` `changelog` `release` `github` `claude` `deps`.
- CI sets `type:*` and `breaking-change` from the title and `area:*` from the paths; do not pass them.
- Shipped package code changed (`Source/`, `Unity/Runtime/`, `Samples~/`, the generator DLL) and users see it → add an entry under `[Unreleased]` in `CHANGELOG.md`.
  The CHANGELOG is not mandatory yet: CI does not check it.

## Review loop

The bot review rules with the verdict format are in `.github/claude-review.md`.

Logic paths:

- package: `Aspid.Core.HSM/Assets/Plugins/Aspid/Core/HSM/`, `Aspid.Core.HSM/Assets/_Scripts/`, `Aspid.Core.HSM/Packages/`;
- .NET: `Aspid.Core.HSM.Generators/`;
- CI: `scripts/`, `.github/workflows/`, `.github/claude-review.md`, `global.json`.

Other paths are text: `docs/`, `.claude/skills/`, other `*.md`.
A fix there needs no re-review.
