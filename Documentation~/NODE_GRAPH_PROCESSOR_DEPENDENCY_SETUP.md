# NodeGraphProcessor Dependency Setup (Unity UPM)

Short answer: **yes, you can use NodeGraphProcessor as part of this plugin workflow**, but the install shape depends on how NodeGraphProcessor is distributed.

## Unity UPM rule that matters

In `package.json`, Unity only allows versioned dependencies by package name (registry packages). It does **not** support a Git URL as a transitive dependency from one package to another.

That means:
- You **can** require NodeGraphProcessor in your docs/install instructions.
- You **cannot reliably force-install** NodeGraphProcessor via this package's `package.json` if it's only consumed via Git URL.

## Recommended options

## Option A (best if available): registry dependency

If NodeGraphProcessor is available on a scoped registry, add a normal dependency in this package:

```json
{
  "dependencies": {
    "com.alelievr.node-graph-processor": "<version>"
  }
}
```

Pros:
- Proper transitive install behavior
- Clear semver pinning

Cons:
- Requires registry publication/maintenance

## Option B (most common with Git packages): dual install in consumer manifest

Tell users to add both packages to `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.ronjames.dependency-graph-tool": "https://github.com/Ron-James/Dependency-Graph-Tool.git",
    "com.alelievr.node-graph-processor": "https://github.com/alelievr/NodeGraphProcessor.git"
  }
}
```

Pros:
- Works today with Git-only distribution

Cons:
- Consumer must install both explicitly

## Option C: vendor NodeGraphProcessor source inside this package

Copy vendor code under a third-party folder in your package and reference it directly.

Pros:
- Single install URL for users

Cons:
- Maintenance burden
- License/update handling responsibility
- Larger package

## Integration pattern for this repo

To keep your plugin maintainable, use a bridge assembly:

- Keep current graph scanner/model code independent.
- Add a separate editor assembly for NodeGraphProcessor-backed view.
- Guard bridge code with scripting defines if needed.

Suggested structure:

- `Editor/` (existing default GraphView implementation)
- `Editor/NodeGraphProcessorIntegration/` (new optional bridge)

This lets you:
- ship existing behavior for all users,
- enable richer workflow for users who also install NodeGraphProcessor.

## Practical recommendation

Given your current distribution is Git URL based, start with **Option B** (dual-install instructions). If you later publish to a scoped registry, move to **Option A** for true transitive dependency management.
