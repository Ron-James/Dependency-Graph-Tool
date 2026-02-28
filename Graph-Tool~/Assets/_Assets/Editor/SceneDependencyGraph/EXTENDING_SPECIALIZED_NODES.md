# Extending Scene Dependency Graph with Specialized Nodes

This project supports a contract-based extension point so new services can emit graph dependencies
without modifying the core scanners each time.

> Note: `IDependencyEmitter` and `IDependencyEmitContext` are defined under the **Editor** folder so they do not affect player builds.

## 1) Implement `IDependencyEmitter` on your component/service

Use an editor-only partial class (recommended) if your component is runtime-facing.

```csharp
using UnityEngine;

public class UiBuilderService : MonoBehaviour, IDependencyEmitter
{
    [SerializeField] private MonoBehaviour viewFactory; // example dependency
    [SerializeField] private ScriptableObject skinConfig; // example dependency

    public void EmitDependencies(IDependencyEmitContext context)
    {
        // dependencyKind can be any DependencyType enum string (case-insensitive)
        context.AddDependency(viewFactory, nameof(viewFactory), dependencyKind: "SerializedUnityRef", details: "Builds UI views");
        context.AddDependency(skinConfig, nameof(skinConfig), dependencyKind: "CustomEmitter", details: "Theme source");
    }
}
```

## 2) Pick dependency type labels

`dependencyKind` maps to `DependencyType` by enum-name parse. If parsing fails, it falls back to
`DependencyType.CustomEmitter`.

Examples:
- `"UnityEvent"`
- `"SerializedUnityRef"`
- `"CommandMember"`
- `"CustomEmitter"`

## 3) Port naming rule

The `memberName` you pass into `AddDependency(...)` becomes the graph port label:
- output: `OUT: <memberName>`
- input: `IN: <memberName>`

This lets you keep ports meaningful and field-driven.

## 4) Optional: add new visual type

If you want a brand new colored edge type for a contract:
1. Add a `DependencyType` enum value in `DependencyModel.cs`
2. Return a color for it in `SceneGraphView.EdgeColor(...)`
3. (Optional) update `ResolvePortName(...)` fallback text

If you want a brand new node style/category:
1. Add a `DependencyNodeCategory` enum value in `DependencyModel.cs`
2. Assign that category in your scanner or custom emission path
3. Add a style case in `SceneGraphView.ApplyNodeStyle(...)`

## 5) Direction mental model

- `From` node = owner/emitter of the dependency member (output)
- `To` node = referenced target (input)

Think: `Owner.OUT(member)` -> `Target.IN(member)`

## 6) Layout/organize

Use the **Organize** toolbar button after refresh. The graph uses dependency-aware layered layout to
reduce overlaps and crossings.
