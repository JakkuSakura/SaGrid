# SaGrid.SolidAvalonia

Reactive bindings that marry the headless SaGrid engine and the `SaGrid.Avalonia` building blocks with SolidAvalonia's component model.

## What it provides
- `SolidTable<TData>` – a SolidAvalonia `Component` that re-renders Avalonia header/body/footer controls whenever SaGrid table state changes.
- `SolidTableBuilder` / `SolidColumnHelper` – convenience factories for common scenarios (sorting, filtering, pagination, command columns).
- `SolidTableExtensions` – Solid-powered helpers for global filters, pagination controls, and column visibility panels.

## Architecture
```
SaGrid.Core (headless table)
        ↓
SaGrid.Avalonia (non-reactive controls)
        ↓
SaGrid.SolidAvalonia (Solid reactive wrappers)
        ↓
SaGrid.Advanced / your app
```

## When to use
- Pick **`SaGrid.SolidAvalonia`** if you want automatically updating Avalonia UI with Solid-style reactivity.
- Stick with **`SaGrid.Avalonia`** alone when you prefer to drive refreshes manually (e.g., MVVM, imperative redraws).

The package targets `net9.0`, depends on Avalonia 11.3, and reuses the upstream `SolidAvalonia` runtime.
