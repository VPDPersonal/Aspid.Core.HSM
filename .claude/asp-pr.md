# PR rules for Aspid.Core.HSM

- Types: `feat` `fix` `perf` `refactor` `docs` `test` `chore` `ci` `style`.
- Scopes (or none): `hsm` `unity` `generator` `samples` `tests` `package` · `readme` `changelog` `release` `github` `claude` `deps`.
- Shipped package code changed (`Source/`, `Unity/Runtime/`, the generator DLL) → an entry under `[Unreleased]` in `CHANGELOG.md`.
  Users see no change? Say so in the PR body instead.

## Review loop

The bot review rules with the verdict format are in `.github/claude-review.md`.

Logic paths:

- package: `Aspid.Core.HSM/Assets/Plugins/Aspid/Core/HSM/`, `Aspid.Core.HSM/Assets/_Scripts/`, `Aspid.Core.HSM/Packages/`;
- .NET: `Aspid.Core.HSM.Generators/`;
- CI: `.github/workflows/`, `.github/claude-review.md`.

Other paths are text: `docs/`, `.claude/skills/`, other `*.md`.
A fix there needs no re-review.
