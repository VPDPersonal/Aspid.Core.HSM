# PR rules for Aspid.Core.HSM

Checked by `.github/workflows/pr-checks.yml`; keep the types and scopes in sync with it.

- Types: `feat` `fix` `perf` `refactor` `docs` `test` `chore` `ci` `style`.
- Scopes (or none): `hsm` `unity` `generator` `samples` `tests` `package` · `readme` `changelog` `release` `github` `claude` `deps`.
- CI sets `type:*` and `breaking-change` from the title and `area:*` from the paths; do not pass them.
- Shipped package code changed (`Source/`, `Unity/Runtime/`, `Samples~/`, the generator DLL) and users see it → add an entry under `[Unreleased]` in `CHANGELOG.md`.
  The CHANGELOG is not mandatory yet: CI does not check it.

## Manual review

Automatic Claude PR reviews are disabled. Request a review with an `@claude` comment only when the user asks.
The manual review rules and verdict format are in `.github/claude-review.md`.
Before merging, check the current CI results and address any posted review findings. Do not wait for an
automatic Claude verdict or request one as a merge prerequisite.
