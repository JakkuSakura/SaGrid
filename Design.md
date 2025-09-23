# SaGrid Design & AG Grid Borrowing Guide

## Purpose
SaGrid targets a strongly typed, headless table engine for .NET while AG Grid represents a mature, feature-rich JavaScript grid. This document catalogs SaGrid's current design, highlights AG Grid patterns, and documents concrete opportunities where SaGrid can borrow ideas to accelerate capability, maintainability, and ecosystem breadth.

## SaGrid Snapshot (Current State)
- Core engine (`SaGrid.Core/src/Models/Table.cs:6`) builds immutable column/row caches and applies a linear feature pipeline for filtering, sorting, grouping, expansion, and pagination.
- Feature toggles live in `TableOptions<TData>` (`SaGrid.Core/src/Types.cs:13`) with optional delegates to replace row-model stages.
- Avalonia UI layer (`SaGrid.Avalonia/src/TableHeaderRenderer.cs:1`, `TableBodyRenderer.cs:1`) delivers non-reactive building blocks (header/body/footer renderers, cell helpers).
- SolidAvalonia reactive layer (`SaGrid.SolidAvalonia/src/SolidTable.cs:1`) wraps those building blocks with Solid-style reactivity and helper builders.
- Advanced layer (`SaGrid.Advanced/src/SaGrid.cs:8`, `SaGrid.Advanced/src/SaGridComponent.cs:15`) extends the base table with export helpers, keyboard navigation, and Avalonia-centric UX callbacks.
- Repository structure is a focused .NET solution: `SaGrid.Core`, `SaGrid.Avalonia`, `SaGrid.Advanced`, examples, and tests.


## AG Grid Reference Architecture
- Modular Nx monorepo with core (`packages/ag-grid-community`), enterprise (`packages/ag-grid-enterprise`), and official framework wrappers (`packages/ag-grid-react`, `ag-grid-angular`, `ag-grid-vue3`).
- Dependency-injected "bean" system orchestrated by `GridCoreCreator` (`packages/ag-grid-community/src/grid.ts:1`) and `AgContext` (`packages/ag-grid-community/src/context/context.ts:1`).
- Module registry (`packages/ag-grid-community/src/modules/moduleRegistry.ts:1`) governs feature activation, row model support, and version consistency.
- Client-side row model (`packages/ag-grid-community/src/clientSideRowModel/clientSideRowModel.ts:1`) composes staged processors (group, filter, pivot, aggregate, sort, flatten) with enterprise stages injected when available.
- Separate enterprise modules add advanced analytics, server-side row models, charting, tool panels, and UI chrome.
- Extensive build/test toolchain (Nx, Vitest, Jest, e2e) and framework adapters deliver first-class developer ergonomics.

## Borrowing Themes & Recommendations

### 1. Modular Architecture & Dependency Management
- **Current SaGrid**: Feature list stored in `_features` with simple `Initialize()` calls (`SaGrid.Core/src/Models/Table.cs:105`). No explicit module boundaries beyond extension methods.
- **AG Grid Pattern**: Beans/modules registered centrally (`moduleRegistry.ts:1`) enable opt-in features, dependency checks, and per-grid modularity.
- **Borrowing Ideas**:
  - Introduce a lightweight module registry for SaGrid where features register required state slices, option flags, and lifecycle hooks.
  - Allow external packages to contribute `ITableFeature<T>` implementations without modifying the core assembly, similar to AG Grid community vs enterprise modules.
  - Add version/compatibility guards when multiple feature assemblies are loaded.

### 2. Row Model Pipeline & Virtualization
- **Current SaGrid**: Single `UpdateRowModel` method computes sequential stages with optional delegates (`SaGrid.Core/src/Models/Table.cs:128`). Virtualization flag exists (`EnableVirtualization`) but lacks implementation.
- **AG Grid Pattern**: Dedicated row model classes (client-side, infinite, server-side) expose transactions, async batching, and virtualization hooks (`clientSideRowModel.ts:1`). Stages are modular services injected via IoC.
- **Borrowing Ideas**:
  - Extract row model stages (filter, sort, group, paginate) into pluggable services so alternate row models (server-driven, virtualized) can be added akin to AG Grid's row model family.
  - Implement viewport/infinite row models that stream data on demand, borrowing AG Grid’s transaction API concepts (e.g., `RowDataTransaction`).
  - Provide async batching mechanisms to coalesce state updates for large datasets.

### 3. Feature Surface Expansion
- **Current SaGrid**: Core feature set matches TanStack baseline; advanced export is limited to CSV/JSON helpers (`SaGrid.Advanced/src/SaGrid.cs:47`).
- **AG Grid Pattern**: Rich feature portfolio (aggregation, pivot, charts, tool panels, sidebars, status bars) isolated in modules (`packages/ag-grid-enterprise/src`).
- **Borrowing Ideas**:
  - Define an "enterprise" namespace for optional features (pivoting, aggregation, tree data) while keeping core lean.
  - Replicate tool panel patterns (column visibility, filter explorer) by mapping SaGrid state records into reusable Avalonia panes.
  - Introduce charting/export adapters by abstracting table snapshots, inspired by AG Grid’s chart module interfaces.

### 4. State Management & Events
- **Current SaGrid**: Immutable `TableState<TData>` updates via functional setters; events limited to `OnStateChange` callback.
- **AG Grid Pattern**: Central `EventService` and named events propagate granular notifications (`packages/ag-grid-community/src/events.ts`). Grid options can register listeners per event type.
- **Borrowing Ideas**:
  - Add a typed event bus so consumers can subscribe to specific lifecycle events (e.g., `SortingChanged`, `RowSelectionChanged`) without diffing state snapshots.
  - Track state history and expose convenience APIs similar to AG Grid’s `ColumnState`/`FilterModel` getters for serialization.
  - Support undo/redo by leveraging immutable state snapshots combined with an event timeline.

### 5. UI Composition & Framework Reach
- **Current SaGrid**: `SaGrid.Avalonia` offers non-reactive building blocks and `SaGrid.SolidAvalonia` adds Solid-powered reactivity; the advanced component builds on the reactive layer and still couples strongly to Avalonia.
- **AG Grid Pattern**: UI is DOM-first, but wrappers for React/Angular/Vue exist as thin adapters over the vanilla grid.
- **Borrowing Ideas**:
  - Factor the Avalonia renderers into a generic view-model interface so alternate UI layers (WPF, MAUI, Blazor) can follow the same contract.
  - Create lightweight wrappers similar to AG Grid’s React/Angular packages, where SaGrid exposes a headless core plus framework-specific binding packages.
  - Mirror AG Grid’s theme strategy by centralizing styling primitives to allow skinning across UI stacks.

### 6. Performance Practices
- **Current SaGrid**: Relies on caching maps/lists and manual recompute triggers. No virtualization yet.
- **AG Grid Pattern**: Emphasizes viewport row rendering, animation suppression toggles, and change detection optimizations (`clientSideRowModel.ts:73` event listeners, async transactions).
- **Borrowing Ideas**:
  - Adopt viewport/windowing for Avalonia body rendering to limit visual tree size.
  - Provide knobs for throttling re-render frequency and disabling animations during large mutations.
  - Integrate change detection metrics (e.g., row counts ready flags) to avoid redundant recomputes.

### 7. Developer Tooling & Build Pipeline
- **Current SaGrid**: Traditional .NET solution with project references and manual packaging.
- **AG Grid Pattern**: Nx workspace orchestrates builds, shared configs, automated linting (`nx.json`, `vitest.workspace.ts`, `eslint.config.mjs`).
- **Borrowing Ideas**:
  - Introduce a build orchestrator (e.g., `dotnet` + `nuke`, or use `dotnet` solution filters) to standardize tasks across modules.
  - Mirror AG Grid’s multi-package release approach by creating NuGet packages per adapter (core, Avalonia, advanced) with shared tooling.
  - Adopt linting/analyzers and commit hooks to match AG Grid’s consistent code quality gates.

### 8. Testing Strategy
- **Current SaGrid**: Tests organized into contracts/features/integration (`Tests/src`). Limited UI testing.
- **AG Grid Pattern**: Uses Jest/Vitest for unit testing, plus scenario-based harnesses under `testing/`.
- **Borrowing Ideas**:
  - Expand test fixtures to cover large-row scenarios, virtualization, and state persistence analogous to AG Grid sample suites.
  - Add UI regression tests (e.g., Avalonia headless rendering snapshots) similar to AG Grid’s demo harnesses.
  - Provide public "test utils" packages for consumers, inspired by `packages/ag-grid-community/src/test-utils`.

### 9. Documentation & Community Assets
- **Current SaGrid**: README showcases features; design notes live in this document.
- **AG Grid Pattern**: Comprehensive documentation site, kitchen-sink demos, migration guides, and community engagement.
- **Borrowing Ideas**:
  - Create structured docs (concepts, API, tutorials) and host interactive samples akin to AG Grid’s documentation portal.
  - Maintain a roadmap highlighting enterprise feature parity goals to guide contributors.
  - Provide upgrade guides when breaking API changes occur, borrowing AG Grid’s release cadence playbook.

## SaGrid.Advanced Borrowing Plan (from AG Grid)
1. **Modular Feature Surface**
   - Mirror AG Grid's enterprise module registry by grouping premium SaGrid.Advanced capabilities (row grouping, pivoting, rich side panels) into opt-in packages.
   - Define clear module contracts (dependencies, exposed services) so hosts can cherry-pick features similar to AG Grid's `RowGroupingModule`, `SideBarModule`, etc.
2. **Tool Panels & Side Bar**
   - Introduce a configurable side bar service that renders column manager, filters, and custom panels; follow AG Grid's `SideBarService` pattern to register panels and expose API functions (`OpenPanel`, `RefreshPanel`).
   - Build Avalonia counterparts to AG Grid's column drop zones/header panels for drag-and-drop grouping and pivot configuration.
3. **Row Grouping, Pivoting, Aggregation**
   - Port grouping pipeline into SaGrid.Advanced: reusable services for group strategies, aggregation functions, and group-aware filters.
   - Provide UI (chips/drop zones) and APIs (`SetRowGroupColumns`, `MoveRowGroupColumn`) inspired by AG Grid's grouping API.
4. **Advanced Filtering & Editors**
   - Implement multi-filter compositions, set filters, and quick filter services similar to AG Grid's enterprise filters.
   - Add rich cell editors (combo boxes, date pickers, sliders) and batch editing flows.
5. **Analytics & Visualization**
   - Borrow AG Grid's chart integration concepts: export table slices into chart-friendly models, embed Avalonia charts triggered from context menus.
   - Extend export tooling (CSV, Excel, clipboard) with asynchronous services.
6. **Row Models & Virtualization**
   - Offer multiple row models (client-side, viewport/server-side) patterned after AG Grid's row-model contracts.
   - Integrate virtualization hooks so UI components can lazily render rows with large datasets.
7. **Status Bar & Widgets**
   - Provide a status bar framework for summary widgets (row counts, selection totals) similar to AG Grid's widgets.
   - Support custom widget injection via registration.
8. **API Surface & Events**
   - Design an advanced API layer that surfaces module-specific operations (visibility, grouping, context actions) echoing AG Grid's grid API.
   - Publish granular events so hosts can react to module state changes without diffing entire table state.


## Next Steps
1. Prototype a module registry for SaGrid features and evaluate integration with existing `ITableFeature<TData>` implementations.
2. Design a virtualization-capable row model leveraging AG Grid’s staged pipeline as a blueprint.
3. Publish a roadmap mapping AG Grid enterprise features to SaGrid milestones, prioritizing column tool panels, aggregation, and server-driven row models.
4. Evolve the build/test pipeline to support multi-package distribution and richer automated coverage.
