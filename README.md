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
