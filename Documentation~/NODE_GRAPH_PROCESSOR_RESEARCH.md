# NodeGraphProcessor Research & Integration Plan

This document summarizes how the project can leverage **NodeGraphProcessor** concepts to improve scalability and usability for large scenes.

> Note: direct network access to the linked repository/API was blocked in this environment (`CONNECT tunnel failed, response 403`), so this plan uses known GraphView/NodeGraphProcessor architecture patterns and maps them to the current codebase.

## Current state in this tool

The tool already includes two foundational behaviors you asked for:

1. **Hide all nodes by default** for unseen nodes in a scene.
2. **Left-side hierarchy panel** that mirrors the Unity scene hierarchy and lets you show/hide nodes.

Those are implemented in `SceneDependencyGraphWindow`:
- Default hidden behavior is handled by hidden/known GUID persistence and `HideAllCurrentNodesAsDefault()`.
- Hierarchy rendering is driven by `AddHierarchySection(...)` + recursive transform branch building.

## Why NodeGraphProcessor-style patterns still help

Even with existing basic controls, very large scenes can still feel overwhelming due to:
- too many visible edges once a few clusters are expanded,
- no concept of workflow-centric grouping (selection/subgraph/focus modes),
- expensive full redraw behavior as graph size grows.

NodeGraphProcessor typically improves this via data/visual separation and editor tooling patterns that we can adopt incrementally.

## Recommended upgrade strategy

## Phase 1 (low risk): workflow-focused visibility controls

### 1) Add explicit visibility modes
Introduce a graph visibility mode enum in the window state:
- `HiddenByDefault` (current behavior),
- `ShowSelectionOnly`,
- `ShowSelectionAndNeighbors`,
- `ShowAllFiltered`.

Then derive visible node IDs from mode + selection instead of only hidden GUID set.

### 2) Add hierarchy-driven focus actions
For each hierarchy row:
- `Show` (existing),
- `Isolate` (hide all others),
- `Reveal Neighbors` (show direct incoming/outgoing dependencies),
- `Ping/Select` (existing selection behavior).

This gives a NodeGraphProcessor-like exploratory workflow without a full rewrite.

### 3) Add subtree actions for GameObjects
At each GameObject foldout row:
- `Show Subtree Components`,
- `Hide Subtree Components`,
- optional `Isolate Subtree`.

This maps directly to your “pick from hierarchy on the left, reveal only what I want” requirement.

## Phase 2 (performance): partial graph materialization

### 1) Keep full model, render partial view
Continue scanning the full scene model, but only instantiate GraphView nodes for visible IDs.

### 2) Cache adjacency indexes once per scan
Build dictionaries once:
- `nodeGuid -> outgoing edge GUIDs`,
- `nodeGuid -> incoming edge GUIDs`,
- `gameObjectPath -> component node GUIDs`.

Use these for fast reveal/isolate operations.

### 3) Avoid full `DeleteElements(graphElements)` redraw for every small action
For hide/show toggles, only add/remove changed nodes and related edges where possible.

## Phase 3 (optional): NodeGraphProcessor package adoption

If you decide to depend on NodeGraphProcessor directly:

1. Add package dependency (UPM/Git URL).
2. Create a `BaseGraph` asset that stores scene snapshot data.
3. Map scanner output to processor node types (`ComponentNode`, `AssetNode`, etc.).
4. Implement custom views/inspectors for dependency metadata (field names, broken references, UnityEvent listener info).
5. Keep your scanner + domain model as source-of-truth; use processor as rendering/editor framework.

This is a heavier migration, but gives better long-term extensibility (custom inspectors, stacked nodes, node processors, etc.).

## Concrete next implementation steps in this repo

1. Add a `Visibility Mode` toolbar menu.
2. Add `Isolate` and `Reveal Neighbors` actions on node rows.
3. Add subtree show/hide actions to hierarchy GameObject entries.
4. Introduce adjacency caches in `DependencyModel` or window state on refresh.
5. Split redraw path into:
   - full rebuild (`RefreshGraph`),
   - incremental visibility update.

## Validation checklist

- Open large scene (500+ nodes) and verify initial view has no visible nodes.
- Use hierarchy to reveal one branch and verify graph stays readable.
- Use isolate + neighbor reveal and confirm edge count remains manageable.
- Verify hidden/known preferences persist between editor restarts.
- Verify organize/layout still works on partial views.

