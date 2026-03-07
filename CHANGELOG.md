# Changelog

All notable changes to this package are documented in this file.

## [Unreleased]
### Changed
- Removed hard dependency on `com.unity.graphtoolkit` so the package can be imported into projects that do not have Graph Toolkit available.
- Removed `IDependencyEmitter` / `IDependencyEmitContext` interfaces from runtime and editor package code.
- Scene dependency discovery now relies on built-in scanners only.

## [1.0.0] - 2026-02-24
### Added
- Initial Unity Package Manager package setup for Dependency Graph Tool.
- Editor window, graph view, and scanners for scene dependency visualization.
- Runtime `IDependencyEmitter` contract for custom dependency emission.
