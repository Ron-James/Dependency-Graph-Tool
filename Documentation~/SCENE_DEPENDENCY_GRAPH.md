# Scene Dependency Graph (Editor Tool)

This tool is **editor-only** and scans the active scene to render dependency nodes and edges.

## Scanned dependency sources

The scanner currently collects dependencies from:

- Serialized Unity object references
- Managed `[SerializeReference]` fields
- UnityEvent persistent listeners

## Port labeling rule

`memberName` from scanned members becomes the port label:

- output port: `OUT: <memberName>`
- input port: `IN: <memberName>`

Use member/field-like names for best clarity, e.g.:

- `viewFactory`
- `themeConfig`
- `_commands[0]`
- `OnBuildRequested`

## Dependency kind mapping

Scanner dependency kinds map to `DependencyType` by enum-name parsing. If no match is found, it falls back to `DependencyType.SerializedUnityRef`.

## Direction mental model

The graph always uses:

`Owner.OUT(member)` -> `Target.IN(member)`

So the node that holds/emits a reference/event/member is the **From** side.
