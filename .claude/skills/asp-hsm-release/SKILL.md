---
name: asp-hsm-release
description: Release an Aspid.Core.HSM version end to end. Phase 1 opens the release PR (version, CHANGELOG section, local tests, DLL check, Unity minimum); the user merges it. Phase 2 pushes the v<version> tag after the user's yes. Phase 3 checks what the Release workflow published. The version alone picks the channel. - `/asp-hsm-release 0.0.1-alpha.2`, `/asp-hsm-release 1.0.0`.
argument-hint: <version>
disable-model-invocation: true
---

# Release

The argument is the version, for example `0.0.1-alpha.2`. Remove a leading `v`. No argument → ask for the version.

## Channel

The version picks the channel. It is the same rule as in `scripts/set-version.sh` and `.github/workflows/release.yml`.

- A version with a hyphen (`0.0.1-alpha.2`) → `upm-preview`, a GitHub prerelease.
- A version without a hyphen (`1.0.0`) → `upm`, a stable release.

## Which phase

Run `git fetch origin`. Then pick the phase:

1. The tag `v<version>` is on origin (`git ls-remote --tags origin v<version>`) → go to "Phase 3".
2. The `package.json` on `origin/main` has the version → go to "Phase 2".
3. The PR from `chore/release-<version>` is open (`gh pr view chore/release-<version> --json state,url`) →
   report its link and its checks, then stop.
4. Else → go to "Phase 1".

## Phase 1: release PR

Do every step without questions. Stop at the first failure and report it.

1. Check that the working tree is clean.
2. Check that the version is greater than the `package.json` version on `origin/main`. Use SemVer precedence:
   `0.0.1-alpha.10` > `0.0.1-alpha.9`, and `1.0.0` > `1.0.0-rc.9`.
3. Create the branch: `git switch -c chore/release-<version> origin/main`.
4. Run `scripts/set-version.sh <version>`. It fails when `[Unreleased]` in `CHANGELOG.md` has no notes:
   then stop and ask the user for the release notes.
5. Start a `unity-verify` agent in the background. Its project comes from
   `scripts/make-unity-test-project.sh <scratchpad>/unity-min <minimum>`, where `<minimum>` is the `unity`
   matrix entry of `.github/workflows/tests.yml`. Ask for EditMode tests with no filter.
   The `unity-verify` agent is user-level. Without it, run the batch command from the header of
   `scripts/make-unity-test-project.sh`, in the background.
6. Run the checks of `.github/workflows/release.yml` from the repository root:
   - `dotnet test Aspid.Core.HSM.Generators/Aspid.Core.HSM.Generators/Aspid.Core.HSM.Generators.Tests --nologo`;
   - `dotnet build Aspid.Core.HSM.Generators/Aspid.Core.HSM.Generators/Aspid.Core.HSM.Runtime --nologo`;
   - `node scripts/check-package.mjs`;
   - the Release build of the step "Verify the committed generator DLL matches the sources", then its `git diff`.
7. A DLL diff in step 6 means a stale DLL on `main`. Run `git checkout --` on the DLL. Stop and report.
8. Find the old version in prose:
   `git grep -n -F '<old>' -- . ':!CHANGELOG.md' ':!scripts/'`.
   Do not edit the hits. Put them into the report.
9. Wait for the `unity-verify` agent. A compile error or a failed test stops the release.
10. Commit with the `asp-commit` skill. Message: `chore(release): v<version>`.
11. Open the PR with the `asp-pr` skill. Title: `chore(release): v<version>`. Body: the channel and the version's
    CHANGELOG section in one line.
    The `asp-commit` and `asp-pr` skills are user-level. Without them, use `git commit`, `git push` and
    `gh pr create`, and follow `.claude/asp-pr.md`.
12. Send the report (format below). The user merges the PR.

## Phase 2: tag

1. Find the merge commit: `gh pr view chore/release-<version> --json state,mergeCommit`.
2. The PR is not merged → say so and stop.
3. Ask the user in one message: the version, the channel, the commit SHA and subject. Say that the tag starts the
   Release workflow and that a published tag cannot be replaced.
4. Wait for a clear yes. Any other answer → stop.
5. Create the tag: `git tag -a v<version> -m "Release v<version>" <merge-sha>`.
6. Push it: `git push origin v<version>`.
7. Find the run of the tag:
   `gh run list --workflow release.yml --branch v<version> --limit 1 --json databaseId,status,conclusion`.
   No run yet → wait 10 seconds and try again. After 6 tries, stop and report.
8. Run `gh run watch <id> --exit-status` in the background. Go to "Phase 3" when it ends.

## Phase 3: check the publication

1. Find the run of the tag as in "Phase 2" step 7.
2. `status` is not `completed` → go to "Phase 2" step 8.
3. `conclusion` is not `success` → go to "Release failed".
4. Check the GitHub release: `gh release view v<version> --json url,isPrerelease`.
   `isPrerelease` must be true on `upm-preview` and false on `upm`.
5. Check that `refs/heads/<channel>` and `refs/tags/<channel>/<version>` point to one commit:
   `git ls-remote origin refs/heads/<channel> refs/tags/<channel>/<version>`.
6. Check the package version on the channel:
   `gh api 'repos/VPDPersonal/Aspid.Core.HSM/contents/package.json?ref=<channel>' --jq .content | base64 -d`.
7. Check that the package CHANGELOG on the channel has the version's section. The release generates that copy:
   `gh api 'repos/VPDPersonal/Aspid.Core.HSM/contents/CHANGELOG.md?ref=<channel>' --jq .content | base64 -d`.
8. Send the final report: the release URL, the channel, the open items from the Phase 1 report.

## Release failed

Read the failed step: `gh run view <id> --log-failed`. Then:

- **A step before "Publish release tag and UPM subtree" failed.** Only the `v<version>` tag is out.
  1. Report the cause. A fix goes to `main` in a separate PR.
  2. After the fix, ask before you delete the tag: `git push origin :refs/tags/v<version>`, `git tag -d v<version>`.
  3. Go to "Phase 2" step 3 with the merge commit of the fix PR.
- **Only "Create GitHub release" failed.** The tags and the channel branch are out.
  1. Ask, then create the release by hand. The command is in the comment above that step in `release.yml`.

## Report format

Write the report in Russian. Keep it short:

1. The first line: the PR link (Phase 1) or the release link (Phase 3), and the channel.
2. The checks, one line each: `dotnet test`, C# 9 build, package check, DLL, Unity minimum.
3. The old-version hits from Phase 1 step 8: the file and the line.
4. The last line: what the user does next.
