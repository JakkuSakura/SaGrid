using System;
using System.Collections.Generic;
using Avalonia.Controls;
using SaGrid.Core;

namespace SaGrid.Advanced.Modules.Editing;

public sealed record CellEditorContext<TData>(
    SaGrid<TData> Grid,
    Row<TData> Row,
    Column<TData> Column,
    object? InitialValue,
    IReadOnlyDictionary<string, object>? Meta,
    Action Commit,
    Action Cancel);

public interface ICellEditor<TData>
{
    Control BuildEditor(CellEditorContext<TData> context);
    void SetInitialValue(object? value);
    object? GetValue();
    bool Validate(out string? validationMessage);
}

public sealed class CellEditingSession<TData>
{
    public CellEditingSession(Row<TData> row, Column<TData> column, ICellEditor<TData> editor, Control editorControl, object? originalValue)
    {
        Row = row;
        Column = column;
        Editor = editor;
        EditorControl = editorControl;
        OriginalValue = originalValue;
    }

    public Row<TData> Row { get; }
    public Column<TData> Column { get; }
    public ICellEditor<TData> Editor { get; }
    public Control EditorControl { get; }
    public object? OriginalValue { get; }
}

public sealed class CellEditingChangedEventArgs<TData> : EventArgs
{
    public CellEditingChangedEventArgs(SaGrid<TData> grid, CellEditingSession<TData>? session)
    {
        Grid = grid;
        Session = session;
    }

    public SaGrid<TData> Grid { get; }
    public CellEditingSession<TData>? Session { get; }
}

public readonly struct CellCoordinate : IEquatable<CellCoordinate>
{
    public CellCoordinate(string rowId, string columnId)
    {
        RowId = rowId;
        ColumnId = columnId;
    }

    public string RowId { get; }
    public string ColumnId { get; }

    public bool Equals(CellCoordinate other)
    {
        return string.Equals(RowId, other.RowId, StringComparison.Ordinal) &&
               string.Equals(ColumnId, other.ColumnId, StringComparison.Ordinal);
    }

    public override bool Equals(object? obj)
    {
        return obj is CellCoordinate other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(RowId, ColumnId);
    }
}

public sealed record CellEditEntry<TData>(string RowId, string ColumnId, object? OldValue, object? NewValue);
