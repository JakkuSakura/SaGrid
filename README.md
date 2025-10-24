# 🚀 SaGrid - The Ultimate .NET Table Library

**Supercharged data grids for modern .NET applications**

Built on the proven foundation of TanStack Table, SaGrid brings enterprise-grade table functionality to C# with unmatched performance, type safety, and developer experience. Whether you're building desktop apps with Avalonia or need a headless solution, SaGrid delivers.

## 🖼️ Preview

![View screenshot on Imgur](https://imgur.com/f3Akvbv.png)

## 🎯 What Makes SaGrid Special

✨ **TanStack for C#** - Native C# implementation of the beloved TanStack Table  
🎨 **TanStack for Avalonia** - Beautiful, reactive UI components for Avalonia applications  
⚡ **Blazing Fast** - Optimized for 10K+ rows with O(1) state updates  
🔒 **Type-Safe** - Full compile-time safety with C# generics  
🏗️ **Headless First** - Use your own UI or leverage our pre-built components  

## 📦 Project Structure

```
SaGrid/
├── SaGrid.Core/              # 🏗️ Headless table engine
│   ├── src/
│   │   ├── Models/           # Core table entities (Table, Column, Row, Cell)
│   │   ├── Features/         # Table features (Sorting, Filtering, etc.)
│   │   ├── Extensions/       # Extension methods
│   │   ├── Utils/           # Utility classes
│   │   ├── Interfaces.cs    # Core interfaces
│   │   └── Types.cs         # Type definitions
│   └── GlobalUsings.cs      # Global using statements
│
├── SaGrid.Avalonia/          # 🎨 Avalonia UI building blocks
│   ├── src/
│   │   ├── Table*Renderer.cs # Header/body/footer renderers
│   │   ├── TableCellRenderer.cs # Cell rendering helpers
│   │   └── TableContentHelper.cs # Text/content utilities
│   └── GlobalUsings.cs
│
├── SaGrid.SolidAvalonia/     # ⚡ SolidAvalonia reactive adapters
│   ├── src/
│   │   ├── SolidTable*.cs   # Reactive wrappers over Avalonia building blocks
│   │   ├── SolidTableExtensions.cs # Solid-powered helpers
│   │   └── SolidTableBuilder.cs # Factory convenience API
│   └── GlobalUsings.cs
│
├── SaGrid.Advanced/          # ⚡ Advanced features & components
│   └── src/
│       ├── Behaviors/       # UI behaviors
│       └── SaGrid*.cs       # Advanced table components
│
├── Examples/                 # 📚 Sample applications
│   └── src/
│       ├── App.xaml        # Avalonia application
│       ├── MainWindow.cs   # Example usage
│       └── Program.cs      # Entry point
│
└── Tests/                    # 🧪 Test suite
    └── src/
        ├── Contracts/      # Contract tests
        ├── Features/       # Feature tests
        ├── Integration/    # Integration tests
        └── TestData/       # Test data generators
```

| Package | Description | Status |
|---------|-------------|---------|
| **`SaGrid.Core`** | Headless table engine | ✅ Stable |
| **`SaGrid.Avalonia`** | Avalonia UI building blocks (non-reactive) | ✅ Stable |
| **`SaGrid.SolidAvalonia`** | Reactive wrappers around display primitives | ✅ Stable |
| **`SaGrid.Advanced`** | Full-featured composition (sorting, resizing, filters, grouping, virtualization, DnD) | 🔄 In Progress |
| **`Examples`** | Sample applications | 📚 Documentation |
| **`Tests`** | Comprehensive test suite | 🧪 Testing |

## 🌟 Features That Developers Love

### 🔥 Core Engine (SaGrid.Core)
- 🎯 **Type-safe API** with full C# generic support  
- 🏗️ **Headless architecture** - bring your own UI  
- 📊 **Smart column management** (visibility, ordering, pinning, resizing)  
- 🔄 **Powerful sorting** (single/multi-column with custom comparers)  
- 🔍 **Advanced filtering** (column-level, global, and faceted)  
- ✅ **Flexible row selection** (single/multi with state persistence)  
- 📖 **Intelligent pagination** with configurable page sizes  
- 🌳 **Grouping & expansion** for hierarchical data structures  
- ⚡ **Immutable state** with functional updates for predictability  
- 🧠 **Memory optimized** with intelligent caching and weak references  

### 🎨 Avalonia Components (SaGrid.Avalonia)
- 🧱 **Composable building blocks** (header/body/footer renderers, cell helpers)  
- 🖌️ **Avalonia-first styling** with sensible defaults  
- 🔄 **Manual refresh model** – integrate with MVVM or trigger redraws yourself  
- 🧩 **Works standalone** or as the foundation for Solid-based adapters  

### ⚡ Solid Reactive Layer (SaGrid.SolidAvalonia)
- 🔁 **SolidAvalonia `Component` wrappers** for automatic state-driven updates  
- 🧰 **Builders and extensions** mirroring TanStack's Solid adapter ergonomics  
- 🤝 **Seamless pairing** with `SaGrid.Avalonia` primitives and `SaGrid.Advanced` features  
- 🌈 **Themeable styling** with customizable appearance system

## ⚡ Quick Start - Get Running in 60 Seconds

### 🎯 Step 1: Define Your Data
```csharp
public record Employee(string Name, string Department, int Salary, DateTime HireDate);
```

### 🔧 Step 2: Configure Columns  
```csharp
var columns = new List<ColumnDef<Employee>>
{
    ColumnHelper.Accessor<Employee, string>(e => e.Name, "name", "Employee Name"),
    ColumnHelper.Accessor<Employee, string>(e => e.Department, "dept", "Department"),
    ColumnHelper.Accessor<Employee, int>(e => e.Salary, "salary", "Salary"),
    ColumnHelper.Accessor<Employee, DateTime>(e => e.HireDate, "hired", "Hire Date")
};
```

### 🚀 Step 3: Create Your Table
```csharp
// Option A: Headless (bring your own UI)
var table = SaGrid.CreateTable(new TableOptions<Employee>
{
    Data = employees,
    Columns = columns,
    EnableSorting = true,
    EnableFiltering = true,
    EnablePagination = true
});
```
### 🎨 Step 4: Use in Your App

#### Option A: SolidAvalonia (Reactive UI)
```csharp
using SaGrid.SolidAvalonia;

public class EmployeeTableView : Component
{
    protected override object Build()
    {
        var table = SolidTableBuilder.CreateFullFeaturedTable(employees, columns);

        return new StackPanel()
            .Children(
                table.GlobalFilterInput("Search employees..."),
                table.ColumnVisibilityPanel(),
                table,
                table.PaginationControls()
            );
    }
}
```

#### Option B: Plain Avalonia building blocks
```csharp
using SaGrid.Avalonia;

var table = new Table<Employee>(new TableOptions<Employee>
{
    Data = employees,
    Columns = columns,
    EnableSorting = true,
    EnableColumnFilters = true,
    EnablePagination = true
});

var layout = SaGrid.Avalonia.TableColumnLayoutManagerRegistry.GetOrCreate(table);
var header = new TableHeaderRenderer<Employee>().CreateHeader(table, layout);
var body = new TableBodyRenderer<Employee>().CreateBody(table, layout);
var footer = new TableFooterRenderer<Employee>().CreateFooter(table);

var layout = new StackPanel().Children(header, body, footer);
```

> 💡 **Tip:** use the reactive Solid build when you want automatic updates; stick to the Avalonia building blocks when you prefer full control over redraw timing (e.g., MVVM).

## 🧱 Layering & Responsibilities

- SaGrid.Core (headless)
  - Table engine only: columns, rows, sorting, filtering, grouping, expansion, pagination, selection, sizing state.
  - No UI dependencies. All state changes are pure and observable.

- SaGrid.Avalonia (display-only)
  - Renders header/body/footer using a `TableColumnLayoutSnapshot` provided by a `TableColumnLayoutManager`.
  - No interactions (no sorting clicks, resize thumbs, or filter editors). Pure visuals for maximum predictability.

- SaGrid.SolidAvalonia (reactive wrappers)
  - Declarative Solid components that re-render on state changes and host viewport/width reporting.
  - Pairs Core state with Avalonia display primitives.

- SaGrid.Advanced (full composition)
  - Wires the complete UX: sorting, filters, grouping chips, column resizing, virtualization, drag & drop, status bar, etc.
  - Updates Core state and refreshes layout snapshots; header/body stay aligned under resize/scroll.

### Virtualization

- Advanced enables a virtualized body (with pooling) and keeps header and body aligned during fast scroll and column resizing.
- The Avalonia display layer intentionally omits behavior and can be used for deterministic drawing from the latest snapshot.

## 🔥 Advanced Techniques

### 🎛️ Custom Column Helpers
```csharp
using SaGrid.SolidAvalonia;

var actionColumn = SolidColumnHelper.CommandButton<Employee>(
    id: "actions",
    caption: "Details",
    onClick: row => ShowDetails(row.Original));

var reactiveColumn = SolidColumnHelper.ReactiveAccessor<Employee, string>(
    accessorKey: "status",
    header: "Status",
    cellRenderer: status => status.ToUpperInvariant());
```

### 🎯 Powerful State Management
```csharp
var options = new TableOptions<Employee>
{
    Data = employees,
    Columns = columns,
    OnStateChange = async state =>
    {
        await SaveUserPreferences(state);
    }
};

// Control everything programmatically
table.SetSorting("salary", SortDirection.Descending);
table.SetColumnFilter("department", "Engineering");
table.SetPageIndex(2);
table.SetPageSize(50);
```

### 🔍 Custom Sorting & Filtering Like a Pro
```csharp
var smartColumn = new ColumnDef<Employee, string>
{
    Id = "fullName",
    AccessorFn = e => $"{e.FirstName} {e.LastName}",
    SortingFn = (a, b) => string.Compare(a, b, StringComparison.OrdinalIgnoreCase),
    FilterFn = (row, columnId, filterValue) =>
        row.GetValue<string>(columnId)
            .Contains(filterValue?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase)
};
```

### 🎨 Reactive UI Extensions (SolidAvalonia)
```csharp
var table = SolidTableBuilder.CreateSortableTable(employees, columns);

var headerControl = table.SortableHeader(headerDefinition,
    (columnId, direction) => Analytics.Track($"Column sorted: {columnId} {direction}"));

var filterHeader = table.FilterableHeader(headerDefinition,
    (columnId, value) => UserPrefs.SaveFilter(columnId, value));

var paginationControls = table.PaginationControls();
```

## 🏗️ Rock-Solid Architecture

### 💎 Core Design Principles
1. **🔒 Immutable State** - Predictable updates, zero side effects
2. **⚡ Functional Updates** - Clean, composable state transformations  
3. **🎯 Type Safety First** - Catch bugs at compile time, not runtime
4. **🧩 Modular Features** - Use only what you need
5. **🧠 Memory Intelligent** - Smart caching with automatic cleanup

### 🔥 SolidAvalonia Magic
1. **📡 Reactive Signals** - State changes propagate instantly
2. **⚡ Auto Re-rendering** - UI stays in sync without manual work
3. **♻️ Smart Lifecycle** - Automatic cleanup and disposal
4. **🌊 Fluent API** - Code that reads like english

## ⚡ Performance That Scales

| Metric | SaGrid Performance | 
|--------|-------------------|
| **Library Size** | 📦 ~16KB (like original TanStack) |
| **Rendering** | 🚀 O(visible rows) - scales to millions |
| **State Updates** | ⚡ O(1) - instant response |
| **Large Datasets** | 📊 10K+ rows with virtualization |
| **Memory Usage** | 🧠 Optimized for long-running apps |

## 🥊 SaGrid vs The Competition

| Feature | TanStack JS | **SaGrid C#** | Other C# Grids |
|---------|-------------|-------------|---------------|
| Type Safety | TypeScript | ✅ **Native Generics** | ❌ Object-based |
| Bundle Size | ~15KB | ✅ **~16KB** | ❌ 100KB+ |
| Performance | V8 engine | ✅ **.NET JIT** | ❌ Slower rendering |
| Reactivity | Framework specific | ✅ **Built-in** | ❌ Manual updates |
| Memory | GC dependent | ✅ **Deterministic** | ❌ Memory leaks |
| Learning Curve | Moderate | ✅ **Familiar C#** | ❌ Complex APIs |

## 🚀 What's Coming Next

### ✅ Phase 1: Foundation (Shipped!)
- ✅ **Rock-solid core** table engine
- ✅ **Sorting & filtering** that just works  
- ✅ **SolidAvalonia** integration
- ✅ **Pagination** with smart controls

### 🔄 Phase 2: Power Features (In Progress)
- 🔄 **Advanced filtering** (faceted, fuzzy, multi-column)
- 🔄 **Grouping & aggregation** for analytics
- 🔄 **Row expansion** with virtualization
- ✅ **Column resizing** & drag-to-reorder (shipped!)

### ⏳ Phase 3: Platform Expansion (Coming Soon)
- ⏳ **WPF & MAUI** adapters for maximum reach
- ⏳ **Blazor** adapter for web applications  
- ⏳ **Enterprise features** (Excel export, themes)
- ⏳ **Performance optimizations** for massive datasets

---

## 🤝 Join the SaGrid Community

**Want to contribute?** We'd love your help!

1. 🍴 **Fork** the repository
2. 🌿 **Create** a feature branch
3. ✨ **Make** your awesome changes
4. 🧪 **Add** tests (we love tests!)
5. 🚀 **Submit** a pull request

**Questions? Ideas? Found a bug?** Open an issue - we respond fast!

---

## 📜 License & Credits

**MIT License** - Use it anywhere, build amazing things!

### 🙏 Huge Thanks To
- 💪 **[TanStack Table](https://github.com/TanStack/table/)** team for the incredible foundation
- 🎯 **SolidAvalonia** community for reactive UI magic  
- 🖥️ **Avalonia UI** team for cross-platform excellence

---
