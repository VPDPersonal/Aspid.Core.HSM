#!/bin/sh
# Set the package version everywhere it is written by hand: the "version" of package.json and the CHANGELOG.md
# section. The [Unreleased] notes become the version's section, dated today, with its release link; the [Unreleased]
# compare link moves to the new tag. The README badges read the version from the upm branches, so no README changes.
# The version also picks the channel .github/workflows/release.yml publishes to: a prerelease (0.0.1-alpha.2) goes to
# `upm-preview`, a stable version (1.0.0) to `upm`.
# Running it again with the version already set repairs a file that drifted (a hand-edited package.json, say).
#   scripts/set-version.sh 0.0.1-alpha.2
set -eu
cd "$(dirname "$0")/.."
NEW="${1:?usage: scripts/set-version.sh <version>}"
# \A..\z on the whole argument: a line-by-line grep would let "1.0.0<newline>x" through into package.json.
perl -e 'exit($ARGV[0] !~ /\A[0-9]+\.[0-9]+\.[0-9]+(-[0-9A-Za-z.-]+)?\z/)' "$NEW" || { echo "$NEW is not a SemVer version" >&2; exit 1; }
PKG=Aspid.Core.HSM/Assets/Plugins/Aspid/Core/HSM
# A CHANGELOG that already has the version's section keeps it. Otherwise its [Unreleased] must have notes to release.
# The heading is matched whole, as the move below needs it: a trailing space or a CRLF fails here, before any change.
has_section() { perl -ne 'BEGIN { $v = shift } $f = 1 if /^## \[\Q$v\E\]/; END { exit !$f }' "$NEW" CHANGELOG.md; }
if ! has_section; then
  awk '/^## \[Unreleased\]$/ { on = 1; next } /^## \[/ { exit } on && NF && !/^#/ { notes = 1 } END { exit !notes }' CHANGELOG.md \
    || { echo "CHANGELOG.md: no \"## [Unreleased]\" line with notes to release as $NEW" >&2; exit 1; }
fi
OLD=$(sed -n 's/^  "version": "\(.*\)",$/\1/p' "$PKG/package.json")
[ -n "$OLD" ] || { echo "package.json version not found" >&2; exit 1; }
case "$NEW" in
  *-*) BRANCH=upm-preview ;;
  *) BRANCH=upm ;;
esac
DATE=$(date +%Y-%m-%d) REPO=https://github.com/VPDPersonal/Aspid.Core.HSM
export NEW OLD DATE REPO
# Only the "version" line: a dependency can be at the same number.
perl -pi -e 's/^(  "version": ")\Q$ENV{OLD}\E(",)$/$1$ENV{NEW}$2/' "$PKG/package.json"
# The new section goes under an empty [Unreleased], and its release link above the older ones.
# The package copy of CHANGELOG.md is generated at release (scripts/package-changelog.mjs).
if ! has_section; then
  # shellcheck disable=SC2016 # perl code, expanded by perl
  perl -0pi -e 's/^## \[Unreleased\]\n/## [Unreleased]\n\n## [$ENV{NEW}] — $ENV{DATE}\n/m or die "$ARGV: no [Unreleased] line\n";
    s{^\[Unreleased\]: \S+}{[Unreleased]: $ENV{REPO}/compare/v$ENV{NEW}...HEAD}m;
    my $link = "[$ENV{NEW}]: $ENV{REPO}/releases/tag/v$ENV{NEW}\n";
    s/^(?=\[[0-9][^\]]*\]: )/$link/m or $_ .= "\n$link"' CHANGELOG.md
fi
# Whatever a pattern above missed fails here instead of at the release.
node scripts/check-version.mjs
echo "$OLD -> $NEW ($BRANCH)"
git status --short
