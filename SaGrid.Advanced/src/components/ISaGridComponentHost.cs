using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SaGrid.Advanced.Events;
using SaGrid.Advanced.Interactive;
using SaGrid.Advanced.Interfaces;
using SaGrid.Core;
using SaGrid.Core.Models;

namespace SaGrid.Advanced.Components;

public interface ISaGridComponentHost<TData>
{
    event EventHandler? RowDataChanged;

    void SetUIUpdateCallbacks(Action? gridCallback, Action<CellSelectionDelta?>? selectionCallback);
    IEventService GetEventService();
    ColumnInteractiveService<TData> GetColumnInteractiveService();
    IReadOnlyList<string> GetGroupedColumnIds();
    void RemoveGroupingColumn(string columnId);
    void AddGroupingColumn(string columnId, int? insertAtIndex = null);
    void MoveGroupingColumn(string columnId, int targetIndex);
    bool IsMultiSortEnabled();
    Column<TData>? GetColumn(string columnId);
    void SetColumnFilter(string columnId, object? value);
    void SetSorting(IEnumerable<ColumnSort> sorts);
    void SetSorting(string columnId, SortDirection direction);
    void ToggleSort(string columnId);
    bool IsStatusBarVisible();
    void SetPageIndex(int pageIndex);
    Task EnsureDataRangeAsync(int startRow, int endRow, CancellationToken cancellationToken = default);
    int GetPreferredFetchSize();
    Row<TData>? TryGetDisplayedRow(int index);
    int GetApproximateRowCount();
    RowModelType GetActiveRowModelType();
    void SelectCell(int rowIndex, string columnId, bool addToSelection);
    bool IsCellSelected(int rowIndex, string columnId);
    (int RowIndex, string ColumnId)? GetActiveCell();
    ICellEditorService<TData> GetEditingService();
    bool BeginCellEdit(Row<TData> row, Column<TData> column);
    bool IsSameGrid(object gridInstance);
}
