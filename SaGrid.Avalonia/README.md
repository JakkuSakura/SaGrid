# SaGrid.Avalonia vs TanStack Solid Table

## Scope of SaGrid.Avalonia
- Provides **non-reactive Avalonia controls** (header/body/footer renderers, basic cell templates) for SaGrid tables.
- Delivers styling defaults, pagination affordances, and column helpers that can be composed inside any Avalonia `Control`.
- Leaves change detection to the caller – you re-render or update bindings when table state changes.
- Pairs naturally with `SaGrid.SolidAvalonia` if you want Solid-style reactivity on top of these building blocks.

## Scope of TanStack Solid adapter (`@tanstack/solid-table`)
- Exposes `createSolidTable` and `flexRender`, forwarding state management to `@tanstack/table-core`.
- Runs entirely in the browser and expects the host app to render DOM nodes manually.
- Ships no preset UI, styling, or pagination controls; it is a pure reactivity bridge for SolidJS.

## Feature comparison
| Aspect | SaGrid.Avalonia | TanStack Solid adapter |
| --- | --- | --- |
| Platform | Avalonia (desktop, XAML-friendly) | SolidJS / web DOM |
| Deliverable | Header/body/footer renderers, cell helpers, pagination stubs | `createSolidTable`, `flexRender` bindings only |
| Reactivity | Manual (consumer re-renders) – optionally add `SaGrid.SolidAvalonia` for Solid-style reactivity | Built-in Solid signals wrapping `@tanstack/table-core` |
| Styling | Avalonia `Border`, `StackPanel`, default brushes | None (author your own CSS/DOM) |
| Usage | Compose into Avalonia views; call table APIs to mutate state | Compose into Solid components; call table-core APIs |
| Goal | Give Avalonia developers ready-made building blocks | Give SolidJS devs a bridge to table-core |

## When to reach for each
1. **Use `SaGrid.Avalonia`** when you build Avalonia desktop apps and want prebuilt UI pieces that you can wire up manually or via MVVM.
2. **Use `SaGrid.SolidAvalonia`** when you want Solid-style change propagation layered over those building blocks.
3. **Use `@tanstack/solid-table`** when you are in the browser with SolidJS and prefer to drive DOM rendering yourself.

SaGrid.Avalonia targets `net9.0` and depends on Avalonia 11.3.
