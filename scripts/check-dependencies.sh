#!/usr/bin/env bash
# "One dependency" is a promise in the README, so it is checked against the thing people actually install:
# the packed .nupkg's nuspec, not the csproj. EACH target-framework group must declare exactly
# Microsoft.Extensions.Logging.Abstractions and nothing else — per group, not in aggregate, so a package
# that doubles up in one framework and drops it in another cannot average its way through.
#
#   scripts/check-dependencies.sh artifacts/TypeSafe.Jev.1.0.0.nupkg
set -euo pipefail

pkg="${1:?usage: check-dependencies.sh <path-to.nupkg>}"
allowed='Microsoft.Extensions.Logging.Abstractions'

command -v unzip >/dev/null || { echo "FAIL: unzip is required" >&2; exit 1; }

# One line per group: "<framework>|<id> <id> ...". Self-closing groups (<group … />) have no dependencies.
groups=$(unzip -p "$pkg" '*.nuspec' | tr -d '\r' | tr '\n' ' ' \
  | sed 's/<group /\n<group /g' | grep '^<group ' \
  | sed -E 's/<\/group>.*//' \
  | while IFS= read -r block; do
      tfm=$(printf '%s' "$block" | sed -E 's/^<group targetFramework="([^"]*)".*/\1/')
      ids=$(printf '%s' "$block" | grep -o '<dependency id="[^"]*"' | sed 's/.*id="//; s/"$//' | sort | tr '\n' ' ' | sed 's/ $//' || true)
      printf '%s|%s\n' "$tfm" "$ids"
    done)

[ -n "$groups" ] || { echo "FAIL: no dependency groups found in the nuspec — the check read nothing" >&2; exit 1; }

fail=0
while IFS='|' read -r tfm ids; do
  if [ "$ids" = "$allowed" ]; then
    echo "  ok   $tfm: $ids"
  else
    echo "  FAIL $tfm: expected exactly [$allowed], found [${ids:-nothing}]" >&2
    fail=1
  fi
done <<< "$groups"

[ "$fail" -eq 0 ] || { echo "The one-dependency promise is broken." >&2; exit 1; }
echo "One dependency, in every target framework group."
