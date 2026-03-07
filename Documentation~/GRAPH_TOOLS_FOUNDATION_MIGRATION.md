# Migrating from `GraphView` to Unity Graph Toolkit (`com.unity.graphtoolkit`)

This package currently renders the graph with `UnityEditor.Experimental.GraphView` (`SceneGraphView : GraphView`).

If you want to move to Unity's Graph Toolkit package (`com.unity.graphtoolkit`), this document gives a practical migration path that starts with safe dependency wiring and then moves into backend replacement.

---

## 1) Current implementation (what stays the same)

The current editor graph is GraphView-based:

- `Editor/SceneDependencyGraph/SceneGraphView.cs`
  - derives from `GraphView`
  - builds `Node`, `Port`, and `Edge` visuals
- `Editor/SceneDependencyGraph/SceneDependencyGraphWindow.cs`
  - hosts `SceneGraphView`
  - controls scan/filter/organize/rebuild interactions

The scanner/data side (`DependencyNode`, `DependencyEdge`, `SceneScanner`) can be reused during migration.

---

## 2) Install the package you asked for

Target package:

- `com.unity.graphtoolkit`

Unity docs reference:

- `https://docs.unity3d.com/Packages/com.unity.graphtoolkit@0.1/manual/introduction.html`

### Project-level install (recommended)

Install in the consuming Unity project via Package Manager or `manifest.json`.

Example `Packages/manifest.json` entry:

```json
{
  "dependencies": {
    "com.unity.graphtoolkit": "0.1.0"
  }
}
```

> Keep it project-level first so this package remains compatible for users that have not installed Graph Toolkit yet.

---

## 3) Dependency plumbing added in this repo

`Editor/DependencyGraphTool.Editor.asmdef` now includes a Version Define:

- package: `com.unity.graphtoolkit`
- expression: `0.1.0`
- symbol: `HAS_UNITY_GRAPH_TOOLKIT`

Use this symbol to guard toolkit-specific source files:

```csharp
#if HAS_UNITY_GRAPH_TOOLKIT
// Unity Graph Toolkit backend code
#endif
```

This gives you an optional migration path: GraphView remains available when Graph Toolkit is not installed.

---

## 4) Recommended migration architecture

Use a backend abstraction so window/scanner logic stays untouched while the rendering backend changes.

Example abstraction:

- `IDependencyGraphBackendView`
  - `VisualElement Root { get; }`
  - `void Populate(IReadOnlyList<DependencyNode> nodes, IReadOnlyList<DependencyEdge> edges, DependencyType? filterType)`
  - `void Clear()`
  - `void FrameAll()`
  - selection callbacks/events

Implementations:

1. `GraphViewDependencyGraphBackend` (current behavior)
2. `GraphToolkitDependencyGraphBackend` (`#if HAS_UNITY_GRAPH_TOOLKIT`)

---

## 5) Data mapping from current model to Graph Toolkit

Map existing model objects directly:

- `DependencyNode`
  - graph element id = `GUID`
  - title = `DisplayName`
  - style metadata = type key / icon / color
- `DependencyEdge`
  - source = `From.GUID` + output port (`FieldName`)
  - target = `To.GUID` + input port (`FieldName`)
  - visual style = `DependencyType` and broken-link state
- `FieldSlot`
  - port definition (direction + capacity + label)

So scanner/runtime behavior does not need to change for phase 1.

---

## 6) Incremental execution plan

### Phase 0 — Compile-safe setup (done)

- Add `HAS_UNITY_GRAPH_TOOLKIT` Version Define in editor asmdef.
- Keep existing GraphView implementation as default.

### Phase 1 — Skeleton backend

- Add `GraphToolkitDependencyGraphBackend` behind `#if HAS_UNITY_GRAPH_TOOLKIT`.
- Render basic nodes/edges from existing model.
- Add backend toggle in window settings: `GraphView | Graph Toolkit`.

### Phase 2 — Parity

Migrate these in order:

1. selection sync,
2. node position persistence,
3. edge/port labeling semantics,
4. framing/zoom + organize behavior,
5. contextual commands.

### Phase 3 — Stabilize

- run both backends side-by-side for a cycle,
- resolve behavior differences,
- switch default to Graph Toolkit when stable.

---

## 7) Known risks and guardrails

- Graph Toolkit APIs may evolve by package version; isolate toolkit code in dedicated files.
- Keep scanner/runtime untouched while migrating editor backend.
- Define up front where editor layout state lives (existing data vs toolkit-side model).
- For large scenes, batch updates and avoid full redraw on small diffs.

---

## 8) Immediate next coding step

Implement these first:

1. Introduce `IDependencyGraphBackendView`.
2. Wrap current `SceneGraphView` behind `GraphViewDependencyGraphBackend`.
3. Add empty `GraphToolkitDependencyGraphBackend` behind `HAS_UNITY_GRAPH_TOOLKIT` and wire backend selection.

That gives you a low-risk, testable migration starting point while keeping current users unblocked.
