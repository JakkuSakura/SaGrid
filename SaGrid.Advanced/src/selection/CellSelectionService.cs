using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SaGrid.Core;

namespace SaGrid.Advanced.Selection;

/// <summary>
/// Handles cell selection behaviours for SaGrid instances. Inspired by AG Grid's selection service pattern.
/// </summary>
public class CellSelectionService
{
    public void SelectCell<TData>(SaGrid<TData> grid, int rowIndex, string columnId, bool addToSelection)
    {
        if (!grid.Options.EnableCellSelection)
        {
            return;
        }

        var cellPosition = new CellPosition(rowIndex, columnId);
        var currentSelection = grid.State.CellSelection ?? new CellSelectionState();

        var newSelectedCells = addToSelection
            ? new HashSet<CellPosition>(currentSelection.SelectedCells)
            : new HashSet<CellPosition>();

        newSelectedCells.Add(cellPosition);

        var added = newSelectedCells.Except(currentSelection.SelectedCells).ToList();
        var removed = currentSelection.SelectedCells.Except(newSelectedCells).ToList();

        grid.SetState(state => state with
        {
            CellSelection = new CellSelectionState(newSelectedCells, cellPosition, null)
        }, updateRowModel: false);

        var delta = new CellSelectionDelta(added, removed, cellPosition, null);
        grid.NotifySelectionUpdate(delta);
    }

    public void SelectCellRange<TData>(SaGrid<TData> grid, int startRowIndex, string startColumnId, int endRowIndex, string endColumnId)
    {
        if (!grid.Options.EnableCellSelection)
        {
            return;
        }

        var startPos = new CellPosition(startRowIndex, startColumnId);
        var endPos = new CellPosition(endRowIndex, endColumnId);
        var range = new CellRange(startPos, endPos);
        var currentSelection = grid.State.CellSelection ?? new CellSelectionState();

        var selectedCells = new HashSet<CellPosition>();
        var startRow = Math.Min(startRowIndex, endRowIndex);
        var endRow = Math.Max(startRowIndex, endRowIndex);

        var visibleColumns = grid.VisibleLeafColumns.ToList();
        var startColIndex = visibleColumns.FindIndex(c => c.Id == startColumnId);
        var endColIndex = visibleColumns.FindIndex(c => c.Id == endColumnId);

        if (startColIndex >= 0 && endColIndex >= 0)
        {
            var minColIndex = Math.Min(startColIndex, endColIndex);
            var maxColIndex = Math.Max(startColIndex, endColIndex);

            for (int rowIndex = startRow; rowIndex <= endRow; rowIndex++)
            {
                for (int colIndex = minColIndex; colIndex <= maxColIndex; colIndex++)
                {
                    selectedCells.Add(new CellPosition(rowIndex, visibleColumns[colIndex].Id));
                }
            }
        }

        grid.SetState(state => state with
        {
            CellSelection = new CellSelectionState(selectedCells, startPos, range)
        }, updateRowModel: false);

        var added = selectedCells.Except(currentSelection.SelectedCells).ToList();
        var removed = currentSelection.SelectedCells.Except(selectedCells).ToList();
        var delta = new CellSelectionDelta(added, removed, startPos, range);
        grid.NotifySelectionUpdate(delta);
    }

    public void ClearSelection<TData>(SaGrid<TData> grid)
    {
        var currentSelection = grid.State.CellSelection ?? new CellSelectionState();
        var removed = currentSelection.SelectedCells.ToList();

        grid.SetState(state => state with
        {
            CellSelection = new CellSelectionState()
        }, updateRowModel: false);

        var delta = new CellSelectionDelta(Array.Empty<CellPosition>(), removed, null, null);
        grid.NotifySelectionUpdate(delta);
    }

    public bool IsCellSelected<TData>(SaGrid<TData> grid, int rowIndex, string columnId)
    {
        return grid.State.CellSelection?.IsCellSelected(rowIndex, columnId) == true;
    }

    public (int RowIndex, string ColumnId)? GetActiveCell<TData>(SaGrid<TData> grid)
    {
        var activeCell = grid.State.CellSelection?.ActiveCell;
        return activeCell != null ? (activeCell.RowIndex, activeCell.ColumnId) : null;
    }

    public IReadOnlyCollection<CellPosition> GetSelectedCells<TData>(SaGrid<TData> grid)
    {
        return grid.State.CellSelection?.SelectedCells ?? new HashSet<CellPosition>();
    }

    public int GetSelectedCellCount<TData>(SaGrid<TData> grid)
    {
        return grid.State.CellSelection?.SelectedCells.Count ?? 0;
    }

    public string CopySelectedCells<TData>(SaGrid<TData> grid)
    {
        var selection = grid.State.CellSelection;
        if (selection == null || selection.SelectedCells.Count == 0)
        {
            return string.Empty;
        }

        var visibleColumns = grid.VisibleLeafColumns.ToList();
        var result = new StringBuilder();

        foreach (var rowGroup in selection.SelectedCells.GroupBy(c => c.RowIndex).OrderBy(g => g.Key))
        {
            var rowIndex = rowGroup.Key;
            if (rowIndex < 0 || rowIndex >= grid.RowModel.Rows.Count)
            {
                continue;
            }

            var row = grid.RowModel.Rows[rowIndex];
            var sortedCells = rowGroup
                .OrderBy(cell => visibleColumns.FindIndex(col => col.Id == cell.ColumnId))
                .ToList();

            var cellValues = sortedCells.Select(cellPos =>
            {
                var cell = row.GetCell(cellPos.ColumnId);
                return cell.Value?.ToString() ?? string.Empty;
            });

            result.AppendLine(string.Join("\t", cellValues));
        }

        return result.ToString().TrimEnd();
    }

    public bool Navigate<TData>(SaGrid<TData> grid, CellNavigationDirection direction)
    {
        var activeCell = GetActiveCell(grid);
        if (activeCell == null)
        {
            return false;
        }

        var visibleColumns = grid.VisibleLeafColumns.ToList();
        var currentColIndex = visibleColumns.FindIndex(c => c.Id == activeCell.Value.ColumnId);
        var currentRowIndex = activeCell.Value.RowIndex;

        (int RowIndex, string ColumnId)? newActiveCell = direction switch
        {
            CellNavigationDirection.Up when currentRowIndex > 0 =>
                (currentRowIndex - 1, activeCell.Value.ColumnId),

            CellNavigationDirection.Down when currentRowIndex < grid.RowModel.Rows.Count - 1 =>
                (currentRowIndex + 1, activeCell.Value.ColumnId),

            CellNavigationDirection.Left when currentColIndex > 0 =>
                (currentRowIndex, visibleColumns[currentColIndex - 1].Id),

            CellNavigationDirection.Right when currentColIndex < visibleColumns.Count - 1 =>
                (currentRowIndex, visibleColumns[currentColIndex + 1].Id),

            _ => null
        };

        if (newActiveCell != null)
        {
            SelectCell(grid, newActiveCell.Value.RowIndex, newActiveCell.Value.ColumnId, addToSelection: false);
            return true;
        }

        return false;
    }
}
