# Rules for Claude in this repository

Both jobs in `.github/workflows/claude.yml` append this file to the system prompt:
the automatic review and the `@claude` replies. Edit the rules here, in one place.

## Always

- Follow CLAUDE.md: it holds the layout, the HSM architecture and the generator rules.
- Reply in Russian. Keep code identifiers as they are.
- Write code, code comments and commit messages in English.

## When you review a pull request

Check, in this order:

1. Correctness: bugs and missed edge cases, with the input that breaks.
   In the HSM core, check the invariant from CLAUDE.md:
   - `StateFactory` owns state lifetime and builds the parent chain;
   - `StateMachineBase` only diffs the chain and calls enter and exit;
   - a change on one side keeps the assumptions of the other side: `Release` on exit, `_initializedStates` for the first enter.
2. Boundaries:
   - `package.json` declares Unity `2022.3`: no Unity API newer than 2022.3 without a version guard;
   - `Source/` (asmdef `Aspid.Core.HSM`) does not reference `UnityEngine`;
   - Unity-specific code goes to `Unity/Runtime/`;
   - no editor-only APIs reachable from runtime code;
   - the generator targets `netstandard2.0` and does not write to `Console`;
   - a new file under `Assets/` ships with its `.meta`;
   - Unity manages `.meta` files: no `.meta` is edited by hand.
3. Public API: a change needs an entry under `[Unreleased]` in `CHANGELOG.md`.
   New or changed members need XML docs.
   A rename of a public attribute or type in the runtime needs an update of
   `Descriptions/HsmClasses.cs` or `HsmNamespaces.cs` in the generator.
4. Generators: a source change needs the rebuilt `Aspid.Core.HSM.Generators.dll` in the package.
   `dotnet build` copies it there.
   A new or changed emit branch needs a snapshot test in `Aspid.Core.HSM.Generators.Tests/`.
5. Version: the `package.json` version and the `CHANGELOG.md` heading must agree. `release.yml` checks this.

Skip style and naming that CLAUDE.md does not cover.

Format:

- At most 8 findings, most severe first.
- Put each specific issue in an inline comment on its line.
- Start each inline comment with a severity tag:
  - `[blocking]`: the PR must not merge until it is fixed;
  - `[minor]`: an improvement that can wait.
- Post the main comment last, as a new comment with `gh pr comment`.
  This applies to a requested review too. Do not put the verdict only into an edit of an earlier comment:
  an edit does not notify the agent that waits for the verdict.
- Start the main comment with exactly one verdict line: `Verdict: <N> blocking, <M> minor`.
  An agent reads this line to decide the next step, so keep the format.
  Put at most 2 short sentences after it.
- Write the verdict line and the severity tags in English, exactly as shown here.
  Write the rest of each comment in Russian.
- Do not praise.
- Do not write the literal trigger phrase (at-sign + "claude") in your comments:
  it starts the workflow again.
