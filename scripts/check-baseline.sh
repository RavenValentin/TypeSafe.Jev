#!/usr/bin/env bash
# Package validation only catches a breaking change if its baseline is the LATEST published version.
# Leave the baseline behind and every change made since then goes unchecked — silently. This fails the
# build when that happens, so the guard cannot quietly decay.
#
#   scripts/check-baseline.sh
set -euo pipefail

csproj='src/Jev.Net/Jev.Net.csproj'

# Plain sed, not grep -P: this has to run the same on a CI runner and on a developer's Git Bash.
value_of() { sed -n "s:.*<$1>\([^<]*\)</$1>.*:\1:p" "$csproj" | head -1; }

package=$(value_of PackageId)
[ -n "$package" ] || { echo "FAIL: no <PackageId> in $csproj" >&2; exit 1; }

# A commented-out baseline does not count as set, which is how a pre-release repo stays green.
baseline=$(grep -v '<!--' "$csproj" | sed -n 's:.*<PackageValidationBaselineVersion>\([^<]*\)</PackageValidationBaselineVersion>.*:\1:p' | head -1)

lower=$(printf '%s' "$package" | tr '[:upper:]' '[:lower:]')
latest=$(curl -fsSL "https://api.nuget.org/v3-flatcontainer/$lower/index.json" 2>/dev/null \
  | tr ',' '\n' | sed -n 's:.*"\([0-9][0-9.]*\)".*:\1:p' | tail -1 || true)

if [ -z "$latest" ]; then
  if [ -n "$baseline" ]; then
    echo "::error::baseline is set to $baseline but nothing is published for $package" >&2
    exit 1
  fi
  echo "ok: $package has no published release yet, so there is nothing to diff against."
  exit 0
fi

if [ -z "$baseline" ]; then
  echo "::error::$package $latest is published, so PackageValidationBaselineVersion must be set in $csproj" >&2
  exit 1
fi

if [ "$baseline" != "$latest" ]; then
  echo "::error::baseline is $baseline but the newest published version is $latest — changes since then go unchecked" >&2
  exit 1
fi

echo "ok: baseline $baseline is the newest published version of $package."
