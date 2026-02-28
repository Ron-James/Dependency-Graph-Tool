# Dependency Graph Tool

Unity editor tool for visualizing scene dependencies as a graph.

> Note: the included sample Unity project is stored under `Graph-Tool~` so Unity Package Manager ignores it when this repository is installed as a package from Git.

## Install via Unity Package Manager (Git URL)

Use **Add package from git URL...** in the Unity Package Manager and paste:

`https://github.com/Ron-James/Dependency-Graph-Tool.git`

## Open the tool

In Unity, open:

`Tools > Scene Dependency Graph`

## What it scans

- Serialized Unity object references
- Managed objects marked with `[SerializeReference]`
- UnityEvent persistent listeners

## Runtime/editor split

- `Runtime/IDependencyGraphNodeNameProvider.cs` lets your objects provide custom graph node labels without relying on app-specific naming interfaces.
- Editor graph/scanning code lives under `Editor/`.

For extension details, see [Documentation~/SCENE_DEPENDENCY_GRAPH.md](Documentation~/SCENE_DEPENDENCY_GRAPH.md).

## Large scene workflow (already supported)

- Nodes are hidden by default on first load for each scene.
- Use the left **Hierarchy** pane to reveal only the nodes you want.
- Use **Show Filtered / Hide Filtered** with search to batch-toggle visibility.

For forward-looking optimization and NodeGraphProcessor integration guidance, see [Documentation~/NODE_GRAPH_PROCESSOR_RESEARCH.md](Documentation~/NODE_GRAPH_PROCESSOR_RESEARCH.md).

### NodeGraphProcessor integration shortcuts in the editor

When `com.alelievr.node-graph-processor` is installed in the Unity project, the Scene Dependency Graph toolbar now includes:

- **NGP Window**: opens the NodeGraphProcessor window directly.
- **Create NGP Graph**: creates a `BaseGraph` asset (when supported by the installed version).
- **NGP status label**: shows whether NodeGraphProcessor is detected.
- **Dependency node class scaffolding**: adds NodeGraphProcessor `BaseNode`-derived classes for MonoBehaviour, ScriptableObject, and managed-object dependency nodes (compiled only when NodeGraphProcessor is installed).

This keeps your existing dependency graph workflow intact while enabling immediate API-level interoperability when the package is present.

## Using NodeGraphProcessor as a dependency

Yes — but with an important Unity Package Manager caveat:

- A package's `package.json` **cannot** declare a Git URL dependency directly.
- `package.json` dependencies only accept `"package-name": "version"` entries from registries.

### Recommended setup

1. In the **consumer project** (`Packages/manifest.json`), add NodeGraphProcessor from Git URL.
2. Add this package from Git URL (or version/registry).
3. Keep this package's NodeGraphProcessor integration code in a separate bridge assembly (optional but recommended).

Example `Packages/manifest.json` snippet:

```json
{
  "dependencies": {
    "com.ronjames.dependency-graph-tool": "https://github.com/Ron-James/Dependency-Graph-Tool.git",
    "com.alelievr.node-graph-processor": "https://github.com/alelievr/NodeGraphProcessor.git"
  }
}
```

If you publish NodeGraphProcessor to a registry (for example OpenUPM), then this package can depend on it via normal `package.json` versioned dependency.

For a full integration decision matrix (registry vs Git vs vendored copy), see [Documentation~/NODE_GRAPH_PROCESSOR_DEPENDENCY_SETUP.md](Documentation~/NODE_GRAPH_PROCESSOR_DEPENDENCY_SETUP.md).

### Using the vendored copy in this repository

This repo includes a copy of NodeGraphProcessor at `Packages/com.alelievr.node-graph-processor/NodeGraphProcessor-1.3.0`.
For the sample Unity project under `Graph-Tool~`, reference that folder in `Graph-Tool~/Packages/manifest.json` with:

```json
"com.alelievr.node-graph-processor": "file:../../Packages/com.alelievr.node-graph-processor/NodeGraphProcessor-1.3.0"
```

No extra files are required in `Graph-Tool~/Packages/` as long as the copied package folder contains its own `package.json` (which it does).

