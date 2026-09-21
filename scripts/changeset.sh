#!/usr/bin/env bash
# A changelog you write while the change is fresh, with no Node toolchain to install.
#
#   scripts/changeset.sh new patch "What changed, in one sentence"
#   scripts/changeset.sh release [--dry-run]
set -euo pipefail

root=$(cd "$(dirname "$0")/.." && pwd)
dir="$root/.changeset"
csproj="$root/src/Jev.Net/Jev.Net.csproj"
changelog="$root/CHANGELOG.md"

current_version() { sed -n 's:.*<Version>\([^<]*\)</Version>.*:\1:p' "$csproj" | head -1; }

usage() {
  echo "usage: changeset.sh new <major|minor|patch> \"summary\"" >&2
  echo "       changeset.sh release [--dry-run]" >&2
  exit 2
}

case "${1:-}" in
  new)
    bump="${2:-}"
    summary="${3:-}"
    case "$bump" in major|minor|patch) ;; *) usage ;; esac
    [ -n "$summary" ] || usage

    file="$dir/$(date +%Y%m%d%H%M%S)-$bump.md"
    printf -- '---\nbump: %s\n---\n\n%s\n' "$bump" "$summary" > "$file"
    echo "wrote ${file#"$root/"}"
    ;;

  release)
    dry_run=false
    [ "${2:-}" = "--dry-run" ] && dry_run=true

    shopt -s nullglob
    notes=("$dir"/*.md)
    notes=("${notes[@]/$dir\/README.md}")
    pending=()
    for note in "${notes[@]}"; do [ -n "$note" ] && [ "$(basename "$note")" != "README.md" ] && pending+=("$note"); done

    [ ${#pending[@]} -gt 0 ] || { echo "Nothing to release: no changesets in .changeset/." >&2; exit 1; }

    # The largest pending bump wins: one breaking change makes the whole release major.
    level=patch
    for note in "${pending[@]}"; do
      case "$(sed -n 's/^bump: *//p' "$note" | head -1)" in
        major) level=major ;;
        minor) [ "$level" = major ] || level=minor ;;
      esac
    done

    version=$(current_version)
    IFS=. read -r major minor patch <<< "$version"
    case "$level" in
      major) major=$((major + 1)); minor=0; patch=0 ;;
      minor) minor=$((minor + 1)); patch=0 ;;
      patch) patch=$((patch + 1)) ;;
    esac
    next="$major.$minor.$patch"

    entry="## $next — $(date +%Y-%m-%d)\n"
    for note in "${pending[@]}"; do
      body=$(sed '1{/^---$/!q;};1,/^---$/d' "$note" | sed '/^[[:space:]]*$/d')
      while IFS= read -r line; do entry+="\n- $line"; done <<< "$body"
    done

    if $dry_run; then
      echo "$level bump: $version -> $next"
      printf '%b\n' "$entry"
      exit 0
    fi

    tmp=$(mktemp)
    { head -n 2 "$changelog"; printf '%b\n\n' "$entry"; tail -n +3 "$changelog"; } > "$tmp"
    mv "$tmp" "$changelog"

    sed -i.bak "s:<Version>$version</Version>:<Version>$next</Version>:" "$csproj" && rm -f "$csproj.bak"
    rm -f "${pending[@]}"

    echo "released $next ($level). Review CHANGELOG.md, then:"
    echo "  git commit -am \"Release $next\" && git tag v$next && git push --follow-tags"
    ;;

  *) usage ;;
esac
