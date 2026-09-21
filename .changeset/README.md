# Changesets

A change that users will notice ships with a note describing it, written when the change is fresh rather
than reconstructed from git log at release time.

```bash
scripts/changeset.sh new patch "Retry-After longer than MaxRetryAfter now falls back to backoff"
```

That writes a small Markdown file here. Several can pile up between releases; each one records its own
bump (`major`, `minor` or `patch`) and its own sentence.

At release time:

```bash
scripts/changeset.sh release      # --dry-run to see what it would do
```

It takes the largest pending bump, works out the next version, moves every pending note into
`CHANGELOG.md`, bumps `<Version>` in `src/Jev.Net/Jev.Net.csproj`, and deletes the notes. Commit that,
then tag it — the tag is what publishes:

```bash
git commit -am "Release 0.2.0" && git tag v0.2.0 && git push --follow-tags
```

This is deliberately not `@changesets/cli`: a .NET repo should not need a Node toolchain to write a
changelog. The file format is the same idea, minus the dependency.
