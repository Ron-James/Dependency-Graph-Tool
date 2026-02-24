# Dependency Graph Tool

Unity editor tool for visualizing scene dependencies as a graph.

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
- Custom dependencies emitted through `IDependencyEmitter`

## Runtime/editor split

- `Runtime/IDependencyEmitter.cs` contains the dependency emitter contract.
- Editor graph/scanning code lives under `Editor/`.

For extension details, see [Documentation~/SCENE_DEPENDENCY_GRAPH.md](Documentation~/SCENE_DEPENDENCY_GRAPH.md) and [Documentation~/EXTENDING_SPECIALIZED_NODES.md](Documentation~/EXTENDING_SPECIALIZED_NODES.md).
