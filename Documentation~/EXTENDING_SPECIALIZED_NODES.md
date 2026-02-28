# Extending Scene Dependency Graph with Specialized Nodes

The old `IDependencyEmitter` contract has been removed from this package.

To extend graph behavior now:

1. Add or update scanner logic in editor code under `Editor/SceneDependencyGraph/`.
2. Add any new `DependencyType` values in `DependencyModel.cs`.
3. Add styling updates in `SceneGraphView.cs` for new node/edge types.

## Optional: add new visual type

If you want a brand new colored edge type:
1. Add a `DependencyType` enum value in `DependencyModel.cs`
2. Return a color for it in `SceneGraphView.EdgeColor(...)`
3. (Optional) update port-name fallback text

If you want a brand new node style/category:
1. Add a `DependencyNodeCategory` enum value in `DependencyModel.cs`
2. Assign that category in scanner output
3. Add a style case in `SceneGraphView.ApplyNodeStyle(...)`

## Direction mental model

- `From` node = owner of the dependency member (output)
- `To` node = referenced target (input)

Think: `Owner.OUT(member)` -> `Target.IN(member)`
