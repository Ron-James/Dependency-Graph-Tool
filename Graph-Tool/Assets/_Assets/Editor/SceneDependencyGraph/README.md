# Scene Dependency Graph (Editor Tool)

This tool is **editor-only** and scans the active scene to render dependency nodes and edges.

## Editor-only custom dependency declarations

To keep runtime/player builds clean, dependency-emitter contracts live under the editor folder:

- `Assets/_Assets/Code/Scripts/Editor/SceneDependencyGraph/Contracts/IDependencyEmitter.cs`

Because this contract is editor-only, use one of these patterns when adding custom dependencies:

### Pattern A (recommended): partial class split

Create your runtime MonoBehaviour normally, then add an editor-only partial companion that implements
`IDependencyEmitter`.

#### Runtime file (included in player)
```csharp
using UnityEngine;

public partial class UiBuilderService : MonoBehaviour
{
    [SerializeField] private MonoBehaviour viewFactory;
    [SerializeField] private ScriptableObject themeConfig;
}
```

#### Editor file (excluded from player)
Create this under an `Editor/` folder.
```csharp
#if UNITY_EDITOR
public partial class UiBuilderService : IDependencyEmitter
{
    public void EmitDependencies(IDependencyEmitContext context)
    {
        context.AddDependency(viewFactory, nameof(viewFactory), dependencyKind: "SerializedUnityRef", details: "Factory used to build views");
        context.AddDependency(themeConfig, nameof(themeConfig), dependencyKind: "CustomEmitter", details: "Theme source");
    }
}
#endif
```

### Pattern B: full class wrapped in `#if UNITY_EDITOR`
Use this only if the class itself is purely editor/debug and should not exist at runtime.

## Port labeling rule

`memberName` in `AddDependency(...)` becomes the port label:

- output port: `OUT: <memberName>`
- input port: `IN: <memberName>`

Use member/field-like names for best clarity, e.g.:

- `viewFactory`
- `themeConfig`
- `_commands[0]`
- `OnBuildRequested`

## Dependency kind mapping

`dependencyKind` maps to `DependencyType` by enum-name parsing. If no match is found, it falls back to
`DependencyType.CustomEmitter`.

## Direction mental model

The graph always uses:

`Owner.OUT(member)` -> `Target.IN(member)`

So the node that holds/emits a reference/event/member is the **From** side.
