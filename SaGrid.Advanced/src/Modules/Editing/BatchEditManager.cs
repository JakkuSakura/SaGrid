using System;
using System.Collections.Generic;
using System.Linq;
using SaGrid.Core;

namespace SaGrid.Advanced.Modules.Editing;

public sealed class BatchEditManager<TData>
{
    private bool _batchInProgress;
    private readonly Dictionary<CellCoordinate, CellEditEntry<TData>> _pendingEdits = new();
    private readonly Stack<Dictionary<CellCoordinate, CellEditEntry<TData>>> _undoStack = new();
    private readonly Stack<Dictionary<CellCoordinate, CellEditEntry<TData>>> _redoStack = new();

    public bool IsBatchInProgress => _batchInProgress;
    public IReadOnlyDictionary<CellCoordinate, CellEditEntry<TData>> PendingEdits => _pendingEdits;

    public void BeginBatch()
    {
        if (_batchInProgress)
        {
            return;
        }

        _batchInProgress = true;
        _pendingEdits.Clear();
    }

    public bool RecordEdit(SaGrid<TData> grid, Row<TData> row, Column<TData> column, object? newValue)
    {
        var started = false;
        if (!_batchInProgress)
        {
            BeginBatch();
            started = true;
        }

        var key = new CellCoordinate(row.Id, column.Id);

        if (!_pendingEdits.TryGetValue(key, out var entry))
        {
            entry = new CellEditEntry<TData>(row.Id, column.Id, row.GetCell(column.Id).Value, newValue);
        }
        else
        {
            entry = entry with { NewValue = newValue };
        }

        _pendingEdits[key] = entry;
        ApplyValue(grid, entry.RowId, entry.ColumnId, newValue);
        return started;
    }

    public IReadOnlyCollection<CellEditEntry<TData>> Commit(SaGrid<TData> grid)
    {
        if (_pendingEdits.Count == 0)
        {
            _batchInProgress = false;
            return Array.Empty<CellEditEntry<TData>>();
        }

        var committed = new Dictionary<CellCoordinate, CellEditEntry<TData>>(_pendingEdits);
        _undoStack.Push(committed.ToDictionary(kvp => kvp.Key, kvp => kvp.Value));
        _redoStack.Clear();

        _pendingEdits.Clear();
        _batchInProgress = false;
        return committed.Values.ToList();
    }

    public IReadOnlyCollection<CellEditEntry<TData>> Cancel(SaGrid<TData> grid)
    {
        if (_pendingEdits.Count == 0)
        {
            _batchInProgress = false;
            return Array.Empty<CellEditEntry<TData>>();
        }

        foreach (var entry in _pendingEdits.Values)
        {
            ApplyValue(grid, entry.RowId, entry.ColumnId, entry.OldValue);
        }

        var cancelled = _pendingEdits.Values.ToList();
        _pendingEdits.Clear();
        _batchInProgress = false;
        return cancelled;
    }

    public IReadOnlyCollection<CellEditEntry<TData>> Undo(SaGrid<TData> grid)
    {
        if (_undoStack.Count == 0)
        {
            return Array.Empty<CellEditEntry<TData>>();
        }

        var edits = _undoStack.Pop();
        foreach (var entry in edits.Values)
        {
            ApplyValue(grid, entry.RowId, entry.ColumnId, entry.OldValue);
        }

        _redoStack.Push(edits);
        return edits.Values.ToList();
    }

    public IReadOnlyCollection<CellEditEntry<TData>> Redo(SaGrid<TData> grid)
    {
        if (_redoStack.Count == 0)
        {
            return Array.Empty<CellEditEntry<TData>>();
        }

        var edits = _redoStack.Pop();
        foreach (var entry in edits.Values)
        {
            ApplyValue(grid, entry.RowId, entry.ColumnId, entry.NewValue);
        }

        _undoStack.Push(edits);
        return edits.Values.ToList();
    }

    private static void ApplyValue(SaGrid<TData> grid, string rowId, string columnId, object? value)
    {
        grid.UpdateCellValue(rowId, columnId, value);
    }
}
