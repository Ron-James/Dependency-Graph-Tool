# Dependency Graph Tool

Unity editor tool for visualizing scene dependencies as a graph.

> Note: the included sample Unity project is stored under `Graph-Tool~` so Unity Package Manager ignores it when this repository is installed as a package from Git.

## Install via Unity Package Manager (Git URL)

Use **Add package from git URL...** in the Unity Package Manager and paste:

`https://github.com/Ron-James/Dependency-Graph-Tool.git`

### Troubleshooting: "immutable folder" / GUID conflicts

If Unity logs errors like:

- `... conflicts with: Assets/Plugins (current owner)`
- `... has no meta file, but it's in an immutable folder`

then your package import includes a nested Unity project (for example `SuperSpringBros/Assets/...`) inside `Packages/com.ronjames.dependency-graph-tool`. That creates duplicate GUIDs with your real project `Assets/...` folder.

Use this checklist:

1. In `Packages/com.ronjames.dependency-graph-tool`, remove any nested project folders that contain their own `Assets`, `ProjectSettings`, or `Packages` directories.
2. Keep sample content in folders that end with `~` (for example `Graph-Tool~`) so Unity ignores them as importable assets.
3. Delete `Library/PackageCache/com.ronjames.dependency-graph-tool*` and reopen Unity so the package is reimported cleanly.
4. If you use a Git URL with a subfolder path, make sure it points only to the package root that contains this package's `package.json`.

## Open the tool

In Unity, open:

`Tools > Scene Dependency Graph`

For migration work-in-progress, there is also a separate UI Toolkit surface at:

`Tools > Scene Dependency Graph (UI Toolkit WIP)`

> The UI Toolkit WIP window is optional and only appears when Unity's `com.unity.graphtoolkit` package is installed in your project.

## What it scans

- Serialized Unity object references
- Managed objects marked with `[SerializeReference]`
- UnityEvent persistent listeners

## Runtime/editor split

- `Runtime/IDependencyGraphNodeNameProvider.cs` lets your objects provide custom graph node labels without relying on app-specific naming interfaces.
- Editor graph/scanning code lives under `Editor/`.

For extension details, see [Documentation~/SCENE_DEPENDENCY_GRAPH.md](Documentation~/SCENE_DEPENDENCY_GRAPH.md).

If you want to migrate from `UnityEditor.Experimental.GraphView` to Unity Graph Toolkit (`com.unity.graphtoolkit`), see [Documentation~/GRAPH_TOOLS_FOUNDATION_MIGRATION.md](Documentation~/GRAPH_TOOLS_FOUNDATION_MIGRATION.md).

## Large scene workflow (already supported)

- Nodes are hidden by default on first load for each scene.
- Use the left **Hierarchy** pane to reveal only the nodes you want.
- Use **Show Filtered / Hide Filtered** with search to batch-toggle visibility.
