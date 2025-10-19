using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq.Expressions;
using SaGrid.Core.Models;

namespace SaGrid.Core;

public delegate T Updater<T>(T value);
public delegate T AccessorFn<TData, T>(TData data);
public delegate TResult ColumnDefTemplate<TData, TValue, TResult>(ColumnDef<TData, TValue> columnDef);

public record TableOptions<TData>
{
    public required IEnumerable<TData> Data { get; init; }
    public required IReadOnlyList<ColumnDef<TData>> Columns { get; init; }
    public TableState<TData>? State { get; init; }
    public Action<TableState<TData>>? OnStateChange { get; init; }
    public Func<TData[], RowModel<TData>>? GetCoreRowModel { get; init; }
    public Func<Table<TData>, RowModel<TData>>? GetFilteredRowModel { get; init; }
    public Func<Table<TData>, RowModel<TData>>? GetSortedRowModel { get; init; }
    public Func<Table<TData>, RowModel<TData>>? GetGroupedRowModel { get; init; }
    public Func<Table<TData>, RowModel<TData>>? GetExpandedRowModel { get; init; }
    public Func<Table<TData>, RowModel<TData>>? GetPaginationRowModel { get; init; }
    public bool EnableColumnFilters { get; init; } = true;
    public bool EnableGlobalFilter { get; init; } = true;
    public bool EnableSorting { get; init; } = true;
    public bool EnableMultiSort { get; init; } = true;
    public bool EnableGrouping { get; init; } = false;
    public bool EnableExpanding { get; init; } = false;
    public bool EnableRowSelection { get; init; } = false;
    public bool EnableCellSelection { get; init; } = false;
    public bool EnableColumnResizing { get; init; } = false;
    public bool EnableColumnReordering { get; init; } = false;
    public bool EnableColumnPinning { get; init; } = false;
    public bool EnablePagination { get; init; } = false;
    public bool EnableVirtualization { get; init; } = false;
    public Dictionary<string, object>? Meta { get; init; }
}

public record TableState<TData>
{
    public ColumnFiltersState? ColumnFilters { get; init; }
    public GlobalFilterState? GlobalFilter { get; init; }
    public SortingState? Sorting { get; init; }
    public GroupingState? Grouping { get; init; }
    public ExpandedState? Expanded { get; init; }
    public RowSelectionState? RowSelection { get; init; }
    public ColumnOrderState? ColumnOrder { get; init; }
    public ColumnPinningState? ColumnPinning { get; init; }
    public ColumnSizingState? ColumnSizing { get; init; }
    public CellSelectionState? CellSelection { get; init; }
    public ColumnVisibilityState? ColumnVisibility { get; init; }
    public PaginationState? Pagination { get; init; }
}

public record RowModel<TData>
{
    public required IReadOnlyList<Row<TData>> Rows { get; init; }
    public required IReadOnlyList<Row<TData>> FlatRows { get; init; }
    public required IReadOnlyDictionary<string, Row<TData>> RowsById { get; init; }
}

public enum SortDirection
{
    Ascending,
    Descending
}

public record SortingState(List<ColumnSort> Columns = null!)
{
    public List<ColumnSort> Columns { get; init; } = Columns ?? new();
}

public record ColumnSort(string Id, SortDirection Direction);

public record ColumnFiltersState(List<ColumnFilter> Filters = null!)
{
    public List<ColumnFilter> Filters { get; init; } = Filters ?? new();
}

public record ColumnFilter(string Id, object? Value);

public record GlobalFilterState(object? Value);

public enum SetFilterOperator
{
    Any,
    All
}

public record SetFilterState(IReadOnlyList<string> SelectedValues, SetFilterOperator Operator = SetFilterOperator.Any, bool IncludeBlanks = false);

public record GroupingState(List<string> Groups = null!)
{
    public List<string> Groups { get; init; } = Groups ?? new();
}

public record ExpandedState(Dictionary<string, bool> Items = null!)
{
    public Dictionary<string, bool> Items { get; init; } = Items ?? new();
}

public record RowSelectionState(Dictionary<string, bool> Items = null!)
{
    public Dictionary<string, bool> Items { get; init; } = Items ?? new();

    public bool GetValueOrDefault(string key, bool defaultValue = false)
    {
        return Items.GetValueOrDefault(key, defaultValue);
    }
}

public record ColumnOrderState(List<string> Order = null!)
{
    public List<string> Order { get; init; } = Order ?? new();
}

public record ColumnPinningState
{
    public IReadOnlyList<string>? Left { get; init; }
    public IReadOnlyList<string>? Right { get; init; }
}

// Cell selection types
public record CellPosition(int RowIndex, string ColumnId)
{
    public override string ToString() => $"{RowIndex}:{ColumnId}";
}

public record CellRange(CellPosition Start, CellPosition End);

public record CellSelectionState(HashSet<CellPosition> SelectedCells = null!, CellPosition? ActiveCell = null, CellRange? SelectionRange = null)
{
    public HashSet<CellPosition> SelectedCells { get; init; } = SelectedCells ?? new HashSet<CellPosition>();
    public CellPosition? ActiveCell { get; init; } = ActiveCell;
    public CellRange? SelectionRange { get; init; } = SelectionRange;

    public bool IsCellSelected(int rowIndex, string columnId)
    {
        return SelectedCells.Contains(new CellPosition(rowIndex, columnId));
    }

    public bool IsCellInRange(int rowIndex, string columnId)
    {
        if (SelectionRange == null) return false;
        
        var startRow = Math.Min(SelectionRange.Start.RowIndex, SelectionRange.End.RowIndex);
        var endRow = Math.Max(SelectionRange.Start.RowIndex, SelectionRange.End.RowIndex);
        
        // For simplicity, assume column ordering by string comparison
        // In a real implementation, this would use column display order
        var startCol = string.Compare(SelectionRange.Start.ColumnId, SelectionRange.End.ColumnId) <= 0 
            ? SelectionRange.Start.ColumnId 
            : SelectionRange.End.ColumnId;
        var endCol = string.Compare(SelectionRange.Start.ColumnId, SelectionRange.End.ColumnId) <= 0 
            ? SelectionRange.End.ColumnId 
            : SelectionRange.Start.ColumnId;
        
        return rowIndex >= startRow && rowIndex <= endRow && 
               string.Compare(columnId, startCol) >= 0 && string.Compare(columnId, endCol) <= 0;
    }
}

public record CellSelectionDelta(
    IReadOnlyCollection<CellPosition> Added,
    IReadOnlyCollection<CellPosition> Removed,
    CellPosition? ActiveCell,
    CellRange? Range);

public record ColumnSizingState(
    Dictionary<string, double>? items = null,
    Dictionary<string, double>? starWeights = null,
    double? totalWidth = null)
{
    public Dictionary<string, double> Items { get; init; } = items ?? new();
    public Dictionary<string, double> StarWeights { get; init; } = starWeights ?? new();
    public double? TotalWidth { get; init; } = totalWidth;
}

public enum ColumnWidthMode
{
    Fixed,
    Star
}

public readonly record struct ColumnWidthDefinition(ColumnWidthMode Mode, double Value)
{
    public static ColumnWidthDefinition Fixed(double width)
    {
        var clamped = double.IsNaN(width) || width < 0 ? 0 : width;
        return new ColumnWidthDefinition(ColumnWidthMode.Fixed, clamped);
    }

    public static ColumnWidthDefinition Star(double weight)
    {
        var normalized = double.IsNaN(weight) || weight <= 0 ? 1 : weight;
        return new ColumnWidthDefinition(ColumnWidthMode.Star, normalized);
    }

    public static bool TryParse(string text, out ColumnWidthDefinition definition)
    {
        definition = default;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var trimmed = text.Trim();
        if (trimmed.EndsWith("*", StringComparison.Ordinal))
        {
            var weightPart = trimmed[..^1].Trim();
            if (string.IsNullOrEmpty(weightPart))
            {
                definition = Star(1);
                return true;
            }

            if (double.TryParse(weightPart, NumberStyles.Float, CultureInfo.InvariantCulture, out var weight))
            {
                definition = Star(weight);
                return true;
            }

            return false;
        }

        if (double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var width))
        {
            definition = Fixed(width);
            return true;
        }

        return false;
    }

    public static ColumnWidthDefinition Parse(string text)
    {
        if (TryParse(text, out var definition))
        {
            return definition;
        }

        throw new FormatException($"无法解析列宽表达式 \"{text}\"。");
    }

    public override string ToString()
    {
        return Mode == ColumnWidthMode.Star
            ? $"{Value.ToString(CultureInfo.InvariantCulture)}*"
            : Value.ToString(CultureInfo.InvariantCulture);
    }
}

public record ColumnVisibilityState(Dictionary<string, bool> Items = null!)
{
    public Dictionary<string, bool> Items { get; init; } = Items ?? new();

    public bool GetValueOrDefault(string key, bool defaultValue = true)
    {
        return Items.GetValueOrDefault(key, defaultValue);
    }
}

public record PaginationState
{
    public int PageIndex { get; init; } = 0;
    public int PageSize { get; init; } = 10;
}

public abstract record ColumnDef<TData>
{
    public string? Id { get; init; }
    public string? AccessorKey { get; init; }
    public object? AccessorFn { get; init; }
    public object? Header { get; init; }
    public object? Footer { get; init; }
    public bool? EnableSorting { get; init; }
    public bool? EnableColumnFilter { get; init; }
    public bool? EnableGlobalFilter { get; init; }
    public bool? EnableGrouping { get; init; }
    public bool? EnableResizing { get; init; }
    public int? Size { get; init; }
    public int? MinSize { get; init; }
    public int? MaxSize { get; init; }
    public ColumnWidthDefinition? Width { get; init; }
    public Dictionary<string, object>? Meta { get; init; }
}

public record ColumnDef<TData, TValue> : ColumnDef<TData>
{
    public new AccessorFn<TData, TValue>? AccessorFn { get; init; }
    public new string? AccessorKey { get; init; }
    public object? Cell { get; init; }
    public Func<TValue, TValue, int>? SortingFn { get; init; }
    public Func<Row<TData>, string, TValue, bool>? FilterFn { get; init; }
    public Func<IEnumerable<TValue>, TValue>? AggregationFn { get; init; }
}

public record GroupColumnDef<TData> : ColumnDef<TData>
{
    public required IReadOnlyList<ColumnDef<TData>> Columns { get; init; }
}

// Context menu and row actions support
public record ContextMenuItem
{
    public required string Id { get; init; }
    public required string Label { get; init; }
    public required Action<object> Action { get; init; }
    public bool IsEnabled { get; init; } = true;
    public bool IsSeparator { get; init; } = false;

    // Constructor for test compatibility
    [SetsRequiredMembers]
    public ContextMenuItem(string id, string label)
    {
        Id = id;
        Label = label;
        Action = _ => { }; // Default empty action
    }
}

public record RowAction<TData>
{
    public required string Id { get; init; }
    public required string Label { get; init; }
    public required Action<Row<TData>> Action { get; init; }
    public bool IsEnabled { get; init; } = true;
    public string? Icon { get; init; }
}
