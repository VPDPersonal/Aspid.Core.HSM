# Rules for Claude in this repository

Both jobs in `.github/workflows/claude.yml` append this file to the system prompt:
the automatic review and the `@claude` replies. Edit the rules here, in one place.

## Always

- Follow CLAUDE.md: it holds the layout, the HSM architecture and the generator rules.
- Write replies, code, code comments and commit messages in English. Keep code identifiers as they are.

## When you review a pull request

Check, in this order:

1. Correctness: bugs and missed edge cases, with the input that breaks.
   In the HSM core, the invariant from CLAUDE.md: `StateFactory` owns state lifetime and parent-chain
   construction, `StateMachineBase` only diffs the chain and enters/exits. A change on one side must keep
   the other side's assumptions (`Release` on exit, `_initializedStates` for first enter).
2. Boundaries:
   - the package declares Unity `2022.3` in `package.json`: no Unity API newer than 2022.3 without a version guard;
   - `Source/` (asmdef `Aspid.Core.HSM`) stays free of `UnityEngine`; Unity-specific code goes to `Unity/Runtime/`;
   - no editor-only APIs reachable from runtime code;
   - the generator targets `netstandard2.0` and does not write to `Console`;
   - `.meta` files are managed by Unity: a new file under `Assets/` ships with its `.meta`, and no `.meta` is edited by hand.
3. Public API: a change needs an entry under `[Unreleased]` in `CHANGELOG.md` and XML docs on the new or changed members.
   A rename of a public attribute or type in the runtime also updates `Descriptions/HsmClasses.cs` / `HsmNamespaces.cs`
   in the generator.
4. Generators: a source change needs the rebuilt `Aspid.Core.HSM.Generators.dll` committed into the package
   (`dotnet build` copies it) and a snapshot test in `Aspid.Core.HSM.Generators.Tests/` for a new or changed emit branch.
5. Version: `package.json` version and the `CHANGELOG.md` heading must agree, as `release.yml` checks.

Skip style and naming that CLAUDE.md does not cover.

Format:

- At most 8 findings, most severe first.
- Put each specific issue in an inline comment on its line.
- Start each inline comment with a severity tag:
  - `[blocking]`: the PR must not merge until it is fixed;
  - `[minor]`: an improvement that can wait.
- Start the main comment with exactly one verdict line: `Verdict: <N> blocking, <M> minor`.
  An agent reads this line to decide the next step, so keep the format.
  Put at most 2 short sentences after it.
- Do not praise.
- Do not write the literal trigger phrase (at-sign + "claude") in your comments:
  it starts the workflow again.
